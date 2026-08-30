using System.Xml.Linq;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;
using NovaFE.Infrastructure.Ecf;

namespace NovaFE.UnitTests.Ecf;

public class EcfXmlSerializerTests
{
    private static readonly EcfXmlSerializer Sut = new();

    private static XElement Serialize(EcfDocument document)
        => XDocument.Parse(Sut.Serialize(document, EcfTestData.SignedAt)).Root!;

    [Fact]
    public void Root_is_ECF_with_no_namespace()
    {
        var root = Serialize(EcfTestData.CreditoFiscal());

        root.Name.LocalName.ShouldBe("ECF");
        root.Name.NamespaceName.ShouldBe("");
    }

    [Fact]
    public void IdDoc_carries_the_expected_values_in_order()
    {
        var idDoc = Serialize(EcfTestData.CreditoFiscal()).Element("Encabezado")!.Element("IdDoc")!;

        idDoc.Elements().Select(e => e.Name.LocalName).ShouldBe(
        [
            "TipoeCF", "eNCF", "FechaVencimientoSecuencia", "IndicadorMontoGravado",
            "TipoIngresos", "TipoPago", "FechaLimitePago", "TablaFormasPago",
        ]);
        idDoc.Element("TipoeCF")!.Value.ShouldBe("31");
        idDoc.Element("eNCF")!.Value.ShouldBe("E310000000042");
        idDoc.Element("FechaVencimientoSecuencia")!.Value.ShouldBe("31-12-2027");
        idDoc.Element("IndicadorMontoGravado")!.Value.ShouldBe("0");
        idDoc.Element("TipoPago")!.Value.ShouldBe("2");
        idDoc.Element("FechaLimitePago")!.Value.ShouldBe("15-03-2026");
    }

    [Fact]
    public void Consumo_iddoc_drops_the_sequence_expiry_but_keeps_the_payment_methods()
    {
        var root = Serialize(EcfTestData.Consumo());
        var idDoc = root.Element("Encabezado")!.Element("IdDoc")!;

        idDoc.Elements().Select(e => e.Name.LocalName).ShouldBe(
        [
            "TipoeCF", "eNCF", "IndicadorMontoGravado",
            "TipoIngresos", "TipoPago", "FechaLimitePago", "TablaFormasPago",
        ]);
        idDoc.Element("TipoeCF")!.Value.ShouldBe("32");
        idDoc.Element("FechaVencimientoSecuencia").ShouldBeNull();
        idDoc.Element("IndicadorNotaCredito").ShouldBeNull();

        root.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["Encabezado", "DetallesItems", "FechaHoraFirma"]);   // sin InformacionReferencia
    }

    [Fact]
    public void Consumo_below_the_threshold_omits_the_buyer_rnc()
    {
        var comprador = Serialize(EcfTestData.Consumo()).Element("Encabezado")!.Element("Comprador")!;

        comprador.Element("RNCComprador").ShouldBeNull();
        comprador.Element("RazonSocialComprador")!.Value.ShouldBe("Consumidor Final");
    }

    [Fact]
    public void Nota_debito_keeps_the_type_31_iddoc_and_adds_the_mandatory_reference()
    {
        var root = Serialize(EcfTestData.NotaDebito());
        var idDoc = root.Element("Encabezado")!.Element("IdDoc")!;

        idDoc.Elements().Select(e => e.Name.LocalName).ShouldBe(
        [
            "TipoeCF", "eNCF", "FechaVencimientoSecuencia", "IndicadorMontoGravado",
            "TipoIngresos", "TipoPago", "FechaLimitePago", "TablaFormasPago",
        ]);
        idDoc.Element("TipoeCF")!.Value.ShouldBe("33");
        idDoc.Element("IndicadorNotaCredito").ShouldBeNull();

        root.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["Encabezado", "DetallesItems", "InformacionReferencia", "FechaHoraFirma"]);
        root.Element("InformacionReferencia")!.Element("NCFModificado")!.Value.ShouldBe("E310000000010");
    }

    [Fact]
    public void Nota_credito_iddoc_swaps_the_sequence_expiry_for_the_credit_note_indicator()
    {
        var idDoc = Serialize(EcfTestData.NotaCredito()).Element("Encabezado")!.Element("IdDoc")!;

        idDoc.Elements().Select(e => e.Name.LocalName).ShouldBe(
        [
            "TipoeCF", "eNCF", "IndicadorNotaCredito", "IndicadorMontoGravado",
            "TipoIngresos", "TipoPago", "FechaLimitePago",
        ]);
        idDoc.Element("TipoeCF")!.Value.ShouldBe("34");
        idDoc.Element("FechaVencimientoSecuencia").ShouldBeNull();
        idDoc.Element("IndicadorNotaCredito")!.Value.ShouldBe("0"); // modifica 20 días atrás
        idDoc.Element("TablaFormasPago").ShouldBeNull();            // el XSD del 34 no lo admite
    }

    [Fact]
    public void Nota_credito_after_thirty_days_carries_indicator_one()
    {
        var reference = new EcfReference(
            "E310000000010", EcfTestData.IssueDate.AddDays(-45), ModificationCode.CorrectsAmounts);

        var idDoc = Serialize(EcfTestData.NotaCredito(reference))
            .Element("Encabezado")!.Element("IdDoc")!;

        idDoc.Element("IndicadorNotaCredito")!.Value.ShouldBe("1");
    }

    [Fact]
    public void Nota_credito_emits_the_reference_section_before_the_signature_timestamp()
    {
        var root = Serialize(EcfTestData.NotaCredito());

        root.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["Encabezado", "DetallesItems", "InformacionReferencia", "FechaHoraFirma"]);
        var reference = root.Element("InformacionReferencia")!;
        reference.Element("NCFModificado")!.Value.ShouldBe("E310000000010");
        reference.Element("FechaNCFModificado")!.Value.ShouldBe("01-02-2026");
        reference.Element("CodigoModificacion")!.Value.ShouldBe("3");
    }

    [Fact]
    public void Totales_reflect_the_fiscal_engine()
    {
        var totales = Serialize(EcfTestData.CreditoFiscal()).Element("Encabezado")!.Element("Totales")!;

        totales.Element("MontoGravadoTotal")!.Value.ShouldBe("2000");
        totales.Element("MontoGravadoI1")!.Value.ShouldBe("2000");
        totales.Element("ITBIS1")!.Value.ShouldBe("18");
        totales.Element("TotalITBIS")!.Value.ShouldBe("360");
        totales.Element("TotalITBIS1")!.Value.ShouldBe("360");
        totales.Element("MontoTotal")!.Value.ShouldBe("2360");
        totales.Element("MontoExento").ShouldBeNull();
        totales.Element("MontoNoFacturable").ShouldBeNull();
    }

    [Fact]
    public void Detail_line_amount_comes_from_the_engine_and_dates_are_dd_MM_yyyy()
    {
        var root = Serialize(EcfTestData.CreditoFiscal());
        var item = root.Element("DetallesItems")!.Element("Item")!;

        item.Element("NumeroLinea")!.Value.ShouldBe("1");
        item.Element("IndicadorFacturacion")!.Value.ShouldBe("1");
        item.Element("IndicadorBienoServicio")!.Value.ShouldBe("2");
        item.Element("PrecioUnitarioItem")!.Value.ShouldBe("2000");
        item.Element("MontoItem")!.Value.ShouldBe("2000");
        root.Element("Encabezado")!.Element("Emisor")!.Element("FechaEmision")!.Value.ShouldBe("21-02-2026");
        root.Element("FechaHoraFirma")!.Value.ShouldBe("21-02-2026 10:30:05");
    }

    [Fact]
    public void Empty_optional_elements_are_omitted()
    {
        var emisor = Serialize(EcfTestData.CreditoFiscal()).Element("Encabezado")!.Element("Emisor")!;

        emisor.Element("NombreComercial").ShouldBeNull();
        emisor.Element("Sucursal").ShouldBeNull();
        emisor.Element("CodigoVendedor").ShouldBeNull();
    }

    [Fact]
    public void Text_escapes_the_dgii_special_characters()
    {
        var line = EcfTestData.Line(name: "Café \"René\" & Cía. © €");
        var xml = Sut.Serialize(EcfTestData.CreditoFiscal(line), EcfTestData.SignedAt);

        xml.ShouldContain("&amp;");
        xml.ShouldContain("&quot;");
        xml.ShouldContain("&#169;");
        xml.ShouldContain("&#8364;");
        // y sigue siendo XML válido
        var name = XDocument.Parse(xml).Root!
            .Element("DetallesItems")!.Element("Item")!.Element("NombreItem")!.Value;
        name.ShouldBe("Café \"René\" & Cía. © €");
    }

    [Fact]
    public void Zero_rate_line_produces_the_I3_bucket_not_the_exempt_total()
    {
        var totales = Serialize(EcfTestData.CreditoFiscal(
                EcfTestData.Line(rate: ItbisRate.Zero, unitPrice: 500m)))
            .Element("Encabezado")!.Element("Totales")!;

        totales.Element("MontoGravadoI3")!.Value.ShouldBe("500");
        totales.Element("ITBIS3")!.Value.ShouldBe("0");
        totales.Element("MontoExento").ShouldBeNull();
    }
}
