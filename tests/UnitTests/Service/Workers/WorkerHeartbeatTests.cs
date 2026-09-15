using Microsoft.Extensions.Time.Testing;
using NovaFE.Service.Workers;

namespace NovaFE.UnitTests.Service.Workers;

public class WorkerHeartbeatTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Beat_records_the_current_time_and_the_given_max_silence()
    {
        var clock = new FakeTimeProvider(Now);
        var heartbeat = new WorkerHeartbeat(clock);

        heartbeat.Beat("ecf-submission", TimeSpan.FromMinutes(5));

        var entry = heartbeat.Entries["ecf-submission"];
        entry.LastBeatAt.ShouldBe(Now);
        entry.MaxSilence.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void A_later_beat_overwrites_the_previous_one_for_the_same_worker()
    {
        var clock = new FakeTimeProvider(Now);
        var heartbeat = new WorkerHeartbeat(clock);

        heartbeat.Beat("retention", TimeSpan.FromHours(3));
        clock.Advance(TimeSpan.FromMinutes(1));
        heartbeat.Beat("retention", TimeSpan.FromHours(3));

        heartbeat.Entries.Count.ShouldBe(1);
        heartbeat.Entries["retention"].LastBeatAt.ShouldBe(Now + TimeSpan.FromMinutes(1));
    }
}
