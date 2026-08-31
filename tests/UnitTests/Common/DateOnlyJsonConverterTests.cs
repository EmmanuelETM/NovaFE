using System.Text.Json;
using NovaFE.Domain.Common.Json;

namespace NovaFE.UnitTests.Common;

public class DateOnlyJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new DateOnlyJsonConverter() } };

    private sealed record Box(DateOnly Date, DateOnly? Maybe);

    [Fact]
    public void Writes_dd_MM_yyyy()
    {
        var json = JsonSerializer.Serialize(new Box(new DateOnly(2026, 2, 21), null), Options);

        json.ShouldContain("\"21-02-2026\"");
        json.ShouldContain("\"Maybe\":null");
    }

    [Fact]
    public void Reads_dd_MM_yyyy_and_iso()
    {
        JsonSerializer.Deserialize<Box>("""{"Date":"21-02-2026","Maybe":"2027-12-31"}""", Options)!
            .ShouldBe(new Box(new DateOnly(2026, 2, 21), new DateOnly(2027, 12, 31)));
    }

    [Fact]
    public void Round_trips()
    {
        var original = new Box(new DateOnly(2026, 12, 1), new DateOnly(2026, 1, 5));

        var back = JsonSerializer.Deserialize<Box>(JsonSerializer.Serialize(original, Options), Options);

        back.ShouldBe(original);
    }

    [Fact]
    public void Rejects_garbage()
        => Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<Box>("""{"Date":"not-a-date","Maybe":null}""", Options));
}
