using ErrorOr;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Signing;

/// <summary>
/// Firma un XML con el certificado <b>activo del tenant actual</b> para un
/// ambiente de la DGII: busca el certificado, saca el PKCS#12 del vault, valida
/// que esté vigente, firma con <see cref="IXmlSigner"/> y limpia el material de
/// memoria.
/// </summary>
public interface ICertificateSigner
{
    Task<ErrorOr<SignedXmlResult>> SignAsync(
        string xml,
        DgiiEnvironment environment,
        CancellationToken ct = default);
}
