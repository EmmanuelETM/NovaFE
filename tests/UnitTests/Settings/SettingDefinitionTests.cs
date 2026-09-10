using ErrorOr;
using NovaFE.Domain.Settings;

namespace NovaFE.UnitTests.Settings;

public class SettingDefinitionTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData(" 1 ", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    public void Boolean_parses_the_accepted_forms(string raw, bool expected)
    {
        var def = new BooleanSetting("x.flag", "G", "Flag", @default: false);

        def.TryParse(raw, out var value).ShouldBeTrue();
        value.ShouldBe(expected);
        def.Validate(raw).IsError.ShouldBeFalse();
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("")]
    [InlineData("2")]
    public void Boolean_rejects_garbage(string raw)
    {
        var def = new BooleanSetting("x.flag", "G", "Flag", @default: false);

        def.TryParse(raw, out _).ShouldBeFalse();
        def.Validate(raw).IsError.ShouldBeTrue();
    }

    [Fact]
    public void Boolean_round_trips_the_default()
    {
        var def = new BooleanSetting("x.flag", "G", "Flag", @default: true);

        def.SerializedDefault.ShouldBe("true");
        def.Validate(def.SerializedDefault).IsError.ShouldBeFalse();
    }

    [Fact]
    public void Integer_enforces_the_range()
    {
        var def = new IntegerSetting("x.n", "G", "N", @default: 10, min: 1, max: 100);

        def.Validate("50").IsError.ShouldBeFalse();
        def.Validate("0").IsError.ShouldBeTrue();
        def.Validate("101").IsError.ShouldBeTrue();
        def.Validate("abc").IsError.ShouldBeTrue();
        def.Constraints.ShouldBe("1–100");
    }

    [Fact]
    public void Decimal_uses_invariant_culture()
    {
        var def = new DecimalSetting("x.d", "G", "D", @default: 1.5m, min: 0m);

        def.TryParse("3.25", out var value).ShouldBeTrue();
        value.ShouldBe(3.25m);
        def.Format(3.25m).ShouldBe("3.25");
        def.Validate("-1").IsError.ShouldBeTrue();
    }

    [Theory]
    [InlineData("90s", 90)]
    [InlineData("5m", 300)]
    [InlineData("6h", 21600)]
    [InlineData("00:00:45", 45)]
    public void Duration_accepts_shorthand_and_timespan(string raw, int expectedSeconds)
    {
        var def = new DurationSetting("x.t", "G", "T", @default: TimeSpan.FromSeconds(10));

        def.TryParse(raw, out var value).ShouldBeTrue();
        value.ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void Duration_round_trips_and_enforces_bounds()
    {
        var def = new DurationSetting(
            "x.t", "G", "T", @default: TimeSpan.FromSeconds(10),
            min: TimeSpan.FromSeconds(1), max: TimeSpan.FromMinutes(5));

        def.Validate(def.SerializedDefault).IsError.ShouldBeFalse();
        def.Validate("10m").IsError.ShouldBeTrue();
        def.Validate("0s").IsError.ShouldBeTrue();
    }

    [Fact]
    public void Option_only_accepts_a_declared_option_case_insensitively()
    {
        var def = new OptionSetting("x.layout", "G", "Layout", @default: "letter", options: ["letter", "pos"]);

        def.TryParse("POS", out var value).ShouldBeTrue();
        value.ShouldBe("pos");
        def.Validate("carta").IsError.ShouldBeTrue();
        def.Validate("carta").FirstError.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void Option_rejects_a_default_outside_its_options()
    {
        Should.Throw<ArgumentException>(() =>
            new OptionSetting("x.layout", "G", "Layout", @default: "carta", options: ["letter", "pos"]));
    }

    [Fact]
    public void String_enforces_max_length_and_pattern()
    {
        var def = new StringSetting(
            "x.s", "G", "S", @default: "abc", maxLength: 5, pattern: "^[a-z]+$", allowEmpty: false);

        def.Validate("abcde").IsError.ShouldBeFalse();
        def.Validate("abcdef").IsError.ShouldBeTrue();
        def.Validate("ABC").IsError.ShouldBeTrue();
        def.Validate("").IsError.ShouldBeTrue();
    }
}
