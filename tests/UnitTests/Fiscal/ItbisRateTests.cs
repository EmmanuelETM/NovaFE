using NovaFE.Domain.Fiscal;

namespace NovaFE.UnitTests.Fiscal;

public class ItbisRateTests
{
    [Fact]
    public void There_are_four_billing_indicators()
        => ItbisRate.GetAll().Count().ShouldBe(4);

    [Theory]
    [InlineData(1, 0.18, false)]
    [InlineData(2, 0.16, false)]
    [InlineData(3, 0.00, false)]
    [InlineData(4, 0.00, true)]
    public void Each_indicator_maps_to_its_rate(int indicator, decimal rate, bool isExempt)
    {
        var resolved = ItbisRate.FromIndicatorOrDefault(indicator);

        resolved.ShouldNotBeNull();
        resolved.Rate.ShouldBe(rate);
        resolved.IsExempt.ShouldBe(isExempt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Unknown_indicators_return_null(int indicator)
        => ItbisRate.FromIndicatorOrDefault(indicator).ShouldBeNull();

    [Fact]
    public void Zero_rate_and_exempt_both_carry_no_tax_but_differ_in_exemption()
    {
        ItbisRate.Zero.IsExempt.ShouldBeFalse();
        ItbisRate.Exempt.IsExempt.ShouldBeTrue();
        ItbisRate.Zero.Rate.ShouldBe(ItbisRate.Exempt.Rate);
    }
}
