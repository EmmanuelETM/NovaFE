namespace NovaFE.Application.Tenants.CreateApiKey;

/// <summary>
/// Acuña una API key para un contribuyente. Recurso de operador:
/// <see cref="TenantId"/> viene de la ruta.
/// </summary>
public sealed record CreateApiKeyCommand(
    Guid TenantId,
    string? Label,
    DateTimeOffset? ExpiresAt);
