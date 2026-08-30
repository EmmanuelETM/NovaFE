using ErrorOr;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;

namespace NovaFE.Application.Ecf.Interfaces;

/// <summary>
/// Puente Módulo 2 → Módulo 3 → Módulo 4: toma un <see cref="EcfDocument"/> ya
/// cuadrado y devuelve el <see cref="SignedEcf"/> — el XML serializado, firmado con
/// el certificado del tenant, validado contra el XSD oficial y con su huella de
/// integridad. Si el documento va a la DGII como RFCE (tipo 32 &lt; DOP 250 000),
/// también produce ese resumen.
/// <para>
/// No envía nada a la DGII ni persiste. La firma criptográfica y el manejo del
/// certificado viven en <c>ICertificateSigner</c> / <c>IXmlSigner</c>.
/// </para>
/// </summary>
public interface IEcfSigner
{
    Task<ErrorOr<SignedEcf>> SignAsync(
        EcfDocument document,
        DgiiEnvironment environment,
        CancellationToken ct = default);
}
