using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Maintenance;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Settings;

namespace NovaFE.Service.Workers;

/// <summary>
/// Un barrido de purga por retención sobre las tablas de sistema
/// <c>idempotency_keys</c> y <c>audit_log</c>. Ninguna de las dos es
/// <c>ITenantOwned</c>, así que no hace falta recorrer tenants ni fijar el
/// tenant actual — un solo scope alcanza.
/// </summary>
internal sealed class RetentionPump(
    IServiceScopeFactory scopeFactory,
    ILogger<RetentionPump> logger) : IRetentionPump
{
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsReader>();

        var idempotencyPurged = await scope.ServiceProvider
            .GetRequiredService<IIdempotencyStore>()
            .PurgeAsync(settings.GetValue(SettingDefinitions.IdempotencyRetention), ct);
        if (idempotencyPurged > 0)
            logger.LogInformation("Purgadas {Count} claves de idempotencia", idempotencyPurged);

        var auditLogPurged = await scope.ServiceProvider
            .GetRequiredService<IAuditLogPurger>()
            .PurgeAsync(settings.GetValue(SettingDefinitions.AuditLogRetention), ct);
        if (auditLogPurged > 0)
            logger.LogInformation("Purgadas {Count} filas del registro de auditoría", auditLogPurged);

        return idempotencyPurged + auditLogPurged;
    }
}
