using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Webhooks;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Delivery;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Webhooks;
using NovaFE.Infrastructure.Persistence.EfCore;
using NovaFE.IntegrationTests.Fixtures;
using NovaFE.Service.Common;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace NovaFE.IntegrationTests.Webhooks;

public sealed class WebhookDeliveryTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private Task<int> PumpAsync()
        => Factory.Services.GetRequiredService<IWebhookDeliveryPump>().RunOnceAsync();

    private async Task<int> EnqueueAsync(Guid tenantId, string eventType, object data)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);
        var envelope = WebhookEvent.Create(eventType, data, DateTimeOffset.UtcNow);
        return await scope.ServiceProvider.GetRequiredService<IWebhookOutbox>().EnqueueAsync(envelope, tenantId);
    }

    private async Task<WebhookEndpointCreatedDto> CreateEndpointAsync(string url, params string[] events)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/webhooks", new { url, events, description = "t" });
        response.EnsureSuccessStatusCode();
        return (await LeerAsync<WebhookEndpointCreatedDto>(response))!;
    }

    private async Task<WebhookEndpointDto> GetEndpointAsync(Guid id)
        => (await LeerAsync<WebhookEndpointDto>(await Client.GetAsync($"/api/v1/webhooks/{id}")))!;

    [RequiresDockerFact]
    public async Task Delivers_a_signed_post_to_the_subscribed_endpoint()
    {
        using var receiver = new WireMockFixture();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var tenant = await RegisterAndActAsTenantAsync("140111222");
        var created = await CreateEndpointAsync($"{receiver.BaseUrl}/hook", "ecf.accepted");

        (await EnqueueAsync(tenant, WebhookEventType.EcfAccepted, new { id = "abc", status = "accepted" })).ShouldBe(1);
        (await PumpAsync()).ShouldBe(1);

        var request = receiver.Server.LogEntries.ShouldHaveSingleItem().RequestMessage.ShouldNotBeNull();
        var headers = request.Headers.ShouldNotBeNull();

        headers["X-NovaFE-Event"][0].ShouldBe("ecf.accepted");
        headers.ShouldContainKey("X-NovaFE-Delivery");

        var body = request.Body.ShouldNotBeNull();
        var timestamp = headers["X-NovaFE-Timestamp"][0];

        // La firma prueba que el body llega byte a byte como se firmó.
        var expected = "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(created.Secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}")));
        headers["X-NovaFE-Signature"][0].ShouldBe(expected);

        using var envelope = System.Text.Json.JsonDocument.Parse(body);
        envelope.RootElement.GetProperty("object").GetString().ShouldBe("event");
        envelope.RootElement.GetProperty("type").GetString().ShouldBe("ecf.accepted");
        envelope.RootElement.GetProperty("apiVersion").GetString().ShouldBe("1");
        envelope.RootElement.GetProperty("data").GetProperty("object").GetProperty("status").GetString().ShouldBe("accepted");
    }

    [RequiresDockerFact]
    public async Task A_5xx_reschedules_and_a_later_success_delivers()
    {
        using var receiver = new WireMockFixture();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503));

        var tenant = await RegisterAndActAsTenantAsync("140333444");
        var created = await CreateEndpointAsync($"{receiver.BaseUrl}/hook", "ecf.rejected");

        await EnqueueAsync(tenant, WebhookEventType.EcfRejected, new { id = "x" });
        await PumpAsync();

        (await GetEndpointAsync(created.Endpoint.Id)).ConsecutiveFailures.ShouldBe(1);

        receiver.Server.Reset();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));
        await ForceDueAsync();

        (await PumpAsync()).ShouldBe(1);
        (await GetEndpointAsync(created.Endpoint.Id)).ConsecutiveFailures.ShouldBe(0);
    }

    [RequiresDockerFact]
    public async Task Nothing_is_enqueued_when_no_endpoint_is_subscribed()
    {
        var tenant = await RegisterAndActAsTenantAsync("140555666");
        await CreateEndpointAsync("http://localhost:9/hook", "ecf.rejected");

        (await EnqueueAsync(tenant, WebhookEventType.EcfAccepted, new { id = "y" })).ShouldBe(0);
    }

    [RequiresDockerFact]
    public async Task A_disabled_endpoint_is_not_a_fan_out_target()
    {
        var tenant = await RegisterAndActAsTenantAsync("140777888");
        var created = await CreateEndpointAsync("http://localhost:9/hook", "ecf.accepted");

        (await Client.PatchAsJsonAsync($"/api/v1/webhooks/{created.Endpoint.Id}", new { enabled = false }))
            .EnsureSuccessStatusCode();

        (await EnqueueAsync(tenant, WebhookEventType.EcfAccepted, new { id = "z" })).ShouldBe(0);
    }

    private async Task ForceDueAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE webhook_deliveries SET next_attempt_at = now() - interval '1 minute' WHERE status = 'pending'");
    }
}
