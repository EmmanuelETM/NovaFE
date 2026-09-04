namespace NovaFE.Application.Tenants.RevokeApiKey;

/// <summary>Revoca una API key de un contribuyente. Recurso de operador.</summary>
public sealed record RevokeApiKeyCommand(Guid TenantId, Guid KeyId);
