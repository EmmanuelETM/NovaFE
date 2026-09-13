using System.Reflection;

namespace NovaFE.Domain.Settings;

/// <summary>
/// Catálogo de settings runtime. Declarar un campo <c>static readonly</c> de tipo
/// <see cref="SettingDefinition"/> es todo lo necesario para que el setting exista,
/// aparezca en la pantalla de administración y funcione con su default desde el
/// despliegue — sin migración ni seeder. Ver <c>docs/configuration.md</c>.
/// </summary>
public static class SettingDefinitions
{
    /// <summary>
    /// Modo mantenimiento global. Con <c>true</c>, un middleware responde
    /// <c>503</c> a todo el tráfico salvo los health checks y la propia API de
    /// settings (para poder apagarlo).
    /// </summary>
    public static readonly BooleanSetting MaintenanceMode = new(
        key: "platform.maintenance_mode",
        group: "Plataforma",
        label: "Modo mantenimiento",
        @default: false,
        description: "Rechaza todo el tráfico con 503 salvo health checks y la API de settings.",
        killSwitch: true,
        sensitive: true);

    /// <summary>
    /// Modo contingencia (Decreto 587-24, M11): emitir con envío diferido cuando la
    /// DGII está caída. Declarado ya para que M11 lo consuma; hoy sin efecto.
    /// </summary>
    public static readonly BooleanSetting ContingencyMode = new(
        key: "platform.contingency_mode",
        group: "Plataforma",
        label: "Modo contingencia",
        @default: false,
        description: "Reservado para M11 (contingencia DGII). Sin efecto todavía.",
        killSwitch: true,
        sensitive: true);

    /// <summary>
    /// Formato por defecto de la Representación Impresa de un contribuyente
    /// (<c>letter</c> o <c>pos</c>). Rige cuando la descarga de la RI no trae
    /// <c>?layout</c>. Scope <see cref="SettingScope.Tenant"/>: lo edita el propio
    /// contribuyente desde el dashboard.
    /// </summary>
    public static readonly OptionSetting RepresentationDefaultLayout = new(
        key: "representation.default_layout",
        group: "Representación Impresa",
        label: "Formato por defecto",
        @default: "letter",
        options: ["letter", "pos"],
        description: "El formato de la Representación Impresa cuando la descarga no especifica uno.",
        scope: SettingScope.Tenant,
        tenantWritable: true);

    /// <summary>
    /// Ventana tras la cual una fila <c>pending</c> de <c>idempotency_keys</c> se
    /// considera abandonada y se puede reclamar (mismo <c>Idempotency-Key</c>
    /// reintentado). Antes hardcoded en <c>PostgresIdempotencyStore</c>.
    /// </summary>
    public static readonly DurationSetting IdempotencyStalePendingWindow = new(
        key: "idempotency.stale_pending_window",
        group: "Idempotencia",
        label: "Ventana de reclamo",
        @default: TimeSpan.FromMinutes(10),
        min: TimeSpan.FromMinutes(1),
        description: "Tras cuánto tiempo una clave de idempotencia en curso se considera abandonada y se puede reclamar.");

    /// <summary>
    /// Antigüedad a partir de la cual una fila de <c>idempotency_keys</c>
    /// (completada, o pendiente y abandonada) se purga.
    /// </summary>
    public static readonly DurationSetting IdempotencyRetention = new(
        key: "idempotency.retention",
        group: "Idempotencia",
        label: "Retención",
        @default: TimeSpan.FromDays(7),
        min: TimeSpan.FromHours(1),
        description: "Antigüedad a partir de la cual se purga una clave de idempotencia ya resuelta.");

    /// <summary>
    /// Antigüedad a partir de la cual una fila de <c>audit_log</c> (RF-14.4) se
    /// purga. La purga la hace <c>AuditLogPurger</c>, separada de
    /// <c>AuditLogWriter</c> (que sigue siendo insert-only).
    /// </summary>
    public static readonly DurationSetting AuditLogRetention = new(
        key: "audit_log.retention",
        group: "Auditoría",
        label: "Retención",
        @default: TimeSpan.FromDays(180),
        min: TimeSpan.FromDays(30),
        description: "Antigüedad a partir de la cual se purga una fila del registro de auditoría.");

    private static readonly SettingDefinition[] AllDefinitions = BuildRegistry();

    /// <summary>Todas las definiciones declaradas, ordenadas por grupo y etiqueta.</summary>
    public static IReadOnlyList<SettingDefinition> All => AllDefinitions;

    /// <summary>Busca una definición por su clave, o null si no existe.</summary>
    public static SettingDefinition? FindByKey(string key)
        => AllDefinitions.FirstOrDefault(
            d => string.Equals(d.Key, key, StringComparison.Ordinal));

    private static SettingDefinition[] BuildRegistry()
    {
        var definitions = typeof(SettingDefinitions)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(field => field.GetValue(null))
            .OfType<SettingDefinition>()
            .OrderBy(d => d.Group, StringComparer.Ordinal)
            .ThenBy(d => d.Label, StringComparer.Ordinal)
            .ToArray();

        var duplicate = definitions
            .GroupBy(d => d.Key, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException(
                $"Hay dos SettingDefinition con la clave «{duplicate.Key}».");

        return definitions;
    }
}
