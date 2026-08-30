using NovaFE.Domain.Fiscal;

namespace NovaFE.UnitTests.Fiscal;

public class FiscalRulesTests
{
    [Theory]
    [InlineData(100, 200)]
    [InlineData(200, 200)] // igual está permitido
    [InlineData(0, 0)]
    public void Credit_note_total_within_original_passes(decimal creditNote, decimal original)
        => FiscalRules.CreditNoteTotalWithinOriginal(creditNote, original).IsError.ShouldBeFalse();

    [Fact]
    public void Credit_note_total_above_original_fails()
        => FiscalRules.CreditNoteTotalWithinOriginal(200.01m, 200m).FirstError.Code
            .ShouldBe("Fiscal.CreditNoteTotalExceedsOriginal");
}
