using System.Globalization;
using Asp.Versioning;
using NovaFE.Application;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Ecf.Submission;
using NovaFE.Application.Notifications;
using NovaFE.Application.Webhooks.Delivery;
using NovaFE.Domain.Common.Json;
using NovaFE.Infrastructure;
using NovaFE.Infrastructure.Persistence;
using NovaFE.Service.Common;
using NovaFE.Service.Configuration;
using NovaFE.Service.DevTools;
using NovaFE.Service.Extensions;
using NovaFE.Service.Maintenance;
using NovaFE.Service.Middlewares;
using NovaFE.Service.Workers;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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

    // Detrás del ingress de Azure Container Apps (proxy Envoy que termina TLS) la
    // petición llega por HTTP con X-Forwarded-Proto/For. Sin esto, Request.Scheme
    // sería "http" y UseHttpsRedirection entraría en loop. La IP del proxy de ACA
    // es dinámica, así que no se puede acotar por KnownProxies.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // ACA manda SIGTERM y espera antes de matar el contenedor: se le da margen al
    // EcfSubmissionWorker para terminar el tick en curso en vez de cortarlo.
    builder.Services.Configure<HostOptions>(options =>
        options.ShutdownTimeout = TimeSpan.FromSeconds(25));

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

    // Envío a la DGII (Módulo 4): opciones, tiempos internos, pump y worker.
    builder.Services.AddOptions<EcfSubmissionOptions>()
        .Bind(builder.Configuration.GetSection(EcfSubmissionOptions.SectionName))
        .ValidateDataAnnotations();
    // Proyección para la capa Application: transient sobre IOptionsMonitor para
    // que un cambio de configuración en caliente llegue al próximo scope, sin
    // reiniciar el proceso. Ver docs/configuration.md.
    builder.Services.AddTransient(sp =>
        sp.GetRequiredService<IOptionsMonitor<EcfSubmissionOptions>>().CurrentValue.ToSettings());
    builder.Services.AddSingleton<IEcfSubmissionPump, EcfSubmissionPump>();

    if (builder.Configuration.GetValue("EcfSubmission:Enabled", defaultValue: true))
        builder.Services.AddHostedService<EcfSubmissionWorker>();

    // Webhooks (RF-12.7): opciones, settings internos, pump y worker de entrega.
    builder.Services.AddOptions<WebhooksOptions>()
        .Bind(builder.Configuration.GetSection(WebhooksOptions.SectionName))
        .ValidateDataAnnotations();
    // Transient sobre IOptionsMonitor (ver la nota del envío a la DGII, arriba).
    // Transient, no scoped: la config del HttpClient de entrega lo resuelve desde
    // el proveedor raíz.
    builder.Services.AddTransient(sp =>
        sp.GetRequiredService<IOptionsMonitor<WebhooksOptions>>().CurrentValue.ToSettings());
    builder.Services.AddSingleton<IWebhookDeliveryPump, WebhookDeliveryPump>();

    if (builder.Configuration.GetValue("Webhooks:Enabled", defaultValue: true))
        builder.Services.AddHostedService<WebhookDeliveryWorker>();

    // Monitor de vencimientos de certificados y secuencias (RF-01.6).
    builder.Services.AddOptions<ExpiryMonitorOptions>()
        .Bind(builder.Configuration.GetSection(ExpiryMonitorOptions.SectionName))
        .ValidateDataAnnotations();
    builder.Services.AddSingleton<IExpiryMonitorPump, ExpiryMonitorPump>();

    if (builder.Configuration.GetValue("ExpiryMonitor:Enabled", defaultValue: true))
        builder.Services.AddHostedService<ExpiryMonitorWorker>();

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
    // Clientes: API key (header X-API-Key) → el tenant sale de la key.
    // Operador: admin key estática (header X-Admin-Key).
    // Development: además se acepta X-Tenant-Id sin credencial (sandbox, tests).
    // Ver src/Service/Security/ y docs/api-auth.md.
    builder.Services.AddApiSecurity(builder.Environment, builder.Configuration);

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

    // Modo "migrar y salir": lo usa un Container Apps Job con la misma imagen como
    // paso previo al despliegue (RUN_MIGRATIONS_AND_EXIT=true). Aplica migraciones
    // y seeds y termina, sin levantar el servidor. El advisory lock de
    // DatabaseInitializer hace segura la concurrencia. Ver docs/deployment.md.
    if (app.Configuration.GetValue("RUN_MIGRATIONS_AND_EXIT", defaultValue: false))
    {
        Log.Information("RUN_MIGRATIONS_AND_EXIT: aplicando migraciones y seeds, luego cierre.");
        await app.MigrateAndSeedDatabaseAsync(force: true);
        Log.Information("Migraciones y seeds completados.");
        return 0;
    }

    // Modo "re-envolver y salir": una sola vez, al cambiar CertificateVault:Provider
    // en un despliegue con certificados ya cargados. Ver docs/certificates.md.
    if (app.Configuration.GetValue(RewrapCertificateSecrets.EnabledKey, defaultValue: false))
    {
        var count = await app.RewrapCertificateSecretsAsync();
        Log.Information("Re-envueltos {Count} secreto(s) de certificado. Cierre.", count);
        return 0;
    }

    // Fuera de Development, los endpoints de operador exigen Security:AdminApiKey.
    // Sin ella el AdminKeyAuthenticationHandler rechaza todo — se avisa fuerte.
    if (!app.Environment.IsDevelopment()
        && string.IsNullOrWhiteSpace(app.Configuration[$"{NovaFE.Service.Configuration.SecurityOptions.SectionName}:AdminApiKey"]))
    {
        Log.Warning(
            "Security:AdminApiKey no está configurada: los endpoints de operador rechazarán toda petición.");
    }

    // app.tenant_id es una variable de SESIÓN: un pooler en modo transaction
    // (endpoint -pooler de Neon, PgBouncer transaction) hace que RLS deje de
    // aislar por tenant sin avisar. Se avisa fuerte; no se aborta. Ver
    // docs/multi-tenancy.md §3.
    if (DatabaseConnectionString.LooksLikeTransactionPooler(
            app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString))
    {
        Log.Warning(
            "El host de la base parece un pooler en modo transaction (-pooler / pgbouncer). "
            + "Con transaction pooling el aislamiento por tenant (RLS) se rompe: usá el endpoint "
            + "directo o el pooler en modo session. Ver docs/multi-tenancy.md.");
    }

    // Migraciones + seeds al arrancar, solo si Database:MigrateOnStartup está
    // activo (por defecto: on en Development, off en el resto). Corre antes de
    // aceptar tráfico.
    await app.MigrateAndSeedDatabaseAsync();

    // El orden de los middlewares importa. Cada línea está donde está por una razón:

    // Primero de todo: reescribe Scheme/RemoteIp desde X-Forwarded-* del ingress
    // de Container Apps, para que el resto del pipeline (HTTPS redirect, logs,
    // rate limiter por IP) vea los valores reales del cliente.
    app.UseForwardedHeaders();

    app.UseExceptionHandler();

    // Sin esto, un 404 de ruta inexistente o un 405 devuelven cuerpo vacío.
    // Con ProblemDetails registrado, cada código de error sale en el mismo
    // formato JSON que el resto de la API, con su traceId.
    app.UseStatusCodePages();

    // Antes que todo lo demás para que el TraceId aparezca en cada log y respuesta.
    app.UseMiddleware<TraceIdMiddleware>();

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

    app.UseAuthentication();

    // Antes de UseAuthorization a propósito (RF-14.4): su código posterior a
    // `await next()` corre al final de todo el pipeline, así que ve el
    // StatusCode final incluyendo los 401/403 que UseAuthorization() corta y que
    // nunca llegarían a un middleware registrado después de ella.
    app.UseMiddleware<AuditLoggingMiddleware>();

    // Después de autenticar el esquema por defecto (API key): así el limitador
    // puede particionar por contribuyente y no solo por IP.
    app.UseRateLimiter();

    app.UseAuthorization();

    // Después de autorizar (que ya autenticó los esquemas de la política y pobló
    // HttpContext.User): pasa el claim tenant_id del principal a ICurrentTenant,
    // que consumen los casos de uso y la persistencia (filtro de EF, RLS).
    app.UseMiddleware<TenantResolutionMiddleware>();

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
