namespace NovaFE.Application.Tenants.Contracts;

/// <summary>Vista de una API key. Nunca incluye el token ni su hash.</summary>
public sealed record ApiKeyDto(
    Guid Id,
    Guid TenantId,
    string Prefix,
    string Label,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt)
{
    /// <summary>Vigente: ni revocada ni (a <paramref name="asOf"/>) vencida.</summary>
    public bool IsActiveAt(DateTimeOffset asOf) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > asOf);
}

/// <summary>
/// Respuesta de la creación de una API key: la vista más el <b>token en claro</b>,
/// que solo se devuelve aquí y no se puede recuperar después.
/// </summary>
public sealed record ApiKeyCreatedDto(ApiKeyDto Key, string Token);
