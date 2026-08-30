using NovaFE.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NovaFE.Service.Extensions;

internal static class HealthCheckExtensions
{
    private const string TagReady = "ready";

    /// <summary>
    /// Registra las comprobaciones de salud. Las que dependen de un recurso
    /// externo se etiquetan como "ready" para poder separarlas de la simple
    /// señal de "el proceso está vivo".
    /// </summary>
    internal static IServiceCollection AddHealthChecksSetup(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionName) ?? string.Empty;

        services.AddHealthChecks()
            .AddNpgSql(
                connectionString: connectionString,
                healthQuery: "SELECT 1;",
                name: "postgresql",
                failureStatus: HealthStatus.Unhealthy,
                tags: [TagReady],
                // Sin este timeout, un PostgreSQL inalcanzable tarda ~15s en fallar
                // (el connect timeout del driver) y el probe de readiness expira antes
                // de recibir respuesta, que es peor que recibir un 503 rápido.
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    /// <summary>
    /// Expone dos endpoints con propósitos distintos, que es lo que esperan
    /// Kubernetes, IIS y los monitores de disponibilidad:
    /// <list type="bullet">
    /// <item><c>/health/live</c>: ¿el proceso responde? No toca dependencias.
    /// Si falla, hay que reiniciar el contenedor.</item>
    /// <item><c>/health/ready</c>: ¿puede atender tráfico? Verifica la base de
    /// datos. Si falla, hay que sacarlo del balanceador pero no reiniciarlo.</item>
    /// </list>
    /// </summary>
    internal static WebApplication MapHealthCheckEndpoints(this WebApplication app)
    {
        var incluirDetalleDeError = app.Environment.IsDevelopment();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = (context, report) => EscribirRespuesta(context, report, incluirDetalleDeError)
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(TagReady),
            ResponseWriter = (context, report) => EscribirRespuesta(context, report, incluirDetalleDeError)
        }).AllowAnonymous();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = (context, report) => EscribirRespuesta(context, report, incluirDetalleDeError)
        }).AllowAnonymous();

        return app;
    }

    private static Task EscribirRespuesta(HttpContext context, HealthReport report, bool incluirDetalleDeError)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                description = entry.Value.Description,
                // El detalle del error puede exponer el connection string o la
                // topología interna: solo se incluye en desarrollo.
                error = incluirDetalleDeError ? entry.Value.Exception?.Message : null
            })
        };

        return context.Response.WriteAsJsonAsync(payload);
    }
}
