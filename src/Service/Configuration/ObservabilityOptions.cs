namespace NovaFE.Service.Configuration;

/// <summary>
/// Configuración de trazas y métricas (OpenTelemetry).
/// <para>
/// Si <see cref="OtlpEndpoint"/> está vacío <b>no se registra ningún exportador</b>:
/// la instrumentación sigue activa pero nada se envía a la red. Así el proyecto
/// recién creado no llena el log de errores de conexión a un colector inexistente.
/// </para>
/// </summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>Nombre con el que este servicio aparece en el backend de trazas.</summary>
    public string ServiceName { get; set; } = "NovaFE";

    /// <summary>Endpoint OTLP del colector. Ej: http://localhost:4317</summary>
    public string? OtlpEndpoint { get; set; }
}
