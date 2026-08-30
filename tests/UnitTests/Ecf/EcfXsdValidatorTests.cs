using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Infrastructure.Ecf;

namespace NovaFE.UnitTests.Ecf;

/// <summary>
/// Valida el XML generado contra el <b>XSD oficial de la DGII</b> vendorizado
/// (<c>e-CF-31-v1.0.xsd</c>). El e-CF pre-firma no valida solo (falta
/// <c>&lt;Signature&gt;</c>, que el XSD exige con <c>&lt;xs:any minOccurs="1"&gt;</c>),
/// así que se le agrega una firma de relleno — el XSD no valida su contenido
/// (<c>processContents="skip"</c>).
/// </summary>
public class EcfXsdValidatorTests
{
    private static readonly EcfXmlSerializer Serializer = new();
    private static readonly EcfXsdValidator Validator = new();

    private static string SignedShapeXml(EcfDocument document)
    {
        var xml = Serializer.Serialize(document, EcfTestData.SignedAt);
        return xml.Replace(
            "</ECF>",
            "<Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"/></ECF>",
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_credito_fiscal_validates_against_the_official_xsd()
    {
        var result = Validator.Validate(SignedShapeXml(EcfTestData.CreditoFiscal()), EcfType.CreditoFiscal);

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
    }

    [Fact]
    public void A_multi_line_multi_rate_credito_fiscal_validates()
    {
        var document = EcfTestData.CreditoFiscal(
            EcfTestData.Line(number: 1, unitPrice: 1000m),
            EcfTestData.Line(number: 2, rate: NovaFE.Domain.Fiscal.ItbisRate.Sixteen, unitPrice: 500m),
            EcfTestData.Line(number: 3, rate: NovaFE.Domain.Fiscal.ItbisRate.Exempt, unitPrice: 300m));

        Validator.Validate(SignedShapeXml(document), EcfType.CreditoFiscal)
            .IsError.ShouldBeFalse();
    }

    [Fact]
    public void A_nota_credito_validates_against_the_official_type_34_xsd()
    {
        var result = Validator.Validate(SignedShapeXml(EcfTestData.NotaCredito()), EcfType.NotaCredito);

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
    }

    [Fact]
    public void Malformed_xml_is_reported()
        => Validator.Validate("<ECF><Encabezado></ECF>", EcfType.CreditoFiscal)
            .FirstError.Code.ShouldBe("Ecf.MalformedXml");

    [Fact]
    public void Xml_that_breaks_the_schema_is_reported()
    {
        // TipoeCF fuera del enum
        var broken = SignedShapeXml(EcfTestData.CreditoFiscal())
            .Replace("<TipoeCF>31</TipoeCF>", "<TipoeCF>99</TipoeCF>", StringComparison.Ordinal);

        Validator.Validate(broken, EcfType.CreditoFiscal).FirstError.Code.ShouldBe("Ecf.XsdInvalid");
    }
}
