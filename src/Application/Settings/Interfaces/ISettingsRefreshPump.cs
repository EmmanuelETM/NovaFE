namespace NovaFE.Application.Settings.Interfaces;

/// <summary>
/// Un tick del refresco de settings: consulta el contador de generación y, si
/// cambió desde la última vez, recarga el snapshot local. Seam para que las
/// pruebas lo disparen sin esperar el timer.
/// </summary>
public interface ISettingsRefreshPump
{
    /// <summary>Devuelve <c>true</c> si recargó el snapshot en este tick.</summary>
    Task<bool> RunOnceAsync(CancellationToken ct = default);
}
