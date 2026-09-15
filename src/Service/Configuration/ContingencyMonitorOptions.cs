using System.ComponentModel.DataAnnotations;

namespace NovaFE.Service.Configuration;

/// <summary>
/// Configuración del monitor de contingencia (sección <c>ContingencyMonitor</c>,
/// M11 Tipo 1): revisa la antigüedad del outbox de envío a la DGII y prende/apaga
/// <c>platform.contingency_mode</c> solo. Bootstrap, no runtime — es tuning de
/// infraestructura, mismo criterio que <c>Dgii:Resilience</c>.
/// </summary>
public sealed class ContingencyMonitorOptions
{
    public const string SectionName = "ContingencyMonitor";

    /// <summary>Arranca el worker. <c>false</c> en pruebas (disparan el pump a mano).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Segundos entre barridos. Tiene que ser frecuente: es lo que decide si activar la contingencia.</summary>
    [Range(15, 600)]
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Minutos que la fila pendiente/en proceso más vieja del outbox de envío
    /// tiene que llevar estancada para declarar contingencia. Por encima del
    /// primer escalón del backoff de envío (~2 min) para no activarse por un
    /// blip transitorio.
    /// </summary>
    [Range(1, 60)]
    public int ActivationThresholdMinutes { get; set; } = 5;

    public TimeSpan Interval => TimeSpan.FromSeconds(IntervalSeconds);

    public TimeSpan ActivationThreshold => TimeSpan.FromMinutes(ActivationThresholdMinutes);
}
