namespace NovaFE.Application.Organizations.Contracts;

/// <summary>
/// Row shape for a tenant as seen from its organization's tenant grid — the
/// plain <c>TenantSummaryDto</c> (operator listing) plus what the grid needs
/// to show at a glance, sin pegarle al backend una vez por tarjeta.
/// </summary>
public sealed record OrganizationTenantSummaryDto(
    Guid Id,
    string Rnc,
    string LegalName,
    string Status,
    string? DefaultEnvironment,
    DateTimeOffset? CertificateExpiresAt,
    DateTimeOffset? LastEcfIssuedAt);
