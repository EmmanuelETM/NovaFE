using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;
using NovaFE.Domain.Sequences;

namespace NovaFE.UnitTests.Ecf;

public class EcfDocumentTests
{
    [Fact]
    public void Create_computes_the_totals_with_the_fiscal_engine()
    {
        var document = EcfDocument.Create(
            EcfType.CreditoFiscal,
            EcfTestData.Header(31),
            [EcfTestData.Line(unitPrice: 2000.00m, quantity: 1m)]);

        document.IsError.ShouldBeFalse();
        var totals = document.Value.Totals;
        totals.MontoGravadoI1.ShouldBe(2000.00m);
        totals.TotalItbis.ShouldBe(360.00m);
        totals.MontoTotal.ShouldBe(2360.00m);
    }

    [Fact]
    public void Create_rejects_an_encf_whose_type_does_not_match()
        => EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(32),   // e-NCF E32...
                [EcfTestData.Line()])
            .FirstError.Code.ShouldBe("Ecf.EncfTypeMismatch");

    [Fact]
    public void Create_rejects_an_empty_detail()
        => EcfDocument.Create(EcfType.CreditoFiscal, EcfTestData.Header(31), [])
            .FirstError.Code.ShouldBe("Ecf.NoLines");

    [Fact]
    public void Create_rejects_non_contiguous_line_numbers()
        => EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(31),
                [EcfTestData.Line(number: 1), EcfTestData.Line(number: 3)])
            .FirstError.Code.ShouldBe("Ecf.NonContiguousLineNumbers");

    [Fact]
    public void Credito_fiscal_needs_a_sequence_expiry()
    {
        var header = EcfTestData.Header(31) with { SequenceExpiresOn = null };

        EcfDocument.Create(EcfType.CreditoFiscal, header, [EcfTestData.Line()])
            .FirstError.Code.ShouldBe("Ecf.SequenceExpiryRequired");
    }

    [Fact]
    public void Nota_credito_must_not_carry_a_sequence_expiry()
    {
        var header = EcfTestData.Header(34) with { SequenceExpiresOn = new DateOnly(2027, 12, 31) };

        EcfDocument.Create(EcfType.NotaCredito, header, [EcfTestData.Line()],
                new EcfReference("E310000000010", new DateOnly(2026, 1, 10), ModificationCode.CorrectsAmounts))
            .FirstError.Code.ShouldBe("Ecf.SequenceExpiryNotApplicable");
    }

    [Fact]
    public void Nota_credito_requires_the_reference_section()
    {
        var header = EcfTestData.Header(34) with { SequenceExpiresOn = null };

        EcfDocument.Create(EcfType.NotaCredito, header, [EcfTestData.Line()])
            .FirstError.Code.ShouldBe("Ecf.ReferenceRequired");
    }

    [Fact]
    public void Nota_credito_computes_the_thirty_day_indicator()
    {
        var header = EcfTestData.Header(34) with { SequenceExpiresOn = null };
        var reference = new EcfReference("E310000000010", EcfTestData.IssueDate.AddDays(-20), ModificationCode.CorrectsAmounts);

        var document = EcfDocument.Create(EcfType.NotaCredito, header, [EcfTestData.Line()], reference);

        document.IsError.ShouldBeFalse();
        document.Value.CreditNoteIndicator.ShouldBe(0); // dentro de 30 días
    }

    [Fact]
    public void Foreign_currency_cross_check_flags_a_mismatch_without_rejecting()
    {
        // MontoTotal DOP = 2360; / 58.50 ≈ 40.34. Declaramos 40.34 → dentro de tolerancia.
        var ok = EcfDocument.Create(
            EcfType.CreditoFiscal,
            EcfTestData.Header(31) with
            {
                ForeignCurrency = new EcfForeignCurrency(CurrencyCode.USD, 58.50m,
                    new EcfForeignCurrencyTotals(MontoTotal: 40.34m)),
            },
            [EcfTestData.Line()]);
        ok.IsError.ShouldBeFalse();
        ok.Value.ForeignCurrencyCheck!.WithinTolerance.ShouldBeTrue();

        // Declaramos 999 → fuera de tolerancia pero el documento SIGUE siendo válido.
        var off = EcfDocument.Create(
            EcfType.CreditoFiscal,
            EcfTestData.Header(31) with
            {
                ForeignCurrency = new EcfForeignCurrency(CurrencyCode.USD, 58.50m,
                    new EcfForeignCurrencyTotals(MontoTotal: 999m)),
            },
            [EcfTestData.Line()]);
        off.IsError.ShouldBeFalse();
        off.Value.ForeignCurrencyCheck!.WithinTolerance.ShouldBeFalse();
    }

    [Fact]
    public void A_line_in_foreign_currency_without_the_header_block_is_rejected()
        => EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(31),
                [EcfTestData.Line() with { ForeignCurrency = new EcfLineForeignCurrency(UnitPrice: 10m) }])
            .FirstError.Code.ShouldBe("Ecf.LineForeignCurrencyWithoutHeader");

    [Fact]
    public void A_non_positive_exchange_rate_is_rejected()
        => EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(31) with
                {
                    ForeignCurrency = new EcfForeignCurrency(CurrencyCode.USD, 0m, new EcfForeignCurrencyTotals()),
                },
                [EcfTestData.Line()])
            .FirstError.Code.ShouldBe("Ecf.InvalidExchangeRate");

    [Fact]
    public void Pagos_exterior_rejects_seccion_d()
        => EcfDocument.Create(
                EcfType.PagosExterior,
                EcfTestData.Header(47) with
                {
                    Buyer = new EcfBuyer("Foreign Co", ForeignId: "X1"),
                    GlobalAdjustments = [new EcfGlobalAdjustment(1, AdjustmentKind.Discount, ItbisRate.Exempt, 10m)],
                },
                [EcfTestData.Line(rate: ItbisRate.Exempt,
                    retention: new EcfLineRetention(RetentionAgent.Withholding, IsrWithheld: 100m))])
            .FirstError.Code.ShouldBe("Ecf.BlockNotApplicable");

    [Fact]
    public void Norma_10_07_is_rejected_on_a_surcharge()
        => EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(31) with
                {
                    GlobalAdjustments =
                        [new EcfGlobalAdjustment(1, AdjustmentKind.Surcharge, ItbisRate.Eighteen, 100m, Norma1007: true)],
                },
                [EcfTestData.Line()])
            .FirstError.Code.ShouldBe("Ecf.Norma1007NotApplicable");

    [Fact]
    public void Seccion_d_line_numbers_must_be_contiguous()
        => EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(31) with
                {
                    GlobalAdjustments =
                    [
                        new EcfGlobalAdjustment(1, AdjustmentKind.Discount, ItbisRate.Eighteen, 10m),
                        new EcfGlobalAdjustment(3, AdjustmentKind.Discount, ItbisRate.Eighteen, 10m),
                    ],
                },
                [EcfTestData.Line()])
            .FirstError.Code.ShouldBe("Ecf.NonContiguousGlobalAdjustmentLines");

    [Fact]
    public void Compras_rejects_the_shipping_block()
        => EcfDocument.Create(
                EcfType.Compras,
                EcfTestData.Header(41) with { Shipping = new EcfShippingInfo(ContainerNumber: "X") },
                [EcfTestData.Line(retention: EcfTestData.Retention())])
            .FirstError.Code.ShouldBe("Ecf.BlockNotApplicable");

    [Fact]
    public void A_non_export_rejects_export_shipping_fields()
        => EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(31) with
                {
                    Shipping = new EcfShippingInfo(Export: new EcfExportDetails(TotalFob: 100m)),
                },
                [EcfTestData.Line()])
            .FirstError.Code.ShouldBe("Ecf.ExportFieldsOnlyForExports");

    [Fact]
    public void Pagos_exterior_transport_rejects_non_destination_fields()
        => EcfDocument.Create(
                EcfType.PagosExterior,
                EcfTestData.Header(47) with
                {
                    Buyer = new EcfBuyer("Foreign Co", ForeignId: "X1"),
                    Transport = new EcfTransport(Driver: "no permitido", DestinationCountry: "UK"),
                },
                [EcfTestData.Line(rate: ItbisRate.Exempt,
                    retention: new EcfLineRetention(RetentionAgent.Withholding, IsrWithheld: 100m))])
            .FirstError.Code.ShouldBe("Ecf.TransportForPagosExteriorIsDestinationOnly");

    [Fact]
    public void Pagos_exterior_requires_isr_retention_on_every_line()
        => EcfDocument.Create(
                EcfType.PagosExterior,
                EcfTestData.Header(47) with { Buyer = new EcfBuyer("Foreign Co", ForeignId: "X1") },
                [EcfTestData.Line(rate: ItbisRate.Exempt,
                    retention: new EcfLineRetention(RetentionAgent.Withholding, IsrWithheld: 100m)),
                 EcfTestData.Line(number: 2, rate: ItbisRate.Exempt)])
            .FirstError.Code.ShouldBe("Ecf.RetentionRequired");

    [Fact]
    public void Pagos_exterior_rejects_itbis_in_the_retention()
        => EcfDocument.Create(
                EcfType.PagosExterior,
                EcfTestData.Header(47) with { Buyer = new EcfBuyer("Foreign Co", ForeignId: "X1") },
                [EcfTestData.Line(rate: ItbisRate.Exempt,
                    retention: new EcfLineRetention(RetentionAgent.Withholding, ItbisWithheld: 50m, IsrWithheld: 100m))])
            .FirstError.Code.ShouldBe("Ecf.ItbisRetentionNotApplicable");

    [Fact]
    public void Pagos_exterior_rejects_a_non_exempt_line()
        => EcfDocument.Create(
                EcfType.PagosExterior,
                EcfTestData.Header(47) with { Buyer = new EcfBuyer("Foreign Co", ForeignId: "X1") },
                [EcfTestData.Line(rate: ItbisRate.Eighteen,
                    retention: new EcfLineRetention(RetentionAgent.Withholding, IsrWithheld: 100m))])
            .FirstError.Code.ShouldBe("Ecf.OnlyExemptLinesAllowed");

    [Fact]
    public void Exportaciones_rejects_an_exempt_line()
        => EcfDocument.Create(
                EcfType.Exportaciones,
                EcfTestData.Header(46) with { Buyer = new EcfBuyer("Global Imports LLC", ForeignId: "US-1") },
                [EcfTestData.Line(rate: ItbisRate.Exempt)])
            .FirstError.Code.ShouldBe("Ecf.OnlyZeroRatedLinesAllowed");

    [Fact]
    public void Exportaciones_rejects_a_taxed_line()
        => EcfDocument.Create(
                EcfType.Exportaciones,
                EcfTestData.Header(46) with { Buyer = new EcfBuyer("Global Imports LLC", ForeignId: "US-1") },
                [EcfTestData.Line(rate: ItbisRate.Eighteen)])
            .FirstError.Code.ShouldBe("Ecf.OnlyZeroRatedLinesAllowed");

    [Fact]
    public void Gastos_menores_rejects_a_taxed_line()
        => EcfDocument.Create(
                EcfType.GastosMenores,
                EcfTestData.Header(43),
                [EcfTestData.Line(rate: ItbisRate.Eighteen)])
            .FirstError.Code.ShouldBe("Ecf.OnlyExemptLinesAllowed");

    [Fact]
    public void Gastos_menores_rejects_a_line_discount()
        => EcfDocument.Create(
                EcfType.GastosMenores,
                EcfTestData.Header(43),
                [EcfTestData.Line(rate: ItbisRate.Exempt) with { Discount = 10m }])
            .FirstError.Code.ShouldBe("Ecf.LineAdjustmentsNotApplicable");

    [Fact]
    public void Gastos_menores_rejects_a_non_invoiceable_amount()
        => EcfDocument.Create(
                EcfType.GastosMenores,
                EcfTestData.Header(43) with { NonInvoiceableAmount = 50m },
                [EcfTestData.Line(rate: ItbisRate.Exempt)])
            .FirstError.Code.ShouldBe("Ecf.NonInvoiceableAmountNotApplicable");

    [Fact]
    public void Regimenes_especiales_rejects_a_taxed_line()
        => EcfDocument.Create(
                EcfType.RegimenesEspeciales,
                EcfTestData.Header(44),
                [EcfTestData.Line(rate: ItbisRate.Eighteen)])
            .FirstError.Code.ShouldBe("Ecf.OnlyExemptLinesAllowed");

    [Fact]
    public void Regimenes_especiales_requires_the_income_type()
        => EcfDocument.Create(
                EcfType.RegimenesEspeciales,
                EcfTestData.Header(44) with { IncomeType = "" },
                [EcfTestData.Line(rate: ItbisRate.Exempt)])
            .FirstError.Code.ShouldBe("Ecf.IncomeTypeRequired");

    [Fact]
    public void Gubernamental_requires_the_buyer_rnc()
        => EcfDocument.Create(
                EcfType.Gubernamental,
                EcfTestData.Header(45) with { Buyer = new EcfBuyer("Ministerio de Hacienda") },
                [EcfTestData.Line()])
            .FirstError.Code.ShouldBe("Ecf.BuyerIdentificationRequired");

    [Fact]
    public void Compras_requires_the_retention_area_on_every_line()
        => EcfDocument.Create(
                EcfType.Compras,
                EcfTestData.Header(41),
                [EcfTestData.Line(retention: EcfTestData.Retention()), EcfTestData.Line(number: 2)])
            .FirstError.Code.ShouldBe("Ecf.RetentionRequired");

    [Fact]
    public void Compras_does_not_carry_an_income_type()
    {
        var document = EcfDocument.Create(
            EcfType.Compras,
            EcfTestData.Header(41) with { IncomeType = "" },
            [EcfTestData.Line(retention: EcfTestData.Retention())]);

        document.IsError.ShouldBeFalse();   // el 41 no lleva TipoIngresos
    }

    [Fact]
    public void A_credito_fiscal_with_a_retention_area_is_rejected()
        => EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(31),
                [EcfTestData.Line(retention: EcfTestData.Retention())])
            .FirstError.Code.ShouldBe("Ecf.RetentionNotApplicable");

    [Fact]
    public void Credito_fiscal_always_requires_the_buyer_identification()
    {
        var header = EcfTestData.Header(31) with { Buyer = new EcfBuyer("Consumidor Final") };

        EcfDocument.Create(EcfType.CreditoFiscal, header, [EcfTestData.Line()])
            .FirstError.Code.ShouldBe("Ecf.BuyerIdentificationRequired");
    }

    // --- identificación del comprador condicional (tipos 32/33/34) --------

    private static EcfHeader Consumo(EcfBuyer buyer) =>
        EcfTestData.Header(32) with { SequenceExpiresOn = null, Buyer = buyer };

    [Fact]
    public void Consumo_requires_the_income_type()
        => EcfDocument.Create(
                EcfType.Consumo,
                Consumo(new EcfBuyer("Consumidor Final")) with { IncomeType = "" },
                [EcfTestData.Line(unitPrice: 1000m)])
            .FirstError.Code.ShouldBe("Ecf.IncomeTypeRequired");

    [Fact]
    public void Consumo_below_the_threshold_does_not_need_a_buyer()
        => EcfDocument.Create(
                EcfType.Consumo,
                Consumo(new EcfBuyer("Consumidor Final")),
                [EcfTestData.Line(unitPrice: 1000m)])
            .IsError.ShouldBeFalse();

    [Fact]
    public void Consumo_at_or_above_the_threshold_needs_the_buyer_identified()
        => EcfDocument.Create(
                EcfType.Consumo,
                Consumo(new EcfBuyer("Cliente Grande")),
                [EcfTestData.Line(unitPrice: 300_000m)])
            .FirstError.Code.ShouldBe("Ecf.BuyerIdentificationRequired");

    [Fact]
    public void Consumo_high_value_with_a_foreign_id_is_accepted()
        => EcfDocument.Create(
                EcfType.Consumo,
                Consumo(new EcfBuyer("Foreign Buyer LLC", ForeignId: "US-99887766")),
                [EcfTestData.Line(unitPrice: 300_000m)])
            .IsError.ShouldBeFalse();

    [Fact]
    public void Nota_credito_that_modifies_a_credito_fiscal_needs_the_buyer()
    {
        var header = EcfTestData.Header(34) with
        {
            SequenceExpiresOn = null,
            Buyer = new EcfBuyer("Sin identificar"),
        };
        var reference = new EcfReference("E310000000010", EcfTestData.IssueDate.AddDays(-5), ModificationCode.CorrectsAmounts);

        EcfDocument.Create(EcfType.NotaCredito, header, [EcfTestData.Line()], reference)
            .FirstError.Code.ShouldBe("Ecf.BuyerIdentificationRequired");
    }

    [Fact]
    public void Nota_credito_that_modifies_a_small_consumo_does_not_need_the_buyer()
    {
        var header = EcfTestData.Header(34) with
        {
            SequenceExpiresOn = null,
            Buyer = new EcfBuyer("Consumidor Final"),
        };
        var reference = new EcfReference("E320000000010", EcfTestData.IssueDate.AddDays(-5), ModificationCode.CorrectsAmounts);

        EcfDocument.Create(EcfType.NotaCredito, header, [EcfTestData.Line(unitPrice: 1000m)], reference)
            .IsError.ShouldBeFalse();
    }

    [Fact]
    public void Nota_credito_over_the_threshold_needs_the_buyer_regardless_of_what_it_modifies()
    {
        var header = EcfTestData.Header(34) with
        {
            SequenceExpiresOn = null,
            Buyer = new EcfBuyer("Consumidor Final"),
        };
        var reference = new EcfReference("E320000000010", EcfTestData.IssueDate.AddDays(-5), ModificationCode.CorrectsAmounts);

        EcfDocument.Create(EcfType.NotaCredito, header, [EcfTestData.Line(unitPrice: 300_000m)], reference)
            .FirstError.Code.ShouldBe("Ecf.BuyerIdentificationRequired");
    }
}
