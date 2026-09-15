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
    /// Texto del <c>503</c> que devuelve <c>MaintenanceModeMiddleware</c> mientras
    /// <see cref="MaintenanceMode"/> está activo. Vacío usa el mensaje genérico por
    /// defecto — así un operador puede avisar una ventana programada sin desplegar.
    /// </summary>
    public static readonly StringSetting MaintenanceMessage = new(
        key: "platform.maintenance_message",
        group: "Plataforma",
        label: "Mensaje de mantenimiento",
        @default: "",
        maxLength: 500,
        description: "Detalle del 503 de mantenimiento mostrado a los clientes. Vacío usa el mensaje genérico.");

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

    /// <summary>Lista separada por comas, cada tramo en formato de atajo de duración (<c>5m</c>, <c>2h</c>, <c>1d</c>).</summary>
    private const string LadderPattern = @"^\d+[smhd](,\d+[smhd])*$";

    /// <summary>
    /// Reprogramación de las consultas de estado tras el envío a la DGII (RF-04.3).
    /// Antes hardcoded en <c>EcfSubmissionSettings.PollLadder</c>, sin ningún camino
    /// de configuración.
    /// </summary>
    public static readonly StringSetting SubmissionPollLadder = new(
        key: "submission.poll_ladder",
        group: "Envío a la DGII",
        label: "Ladder de consultas de estado",
        @default: "5m,30m,30m",
        pattern: LadderPattern,
        allowEmpty: false,
        description: "Lista separada por comas (p. ej. \"5m,30m,30m\"); al agotarse, el comprobante pasa a revisión manual.");

    /// <summary>
    /// Backoff de los reintentos de envío ante fallos de transporte (RF-04.7).
    /// Antes hardcoded en <c>EcfSubmissionSettings.SubmitBackoff</c>.
    /// </summary>
    public static readonly StringSetting SubmissionBackoff = new(
        key: "submission.backoff",
        group: "Envío a la DGII",
        label: "Backoff de envío",
        @default: "2m,10m,30m,2h",
        pattern: LadderPattern,
        allowEmpty: false,
        description: "Lista separada por comas; al agotarse, el envío se marca failed.");

    /// <summary>Filas de la cola de envío que procesa el worker por tick.</summary>
    public static readonly IntegerSetting SubmissionBatchSize = new(
        key: "submission.batch_size",
        group: "Envío a la DGII",
        label: "Tamaño de lote",
        @default: 25,
        min: 1,
        max: 500,
        description: "Filas de la cola de envío a la DGII procesadas por cada tick del worker.");

    /// <summary>Espera antes de cada reintento de entrega de un webhook (índice = intento).</summary>
    public static readonly StringSetting WebhooksBackoffLadder = new(
        key: "webhooks.backoff_ladder",
        group: "Webhooks",
        label: "Backoff de entrega",
        @default: "10s,1m,5m,30m,2h,6h",
        pattern: LadderPattern,
        allowEmpty: false,
        description: "Lista separada por comas; pasado el final se usa el último valor.");

    /// <summary>Intentos de entrega de un webhook antes de marcar la fila dead.</summary>
    public static readonly IntegerSetting WebhooksMaxAttempts = new(
        key: "webhooks.max_attempts",
        group: "Webhooks",
        label: "Intentos máximos",
        @default: 7,
        min: 1,
        max: 20,
        description: "Intentos de entrega antes de marcar la fila dead.");

    /// <summary>Fallos de entrega seguidos tras los que un endpoint se deshabilita solo.</summary>
    public static readonly IntegerSetting WebhooksAutoDisableAfterFailures = new(
        key: "webhooks.auto_disable_after_failures",
        group: "Webhooks",
        label: "Auto-deshabilitar tras",
        @default: 20,
        min: 0,
        max: 1000,
        description: "Fallos de entrega seguidos tras los que un endpoint se deshabilita solo. 0 lo desactiva.");

    /// <summary>Timeout de cada POST de entrega de un webhook.</summary>
    public static readonly DurationSetting WebhooksDeliveryTimeout = new(
        key: "webhooks.delivery_timeout",
        group: "Webhooks",
        label: "Timeout de entrega",
        @default: TimeSpan.FromSeconds(10),
        min: TimeSpan.FromSeconds(1),
        max: TimeSpan.FromSeconds(60),
        description: "Timeout de cada POST de entrega de un webhook.");

    /// <summary>Filas del outbox de entrega de webhooks que procesa el worker por tick.</summary>
    public static readonly IntegerSetting WebhooksBatchSize = new(
        key: "webhooks.batch_size",
        group: "Webhooks",
        label: "Tamaño de lote",
        @default: 50,
        min: 1,
        max: 500,
        description: "Filas del outbox de entrega procesadas por cada tick del worker.");

    /// <summary>Lista separada por comas de enteros positivos (días).</summary>
    private const string DaysListPattern = @"^\d+(,\d+)*$";

    /// <summary>
    /// Fracción de stock restante a partir de la cual una secuencia se considera
    /// baja (RF-07.3). Antes hardcoded en dos lugares con el mismo valor:
    /// <c>NcfSequence.IsLowStock</c> (dominio, sin consumidor real) y
    /// <c>NcfSequenceDto.IsLowStock</c> (el que sí usa <c>ExpiryScan</c> y el
    /// <c>GET /sequences</c>).
    /// </summary>
    public static readonly DecimalSetting SequenceLowStockFraction = new(
        key: "sequences.low_stock_fraction",
        group: "Secuencias e-NCF",
        label: "Fracción de stock bajo",
        @default: 0.20m,
        min: 0.01m,
        max: 1m,
        description: "Fracción del rango autorizado restante a partir de la cual una secuencia se considera con stock bajo.");

    /// <summary>
    /// Umbrales (días antes del vencimiento) para el aviso escalonado de
    /// certificados (RF-01.6). Antes hardcoded en <c>ExpiryScan</c>, sin ningún
    /// camino de configuración.
    /// </summary>
    public static readonly StringSetting CertificateExpiryThresholdsDays = new(
        key: "notifications.certificate_expiry_thresholds_days",
        group: "Vencimientos",
        label: "Umbrales de certificados",
        @default: "90,30,15,7",
        pattern: DaysListPattern,
        allowEmpty: false,
        description: "Días antes del vencimiento del certificado en los que se avisa, separados por coma.");

    /// <summary>
    /// Umbrales (días antes del vencimiento) para el aviso escalonado de
    /// secuencias e-NCF (RF-01.6). Antes hardcoded en <c>ExpiryScan</c>.
    /// </summary>
    public static readonly StringSetting SequenceExpiryThresholdsDays = new(
        key: "notifications.sequence_expiry_thresholds_days",
        group: "Vencimientos",
        label: "Umbrales de secuencias",
        @default: "30,7",
        pattern: DaysListPattern,
        allowEmpty: false,
        description: "Días antes del vencimiento de la secuencia en los que se avisa, separados por coma.");

    /// <summary>
    /// Detección de e-CF duplicados por huella (comprador + tipo + monto + fecha +
    /// ambiente): <c>off</c> no hace nada, <c>observar</c> emite
    /// <c>ecf.duplicate_suspected</c> sin bloquear, <c>bloquear</c> rechaza con
    /// <c>409</c>. Exige que el comprador traiga RNC/cédula — sin identificador no
    /// hay huella confiable (p. ej. facturas de consumo &lt; DOP 250,000 con
    /// "CONSUMIDOR FINAL" repetido, donde el nombre no distingue compradores
    /// distintos). Default <c>off</c>: es comportamiento nuevo que podría rechazar
    /// negocio legítimo si se activa sin pensarlo.
    /// </summary>
    public static readonly OptionSetting EcfDuplicateDetectionMode = new(
        key: "ecf.duplicate_detection_mode",
        group: "Emisión de e-CF",
        label: "Detección de duplicados",
        @default: "off",
        options: ["off", "observar", "bloquear"],
        description: "Detección de e-CF duplicados por huella (comprador con RNC/cédula + tipo + monto + fecha).");

    /// <summary>Ventana dentro de la cual dos comprobantes con la misma huella se consideran duplicados.</summary>
    public static readonly DurationSetting EcfDuplicateDetectionWindow = new(
        key: "ecf.duplicate_detection_window",
        group: "Emisión de e-CF",
        label: "Ventana de detección",
        @default: TimeSpan.FromMinutes(5),
        min: TimeSpan.FromSeconds(30),
        max: TimeSpan.FromHours(1),
        description: "Dos comprobantes con la misma huella emitidos dentro de esta ventana se consideran el mismo.");

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
