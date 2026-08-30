using NovaFE.Domain.Common;

namespace NovaFE.UnitTests.Common;

public class DominicanTimeZoneTests
{
    [Fact]
    public void ToLocal_shifts_a_utc_instant_by_minus_four_hours()
    {
        var utc = new DateTimeOffset(2026, 8, 30, 14, 0, 0, TimeSpan.Zero);

        var local = DominicanTimeZone.ToLocal(utc);

        local.Offset.ShouldBe(TimeSpan.FromHours(-4));
        local.DateTime.ShouldBe(new DateTime(2026, 8, 30, 10, 0, 0));
        local.ToUniversalTime().ShouldBe(utc);
    }

    [Fact]
    public void The_zone_has_no_daylight_saving()
    {
        var winter = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var summer = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        DominicanTimeZone.Zone.GetUtcOffset(winter).ShouldBe(TimeSpan.FromHours(-4));
        DominicanTimeZone.Zone.GetUtcOffset(summer).ShouldBe(TimeSpan.FromHours(-4));
    }

    [Fact]
    public void LocalDate_uses_the_dominican_calendar_day_not_the_utc_one()
    {
        // 02:00 UTC del 1 de enero == 22:00 del 31 de diciembre en RD.
        var newYearUtc = new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.Zero);

        DominicanTimeZone.LocalDate(newYearUtc).ShouldBe(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void Formats_dates_the_dominican_way()
    {
        var instant = new DateTimeOffset(2026, 8, 30, 14, 5, 9, TimeSpan.Zero);

        DominicanTimeZone.ToDateTimeString(instant).ShouldBe("30-08-2026 10:05:09");
        DominicanTimeZone.ToDateString(instant).ShouldBe("30-08-2026");
    }
}
