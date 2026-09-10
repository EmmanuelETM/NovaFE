namespace NovaFE.Domain.Settings;

/// <summary>
/// Alcance de un <see cref="SettingDefinition"/>: qué capas de resolución le
/// aplican (ver <c>docs/configuration.md</c> §"Resolución en capas").
/// </summary>
public enum SettingScope
{
    /// <summary>Solo <c>default de código → override de plataforma</c>. Lo edita el operador.</summary>
    Platform,

    /// <summary>Además pasa por la tabla <c>plans</c> y los overrides de suscripción.</summary>
    Plan,

    /// <summary>Además la fila de <c>tenant_settings</c>. Lo puede editar el contribuyente.</summary>
    Tenant,
}
