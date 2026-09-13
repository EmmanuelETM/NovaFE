using System.ComponentModel.DataAnnotations;

namespace NovaFE.Service.Configuration;

/// <summary>
/// Configuración del barrido de purga por retención (sección <c>Retention</c>):
/// <c>idempotency_keys</c> y <c>audit_log</c>, tablas de sistema sin ningún otro
/// mecanismo de limpieza. Los tiempos de retención en sí son settings runtime
/// (<c>SettingDefinitions.IdempotencyRetention</c> / <c>AuditLogRetention</c>),
/// no bootstrap — esta clase solo controla el worker.
/// </summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Arranca el worker. <c>false</c> en pruebas (disparan el pump a mano).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Horas entre barridos. Es mantenimiento; no hace falta más seguido.</summary>
    [Range(1, 168)]
    public int IntervalHours { get; set; } = 6;

    public TimeSpan Interval => TimeSpan.FromHours(IntervalHours);
}
