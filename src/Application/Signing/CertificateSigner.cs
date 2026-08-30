using System.Security.Cryptography.X509Certificates;
using ErrorOr;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Signing.Contracts;
using NovaFE.Application.Signing.Interfaces;
using NovaFE.Domain.Certificates;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Signing;

/// <summary>
/// Orquesta la firma con el certificado del tenant: repo → vault → validación de
/// vigencia → <see cref="IXmlSigner"/> → limpieza del material.
/// </summary>
internal sealed class CertificateSigner(
    ICurrentTenant currentTenant,
    ICertificateRepository certificates,
    ICertificateVault vault,
    IXmlSigner xmlSigner,
    TimeProvider timeProvider) : ICertificateSigner
{
    public async Task<ErrorOr<SignedXmlResult>> SignAsync(
        string xml,
        DgiiEnvironment environment,
        CancellationToken ct = default)
    {
        if (!currentTenant.HasValue)
            return Errors.Auth.TenantNotResolved;

        var certificate = await certificates.GetActiveAsync(environment, ct);
        if (certificate is null)
            return CertificateErrors.NoActiveCertificate(environment.DisplayName);

        if (!certificate.IsUsable(timeProvider.GetUtcNow()))
            return CertificateErrors.NotUsable(environment.DisplayName);

        using var secret = await vault.RetrieveAsync(certificate.VaultReference, ct);
        using var x509 = X509CertificateLoader.LoadPkcs12(
            secret.Pkcs12, secret.Password, X509KeyStorageFlags.EphemeralKeySet);

        return xmlSigner.Sign(xml, x509);
    }
}
