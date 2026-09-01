using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NovaFE.Service.DevTools;

/// <summary>
/// Genera un PKCS#12 <b>autofirmado</b> para pruebas locales — mismo espíritu que
/// <c>TestPkcs12</c> de las pruebas de integración. El <c>SERIALNUMBER</c> del
/// subject lleva el RNC, que es lo único que <c>Certificate.Issue</c> exige que
/// coincida con el contribuyente. La DGII real no aceptaría esta firma; sirve para
/// ejercitar todo el pipeline contra el simulador.
/// </summary>
internal static class DevCertificateFactory
{
    public const string DefaultPassword = "sandbox";

    public static byte[] Create(string rncOrCedula, string password = DefaultPassword)
    {
        using var rsa = RSA.Create(2048);

        var subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName($"NovaFE Sandbox {rncOrCedula}");
        subject.AddOrganizationName("NovaFE Sandbox");
        subject.Add("2.5.4.5", rncOrCedula); // SERIALNUMBER

        var request = new CertificateRequest(
            subject.Build(), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(5));

        return certificate.Export(X509ContentType.Pkcs12, password);
    }
}
