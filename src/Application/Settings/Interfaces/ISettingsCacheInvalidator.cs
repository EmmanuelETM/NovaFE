namespace NovaFE.Application.Settings.Interfaces;

/// <summary>
/// Fuerza una recarga del snapshot local de settings. Lo llama el caso de uso tras
/// una escritura (para que la instancia que escribió vea el cambio al instante) y
/// el poller en cada tick que detecta una generación nueva.
/// <para>
/// Es la costura del transporte de invalidación: hoy poll + recarga directa;
/// mañana <c>LISTEN/NOTIFY</c> o Redis sin tocar a quien lo llama.
/// </para>
/// </summary>
public interface ISettingsCacheInvalidator
{
    Task RefreshNowAsync(CancellationToken ct = default);
}
