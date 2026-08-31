using Microsoft.Extensions.Time.Testing;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.IssueEcf;

namespace NovaFE.UnitTests.Ecf;

public class IssueEcfCommandValidatorTests
{
    private readonly IssueEcfCommandValidator _validator =
        new(new FakeTimeProvider(new DateTimeOffset(2026, 2, 21, 12, 0, 0, TimeSpan.Zero)));

    private static IssueEcfCommand Valid() => new()
    {
        Type = 31,
        IncomeType = "01",
        Buyer = new EcfBuyerPayload(Name: "Cliente", Rnc: "131880681"),
        Payment = new EcfPaymentPayload("cash", Methods: [new EcfPaymentMethodPayload("cash", 100m)]),
        Lines = [new EcfLinePayload("Item", Kind: "service", Quantity: 1, UnitPrice: 100m, ItbisRate: 1)],
    };

    [Fact]
    public void Accepts_a_well_formed_command()
        => _validator.Validate(Valid()).IsValid.ShouldBeTrue();

    [Fact]
    public void Rejects_an_unknown_type()
        => _validator.Validate(Valid() with { Type = 99 }).IsValid.ShouldBeFalse();

    [Fact]
    public void Rejects_a_future_issue_date()
        => _validator.Validate(Valid() with { IssueDate = new DateOnly(2026, 3, 1) }).IsValid.ShouldBeFalse();

    [Fact]
    public void Rejects_empty_lines()
        => _validator.Validate(Valid() with { Lines = [] }).IsValid.ShouldBeFalse();

    [Fact]
    public void Rejects_a_negative_unit_price()
        => _validator.Validate(Valid() with
        {
            Lines = [new EcfLinePayload("Item", Kind: "service", Quantity: 1, UnitPrice: -1m, ItbisRate: 1)],
        }).IsValid.ShouldBeFalse();

    [Fact]
    public void Rejects_a_bad_itbis_indicator()
        => _validator.Validate(Valid() with
        {
            Lines = [new EcfLinePayload("Item", Kind: "service", Quantity: 1, UnitPrice: 1m, ItbisRate: 9)],
        }).IsValid.ShouldBeFalse();

    [Fact]
    public void Rejects_a_buyer_with_both_rnc_and_foreign_id()
        => _validator.Validate(Valid() with
        {
            Buyer = new EcfBuyerPayload(Name: "X", Rnc: "131880681", ForeignId: "US-1"),
        }).IsValid.ShouldBeFalse();

    [Fact]
    public void Requires_a_due_date_for_credit_payment()
        => _validator.Validate(Valid() with
        {
            Payment = new EcfPaymentPayload("credit", DueDate: null),
        }).IsValid.ShouldBeFalse();

    [Fact]
    public void Requires_a_reference_for_a_credit_note()
        => _validator.Validate(Valid() with { Type = 34, Reference = null }).IsValid.ShouldBeFalse();

    [Fact]
    public void Requires_retention_on_every_line_for_compras()
        => _validator.Validate(Valid() with { Type = 41 }).IsValid.ShouldBeFalse();

    [Fact]
    public void Rejects_a_non_positive_exchange_rate()
        => _validator.Validate(Valid() with
        {
            ForeignCurrency = new EcfForeignCurrencyPayload("USD", 0m, new EcfForeignCurrencyTotalsPayload()),
        }).IsValid.ShouldBeFalse();
}
