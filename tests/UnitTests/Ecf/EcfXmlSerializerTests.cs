using System.Xml.Linq;
using NovaFE.Domain.Common;
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
    public void Pagos_exterior_has_a_reduced_buyer_isr_only_retention_and_the_isr_total()
    {
        var root = Serialize(EcfTestData.PagosExterior());

        var idDoc = root.Element("Encabezado")!.Element("IdDoc")!;
        idDoc.Elements().Select(e => e.Name.LocalName).ShouldBe(
        [
            "TipoeCF", "eNCF", "FechaVencimientoSecuencia",
            "TipoPago", "FechaLimitePago", "TablaFormasPago",
        ]);
        idDoc.Element("TipoeCF")!.Value.ShouldBe("47");
        idDoc.Element("TipoIngresos").ShouldBeNull();
        idDoc.Element("IndicadorMontoGravado").ShouldBeNull();

        var comprador = root.Element("Encabezado")!.Element("Comprador")!;
        comprador.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["IdentificadorExtranjero", "RazonSocialComprador"]);

        var retencion = root.Element("DetallesItems")!.Element("Item")!.Element("Retencion")!;
        retencion.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["IndicadorAgenteRetencionoPercepcion", "MontoISRRetenido"]);
        retencion.Element("MontoISRRetenido")!.Value.ShouldBe("13500");
        retencion.Element("MontoITBISRetenido").ShouldBeNull();

        var totales = root.Element("Encabezado")!.Element("Totales")!;
        totales.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["MontoExento", "MontoTotal", "ValorPagar", "TotalISRRetencion"]);
        totales.Element("MontoTotal")!.Value.ShouldBe("50000");
        totales.Element("ValorPagar")!.Value.ShouldBe("36500");        // 50000 - 13500
        totales.Element("TotalISRRetencion")!.Value.ShouldBe("13500");
        totales.Element("TotalITBISRetenido").ShouldBeNull();
    }

    [Fact]
    public void Exportaciones_omits_the_taxed_indicator_and_totales_use_the_zero_rate_bucket()
    {
        var root = Serialize(EcfTestData.Exportaciones());
        var idDoc = root.Element("Encabezado")!.Element("IdDoc")!;

        idDoc.Elements().Select(e => e.Name.LocalName).ShouldBe(
        [
            "TipoeCF", "eNCF", "FechaVencimientoSecuencia",
            "TipoIngresos", "TipoPago", "FechaLimitePago", "TablaFormasPago",
        ]);
        idDoc.Element("TipoeCF")!.Value.ShouldBe("46");
        idDoc.Element("IndicadorMontoGravado").ShouldBeNull();

        root.Element("Encabezado")!.Element("Comprador")!.Element("IdentificadorExtranjero")!.Value
            .ShouldBe("US-4471203");

        var totales = root.Element("Encabezado")!.Element("Totales")!;
        totales.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["MontoGravadoTotal", "MontoGravadoI3", "ITBIS3", "TotalITBIS3", "MontoTotal"]);
        totales.Element("MontoGravadoI3")!.Value.ShouldBe("15000");
        totales.Element("ITBIS3")!.Value.ShouldBe("0");
        totales.Element("MontoTotal")!.Value.ShouldBe("15000");
        totales.Element("MontoExento").ShouldBeNull();
    }

    [Fact]
    public void Gastos_menores_has_a_minimal_iddoc_no_buyer_and_exempt_only_totales()
    {
        var root = Serialize(EcfTestData.GastosMenores());

        var idDoc = root.Element("Encabezado")!.Element("IdDoc")!;
        idDoc.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["TipoeCF", "eNCF", "FechaVencimientoSecuencia", "TipoPago"]);
        idDoc.Element("TipoeCF")!.Value.ShouldBe("43");

        root.Element("Encabezado")!.Element("Comprador").ShouldBeNull();
        root.Element("Encabezado")!.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["Version", "IdDoc", "Emisor", "Totales"]);

        var totales = root.Element("Encabezado")!.Element("Totales")!;
        totales.Elements().Select(e => e.Name.LocalName).ShouldBe(["MontoExento", "MontoTotal"]);
        totales.Element("MontoExento")!.Value.ShouldBe("350");
        totales.Element("MontoTotal")!.Value.ShouldBe("350");
    }

    [Fact]
    public void Regimenes_especiales_iddoc_omits_the_taxed_indicator_and_totales_are_exempt_only()
    {
        var root = Serialize(EcfTestData.RegimenesEspeciales());
        var idDoc = root.Element("Encabezado")!.Element("IdDoc")!;

        idDoc.Elements().Select(e => e.Name.LocalName).ShouldBe(
        [
            "TipoeCF", "eNCF", "FechaVencimientoSecuencia",
            "TipoIngresos", "TipoPago", "FechaLimitePago", "TablaFormasPago",
        ]);
        idDoc.Element("TipoeCF")!.Value.ShouldBe("44");
        idDoc.Element("IndicadorMontoGravado").ShouldBeNull();

        var totales = root.Element("Encabezado")!.Element("Totales")!;
        totales.Elements().Select(e => e.Name.LocalName).ShouldBe(["MontoExento", "MontoTotal"]);
        totales.Element("MontoExento")!.Value.ShouldBe("2000");
        totales.Element("MontoGravadoTotal").ShouldBeNull();
        totales.Element("ITBIS1").ShouldBeNull();
    }

    [Fact]
    public void Gubernamental_serializes_exactly_like_a_credito_fiscal()
    {
        var idDoc = Serialize(EcfTestData.Gubernamental()).Element("Encabezado")!.Element("IdDoc")!;

        idDoc.Elements().Select(e => e.Name.LocalName).ShouldBe(
        [
            "TipoeCF", "eNCF", "FechaVencimientoSecuencia", "IndicadorMontoGravado",
            "TipoIngresos", "TipoPago", "FechaLimitePago", "TablaFormasPago",
        ]);
        idDoc.Element("TipoeCF")!.Value.ShouldBe("45");
    }

    [Fact]
    public void Compras_iddoc_omits_the_income_type_and_the_item_carries_the_retention()
    {
        var root = Serialize(EcfTestData.Compras(
            EcfTestData.Line(unitPrice: 1000m, retention: EcfTestData.Retention(itbisWithheld: 54m, isrWithheld: 100m))));
        var idDoc = root.Element("Encabezado")!.Element("IdDoc")!;

        idDoc.Elements().Select(e => e.Name.LocalName).ShouldBe(
        [
            "TipoeCF", "eNCF", "FechaVencimientoSecuencia", "IndicadorMontoGravado",
            "TipoPago", "FechaLimitePago", "TablaFormasPago",
        ]);
        idDoc.Element("TipoeCF")!.Value.ShouldBe("41");
        idDoc.Element("TipoIngresos").ShouldBeNull();

        var retencion = root.Element("DetallesItems")!.Element("Item")!.Element("Retencion")!;
        retencion.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["IndicadorAgenteRetencionoPercepcion", "MontoITBISRetenido", "MontoISRRetenido"]);
        retencion.Element("IndicadorAgenteRetencionoPercepcion")!.Value.ShouldBe("1");
        retencion.Element("MontoITBISRetenido")!.Value.ShouldBe("54");
    }

    [Fact]
    public void Compras_totales_carry_the_net_payable_and_the_withholding_totals()
    {
        var totales = Serialize(EcfTestData.Compras(
                EcfTestData.Line(unitPrice: 1000m, retention: EcfTestData.Retention(itbisWithheld: 54m, isrWithheld: 100m))))
            .Element("Encabezado")!.Element("Totales")!;

        totales.Element("MontoTotal")!.Value.ShouldBe("1180");          // 1000 + 180 ITBIS
        totales.Element("ValorPagar")!.Value.ShouldBe("1026");          // 1180 - 54 - 100
        totales.Element("TotalITBISRetenido")!.Value.ShouldBe("54");
        totales.Element("TotalISRRetencion")!.Value.ShouldBe("100");
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
    public void Shipping_and_transport_blocks_sit_between_comprador_and_totales()
    {
        var header = EcfTestData.Header(31) with
        {
            Shipping = new EcfShippingInfo(
                ShipmentDate: new DateOnly(2026, 3, 1),
                ContainerNumber: "MSKU7654321",
                GrossWeight: 1250.50m,
                GrossWeightUnit: "43"),
            Transport = new EcfTransport(Driver: "Juan Pérez", Plate: "A123456"),
        };
        var root = XDocument.Parse(Sut.Serialize(
            EcfDocument.Create(EcfType.CreditoFiscal, header, [EcfTestData.Line()]).Value,
            EcfTestData.SignedAt)).Root!;
        var enc = root.Element("Encabezado")!;

        enc.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["Version", "IdDoc", "Emisor", "Comprador", "InformacionesAdicionales", "Transporte", "Totales"]);
        var info = enc.Element("InformacionesAdicionales")!;
        info.Element("FechaEmbarque")!.Value.ShouldBe("01-03-2026");
        info.Element("PesoBruto")!.Value.ShouldBe("1250.5");
        enc.Element("Transporte")!.Element("Placa")!.Value.ShouldBe("A123456");
    }

    [Fact]
    public void Export_shipping_fields_are_emitted_only_for_type_46_in_the_xsd_order()
    {
        var header = EcfTestData.Header(46) with
        {
            Buyer = new EcfBuyer("Global Imports LLC", ForeignId: "US-4471203"),
            Shipping = new EcfShippingInfo(
                ReferenceNumber: "7788",
                Export: new EcfExportDetails(
                    DeliveryTerms: "FOB", TotalFob: 15000m, Insurance: 300m, Freight: 1200m,
                    OtherCharges: 0m, TotalCif: 16500m),
                GrossWeight: 900m),
            Transport = new EcfTransport(Via: TransportVia.Sea, DestinationCountry: "Estados Unidos"),
        };
        var built = EcfDocument.Create(
            EcfType.Exportaciones, header,
            [EcfTestData.Line(rate: NovaFE.Domain.Fiscal.ItbisRate.Zero, unitPrice: 15000m)]).Value;
        var info = XDocument.Parse(Sut.Serialize(built, EcfTestData.SignedAt)).Root!
            .Element("Encabezado")!.Element("InformacionesAdicionales")!;

        info.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["NumeroReferencia", "CondicionesEntrega", "TotalFob", "Seguro", "Flete", "TotalCif", "PesoBruto"]);
        info.Element("TotalCif")!.Value.ShouldBe("16500");
        var transporte = XDocument.Parse(Sut.Serialize(built, EcfTestData.SignedAt)).Root!
            .Element("Encabezado")!.Element("Transporte")!;
        transporte.Element("ViaTransporte")!.Value.ShouldBe("02");
        transporte.Element("PaisDestino")!.Value.ShouldBe("Estados Unidos");
    }

    [Fact]
    public void Otra_moneda_block_follows_totales_with_the_full_itbis_subset()
    {
        var header = EcfTestData.Header(31) with
        {
            ForeignCurrency = new EcfForeignCurrency(
                CurrencyCode.USD, 58.50m,
                new EcfForeignCurrencyTotals(
                    MontoGravadoTotal: 34.19m, MontoGravadoI1: 34.19m,
                    TotalItbis: 6.15m, TotalItbis1: 6.15m, MontoTotal: 40.34m)),
        };
        var line = EcfTestData.Line() with
        {
            ForeignCurrency = new EcfLineForeignCurrency(UnitPrice: 34.19m, LineAmount: 34.19m),
        };
        var root = XDocument.Parse(Sut.Serialize(
            EcfDocument.Create(EcfType.CreditoFiscal, header, [line]).Value, EcfTestData.SignedAt)).Root!;
        var enc = root.Element("Encabezado")!;

        enc.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["Version", "IdDoc", "Emisor", "Comprador", "Totales", "OtraMoneda"]);
        var om = enc.Element("OtraMoneda")!;
        om.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["TipoMoneda", "TipoCambio", "MontoGravadoTotalOtraMoneda", "MontoGravado1OtraMoneda",
             "TotalITBISOtraMoneda", "TotalITBIS1OtraMoneda", "MontoTotalOtraMoneda"]);
        om.Element("TipoMoneda")!.Value.ShouldBe("USD");
        om.Element("TipoCambio")!.Value.ShouldBe("58.5");
        om.Element("MontoTotalOtraMoneda")!.Value.ShouldBe("40.34");

        var detalle = root.Element("DetallesItems")!.Element("Item")!.Element("OtraMonedaDetalle")!;
        detalle.Element("PrecioOtraMoneda")!.Value.ShouldBe("34.19");
    }

    [Fact]
    public void Seccion_d_is_emitted_after_detalles_items_and_reconciles_the_totales()
    {
        var header = EcfTestData.Header(31) with
        {
            GlobalAdjustments =
            [
                new EcfGlobalAdjustment(1, AdjustmentKind.Discount, ItbisRate.Eighteen, 1000m, Description: "Promo lanzamiento"),
            ],
        };
        var root = XDocument.Parse(Sut.Serialize(
            EcfDocument.Create(EcfType.CreditoFiscal, header, [EcfTestData.Line(unitPrice: 10000m)]).Value,
            EcfTestData.SignedAt)).Root!;

        root.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["Encabezado", "DetallesItems", "DescuentosORecargos", "FechaHoraFirma"]);
        var dr = root.Element("DescuentosORecargos")!.Element("DescuentoORecargo")!;
        dr.Element("TipoAjuste")!.Value.ShouldBe("D");
        dr.Element("MontoDescuentooRecargo")!.Value.ShouldBe("1000");
        dr.Element("IndicadorFacturacionDescuentooRecargo")!.Value.ShouldBe("1");

        var totales = root.Element("Encabezado")!.Element("Totales")!;
        totales.Element("MontoGravadoI1")!.Value.ShouldBe("9000");
        totales.Element("TotalITBIS")!.Value.ShouldBe("1620");
        totales.Element("MontoTotal")!.Value.ShouldBe("10620");
    }

    [Fact]
    public void Norma_10_07_discount_emits_valor_pagar_below_monto_total()
    {
        var header = EcfTestData.Header(31) with
        {
            GlobalAdjustments =
            [
                new EcfGlobalAdjustment(1, AdjustmentKind.Discount, ItbisRate.Eighteen, 1000m, Norma1007: true),
            ],
        };
        var totales = XDocument.Parse(Sut.Serialize(
                EcfDocument.Create(EcfType.CreditoFiscal, header, [EcfTestData.Line(unitPrice: 10000m)]).Value,
                EcfTestData.SignedAt)).Root!
            .Element("Encabezado")!.Element("Totales")!;

        totales.Element("MontoGravadoI1")!.Value.ShouldBe("10000");   // base intacta
        totales.Element("MontoTotal")!.Value.ShouldBe("11800");
        totales.Element("ValorPagar")!.Value.ShouldBe("10800");       // 11800 - 1000
        totales.Element("DescuentoORecargo").ShouldBeNull();          // no se emite en Totales
    }

    [Fact]
    public void Impuestos_adicionales_breakdown_and_line_extras_land_in_the_right_places()
    {
        var line = EcfTestData.Line(name: "Ron añejo", unitPrice: 1000m) with
        {
            AdditionalTaxes = 236.30m,
            AdditionalTaxDetail =
            [
                new EcfAdditionalTax("014", Rate: 10m, IscEspecifico: 191.30m),
                new EcfAdditionalTax("002", Rate: 2m, Otros: 45.00m),
            ],
            Details = new EcfLineDetails(AlcoholDegrees: 40m, ReferenceQuantity: 0.75m, ReferenceUnit: "43"),
        };
        var root = XDocument.Parse(Sut.Serialize(
            EcfDocument.Create(EcfType.CreditoFiscal, EcfTestData.Header(31), [line]).Value,
            EcfTestData.SignedAt)).Root!;

        var totales = root.Element("Encabezado")!.Element("Totales")!;
        var impuestos = totales.Element("ImpuestosAdicionales")!.Elements("ImpuestoAdicional").ToList();
        impuestos[0].Element("TipoImpuesto")!.Value.ShouldBe("002");   // ordenado por código
        impuestos[1].Element("TipoImpuesto")!.Value.ShouldBe("014");
        impuestos[1].Element("MontoImpuestoSelectivoConsumoEspecifico")!.Value.ShouldBe("191.3");

        var item = root.Element("DetallesItems")!.Element("Item")!;
        item.Element("GradosAlcohol")!.Value.ShouldBe("40");
        item.Element("CantidadReferencia")!.Value.ShouldBe("0.75");
        item.Element("TablaImpuestoAdicional")!.Elements("ImpuestoAdicional").Select(e => e.Element("TipoImpuesto")!.Value)
            .ShouldBe(["014", "002"]);
    }

    [Fact]
    public void Subtotales_and_paginacion_are_emitted_after_detalles_in_xsd_order()
    {
        var header = EcfTestData.Header(31) with
        {
            Subtotals = [new EcfSubtotal(Number: 1, Description: "Bebidas", MontoGravadoTotal: 1000m, Amount: 1180m, Lines: 1)],
            Pagination = [new EcfPage(Number: 1, LineFrom: 1, LineTo: 1, Amount: 1180m)],
        };
        var root = XDocument.Parse(Sut.Serialize(
            EcfDocument.Create(EcfType.CreditoFiscal, header, [EcfTestData.Line(unitPrice: 1000m)]).Value,
            EcfTestData.SignedAt)).Root!;

        root.Elements().Select(e => e.Name.LocalName).ShouldBe(
            ["Encabezado", "DetallesItems", "Subtotales", "Paginacion", "FechaHoraFirma"]);
        root.Element("Subtotales")!.Element("Subtotal")!.Element("DescripcionSubtotal")!.Value.ShouldBe("Bebidas");
        root.Element("Paginacion")!.Element("Pagina")!.Element("NoLineaHasta")!.Value.ShouldBe("1");
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
