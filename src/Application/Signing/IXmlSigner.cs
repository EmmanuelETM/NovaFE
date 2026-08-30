using System.Security.Cryptography.X509Certificates;

namespace NovaFE.Application.Signing;

/// <summary>
/// Firma y verifica documentos XML con XMLDSig <b>enveloped</b>, con los
/// parámetros exactos que exige la DGII (C14N estándar —no exclusivo—, SHA-256,
/// <c>Reference URI=""</c>, certificado embebido en <c>KeyInfo</c>). Ver
/// <c>docs/signing.md</c>.
/// <para>
/// Es una operación puramente criptográfica: no toca la DGII ni la base de datos.
/// El manejo del certificado del tenant (vault, vigencia) vive en
/// <see cref="ICertificateSigner"/>.
/// </para>
/// </summary>
public interface IXmlSigner
{
    /// <summary>
    /// Firma <paramref name="xml"/> con <paramref name="certificate"/> (que debe
    /// traer clave privada). Lanza si el XML o el certificado no sirven —son
    /// errores de programación, no de negocio.
    /// </summary>
    SignedXmlResult Sign(string xml, X509Certificate2 certificate);

    /// <summary>
    /// Verifica la firma de un XML firmado usando el certificado embebido en su
    /// <c>KeyInfo</c> (el modelo de la DGII). No valida la cadena de confianza ni
    /// que el firmante esté autorizado; solo integridad y correspondencia con la
    /// clave pública embebida.
    /// </summary>
    bool Verify(string signedXml);
}
