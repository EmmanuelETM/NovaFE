using System.Text;
using System.Xml;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using NovaFE.Application.Signing.Contracts;
using NovaFE.Application.Signing.Interfaces;

namespace NovaFE.Infrastructure.Signing;

/// <summary>
/// Firma XMLDSig <b>enveloped</b> con los parámetros exactos de la DGII. Ver
/// <c>docs/signing.md</c> y <c>C:\workplace\FE_DGII\</c> (§5.8 "Firmado de e-CF").
/// <para>
/// Decisiones que NO se tocan:
/// </para>
/// <list type="bullet">
/// <item>C14N <b>estándar</b> (<c>REC-xml-c14n-20010315</c>), no exclusivo.</item>
/// <item><c>PreserveWhitespace = false</c> — con indentación la firma sale inválida.</item>
/// <item><c>Reference URI=""</c> + transform <c>enveloped-signature</c>.</item>
/// <item>El certificado va embebido en <c>KeyInfo/X509Data/X509Certificate</c>.</item>
/// <item><c>&lt;Signature&gt;</c> se inserta como último hijo del elemento raíz.</item>
/// </list>
/// <para>
/// Con .NET moderno no hace falta el <c>CspParameters(24)</c> del ejemplo de la
/// DGII: <see cref="X509Certificate2.GetRSAPrivateKey"/> devuelve un proveedor
/// (RSAOpenSsl en Linux, RSACng en Windows) que ya soporta SHA-256.
/// </para>
/// </summary>
internal sealed class XmlDsigSigner : IXmlSigner
{
    // URIs exactos de la DGII. Se afirman también en las pruebas para blindarlos.
    internal const string CanonicalizationMethodUri = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315";
    internal const string SignatureMethodUri = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    internal const string DigestMethodUri = "http://www.w3.org/2001/04/xmlenc#sha256";
    internal const string EnvelopedTransformUri = "http://www.w3.org/2000/09/xmldsig#enveloped-signature";
    internal const string XmlDsigNamespace = "http://www.w3.org/2000/09/xmldsig#";

    public SignedXmlResult Sign(string xml, X509Certificate2 certificate)
    {
        ArgumentException.ThrowIfNullOrEmpty(xml);
        ArgumentNullException.ThrowIfNull(certificate);

        if (!certificate.HasPrivateKey)
            throw new InvalidOperationException("El certificado no tiene clave privada.");

        var document = LoadForSigning(xml);

        using var rsa = certificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("El certificado no tiene una clave privada RSA.");

        var signedXml = new SignedXml(document) { SigningKey = rsa };
        signedXml.SignedInfo!.CanonicalizationMethod = CanonicalizationMethodUri;
        signedXml.SignedInfo.SignatureMethod = SignatureMethodUri;

        var reference = new Reference { Uri = string.Empty, DigestMethod = DigestMethodUri };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        signedXml.AddReference(reference);

        signedXml.KeyInfo = new KeyInfo();
        signedXml.KeyInfo.AddClause(new KeyInfoX509Data(certificate.RawData));

        signedXml.ComputeSignature();

        var rawSignatureValue = signedXml.SignatureValue
            ?? throw new InvalidOperationException("ComputeSignature no produjo un SignatureValue.");

        var signatureElement = signedXml.GetXml();
        document.DocumentElement!.AppendChild(document.ImportNode(signatureElement, deep: true));

        var signatureValue = Convert.ToBase64String(rawSignatureValue);

        return new SignedXmlResult(
            Xml: Serialize(document),
            SignatureValue: signatureValue,
            SecurityCode: SecurityCodeOf(signatureValue));
    }

    public bool Verify(string signedXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(signedXml);

        XmlDocument document;
        try
        {
            document = LoadForSigning(signedXml);
        }
        catch (XmlException)
        {
            return false;
        }

        var signatures = document.GetElementsByTagName("Signature", XmlDsigNamespace);
        if (signatures.Count != 1 || signatures[0] is not XmlElement signatureElement)
            return false;

        // La firma debe colgar directamente de la raíz (evita signature-wrapping).
        if (!ReferenceEquals(signatureElement.ParentNode, document.DocumentElement))
            return false;

        var verifier = new SignedXml(document);
        verifier.LoadXml(signatureElement);

        // true => verifica contra el certificado embebido en KeyInfo (modelo DGII).
        return verifier.CheckSignature();
    }

    private static XmlDocument LoadForSigning(string xml)
    {
        var document = new XmlDocument
        {
            PreserveWhitespace = false,
            XmlResolver = null, // sin resolución de entidades externas (XXE)
        };

        document.LoadXml(xml);
        return document;
    }

    private static string Serialize(XmlDocument document)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
        };

        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, settings))
            document.Save(writer);

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(buffer.ToArray());
    }

    // RF-03.5 / RF-09.1: primeros 6 caracteres del Base64 de <SignatureValue>.
    private static string SecurityCodeOf(string signatureValue)
        => signatureValue.Length >= 6 ? signatureValue[..6] : signatureValue;
}
