using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Application.Signing.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;

namespace NovaFE.Application.Ecf;

/// <summary>
/// Orquesta la firma de un e-CF: serializa el <see cref="EcfDocument"/>, lo firma
/// con el certificado activo del tenant, valida el XML <b>firmado</b> contra el XSD
/// oficial (RF-03.3) y calcula su huella de integridad post-firma (RF-03.4). Si el
/// documento se envía a la DGII como RFCE (tipo 32 &lt; DOP 250 000, RF-02.6),
/// genera además ese resumen atándolo al e-CF por el código de seguridad.
/// <para>
/// La validación XSD corre <b>después</b> de firmar a propósito: el XSD exige el
/// bloque <c>&lt;Signature&gt;</c> (<c>xs:any minOccurs="1"</c>), así que el XML
/// pre-firma no valida por sí solo (ver <c>docs/ecf-xml.md</c>).
/// </para>
/// <para>
/// No envía nada a la DGII ni persiste: eso es Módulo 4. La firma criptográfica y
/// el manejo del certificado (vault, vigencia, limpieza de memoria) viven en
/// <see cref="ICertificateSigner"/>.
/// </para>
/// </summary>
internal sealed class EcfSigner(
    IEcfXmlSerializer serializer,
    IRfceSerializer rfceSerializer,
    IEcfXsdValidator xsdValidator,
    ICertificateSigner certificateSigner,
    TimeProvider timeProvider) : IEcfSigner
{
    public async Task<ErrorOr<SignedEcf>> SignAsync(
        EcfDocument document,
        DgiiEnvironment environment,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(environment);

        // El instante de firma queda en <FechaHoraFirma> y en el resultado; es el
        // mismo para el XML y para la auditoría.
        var signedAt = timeProvider.GetUtcNow();
        var ecfXml = serializer.Serialize(document, signedAt);

        var signResult = await certificateSigner.SignAsync(ecfXml, environment, ct);
        if (signResult.IsError)
            return signResult.Errors;

        var signed = signResult.Value;

        var ecfValidation = xsdValidator.Validate(signed.Xml, document.Type);
        if (ecfValidation.IsError)
            return EcfErrors.SignedDocumentFailedXsd(document.Type.Id, ecfValidation.FirstError.Description);

        // Tipo 32 < DOP 250 000: a la DGII va el RFCE (resumen), que también se
        // firma "sobre todo el documento" (Formato RFCE §B). Lo ata al e-CF el
        // <CodigoSeguridadeCF> = los 6 primeros del SignatureValue del e-CF.
        string? rfceXml = null;
        if (document.QualifiesForRfce)
        {
            var rfce = rfceSerializer.Serialize(document, signed.SecurityCode);

            var rfceSignResult = await certificateSigner.SignAsync(rfce, environment, ct);
            if (rfceSignResult.IsError)
                return rfceSignResult.Errors;

            rfceXml = rfceSignResult.Value.Xml;

            var rfceValidation = xsdValidator.ValidateRfce(rfceXml);
            if (rfceValidation.IsError)
                return EcfErrors.SignedDocumentFailedXsd(document.Type.Id, rfceValidation.FirstError.Description);
        }

        return new SignedEcf(
            SignedAt: signedAt,
            EcfXml: signed.Xml,
            RfceXml: rfceXml,
            SignatureValue: signed.SignatureValue,
            SecurityCode: signed.SecurityCode,
            DocumentHash: Sha256Hex(signed.Xml),
            QrUrl: EcfVerificationUrl.For(document, environment, signed.SecurityCode, signedAt));
    }

    private static string Sha256Hex(string xml)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(xml)));
}
