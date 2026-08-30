using NovaFE.Domain.Fiscal;

namespace NovaFE.UnitTests.Fiscal;

public class EcfRoundingTests
{
    [Theory]
    [InlineData(2.344, 2.34)]      // 3er decimal < 5 → trunca
    [InlineData(2.345, 2.35)]      // 3er decimal = 5 → sube
    [InlineData(2.3449, 2.34)]
    [InlineData(2.3451, 2.35)]
    [InlineData(2.9999, 3.00)]
    [InlineData(0.125, 0.13)]
    [InlineData(0.124, 0.12)]
    [InlineData(100.005, 100.01)]
    [InlineData(0, 0)]
    public void Money_rounds_half_away_from_zero_at_two_decimals(decimal value, decimal expected)
        => EcfRounding.Money(value).ShouldBe(expected);

    [Fact]
    public void Money_rounds_negatives_away_from_zero()
        => EcfRounding.Money(-2.345m).ShouldBe(-2.35m);

    [Theory]
    [InlineData(1.23455, 1.2346)]
    [InlineData(1.23454, 1.2345)]
    public void UnitPrice_rounds_at_four_decimals(decimal value, decimal expected)
        => EcfRounding.UnitPrice(value).ShouldBe(expected);

    [Theory]
    [InlineData(1.2345, 1.235)]
    [InlineData(1.2344, 1.234)]
    public void Subquantity_rounds_at_three_decimals(decimal value, decimal expected)
        => EcfRounding.Subquantity(value).ShouldBe(expected);
}
