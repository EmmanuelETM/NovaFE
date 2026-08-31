using System.Globalization;
using Asp.Versioning;
using NovaFE.Application;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Domain.Common.Json;
using NovaFE.Infrastructure;
using NovaFE.Infrastructure.Persistence;
using NovaFE.Service.Common;
using NovaFE.Service.Configuration;
using NovaFE.Service.DevTools;
using NovaFE.Service.Extensions;
using NovaFE.Service.Middlewares;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.IO;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

// ==========================================
//           1. BootStrap Logger
// ==========================================
// Captura fallos que ocurran ANTES de que Serilog lea la configuración.

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ==========================================
    //         2. Serilog Configuration
    // ==========================================
    builder.Host.UseSerilog((context, config) =>
        config.ReadFrom.Configuration(context.Configuration));

    // ==========================================
    //       3. Core & Layer Registrations
    // ==========================================

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddSingleton<RecyclableMemoryStreamManager>();

    // Todo error de la API (incluidos los 404 de ruteo y los 400 de validación
    // del model binder) sale con traceId, no solo los que pasan por un controller.
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["traceId"] =
                context.HttpContext.Items["TraceId"]?.ToString() ?? context.HttpContext.TraceIdentifier;

            context.ProblemDetails.Instance ??=
                $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
        };
    });

    // Reloj inyectable: usa TimeProvider en lugar de DateTime.UtcNow para que las
    // pruebas puedan controlar el tiempo (FakeTimeProvider).
    builder.Services.AddSingleton(TimeProvider.System);

    // Usuario actual leído de los claims. Funciona aunque todavía no haya
    // autenticación configurada; ver sección 6.
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    // Tenant actual. Lo llena TenantResolutionMiddleware (hoy del header
    // X-Tenant-Id). Se registra el tipo concreto para que el middleware pueda
    // asignarlo, y la interfaz apunta a la misma instancia del scope.
    builder.Services.AddScoped<CurrentTenant>();
    builder.Services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ==========================================
    //     4. Observabilidad & Health Checks
    // ==========================================

    builder.Services.AddObservability(builder.Configuration);
    builder.Services.AddHealthChecksSetup(builder.Configuration);

    // ==========================================
    //             5. Rate Limiting
    // ==========================================

    builder.Services.AddRateLimitingSetup(builder.Configuration);

    // ==========================================
    //     6. Authentication & Authorization
    // ==========================================
    // HUECO INTENCIONAL: cada servicio decide su esquema de autenticación.
    //
    // Para habilitar JWT:
    //   1. Agrega el paquete Microsoft.AspNetCore.Authentication.JwtBearer.
    //   2. Descomenta este bloque y completa Authority/Audience desde configuración.
    //   3. Descomenta app.UseAuthentication() en el pipeline (sección 9).
    //
    // builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    //     .AddJwtBearer(options =>
    //     {
    //         options.Authority = builder.Configuration["Jwt:Authority"];
    //         options.Audience = builder.Configuration["Jwt:Audience"];
    //         options.TokenValidationParameters = new TokenValidationParameters
    //         {
    //             ValidateIssuer = true,
    //             ValidateAudience = true,
    //             ValidateLifetime = true,
    //             ValidateIssuerSigningKey = true,
    //             ClockSkew = TimeSpan.FromSeconds(30)
    //         };
    //     });
    //
    // Nota: ICurrentUser ya lee los claims, así que los casos de uso NO cambian
    // cuando se habilite la autenticación.

    builder.Services.AddAuthorization();

    // ==========================================
    //              7. CORS
    // ==========================================

    builder.Services.AddCorsSetup(builder.Configuration);

    // ==========================================
    //   8. Controllers, Versionado & OpenAPI
    // ==========================================

    // Herramientas solo-Development (las consume EcfPreviewController, que fuera de
    // Development tampoco existe).
    if (builder.Environment.IsDevelopment())
        builder.Services.AddScoped<DevEcfSigner>();

    // URLs en minúsculas para lo que genere el link generator (Location, OpenAPI…).
    builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

    builder.Services.AddControllers(options =>
        {
            // [controller]/[action] → kebab-case en minúsculas para todos los controllers.
            options.Conventions.Add(
                new RouteTokenTransformerConvention(new KebabCaseParameterTransformer()));

            // Fuera de Development, los controllers [DevelopmentOnly] no existen.
            if (!builder.Environment.IsDevelopment())
                options.Conventions.Add(new RemoveDevelopmentOnlyConvention());
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = JsonSettings.Bulletproof.PropertyNameCaseInsensitive;
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonSettings.Bulletproof.PropertyNamingPolicy;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonSettings.Bulletproof.DefaultIgnoreCondition;
            options.JsonSerializerOptions.NumberHandling = JsonSettings.Bulletproof.NumberHandling;

            options.JsonSerializerOptions.Converters.Clear();
            foreach (var converter in JsonSettings.Bulletproof.Converters)
            {
                options.JsonSerializerOptions.Converters.Add(converter);
            }
        });

    // Incluye el registro de OpenAPI, con un documento por versión de API.
    builder.Services.AddApiVersioningSetup();

    // ==========================================
    //           9. BUILD & PIPELINE
    // ==========================================

    var app = builder.Build();

    // Migraciones + seeds al arrancar, solo si Database:MigrateOnStartup está
    // activo (por defecto: on en Development, off en el resto). Corre antes de
    // aceptar tráfico.
    await app.MigrateAndSeedDatabaseAsync();

    // El orden de los middlewares importa. Cada línea está donde está por una razón:
    app.UseExceptionHandler();

    // Sin esto, un 404 de ruta inexistente o un 405 devuelven cuerpo vacío.
    // Con ProblemDetails registrado, cada código de error sale en el mismo
    // formato JSON que el resto de la API, con su traceId.
    app.UseStatusCodePages();

    // Antes que todo lo demás para que el TraceId aparezca en cada log y respuesta.
    app.UseMiddleware<TraceIdMiddleware>();

    // Resuelve el tenant de la petición (header X-Tenant-Id por ahora) y lo deja
    // disponible en ICurrentTenant para los casos de uso y la persistencia.
    app.UseMiddleware<TenantResolutionMiddleware>();

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            var path = httpContext.Request.Path;
            if (path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase))
                return LogEventLevel.Verbose;

            return ex != null || httpContext.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : LogEventLevel.Information;
        };
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().WithDocumentPerVersion();
        app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();

    app.UseCors(CorsOptions.PolicyName);
    app.UseRateLimiter();

    //app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthCheckEndpoints();

    await app.RunAsync();

    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException se excluye porque es lo que lanzan las herramientas de
    // `dotnet ef` al abortar el host a propósito; no es un fallo real.
    Log.Fatal(ex, "La aplicación terminó de forma inesperada");
    return 1;
}
finally
{
    // En el finally para que también se vacíe el buffer cuando el arranque falla:
    // ahí es justo cuando más necesitas el log.
    Log.CloseAndFlush();
}

/// <summary>
/// Con top-level statements la clase Program es internal. Se expone para que
/// WebApplicationFactory&lt;Program&gt; pueda levantar la API en las pruebas de
/// integración.
/// </summary>
public partial class Program;
