using System.Text.Json;
using NovaFE.Application.Webhooks;
using NovaFE.Domain.Common.Json;
using NovaFE.Domain.Webhooks;

namespace NovaFE.UnitTests.Webhooks;

public class WebhookEventTests
{
    [Fact]
    public void Create_builds_the_stripe_style_envelope()
    {
        var at = new DateTimeOffset(2026, 9, 7, 14, 3, 11, TimeSpan.Zero);

        var envelope = WebhookEvent.Create(WebhookEventType.EcfAccepted, new { id = "abc", status = "accepted" }, at);

        envelope.Id.ShouldStartWith("evt_");
        envelope.Object.ShouldBe("event");
        envelope.Type.ShouldBe("ecf.accepted");
        envelope.ApiVersion.ShouldBe("1");
        envelope.CreatedAt.ShouldBe(at);
        envelope.Data.Object.ShouldNotBeNull();
    }

    [Fact]
    public void NewId_is_unique_and_prefixed()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => WebhookEvent.NewId()).ToList();

        ids.ShouldAllBe(id => id.StartsWith("evt_"));
        ids.Distinct().Count().ShouldBe(100);
    }

    [Fact]
    public void Serializes_with_the_api_json_settings_camelCase_and_dominican_offset()
    {
        var at = new DateTimeOffset(2026, 9, 7, 18, 0, 0, TimeSpan.Zero); // 14:00 -04:00
        var envelope = WebhookEvent.Create(WebhookEventType.EcfRejected, new { internalNumber = "F-1" }, at);

        var json = JsonSerializer.Serialize(envelope, JsonSettings.Bulletproof);

        json.ShouldContain("\"object\":\"event\"");
        json.ShouldContain("\"type\":\"ecf.rejected\"");
        json.ShouldContain("\"apiVersion\":\"1\"");
        json.ShouldContain("\"internalNumber\":\"F-1\"");
        json.ShouldContain("-04:00");
    }
}
