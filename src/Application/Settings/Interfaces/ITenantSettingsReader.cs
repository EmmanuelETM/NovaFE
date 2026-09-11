using NovaFE.Domain.Settings;

namespace NovaFE.Application.Settings.Interfaces;

/// <summary>
/// Lectura tipada de un setting de scope <see cref="SettingScope.Tenant"/> para el
/// tenant de la petición en curso (sale de <c>ICurrentTenant</c>, no se pasa a mano).
/// <para>
/// A diferencia de <see cref="ISettingsReader"/>, esto <b>sí</b> toca la base la
/// primera vez: la tabla <c>tenant_settings</c> tiene RLS y su contenido no entra
/// al snapshot global (que se carga en un scope sin tenant). El lector vive por
/// scope y memoriza las filas del tenant tras la primera lectura.
/// </para>
/// <para>
/// Resolución: <c>fila tenant_settings → override de plataforma → default de código</c>
/// (capas 5 → 2 → 1 de <c>docs/configuration.md</c>). Un override corrupto no lanza:
/// se registra y se cae a la capa siguiente.
/// </para>
/// </summary>
public interface ITenantSettingsReader
{
    /// <summary>Valor efectivo del setting para el tenant actual.</summary>
    Task<T> GetValueAsync<T>(SettingDefinition<T> definition, CancellationToken ct = default);
}
