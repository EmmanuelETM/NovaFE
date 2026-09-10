using NovaFE.Domain.Settings;

namespace NovaFE.Application.Settings.Interfaces;

/// <summary>
/// Lectura tipada de un setting runtime. Sin E/S en el acceso: sale de un snapshot
/// en memoria que un poller mantiene al día (ver <c>docs/configuration.md</c>).
/// <para>
/// Un override corrupto en la base <b>no lanza</b>: se devuelve el
/// <see cref="SettingDefinition{T}.Default"/> del código.
/// </para>
/// </summary>
public interface ISettingsReader
{
    /// <summary>Valor efectivo del setting: el override de plataforma si lo hay, si no el default.</summary>
    T GetValue<T>(SettingDefinition<T> definition);
}
