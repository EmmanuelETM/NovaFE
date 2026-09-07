namespace NovaFE.Application.Notifications;

/// <summary>
/// Un barrido del monitor de vencimientos: recorre los tenants activos y revisa
/// sus certificados y secuencias. Seam para que las pruebas lo disparen sin
/// esperar el timer.
/// </summary>
public interface IExpiryMonitorPump
{
    /// <summary>Devuelve cuántos tenants se revisaron.</summary>
    Task<int> RunOnceAsync(CancellationToken ct = default);
}
