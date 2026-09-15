namespace NovaFE.Application.Contingency;

/// <summary>
/// Un barrido del monitor de contingencia: revisa el outbox de envío a la DGII y,
/// si hace falta, prende o apaga <c>platform.contingency_mode</c> y avisa a los
/// tenants suscritos. Seam para que las pruebas lo disparen sin esperar el timer.
/// </summary>
public interface IContingencyMonitorPump
{
    /// <summary>La transición aplicada, o <c>null</c> si no hubo cambio.</summary>
    Task<ContingencyTransition?> RunOnceAsync(CancellationToken ct = default);
}
