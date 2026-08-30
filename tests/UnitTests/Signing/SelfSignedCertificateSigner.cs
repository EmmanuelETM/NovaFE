using System.Security.Cryptography.X509Certificates;
using ErrorOr;
using NovaFE.Application.Signing.Contracts;
using NovaFE.Application.Signing.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Infrastructure.Signing;
using NovaFE.UnitTests.Certificates;

namespace NovaFE.UnitTests.Signing;

/// <summary>
/// <see cref="ICertificateSigner"/> para pruebas que necesitan una firma
/// <b>real</b> (no un <c>&lt;Signature&gt;</c> de relleno) sin montar el vault ni un
/// tenant: firma con <see cref="XmlDsigSigner"/> y un certificado autofirmado
/// efímero. Ignora el ambiente.
/// </summary>
internal sealed class SelfSignedCertificateSigner : ICertificateSigner
{
    private readonly XmlDsigSigner _signer = new();
    private readonly X509Certificate2 _certificate = X509CertificateLoader.LoadPkcs12(
        TestPkcs12.Generate(), TestPkcs12.DefaultPassword, X509KeyStorageFlags.EphemeralKeySet);

    public Task<ErrorOr<SignedXmlResult>> SignAsync(
        string xml, DgiiEnvironment environment, CancellationToken ct = default)
        => Task.FromResult<ErrorOr<SignedXmlResult>>(_signer.Sign(xml, _certificate));
}
