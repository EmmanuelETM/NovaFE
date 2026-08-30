using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ErrorOr;

namespace NovaFE.Domain.Certificates;

/// <summary>
/// Abre un PKCS#12 y extrae sus datos. Puro: bytes y contraseña entran, hechos
/// salen. La validación de negocio (RNC, vigencia, clave privada) vive en
/// <see cref="Certificate.Issue"/>.
/// </summary>
public static class CertificateInspector
{
    /// <summary>OID 2.5.4.5 — SERIALNUMBER, el componente del Subject donde va el RNC/cédula.</summary>
    private const string SerialNumberOid = "2.5.4.5";

    public static ErrorOr<CertificateDetails> Inspect(byte[] pkcs12, string password)
    {
        ArgumentNullException.ThrowIfNull(pkcs12);

        X509Certificate2 certificate;

        try
        {
            // EphemeralKeySet: la clave no se escribe en ningún almacén del SO.
            certificate = X509CertificateLoader.LoadPkcs12(
                pkcs12, password, X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (CryptographicException)
        {
            return CertificateErrors.CannotOpen;
        }

        using (certificate)
        {
            return new CertificateDetails(
                HolderIdentifier: ReadHolderIdentifier(certificate),
                Subject: certificate.Subject,
                Issuer: certificate.Issuer,
                Thumbprint: certificate.Thumbprint,
                ValidFrom: new DateTimeOffset(certificate.NotBefore.ToUniversalTime()),
                ValidTo: new DateTimeOffset(certificate.NotAfter.ToUniversalTime()),
                HasPrivateKey: certificate.HasPrivateKey);
        }
    }

    private static string ReadHolderIdentifier(X509Certificate2 certificate)
    {
        foreach (var rdn in certificate.SubjectName.EnumerateRelativeDistinguishedNames())
        {
            if (rdn.GetSingleElementType().Value == SerialNumberOid)
                return rdn.GetSingleElementValue() ?? string.Empty;
        }

        return string.Empty;
    }
}
