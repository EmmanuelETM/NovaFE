using System.ComponentModel.DataAnnotations;

namespace NovaFE.Service.Configuration;

/// <summary>
/// Configuración del monitor de vencimientos (sección <c>ExpiryMonitor</c>,
/// RF-01.6): avisa por webhook cuando un certificado o una secuencia se acerca a
/// su vencimiento o se queda sin stock.
/// </summary>
public sealed class ExpiryMonitorOptions
{
    public const string SectionName = "ExpiryMonitor";

    /// <summary>Arranca el worker. <c>false</c> en pruebas (disparan el pump a mano).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Horas entre barridos. El vencimiento es de granularidad diaria; no hace falta más seguido.</summary>
    [Range(1, 168)]
    public int IntervalHours { get; set; } = 6;

    public TimeSpan Interval => TimeSpan.FromHours(IntervalHours);
}
