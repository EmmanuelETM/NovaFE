using Microsoft.Extensions.Logging.Abstractions;
using NovaFE.Domain.Settings;
using NovaFE.Infrastructure.Settings;

namespace NovaFE.UnitTests.Settings;

public class CachedSettingsReaderTests
{
    private readonly SettingsSnapshotHolder _holder = new();

    private CachedSettingsReader Sut() => new(_holder, NullLogger<CachedSettingsReader>.Instance);

    private static readonly BooleanSetting Flag = new("x.flag", "G", "Flag", @default: false);

    [Fact]
    public void Returns_the_default_when_there_is_no_override()
    {
        Sut().GetValue(Flag).ShouldBeFalse();
    }

    [Fact]
    public void Returns_the_parsed_override_when_present()
    {
        _holder.Replace(new SettingsSnapshot(1, new Dictionary<string, string> { ["x.flag"] = "true" }));

        Sut().GetValue(Flag).ShouldBeTrue();
    }

    [Fact]
    public void Falls_back_to_the_default_when_the_override_is_corrupt()
    {
        _holder.Replace(new SettingsSnapshot(1, new Dictionary<string, string> { ["x.flag"] = "maybe" }));

        Sut().GetValue(Flag).ShouldBeFalse();
    }

    [Fact]
    public void Reflects_a_snapshot_swap()
    {
        var reader = Sut();
        reader.GetValue(Flag).ShouldBeFalse();

        _holder.Replace(new SettingsSnapshot(2, new Dictionary<string, string> { ["x.flag"] = "1" }));

        reader.GetValue(Flag).ShouldBeTrue();
    }
}
