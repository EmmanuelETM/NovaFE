using NovaFE.Domain.Webhooks;

namespace NovaFE.UnitTests.Webhooks;

public class WebhookEventTypeTests
{
    [Theory]
    [InlineData("ecf.accepted", true)]
    [InlineData("ecf.rejected", true)]
    [InlineData("ecf.*", true)]
    [InlineData("*", true)]
    [InlineData("ecf.exploded", false)]
    [InlineData("cert.*", false)]
    [InlineData("webhook.ping", false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    public void IsValidSubscription(string value, bool expected)
        => WebhookEventType.IsValidSubscription(value).ShouldBe(expected);

    [Theory]
    [InlineData("*", "ecf.accepted", true)]
    [InlineData("*", "webhook.ping", true)]
    [InlineData("ecf.*", "ecf.accepted", true)]
    [InlineData("ecf.*", "ecf.submitted", true)]
    [InlineData("ecf.*", "webhook.ping", false)]
    [InlineData("ecf.accepted", "ecf.accepted", true)]
    [InlineData("ecf.accepted", "ecf.rejected", false)]
    public void Covers(string subscription, string eventType, bool expected)
        => WebhookEventType.Covers(subscription, eventType).ShouldBe(expected);

    [Fact]
    public void Subscribable_is_the_six_ecf_lifecycle_events()
    {
        WebhookEventType.Subscribable.ShouldBe(
        [
            "ecf.submitted", "ecf.accepted", "ecf.accepted_conditional",
            "ecf.rejected", "ecf.review", "ecf.failed",
        ], ignoreOrder: true);
    }
}
