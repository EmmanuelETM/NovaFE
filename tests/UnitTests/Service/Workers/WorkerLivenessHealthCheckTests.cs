using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using NovaFE.Service.Workers;

namespace NovaFE.UnitTests.Service.Workers;

public class WorkerLivenessHealthCheckTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Is_healthy_when_no_worker_has_ever_beaten()
    {
        var clock = new FakeTimeProvider(Now);
        var check = new WorkerLivenessHealthCheck(new WorkerHeartbeat(clock), clock);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Is_healthy_while_every_worker_ticked_within_its_own_max_silence()
    {
        var clock = new FakeTimeProvider(Now);
        var heartbeat = new WorkerHeartbeat(clock);
        heartbeat.Beat("retention", TimeSpan.FromHours(3));
        heartbeat.Beat("ecf-submission", TimeSpan.FromMinutes(5));

        clock.Advance(TimeSpan.FromMinutes(1));
        var check = new WorkerLivenessHealthCheck(heartbeat, clock);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Is_unhealthy_when_a_worker_stays_silent_past_its_own_max_silence()
    {
        var clock = new FakeTimeProvider(Now);
        var heartbeat = new WorkerHeartbeat(clock);
        heartbeat.Beat("retention", TimeSpan.FromHours(3));
        heartbeat.Beat("ecf-submission", TimeSpan.FromMinutes(5));

        // El de retención sigue dentro de lo esperado; el de envío ya no.
        clock.Advance(TimeSpan.FromMinutes(10));
        var check = new WorkerLivenessHealthCheck(heartbeat, clock);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        var description = result.Description.ShouldNotBeNull();
        description.ShouldContain("ecf-submission");
        description.ShouldNotContain("retention");
    }
}
