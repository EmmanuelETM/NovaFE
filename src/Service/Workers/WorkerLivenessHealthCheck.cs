using NovaFE.Application.Common.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NovaFE.Service.Workers;

/// <summary>
/// Un worker que se cuelga a mitad de un <c>await</c> (deadlock, una llamada
/// externa sin timeout) no lanza ninguna excepción — sigue "vivo" para
/// <c>/health/live</c> pero deja de latir. Sin tag "ready": un worker atascado
/// no impide que la API sirva tráfico normal (mismo criterio que la DGII caída
/// — el fast-path se degrada, no se cae la API), así que no debe sacar la
/// instancia del balanceador ni reiniciar el contenedor; solo debe ser visible
/// para quien mira <c>/health</c>.
/// </summary>
public sealed class WorkerLivenessHealthCheck(IWorkerHeartbeat heartbeat, TimeProvider timeProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var stale = heartbeat.Entries
            .Where(entry => now - entry.Value.LastBeatAt > entry.Value.MaxSilence)
            .Select(entry => entry.Key)
            .ToArray();

        var result = stale.Length == 0
            ? HealthCheckResult.Healthy($"{heartbeat.Entries.Count} worker(s) con latido reciente.")
            : HealthCheckResult.Unhealthy(
                $"Worker(s) sin latido dentro de lo esperado: {string.Join(", ", stale)}.");

        return Task.FromResult(result);
    }
}
