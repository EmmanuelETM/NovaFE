using System.Text.Json;
using NovaFE.Domain.Common.Json;

namespace NovaFE.UnitTests.Common;

public class DominicanDateTimeOffsetConverterTests
{
    private static readonly JsonSerializerOptions Options = JsonSettings.Bulletproof;

    private sealed record Payload(DateTimeOffset When, DateTimeOffset? Maybe);

    [Fact]
    public void Serializes_in_dominican_time()
    {
        var payload = new Payload(new DateTimeOffset(2026, 8, 30, 14, 0, 0, TimeSpan.Zero), null);

        var json = JsonSerializer.Serialize(payload, Options);

        json.ShouldContain("\"when\":\"2026-08-30T10:00:00-04:00\"");
    }

    [Fact]
    public void Round_trips_to_the_same_instant()
    {
        var original = new Payload(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(3));

        var back = JsonSerializer.Deserialize<Payload>(JsonSerializer.Serialize(original, Options), Options)!;

        back.When.ToUniversalTime().ShouldBe(original.When.ToUniversalTime(), TimeSpan.FromMilliseconds(1));
        back.Maybe!.Value.ToUniversalTime().ShouldBe(original.Maybe!.Value.ToUniversalTime(), TimeSpan.FromMilliseconds(1));
    }

    [Theory]
    [InlineData("\"2026-08-30T14:00:00Z\"")]
    [InlineData("\"2026-08-30T09:00:00-05:00\"")]
    [InlineData("\"2026-08-30T10:00:00-04:00\"")]
    public void Reads_any_offset_as_the_same_instant(string whenJson)
    {
        var json = $$"""{"when": {{whenJson}}, "maybe": null}""";

        var payload = JsonSerializer.Deserialize<Payload>(json, Options)!;

        payload.When.ToUniversalTime().ShouldBe(new DateTimeOffset(2026, 8, 30, 14, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Keeps_sub_second_precision_when_present()
    {
        var payload = new Payload(new DateTimeOffset(2026, 8, 30, 14, 0, 0, 123, TimeSpan.Zero), null);

        JsonSerializer.Serialize(payload, Options).ShouldContain("2026-08-30T10:00:00.123-04:00");
    }
}
