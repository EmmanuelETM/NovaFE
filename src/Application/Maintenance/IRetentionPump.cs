namespace NovaFE.Application.Maintenance;

/// <summary>
/// Un barrido de purga por retención sobre las tablas de sistema que no la
/// tienen (<c>idempotency_keys</c>, <c>audit_log</c>). Seam para que las
/// pruebas lo disparen de forma determinista, igual que
/// <c>IWebhookDeliveryPump</c>/<c>IExpiryMonitorPump</c>.
/// </summary>
public interface IRetentionPump
{
    /// <summary>Devuelve el total de filas purgadas (todas las tablas juntas).</summary>
    Task<int> RunOnceAsync(CancellationToken ct = default);
}
