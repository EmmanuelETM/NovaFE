using System.Xml.Linq;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;
using NovaFE.Infrastructure.Ecf;

namespace NovaFE.UnitTests.Ecf;

/// <summary>
/// El <c>&lt;RFCE&gt;</c> (resumen del tipo 32 &lt; DOP 250 000) contra su XSD oficial
/// vendorizado (<c>RFCE-32-v1.0.xsd</c>). Como el e-CF pre-firma, se le agrega una
/// <c>&lt;Signature&gt;</c> de relleno para el <c>&lt;xs:any&gt;</c>.
/// </summary>
public class RfceSerializerTests
{
    private static readonly RfceSerializer Sut = new();
    private static readonly EcfXsdValidator Validator = new();

    private const string SecurityCode = "aB3xZ9";

    private static string SignedShape(string rfce) =>
        rfce.Replace("</RFCE>", "<Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"/></RFCE>", StringComparison.Ordinal);

    private static EcfDocument Consumo(params EcfLine[] lines) => EcfTestData.Consumo(lines);

    [Fact]
    public void A_low_value_consumo_serializes_to_a_valid_rfce()
    {
        var xml = Sut.Serialize(Consumo(), SecurityCode);

        Validator.ValidateRfce(SignedShape(xml))
            .IsError.ShouldBeFalse();
    }

    [Fact]
    public void The_rfce_is_a_header_summary_without_line_detail_or_signature_timestamp()
    {
        var root = XDocument.Parse(Sut.Serialize(Consumo(), SecurityCode)).Root!;

        root.Name.LocalName.ShouldBe("RFCE");
        root.Elements().Select(e => e.Name.LocalName).ShouldBe(["Encabezado"]);

        var enc = root.Element("Encabezado")!;
        enc.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["Version", "IdDoc", "Emisor", "Comprador", "Totales", "CodigoSeguridadeCF"]);
        enc.Element("CodigoSeguridadeCF")!.Value.ShouldBe(SecurityCode);
        enc.Element("Emisor")!.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["RNCEmisor", "RazonSocialEmisor", "FechaEmision"]);
        enc.Element("Totales")!.Element("MontoTotal")!.Value.ShouldBe("2360");
        enc.Element("Totales")!.Element("ITBIS1").ShouldBeNull();   // el RFCE no lleva los indicadores de tasa
    }

    [Fact]
    public void The_rfce_isc_breakdown_has_no_rate_element_and_still_validates()
    {
        var line = EcfTestData.Line(name: "Ron", unitPrice: 1000m) with
        {
            AdditionalTaxes = 191.30m,
            AdditionalTaxDetail = [new EcfAdditionalTax("014", Rate: 10m, IscEspecifico: 191.30m)],
        };
        var xml = Sut.Serialize(Consumo(line), SecurityCode);
        var impuesto = XDocument.Parse(xml).Root!
            .Element("Encabezado")!.Element("Totales")!
            .Element("ImpuestosAdicionales")!.Element("ImpuestoAdicional")!;

        impuesto.Element("TasaImpuestoAdicional").ShouldBeNull();
        impuesto.Element("MontoImpuestoSelectivoConsumoEspecifico")!.Value.ShouldBe("191.30");  // RFCE: 2 decimales exactos
        var result = Validator.ValidateRfce(SignedShape(xml));
        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
    }

    [Fact]
    public void Serializing_a_non_consumo_document_throws()
        => Should.Throw<ArgumentException>(() => Sut.Serialize(EcfTestData.CreditoFiscal(), SecurityCode));

    [Fact]
    public void A_security_code_that_is_not_six_characters_throws()
        => Should.Throw<ArgumentException>(() => Sut.Serialize(Consumo(), "short"));

    [Fact]
    public void QualifiesForRfce_is_true_only_for_a_low_value_type_32()
    {
        Consumo().QualifiesForRfce.ShouldBeTrue();
        EcfTestData.CreditoFiscal().QualifiesForRfce.ShouldBeFalse();   // no es el tipo 32

        // Tipo 32 sobre el umbral (necesita comprador identificado) → NO va como RFCE.
        var highValue = EcfDocument.Create(
            EcfType.Consumo,
            EcfTestData.Header(32) with
            {
                SequenceExpiresOn = null,
                Buyer = new EcfBuyer("Foreign Buyer", ForeignId: "US-1"),
            },
            [EcfTestData.Line(unitPrice: 300_000m)]).Value;
        highValue.QualifiesForRfce.ShouldBeFalse();
    }
}
