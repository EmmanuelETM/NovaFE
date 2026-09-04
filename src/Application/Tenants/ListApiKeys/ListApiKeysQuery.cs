namespace NovaFE.Application.Tenants.ListApiKeys;

/// <summary>Lista las API keys de un contribuyente. Recurso de operador.</summary>
public sealed record ListApiKeysQuery(Guid TenantId);
