namespace NovaFE.Application.Certificates.Contracts;

/// <summary>What the API returns for a certificate. Never includes the PKCS#12.</summary>
public sealed record CertificateDto(
    Guid Id,
    string Environment,
    string HolderIdentifier,
    string Subject,
    string Issuer,
    string Thumbprint,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    string Status,
    DateTimeOffset? RevokedAt,
    DateTimeOffset CreatedAt);
