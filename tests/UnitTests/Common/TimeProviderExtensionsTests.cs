using NovaFE.Domain.Common;
using Microsoft.Extensions.Time.Testing;

namespace NovaFE.UnitTests.Common;

public class TimeProviderExtensionsTests
{
    [Fact]
    public void GetDominicanNow_is_the_utc_instant_in_minus_four()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 8, 30, 14, 0, 0, TimeSpan.Zero));

        var now = clock.GetDominicanNow();

        now.Offset.ShouldBe(TimeSpan.FromHours(-4));
        now.Hour.ShouldBe(10);
    }

    [Fact]
    public void GetDominicanToday_can_differ_from_the_utc_date()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.Zero));

        clock.GetDominicanToday().ShouldBe(new DateOnly(2025, 12, 31));
    }
}
