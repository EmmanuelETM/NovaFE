using NovaFE.Domain.Sequences;

namespace NovaFE.UnitTests.Sequences;

public class EncfTests
{
    [Fact]
    public void Parses_a_well_formed_encf()
    {
        var result = Encf.Create("E310000000001");

        result.IsError.ShouldBeFalse();
        result.Value.Series.ShouldBe('E');
        result.Value.TypeCode.ShouldBe(31);
        result.Value.Sequential.ShouldBe(1);
        result.Value.Value.ShouldBe("E310000000001");
    }

    [Fact]
    public void Trims_and_upcases_before_parsing()
        => Encf.Create("  e320000012345 ").Value.Value.ShouldBe("E320000012345");

    [Theory]
    [InlineData("")]
    [InlineData("E31000000001")]     // 12 chars
    [InlineData("E3100000000012")]   // 14 chars
    [InlineData("P310000000001")]    // series P is excluded
    [InlineData("D310000000001")]    // series before E
    [InlineData("E990000000001")]    // unknown type
    [InlineData("E310000000000")]    // sequential 0
    [InlineData("E31ABCDEFGHIJ")]    // non-numeric sequential
    public void Rejects_malformed_input(string raw)
        => Encf.Create(raw).IsError.ShouldBeTrue();

    [Fact]
    public void Build_round_trips_through_from_storage()
    {
        var built = Encf.Build('F', 34, 7);

        built.Value.ShouldBe("F340000000007");
        Encf.FromStorage(built.Value).ShouldBe(built);
    }

    [Fact]
    public void Implicitly_converts_to_its_string_value()
    {
        string value = Encf.Build('E', 31, 42);
        value.ShouldBe("E310000000042");
    }
}
