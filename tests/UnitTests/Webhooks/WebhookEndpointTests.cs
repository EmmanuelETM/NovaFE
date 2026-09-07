using NovaFE.Domain.Webhooks;

namespace NovaFE.UnitTests.Webhooks;

public class WebhookEndpointTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private const string Secret = "whsec_test";

    private static WebhookEndpoint Create(
        string? url = "https://hooks.example.com/nova",
        IEnumerable<string>? events = null,
        string? description = null)
        => WebhookEndpoint.Create(TenantId, url, events ?? ["ecf.accepted"], description, Secret).Value;

    [Fact]
    public void Create_normalizes_and_sorts_the_events_and_starts_enabled()
    {
        var endpoint = Create(events: ["ecf.rejected", "ecf.accepted", "ecf.accepted"]);

        endpoint.Events.ShouldBe(["ecf.accepted", "ecf.rejected"]);
        endpoint.Enabled.ShouldBeTrue();
        endpoint.ConsecutiveFailures.ShouldBe(0);
    }

    [Fact]
    public void Create_collapses_to_the_total_wildcard()
    {
        Create(events: ["ecf.accepted", "*"]).Events.ShouldBe(["*"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://host/x")]
    public void Create_rejects_a_bad_url(string? url)
        => WebhookEndpoint.Create(TenantId, url, ["ecf.accepted"], null, Secret).IsError.ShouldBeTrue();

    [Fact]
    public void Create_rejects_an_unknown_event()
        => WebhookEndpoint.Create(TenantId, "https://x.example.com", ["ecf.boom"], null, Secret)
            .FirstError.Code.ShouldBe("WebhookEndpoint.UnknownEventType");

    [Fact]
    public void Create_rejects_an_empty_event_list()
        => WebhookEndpoint.Create(TenantId, "https://x.example.com", [], null, Secret)
            .FirstError.Code.ShouldBe("WebhookEndpoint.NoEvents");

    [Fact]
    public void Update_partial_leaves_untouched_fields_and_clears_description_with_empty_string()
    {
        var endpoint = Create(description: "vieja");

        endpoint.Update(url: "https://new.example.com/hook", events: null, description: "").IsError.ShouldBeFalse();

        endpoint.Url.ShouldBe("https://new.example.com/hook");
        endpoint.Events.ShouldBe(["ecf.accepted"]);
        endpoint.Description.ShouldBeNull();
    }

    [Fact]
    public void Update_with_nothing_is_an_error()
        => Create().Update(null, null, null).FirstError.Code.ShouldBe("WebhookEndpoint.NothingToUpdate");

    [Fact]
    public void SetEnabled_false_then_true_resets_the_failure_state()
    {
        var endpoint = Create();
        endpoint.RecordDeliveryFailure(autoDisableThreshold: 3, DateTimeOffset.UtcNow);
        endpoint.RecordDeliveryFailure(autoDisableThreshold: 3, DateTimeOffset.UtcNow);

        endpoint.SetEnabled(false);
        endpoint.SetEnabled(true);

        endpoint.Enabled.ShouldBeTrue();
        endpoint.ConsecutiveFailures.ShouldBe(0);
        endpoint.DisabledReason.ShouldBeNull();
    }

    [Fact]
    public void RecordDeliveryFailure_auto_disables_at_the_threshold()
    {
        var endpoint = Create();
        var at = DateTimeOffset.UtcNow;

        endpoint.RecordDeliveryFailure(3, at);
        endpoint.RecordDeliveryFailure(3, at);
        endpoint.Enabled.ShouldBeTrue();

        endpoint.RecordDeliveryFailure(3, at);
        endpoint.Enabled.ShouldBeFalse();
        endpoint.DisabledAt.ShouldBe(at);
        endpoint.DisabledReason.ShouldNotBeNull();
    }

    [Fact]
    public void RecordDeliverySuccess_clears_the_failure_count()
    {
        var endpoint = Create();
        endpoint.RecordDeliveryFailure(10, DateTimeOffset.UtcNow);

        endpoint.RecordDeliverySuccess();

        endpoint.ConsecutiveFailures.ShouldBe(0);
    }

    [Theory]
    [InlineData("ecf.accepted", "ecf.accepted", true)]
    [InlineData("ecf.accepted", "ecf.rejected", false)]
    public void IsSubscribedTo(string subscription, string eventType, bool expected)
        => Create(events: [subscription]).IsSubscribedTo(eventType).ShouldBe(expected);

    [Fact]
    public void IsSubscribedTo_honours_a_wildcard()
        => Create(events: ["ecf.*"]).IsSubscribedTo("ecf.review").ShouldBeTrue();
}
