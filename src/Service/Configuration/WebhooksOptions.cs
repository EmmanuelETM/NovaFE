using System.ComponentModel.DataAnnotations;
using NovaFE.Application.Webhooks;

namespace NovaFE.Service.Configuration;

/// <summary>
/// Configuración de webhooks (sección <c>Webhooks</c>, RF-12.7). Ver
/// <c>docs/webhooks.md</c>. El slice de entrega usa los campos de timeout /
/// reintento; el de suscripciones solo <see cref="MaxEndpointsPerTenant"/> y
/// <see cref="RequireHttps"/>.
/// </summary>
public sealed class WebhooksOptions
{
    public const string SectionName = "Webhooks";

    /// <summary>Arranca el worker de entrega. <c>false</c> en pruebas (disparan el pump a mano).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Segundos entre ticks del worker de entrega cuando hay trabajo.</summary>
    [Range(1, 300)]
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Tope del intervalo con la cola vacía: cada tick sin trabajo duplica la
    /// espera hasta este techo, y vuelve a <see cref="PollIntervalSeconds"/> al
    /// procesar algo.
    /// </summary>
    [Range(1, 600)]
    public int MaxPollIntervalSeconds { get; set; } = 60;

    /// <summary>Minutos tras los que una entrega atascada en <c>processing</c> se recupera.</summary>
    [Range(1, 120)]
    public int StuckAfterMinutes { get; set; } = 5;

    /// <summary>Tope de endpoints por contribuyente.</summary>
    [Range(1, 50)]
    public int MaxEndpointsPerTenant { get; set; } = 5;

    /// <summary>Exige <c>https</c> en la URL de destino. <c>false</c> solo en Development.</summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>Timeout de cada POST de entrega (segundos).</summary>
    [Range(1, 60)]
    public int DeliveryTimeoutSeconds { get; set; } = 10;

    /// <summary>Intentos de entrega antes de marcar la fila <c>dead</c>.</summary>
    [Range(1, 20)]
    public int MaxAttempts { get; set; } = 7;

    /// <summary>Fallos de entrega seguidos tras los que un endpoint se deshabilita solo. 0 lo desactiva.</summary>
    [Range(0, 1000)]
    public int AutoDisableAfterConsecutiveFailures { get; set; } = 20;

    /// <summary>Días que se conservan las filas <c>delivered</c> / <c>dead</c> (log de entregas).</summary>
    [Range(1, 365)]
    public int DeliveriesRetentionDays { get; set; } = 30;

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);

    public TimeSpan MaxPollInterval =>
        TimeSpan.FromSeconds(Math.Max(PollIntervalSeconds, MaxPollIntervalSeconds));

    public WebhookSettings ToSettings() => new()
    {
        MaxEndpointsPerTenant = MaxEndpointsPerTenant,
        RequireHttps = RequireHttps,
        DeliveryTimeout = TimeSpan.FromSeconds(DeliveryTimeoutSeconds),
        MaxAttempts = MaxAttempts,
        AutoDisableAfterConsecutiveFailures = AutoDisableAfterConsecutiveFailures,
        DeliveriesRetention = TimeSpan.FromDays(DeliveriesRetentionDays),
        DeliveryStuckAfter = TimeSpan.FromMinutes(StuckAfterMinutes),
    };
}
