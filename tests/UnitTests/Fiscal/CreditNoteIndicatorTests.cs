using NovaFE.Domain.Fiscal;

namespace NovaFE.UnitTests.Fiscal;

public class CreditNoteIndicatorTests
{
    private static readonly DateOnly Original = new(2026, 1, 10);

    [Fact]
    public void The_values_are_zero_and_one_not_one_and_two()
    {
        CreditNoteIndicator.WithinThirtyDays.Value.ShouldBe(0);
        CreditNoteIndicator.AfterThirtyDays.Value.ShouldBe(1);
    }

    [Fact]
    public void Exactly_thirty_days_still_counts_as_within()
        => CreditNoteIndicator.For(Original, Original.AddDays(30)).Value
            .ShouldBe(CreditNoteIndicator.WithinThirtyDays);

    [Fact]
    public void Same_day_counts_as_within()
        => CreditNoteIndicator.For(Original, Original).Value
            .ShouldBe(CreditNoteIndicator.WithinThirtyDays);

    [Fact]
    public void Thirty_one_days_is_after()
        => CreditNoteIndicator.For(Original, Original.AddDays(31)).Value
            .ShouldBe(CreditNoteIndicator.AfterThirtyDays);

    [Fact]
    public void A_credit_note_dated_before_the_original_is_rejected()
        => CreditNoteIndicator.For(Original, Original.AddDays(-1)).FirstError.Code
            .ShouldBe("Fiscal.CreditNoteBeforeOriginal");
}
