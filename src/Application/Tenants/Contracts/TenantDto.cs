namespace NovaFE.Application.Tenants.Contracts;

/// <summary>Full view of a tenant, returned by the detail query.</summary>
public sealed record TenantDto(
    Guid Id,
    string Rnc,
    string LegalName,
    string? TradeName,
    string Plan,
    string Status,
    DateTimeOffset CreatedAt);
