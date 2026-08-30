using System.Security.Cryptography.X509Certificates;
using System.Xml;
using NovaFE.Infrastructure.Security;
using NovaFE.UnitTests.Certificates;

namespace NovaFE.UnitTests.Signing;

public class XmlDsigSignerTests
{
    private const string SampleEcf =
        "<ECF xmlns=\"https://dgii.gov.do/etecf\"><Encabezado><Version>1.0</Version></Encabezado><Detalle><Item>Servicio</Item></Detalle></ECF>";

    private static readonly XmlDsigSigner Signer = new();

    private static X509Certificate2 Certificate(bool withPrivateKey = true)
        => X509CertificateLoader.LoadPkcs12(
            TestPkcs12.Generate(withPrivateKey: withPrivateKey),
            TestPkcs12.DefaultPassword,
            X509KeyStorageFlags.EphemeralKeySet);

    [Fact]
    public void Sign_produces_a_signature_that_verifies()
    {
        using var certificate = Certificate();

        var result = Signer.Sign(SampleEcf, certificate);

        result.Xml.ShouldContain("<Signature");
        Signer.Verify(result.Xml).ShouldBeTrue();
    }

    [Fact]
    public void Sign_uses_the_exact_dgii_algorithm_uris()
    {
        using var certificate = Certificate();
        var (document, ns) = Load(Signer.Sign(SampleEcf, certificate).Xml);

        Attr(document, ns, "//ds:CanonicalizationMethod/@Algorithm")
            .ShouldBe("http://www.w3.org/TR/2001/REC-xml-c14n-20010315");
        Attr(document, ns, "//ds:SignatureMethod/@Algorithm")
            .ShouldBe("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256");
        Attr(document, ns, "//ds:DigestMethod/@Algorithm")
            .ShouldBe("http://www.w3.org/2001/04/xmlenc#sha256");
        Attr(document, ns, "//ds:Transform/@Algorithm")
            .ShouldBe("http://www.w3.org/2000/09/xmldsig#enveloped-signature");
        Attr(document, ns, "//ds:Reference/@URI").ShouldBe(string.Empty);
    }

    [Fact]
    public void Signature_is_the_last_child_of_the_root_element()
    {
        using var certificate = Certificate();
        var (document, _) = Load(Signer.Sign(SampleEcf, certificate).Xml);

        document.DocumentElement!.LastChild!.LocalName.ShouldBe("Signature");
        document.DocumentElement.LastChild.NamespaceURI.ShouldBe(XmlDsigSigner.XmlDsigNamespace);
    }

    [Fact]
    public void Certificate_is_embedded_in_key_info()
    {
        using var certificate = Certificate();
        var (document, ns) = Load(Signer.Sign(SampleEcf, certificate).Xml);

        var embedded = document.SelectSingleNode("//ds:KeyInfo/ds:X509Data/ds:X509Certificate", ns);
        embedded.ShouldNotBeNull();
        Convert.FromBase64String(embedded!.InnerText).ShouldBe(certificate.RawData);
    }

    [Fact]
    public void Security_code_is_the_first_six_chars_of_the_signature_value()
    {
        using var certificate = Certificate();

        var result = Signer.Sign(SampleEcf, certificate);

        result.SecurityCode.Length.ShouldBe(6);
        result.SecurityCode.ShouldBe(result.SignatureValue[..6]);
    }

    [Fact]
    public void Sign_ignores_input_indentation()
    {
        using var certificate = Certificate();

        var compact = Signer.Sign(SampleEcf, certificate).Xml;
        var pretty = Signer.Sign(
            "<ECF xmlns=\"https://dgii.gov.do/etecf\">\n  <Encabezado>\n    <Version>1.0</Version>\n  </Encabezado>\n" +
            "  <Detalle>\n    <Item>Servicio</Item>\n  </Detalle>\n</ECF>",
            certificate).Xml;

        DigestValue(compact).ShouldBe(DigestValue(pretty));
        Signer.Verify(pretty).ShouldBeTrue();
    }

    [Fact]
    public void Verify_fails_when_the_signed_content_was_tampered()
    {
        using var certificate = Certificate();
        var signed = Signer.Sign(SampleEcf, certificate).Xml
            .Replace("Servicio", "Otra cosa", StringComparison.Ordinal);

        Signer.Verify(signed).ShouldBeFalse();
    }

    [Fact]
    public void Verify_fails_on_an_unsigned_document()
        => Signer.Verify(SampleEcf).ShouldBeFalse();

    [Fact]
    public void Sign_rejects_a_certificate_without_a_private_key()
    {
        using var certificate = Certificate(withPrivateKey: false);

        Should.Throw<InvalidOperationException>(() => Signer.Sign(SampleEcf, certificate));
    }

    [Fact]
    public void Sign_rejects_malformed_xml()
    {
        using var certificate = Certificate();

        Should.Throw<XmlException>(() => Signer.Sign("<ECF><unclosed>", certificate));
    }

    private static (XmlDocument Document, XmlNamespaceManager Namespaces) Load(string xml)
    {
        var document = new XmlDocument { PreserveWhitespace = false };
        document.LoadXml(xml);

        var namespaces = new XmlNamespaceManager(document.NameTable);
        namespaces.AddNamespace("ds", XmlDsigSigner.XmlDsigNamespace);

        return (document, namespaces);
    }

    private static string Attr(XmlDocument document, XmlNamespaceManager ns, string xpath)
        => document.SelectSingleNode(xpath, ns)!.Value!;

    private static string DigestValue(string signedXml)
    {
        var (document, ns) = Load(signedXml);
        return document.SelectSingleNode("//ds:Reference/ds:DigestValue", ns)!.InnerText;
    }
}
