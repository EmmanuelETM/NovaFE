using NovaFE.Infrastructure.Ecf;

namespace NovaFE.UnitTests.Ecf;

/// <summary>
/// El dinero en el e-CF siempre lleva sus 2 decimales, incluso cuando el
/// monto es un entero — "153.00", nunca "153" (a diferencia de otros
/// numéricos del XSD, que sí evitan ceros de más).
/// </summary>
public class EcfXmlFormatTests
{
    [Theory]
    [InlineData(153, "153.00")]
    [InlineData(1003, "1003.00")]
    [InlineData(0, "0.00")]
    [InlineData(191.3, "191.30")]
    [InlineData(191.5, "191.50")]
    public void Money_always_shows_two_decimals(double value, string expected) =>
        EcfXmlFormat.Money((decimal)value).ShouldBe(expected);

    [Theory]
    [InlineData(153, "153.00")]
    [InlineData(1003, "1003.00")]
    [InlineData(191.3, "191.30")]
    public void Money2_always_shows_two_decimals(double value, string expected) =>
        EcfXmlFormat.Money2((decimal)value).ShouldBe(expected);
}
