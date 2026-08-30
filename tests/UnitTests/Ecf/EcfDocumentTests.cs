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
    public void Credito_fiscal_requires_the_buyer_rnc()
    {
        var header = EcfTestData.Header(31) with { Buyer = new EcfBuyer("Consumidor Final") };

        EcfDocument.Create(EcfType.CreditoFiscal, header, [EcfTestData.Line()])
            .FirstError.Code.ShouldBe("Ecf.BuyerRncRequired");
    }
}
