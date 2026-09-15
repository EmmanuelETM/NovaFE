using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Ops;

public sealed class OpsStatusEndpointTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string Url = "/api/v1/ops/status";

    private sealed record WorkerView(string Name, DateTimeOffset LastBeatAt, TimeSpan MaxSilence, bool Healthy);
    private sealed record OutboxView(int Pending, int Processing, int Dead, DateTimeOffset? OldestPendingAt);
    private sealed record SequenceAtRiskView(string TenantName, string Type, long Remaining);

    private sealed record StatusView(
        DateTimeOffset GeneratedAt,
        List<WorkerView> Workers,
        OutboxView EcfSubmissionOutbox,
        OutboxView WebhookOutbox,
        List<SequenceAtRiskView> SequencesAtRisk,
        bool ContingencyActive);

    [RequiresDockerFact]
    public async Task Reports_empty_outboxes_and_no_sequences_on_a_fresh_database()
    {
        var status = await LeerAsync<StatusView>(await Client.GetAsync(Url));

        status.ShouldNotBeNull();
        status.EcfSubmissionOutbox.ShouldBe(new OutboxView(0, 0, 0, null));
        status.WebhookOutbox.ShouldBe(new OutboxView(0, 0, 0, null));
        status.SequencesAtRisk.ShouldBeEmpty();
        status.ContingencyActive.ShouldBeFalse();
    }

    [RequiresDockerFact]
    public async Task Every_reported_worker_is_healthy_right_after_startup()
    {
        var status = await LeerAsync<StatusView>(await Client.GetAsync(Url));

        status.ShouldNotBeNull();
        status.Workers.ShouldAllBe(w => w.Healthy);
    }
}
