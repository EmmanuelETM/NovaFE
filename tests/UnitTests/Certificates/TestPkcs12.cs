using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NovaFE.UnitTests.Certificates;

/// <summary>Genera archivos PKCS#12 autofirmados para las pruebas.</summary>
internal static class TestPkcs12
{
    public const string DefaultPassword = "test-password";

    public static byte[] Generate(
        string holderIdentifier = "101672919",
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        bool withPrivateKey = true,
        string password = DefaultPassword)
    {
        using var rsa = RSA.Create(2048);

        var subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName("NovaFE Test");
        subject.Add("2.5.4.5", holderIdentifier); // SERIALNUMBER

        var request = new CertificateRequest(
            subject.Build(), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Ventana amplia por defecto para que sirva con cualquier reloj de prueba.
        var from = notBefore ?? new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = notAfter ?? new DateTimeOffset(2035, 1, 1, 0, 0, 0, TimeSpan.Zero);

        using var certificate = request.CreateSelfSigned(from, to);

        if (withPrivateKey)
            return certificate.Export(X509ContentType.Pkcs12, password);

        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
        return publicOnly.Export(X509ContentType.Pkcs12, password);
    }
}
