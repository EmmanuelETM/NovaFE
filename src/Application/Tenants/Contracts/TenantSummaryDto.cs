namespace NovaFE.Application.Tenants.Contracts;

/// <summary>Row shape for the paged tenant list.</summary>
public sealed record TenantSummaryDto(
    Guid Id,
    string Rnc,
    string LegalName,
    string Plan,
    string Status);
