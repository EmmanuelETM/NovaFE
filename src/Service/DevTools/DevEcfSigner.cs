using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Application.Signing.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;

namespace NovaFE.Service.DevTools;

/// <summary>
/// <b>Solo Development.</b> Firma el XML del preview con un certificado autofirmado
/// <b>efímero</b> (generado una vez por proceso) — sin vault, sin tenant, sin DGII.
/// Sirve para <i>ver</i> la forma del <c>&lt;ECF&gt;</c> / <c>&lt;RFCE&gt;</c> firmado;
/// esa firma <b>no</b> la aceptaría la DGII.
/// <para>
/// Reproduce el flujo de <c>EcfSigner</c> (Módulo 3): serializa, firma, valida el
/// XML firmado contra el XSD y —si el tipo 32 va como RFCE— firma también ese
/// resumen atándolo al e-CF por el código de seguridad.
/// </para>
/// </summary>
public sealed class DevEcfSigner(
    IEcfXmlSerializer serializer,
    IRfceSerializer rfceSerializer,
    IEcfXsdValidator validator,
    IXmlSigner xmlSigner)
{
    private static readonly Lazy<X509Certificate2> EphemeralCertificate = new(CreateEphemeralCertificate);

    public DevSignedEcf Sign(EcfDocument document, bool forceRfce = false)
    {
        var certificate = EphemeralCertificate.Value;

        var ecfXml = serializer.Serialize(document, EcfSampleCatalog.SignedAt);
        var ecfSigned = xmlSigner.Sign(ecfXml, certificate);
        var ecfXsd = validator.Validate(ecfSigned.Xml, document.Type);

        string? rfceXml = null;
        bool? rfceXsdValid = null;
        string? rfceXsdError = null;

        if (document.Type == EcfType.Consumo && (forceRfce || document.QualifiesForRfce))
        {
            var rfce = rfceSerializer.Serialize(document, ecfSigned.SecurityCode);
            var rfceSigned = xmlSigner.Sign(rfce, certificate);
            var rfceXsd = validator.ValidateRfce(rfceSigned.Xml);

            rfceXml = rfceSigned.Xml;
            rfceXsdValid = !rfceXsd.IsError;
            rfceXsdError = rfceXsd.IsError ? rfceXsd.FirstError.Description : null;
        }

        return new DevSignedEcf(
            EcfXml: ecfSigned.Xml,
            EcfXsdValid: !ecfXsd.IsError,
            EcfXsdError: ecfXsd.IsError ? ecfXsd.FirstError.Description : null,
            RfceXml: rfceXml,
            RfceXsdValid: rfceXsdValid,
            RfceXsdError: rfceXsdError,
            SecurityCode: ecfSigned.SecurityCode,
            DocumentHash: Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(ecfSigned.Xml))),
            QrUrl: EcfVerificationUrl.For(
                document, DgiiEnvironment.TestEcf, ecfSigned.SecurityCode, EcfSampleCatalog.SignedAt));
    }

    private static X509Certificate2 CreateEphemeralCertificate()
    {
        using var rsa = RSA.Create(2048);

        var subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName("NovaFE Dev Preview (no DGII)");
        subject.Add("2.5.4.5", "000000000"); // SERIALNUMBER

        var request = new CertificateRequest(
            subject.Build(), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        using var ephemeral = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        // Round-trip por PKCS#12 como en producción — evita rarezas de la clave
        // efímera de CreateSelfSigned en Windows.
        return X509CertificateLoader.LoadPkcs12(
            ephemeral.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.EphemeralKeySet);
    }
}

/// <summary>Salida de <see cref="DevEcfSigner"/> — solo para el endpoint de preview.</summary>
public sealed record DevSignedEcf(
    string EcfXml,
    bool EcfXsdValid,
    string? EcfXsdError,
    string? RfceXml,
    bool? RfceXsdValid,
    string? RfceXsdError,
    string SecurityCode,
    string DocumentHash,
    string QrUrl);
