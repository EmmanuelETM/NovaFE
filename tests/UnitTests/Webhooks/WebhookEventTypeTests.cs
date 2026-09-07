using NovaFE.Domain.Webhooks;

namespace NovaFE.UnitTests.Webhooks;

public class WebhookEventTypeTests
{
    [Theory]
    [InlineData("ecf.accepted", true)]
    [InlineData("ecf.rejected", true)]
    [InlineData("ecf.*", true)]
    [InlineData("certificate.expiring", true)]
    [InlineData("certificate.*", true)]
    [InlineData("sequence.low", true)]
    [InlineData("sequence.*", true)]
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

    [Theory]
    [InlineData("certificate.*", "certificate.expiring", true)]
    [InlineData("sequence.*", "sequence.exhausted", true)]
    [InlineData("certificate.*", "sequence.low", false)]
    public void Covers_the_new_categories(string subscription, string eventType, bool expected)
        => WebhookEventType.Covers(subscription, eventType).ShouldBe(expected);

    [Fact]
    public void Subscribable_covers_the_e_cf_lifecycle_plus_certificate_and_sequence()
    {
        WebhookEventType.Subscribable.ShouldBe(
        [
            "ecf.submitted", "ecf.accepted", "ecf.accepted_conditional",
            "ecf.rejected", "ecf.review", "ecf.failed",
            "certificate.expiring", "certificate.expired",
            "sequence.low", "sequence.exhausted", "sequence.expiring", "sequence.expired",
        ], ignoreOrder: true);
    }
}
