namespace NovaFE.Application.Tenants;

/// <summary>Full view of a tenant, returned by the detail query.</summary>
public sealed record TenantDetail(
    Guid Id,
    string Rnc,
    string LegalName,
    string? TradeName,
    string Plan,
    string Status,
    DateTimeOffset CreatedAt);

/// <summary>Row shape for the paged tenant list.</summary>
public sealed record TenantSummary(
    Guid Id,
    string Rnc,
    string LegalName,
    string Plan,
    string Status);
