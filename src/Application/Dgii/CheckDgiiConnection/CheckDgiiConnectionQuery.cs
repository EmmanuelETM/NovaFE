namespace NovaFE.Application.Dgii.CheckDgiiConnection;

/// <summary>
/// Verifica que el contribuyente actual puede autenticarse ante la DGII en el
/// ambiente dado (fuerza el flujo semilla → token si hace falta).
/// </summary>
public sealed record CheckDgiiConnectionQuery(string Environment);

/// <summary>Nunca incluye el token; solo si la conexión funciona y cuándo vence.</summary>
public sealed record DgiiConnectionStatus(
    bool Connected,
    string Environment,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
