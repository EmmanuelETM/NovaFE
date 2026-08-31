using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;

namespace NovaFE.UnitTests.Ecf;

public class EcfVerificationUrlTests
{
    private static readonly DateTimeOffset SignedAt = EcfTestData.SignedAt; // 2026-02-21 14:30:05Z

    [Fact]
    public void A_normal_ecf_points_to_consultatimbre_with_seven_parameters()
    {
        var url = EcfVerificationUrl.For(
            EcfTestData.CreditoFiscal(), DgiiEnvironment.TestEcf, "aB3xZ9", SignedAt);

        url.ShouldBe(
            "https://ecf.dgii.gov.do/testecf/consultatimbre" +
            "?rncemisor=132786262" +
            "&rnccomprador=132056892" +
            "&encf=E310000000042" +
            "&fechaemision=21-02-2026" +
            "&montototal=2360.00" +
            "&fechafirma=21-02-2026%2010%3A30%3A05" +   // hora dominicana (UTC-4), espacio y ':' encoded
            "&codigoseguridad=aB3xZ9");
    }

    [Fact]
    public void A_low_amount_consumo_points_to_consultatimbrefc_with_four_parameters()
    {
        var document = EcfTestData.Consumo();
        document.QualifiesForRfce.ShouldBeTrue();

        var url = EcfVerificationUrl.For(document, DgiiEnvironment.TestEcf, "aB3xZ9", SignedAt);

        url.ShouldBe(
            "https://fc.dgii.gov.do/testecf/consultatimbrefc" +
            "?rncemisor=132786262" +
            "&encf=E320000000042" +
            "&montototal=2360.00" +
            "&codigoseguridad=aB3xZ9");
        url.ShouldNotContain("rnccomprador");
        url.ShouldNotContain("fechafirma");
        url.ShouldNotContain("fechaemision");
    }

    [Fact]
    public void The_environment_url_segment_selects_the_dgii_path()
    {
        var document = EcfTestData.CreditoFiscal();

        EcfVerificationUrl.For(document, DgiiEnvironment.Production, "aB3xZ9", SignedAt)
            .ShouldStartWith("https://ecf.dgii.gov.do/ecf/consultatimbre?");
        EcfVerificationUrl.For(document, DgiiEnvironment.CertEcf, "aB3xZ9", SignedAt)
            .ShouldStartWith("https://ecf.dgii.gov.do/certecf/consultatimbre?");
    }

    [Fact]
    public void The_security_code_keeps_its_case_and_base64_symbols_are_encoded()
    {
        var url = EcfVerificationUrl.For(
            EcfTestData.CreditoFiscal(), DgiiEnvironment.TestEcf, "aB/x+Z", SignedAt);

        url.ShouldEndWith("&codigoseguridad=aB%2Fx%2BZ");
    }

    [Fact]
    public void A_buyer_without_rnc_uses_the_foreign_identifier()
    {
        // Tipo 47: el comprador es un no residente (IdentificadorExtranjero).
        var url = EcfVerificationUrl.For(
            EcfTestData.PagosExterior(), DgiiEnvironment.TestEcf, "aB3xZ9", SignedAt);

        url.ShouldContain("&rnccomprador=GB-882910&");
    }

    [Fact]
    public void An_empty_security_code_is_rejected()
    {
        Should.Throw<ArgumentException>(() => EcfVerificationUrl.For(
            EcfTestData.CreditoFiscal(), DgiiEnvironment.TestEcf, "", SignedAt));
    }
}
