namespace NovaFE.Application.Dgii.Contracts;

/// <summary>Nunca incluye el token; solo si la conexión funciona y cuándo vence.</summary>
public sealed record DgiiConnectionDto(
    bool Connected,
    string Environment,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
