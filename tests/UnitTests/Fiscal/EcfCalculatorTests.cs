using ErrorOr;
using NovaFE.Domain.Fiscal;

namespace NovaFE.UnitTests.Fiscal;

public class EcfCalculatorTests
{
    private static EcfLineInput Line(
        int number,
        ItbisRate rate,
        decimal quantity,
        decimal unitPrice,
        decimal discount = 0m,
        decimal surcharge = 0m,
        bool priceIncludesTax = false,
        decimal additionalTaxes = 0m,
        decimal? supplied = null,
        decimal itbisWithheld = 0m,
        decimal isrWithheld = 0m)
        => new(number, rate, quantity, unitPrice, discount, surcharge, priceIncludesTax, additionalTaxes, supplied, itbisWithheld, isrWithheld);

    [Fact]
    public void Taxed_line_with_tax_on_top()
    {
        var result = EcfCalculator.Calculate([Line(1, ItbisRate.Eighteen, quantity: 2m, unitPrice: 100m)]);

        result.IsError.ShouldBeFalse();
        var line = result.Value.Lines[0];
        line.LineAmount.ShouldBe(200.00m);
        line.TaxableBase.ShouldBe(200.00m);
        line.TaxAmount.ShouldBe(36.00m);

        var totals = result.Value.Totals;
        totals.MontoGravadoI1.ShouldBe(200.00m);
        totals.Itbis1.ShouldBe(36.00m);
        totals.MontoGravadoTotal.ShouldBe(200.00m);
        totals.TotalItbis.ShouldBe(36.00m);
        totals.MontoTotal.ShouldBe(236.00m);
        totals.MontoPeriodo.ShouldBe(236.00m);
    }

    [Fact]
    public void Taxed_line_with_tax_included_extracts_the_base_and_reconciles()
    {
        var result = EcfCalculator.Calculate(
            [Line(1, ItbisRate.Eighteen, quantity: 1m, unitPrice: 100m, priceIncludesTax: true)]);

        var line = result.Value.Lines[0];
        line.LineAmount.ShouldBe(100.00m);
        line.TaxableBase.ShouldBe(84.75m);
        line.TaxAmount.ShouldBe(15.25m);
        (line.TaxableBase + line.TaxAmount).ShouldBe(line.LineAmount);

        result.Value.Totals.MontoTotal.ShouldBe(100.00m);
    }

    [Fact]
    public void Tax_included_with_a_clean_ratio()
    {
        var result = EcfCalculator.Calculate(
            [Line(1, ItbisRate.Eighteen, quantity: 1m, unitPrice: 118m, priceIncludesTax: true)]);

        result.Value.Lines[0].TaxableBase.ShouldBe(100.00m);
        result.Value.Lines[0].TaxAmount.ShouldBe(18.00m);
    }

    [Fact]
    public void Exempt_line_goes_to_the_exempt_total_and_carries_no_tax()
    {
        var result = EcfCalculator.Calculate([Line(1, ItbisRate.Exempt, quantity: 1m, unitPrice: 500m)]);

        var totals = result.Value.Totals;
        totals.MontoExento.ShouldBe(500.00m);
        totals.MontoGravadoTotal.ShouldBe(0m);
        totals.TotalItbis.ShouldBe(0m);
        totals.MontoTotal.ShouldBe(500.00m);
    }

    [Fact]
    public void Zero_rate_line_is_taxed_at_zero_not_exempt()
    {
        var result = EcfCalculator.Calculate([Line(1, ItbisRate.Zero, quantity: 1m, unitPrice: 100m)]);

        var totals = result.Value.Totals;
        totals.MontoGravadoI3.ShouldBe(100.00m);
        totals.Itbis3.ShouldBe(0m);
        totals.MontoExento.ShouldBe(0m);
        totals.MontoTotal.ShouldBe(100.00m);
    }

    [Fact]
    public void Sixteen_percent_rate()
    {
        var result = EcfCalculator.Calculate([Line(1, ItbisRate.Sixteen, quantity: 1m, unitPrice: 100m)]);

        result.Value.Totals.MontoGravadoI2.ShouldBe(100.00m);
        result.Value.Totals.Itbis2.ShouldBe(16.00m);
        result.Value.Totals.MontoTotal.ShouldBe(116.00m);
    }

    [Fact]
    public void Mixed_rates_bucket_into_their_own_totals()
    {
        var result = EcfCalculator.Calculate(
        [
            Line(1, ItbisRate.Eighteen, 1m, 1000m),
            Line(2, ItbisRate.Sixteen, 1m, 500m),
            Line(3, ItbisRate.Exempt, 1m, 300m),
            Line(4, ItbisRate.Zero, 1m, 200m),
        ]);

        var t = result.Value.Totals;
        t.MontoGravadoI1.ShouldBe(1000.00m);
        t.MontoGravadoI2.ShouldBe(500.00m);
        t.MontoGravadoI3.ShouldBe(200.00m);
        t.MontoGravadoTotal.ShouldBe(1700.00m);
        t.Itbis1.ShouldBe(180.00m);
        t.Itbis2.ShouldBe(80.00m);
        t.TotalItbis.ShouldBe(260.00m);
        t.MontoExento.ShouldBe(300.00m);
        t.MontoTotal.ShouldBe(2260.00m);
    }

    [Fact]
    public void Line_discount_and_surcharge_feed_the_line_amount()
    {
        var result = EcfCalculator.Calculate(
            [Line(1, ItbisRate.Eighteen, quantity: 2m, unitPrice: 100m, discount: 20m, surcharge: 5m)]);

        result.Value.Lines[0].LineAmount.ShouldBe(185.00m);
        result.Value.Totals.TotalItbis.ShouldBe(33.30m);
        result.Value.Totals.MontoTotal.ShouldBe(218.30m);
    }

    [Fact]
    public void Additional_taxes_add_to_the_total_but_not_to_the_itbis_base()
    {
        var result = EcfCalculator.Calculate(
            [Line(1, ItbisRate.Eighteen, quantity: 1m, unitPrice: 1000m, additionalTaxes: 100m)]);

        var t = result.Value.Totals;
        t.MontoGravadoTotal.ShouldBe(1000.00m);
        t.TotalItbis.ShouldBe(180.00m);
        t.TotalOtrosImpuestosAdicionales.ShouldBe(100.00m);
        t.MontoImpuestoAdicional.ShouldBe(100.00m);
        t.MontoTotal.ShouldBe(1280.00m);
    }

    [Fact]
    public void MontoNoFacturable_only_moves_MontoPeriodo_and_can_be_negative()
    {
        var result = EcfCalculator.Calculate(
            [Line(1, ItbisRate.Eighteen, quantity: 1m, unitPrice: 100m)],
            montoNoFacturable: -30m);

        result.Value.Totals.MontoTotal.ShouldBe(118.00m);
        result.Value.Totals.MontoNoFacturable.ShouldBe(-30.00m);
        result.Value.Totals.MontoPeriodo.ShouldBe(88.00m);
    }

    [Fact]
    public void A_global_discount_reduces_the_bucket_and_recomputes_its_itbis()
    {
        var result = EcfCalculator.Calculate(
            [Line(1, ItbisRate.Eighteen, 1m, 10000m), Line(2, ItbisRate.Exempt, 1m, 2000m)],
            globalAdjustments: [new EcfGlobalAdjustmentInput(IsDiscount: true, ItbisRate.Eighteen, 1000m)]);

        var t = result.Value.Totals;
        t.MontoGravadoI1.ShouldBe(9000.00m);
        t.Itbis1.ShouldBe(1620.00m);           // 9000 * 0.18
        t.MontoExento.ShouldBe(2000.00m);
        t.MontoTotal.ShouldBe(12620.00m);      // 9000 + 2000 + 1620
        t.TotalGlobalAdjustment.ShouldBe(-1000.00m);
    }

    [Fact]
    public void A_global_surcharge_on_the_16_percent_bucket_adds_to_it()
    {
        var result = EcfCalculator.Calculate(
            [Line(1, ItbisRate.Sixteen, 1m, 5000m)],
            globalAdjustments: [new EcfGlobalAdjustmentInput(IsDiscount: false, ItbisRate.Sixteen, 500m)]);

        var t = result.Value.Totals;
        t.MontoGravadoI2.ShouldBe(5500.00m);
        t.Itbis2.ShouldBe(880.00m);            // 5500 * 0.16
        t.MontoTotal.ShouldBe(6380.00m);
    }

    [Fact]
    public void A_norma_10_07_discount_keeps_the_18_percent_base_but_lowers_the_net()
    {
        var result = EcfCalculator.Calculate(
            [Line(1, ItbisRate.Eighteen, 1m, 10000m)],
            globalAdjustments: [new EcfGlobalAdjustmentInput(IsDiscount: true, ItbisRate.Eighteen, 1000m, Norma1007: true)]);

        var t = result.Value.Totals;
        t.MontoGravadoI1.ShouldBe(10000.00m);  // no se rebaja
        t.Itbis1.ShouldBe(1800.00m);
        t.MontoTotal.ShouldBe(11800.00m);      // total bruto
        t.Norma1007Discount.ShouldBe(1000.00m);
    }

    [Fact]
    public void A_global_discount_that_exceeds_its_bucket_is_rejected()
        => EcfCalculator.Calculate(
                [Line(1, ItbisRate.Eighteen, 1m, 500m)],
                globalAdjustments: [new EcfGlobalAdjustmentInput(IsDiscount: true, ItbisRate.Eighteen, 900m)])
            .FirstError.Code.ShouldBe("Fiscal.GlobalAdjustmentExceedsBucket");

    [Fact]
    public void A_negative_global_adjustment_is_rejected()
        => EcfCalculator.Calculate(
                [Line(1, ItbisRate.Eighteen, 1m, 500m)],
                globalAdjustments: [new EcfGlobalAdjustmentInput(IsDiscount: true, ItbisRate.Eighteen, -5m)])
            .FirstError.Code.ShouldBe("Fiscal.NegativeGlobalAdjustment");

    [Fact]
    public void Retentions_are_totalized_and_do_not_touch_the_invoice_total()
    {
        var result = EcfCalculator.Calculate(
        [
            Line(1, ItbisRate.Eighteen, 1m, 1000m, itbisWithheld: 54m, isrWithheld: 100m),
            Line(2, ItbisRate.Eighteen, 1m, 2000m, itbisWithheld: 108m, isrWithheld: 200m),
        ]);

        var t = result.Value.Totals;
        t.MontoTotal.ShouldBe(3540.00m);                 // 3000 + 540 ITBIS, sin tocar
        t.TotalItbisWithheld.ShouldBe(162.00m);
        t.TotalIsrWithheld.ShouldBe(300.00m);
    }

    [Fact]
    public void Rejects_a_negative_retention()
        => EcfCalculator.Calculate([Line(1, ItbisRate.Eighteen, 1m, 100m, itbisWithheld: -5m)])
            .FirstError.Code.ShouldBe("Fiscal.NegativeRetention");

    [Fact]
    public void Tolerance_within_limits_is_not_flagged()
    {
        var result = EcfCalculator.Calculate(
            [Line(1, ItbisRate.Eighteen, quantity: 1m, unitPrice: 100m, supplied: 100.00m)]);

        result.Value.Tolerance.WithinTolerance.ShouldBeTrue();
        result.Value.Tolerance.ExpectConditionalAcceptance.ShouldBeFalse();
    }

    [Fact]
    public void Tolerance_exceeded_is_flagged_but_never_an_error()
    {
        var result = EcfCalculator.Calculate(
            [Line(1, ItbisRate.Eighteen, quantity: 1m, unitPrice: 100m, supplied: 98.00m)]);

        result.IsError.ShouldBeFalse();
        result.Value.Tolerance.LineDiffs[0].Difference.ShouldBe(2.00m);
        result.Value.Tolerance.LineDiffs[0].WithinLineTolerance.ShouldBeFalse();
        result.Value.Tolerance.WithinTolerance.ShouldBeFalse();
        result.Value.Tolerance.ExpectConditionalAcceptance.ShouldBeTrue();
    }

    [Fact]
    public void Global_tolerance_is_the_line_count()
    {
        // Dos líneas, cada una desviada 0.60: por línea ≤ 1 y total 1.20 ≤ 2.
        var result = EcfCalculator.Calculate(
        [
            Line(1, ItbisRate.Eighteen, 1m, 100m, supplied: 100.60m),
            Line(2, ItbisRate.Eighteen, 1m, 100m, supplied: 99.40m),
        ]);

        result.Value.Tolerance.GlobalTolerance.ShouldBe(2);
        result.Value.Tolerance.TotalDifference.ShouldBe(1.20m);
        result.Value.Tolerance.WithinTolerance.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_an_empty_detail()
        => EcfCalculator.Calculate([]).FirstError.Code.ShouldBe("Fiscal.NoLines");

    [Fact]
    public void Rejects_a_negative_quantity()
        => EcfCalculator.Calculate([Line(1, ItbisRate.Eighteen, quantity: -1m, unitPrice: 100m)])
            .FirstError.Code.ShouldBe("Fiscal.NegativeQuantity");

    [Fact]
    public void Rejects_a_discount_that_exceeds_the_line()
        => EcfCalculator.Calculate([Line(1, ItbisRate.Eighteen, quantity: 1m, unitPrice: 100m, discount: 150m)])
            .FirstError.Code.ShouldBe("Fiscal.NegativeLineAmount");

    [Fact]
    public void Rejects_duplicate_line_numbers()
        => EcfCalculator.Calculate(
        [
            Line(1, ItbisRate.Eighteen, 1m, 100m),
            Line(1, ItbisRate.Eighteen, 1m, 100m),
        ]).FirstError.Code.ShouldBe("Fiscal.DuplicateLineNumber");

    [Fact]
    public void A_zero_line_amount_is_allowed_for_text_only_credit_notes()
    {
        var result = EcfCalculator.Calculate([Line(1, ItbisRate.Exempt, quantity: 0m, unitPrice: 0m)]);

        result.IsError.ShouldBeFalse();
        result.Value.Lines[0].LineAmount.ShouldBe(0m);
        result.Value.Totals.MontoTotal.ShouldBe(0m);
    }
}
