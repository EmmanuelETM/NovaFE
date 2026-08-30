using NovaFE.Domain.Common;

namespace NovaFE.UnitTests.Common;

public class RncTests
{
    [Theory]
    [InlineData("101672919")]     // 9 digits (RNC)
    [InlineData("0010167291")]    // 10 digits
    [InlineData("00101672919")]   // 11 digits (cédula)
    public void Create_accepts_well_formed(string raw)
        => Rnc.Create(raw).IsError.ShouldBeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("10167291X")]
    [InlineData("101-672-919")]
    [InlineData(null)]
    public void Create_rejects_malformed(string? raw)
        => Rnc.Create(raw).IsError.ShouldBeTrue();

    [Fact]
    public void Create_trims_surrounding_whitespace()
        => Rnc.Create("  101672919 ").Value.Value.ShouldBe("101672919");

    [Fact]
    public void Implicitly_converts_to_its_string_value()
    {
        string value = Rnc.Create("101672919").Value;
        value.ShouldBe("101672919");
    }
}
