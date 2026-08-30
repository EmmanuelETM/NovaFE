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
