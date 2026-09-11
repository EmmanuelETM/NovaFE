using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NovaFE.Infrastructure.Settings;

/// <summary>
/// Al arrancar, revisa los overrides ya cargados (el warm-load de la app corre
/// antes que los hosted services) y loguea los que la resolución está ignorando
/// en silencio — huérfanos o corruptos. No falla el arranque.
/// </summary>
internal sealed class SettingsStartupAudit(
    ISettingsSnapshotHolder holder,
    ILogger<SettingsStartupAudit> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var issues = SettingsSnapshotAudit.Inspect(holder.Current.Overrides);

        if (issues.Count == 0)
        {
            logger.LogDebug("Settings: {Count} override(s), todos válidos", holder.Current.Overrides.Count);
            return Task.CompletedTask;
        }

        foreach (var issue in issues)
            logger.LogWarning("Settings: {Issue}", issue);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
