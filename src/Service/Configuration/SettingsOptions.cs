using System.ComponentModel.DataAnnotations;

namespace NovaFE.Service.Configuration;

/// <summary>
/// Configuración del motor de settings (sección <c>Settings</c>). Es config de
/// <b>bootstrap</b> (estática): fija la cadencia con la que cada instancia
/// consulta el contador de generación para saber si recargar su snapshot. Ver
/// <c>docs/configuration.md</c>.
/// </summary>
public sealed class SettingsOptions
{
    public const string SectionName = "Settings";

    /// <summary>Segundos entre consultas del contador de generación.</summary>
    [Range(2, 300)]
    public int PollIntervalSeconds { get; set; } = 10;

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);
}
