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
    public void A_pago_al_exterior_validates_against_the_official_type_47_xsd()
    {
        var document = EcfTestData.PagosExterior(
            EcfTestData.Line(number: 1, rate: NovaFE.Domain.Fiscal.ItbisRate.Exempt, unitPrice: 50000m, name: "Consultoría",
                retention: new NovaFE.Domain.Ecf.EcfLineRetention(NovaFE.Domain.Ecf.RetentionAgent.Withholding, IsrWithheld: 13500m)),
            EcfTestData.Line(number: 2, rate: NovaFE.Domain.Fiscal.ItbisRate.Exempt, unitPrice: 8000m, name: "Licencia software",
                retention: new NovaFE.Domain.Ecf.EcfLineRetention(NovaFE.Domain.Ecf.RetentionAgent.Withholding, IsrWithheld: 2160m)));

        var result = Validator.Validate(SignedShapeXml(document), EcfType.PagosExterior);

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
    }

    [Fact]
    public void An_exportacion_validates_against_the_official_type_46_xsd()
    {
        var document = EcfTestData.Exportaciones(
            EcfTestData.Line(number: 1, rate: NovaFE.Domain.Fiscal.ItbisRate.Zero, unitPrice: 15000m, name: "Cacao"),
            EcfTestData.Line(number: 2, rate: NovaFE.Domain.Fiscal.ItbisRate.Zero, unitPrice: 4200m, name: "Empaque"));

        var result = Validator.Validate(SignedShapeXml(document), EcfType.Exportaciones);

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
    }

    [Fact]
    public void A_gastos_menores_validates_against_the_official_type_43_xsd()
    {
        var document = EcfTestData.GastosMenores(
            EcfTestData.Line(number: 1, rate: NovaFE.Domain.Fiscal.ItbisRate.Exempt, unitPrice: 200m, name: "Taxi"),
            EcfTestData.Line(number: 2, rate: NovaFE.Domain.Fiscal.ItbisRate.Exempt, unitPrice: 150m, name: "Parqueo"));

        var result = Validator.Validate(SignedShapeXml(document), EcfType.GastosMenores);

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
    }

    [Fact]
    public void A_regimenes_especiales_validates_against_the_official_type_44_xsd()
    {
        var document = EcfTestData.RegimenesEspeciales(
            EcfTestData.Line(number: 1, rate: NovaFE.Domain.Fiscal.ItbisRate.Exempt, unitPrice: 1500m),
            EcfTestData.Line(number: 2, rate: NovaFE.Domain.Fiscal.ItbisRate.Exempt, unitPrice: 800m));

        var result = Validator.Validate(SignedShapeXml(document), EcfType.RegimenesEspeciales);

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
    }

    [Fact]
    public void A_gubernamental_validates_against_the_official_type_45_xsd()
    {
        var document = EcfTestData.Gubernamental(
            EcfTestData.Line(number: 1, unitPrice: 1000m),
            EcfTestData.Line(number: 2, rate: NovaFE.Domain.Fiscal.ItbisRate.Exempt, unitPrice: 300m));

        var result = Validator.Validate(SignedShapeXml(document), EcfType.Gubernamental);

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
    }

    [Fact]
    public void A_compras_with_retention_validates_against_the_official_type_41_xsd()
    {
        var document = EcfTestData.Compras(
            EcfTestData.Line(number: 1, unitPrice: 1000m, retention: EcfTestData.Retention(itbisWithheld: 54m, isrWithheld: 100m)),
            EcfTestData.Line(number: 2, unitPrice: 500m, retention: EcfTestData.Retention(itbisWithheld: 27m, isrWithheld: 50m)));

        var result = Validator.Validate(SignedShapeXml(document), EcfType.Compras);

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
    }

    [Fact]
    public void A_consumo_validates_against_the_official_type_32_xsd()
    {
        var result = Validator.Validate(SignedShapeXml(EcfTestData.Consumo()), EcfType.Consumo);

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
    }

    [Fact]
    public void A_nota_debito_validates_against_the_official_type_33_xsd()
    {
        var result = Validator.Validate(SignedShapeXml(EcfTestData.NotaDebito()), EcfType.NotaDebito);

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
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
