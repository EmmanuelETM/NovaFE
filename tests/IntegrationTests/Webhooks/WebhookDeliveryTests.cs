using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Webhooks;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Delivery;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
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

        // El 503 → la fila se reprograma con backoff y el endpoint suma un fallo.
        await PumpAsync();
        (await DeliveryStatusAsync(created.Endpoint.Id)).ShouldBe("pending");
        (await GetEndpointAsync(created.Endpoint.Id)).ConsecutiveFailures.ShouldBe(1);

        // Ahora el receptor responde 200; se adelanta el next_attempt_at.
        receiver.Server.Reset();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));
        (await ForceDueAsync()).ShouldBe(1);

        // El worker corre cada pocos segundos en producción; acá se dispara a mano.
        // Se reintenta el pump por si la primera pasada no reclama la fila
        // (jitter de tiempos), sin depender de un único intento.
        await EventuallyAsync(async () =>
            await DeliveryStatusAsync(created.Endpoint.Id) == "delivered");

        (await GetEndpointAsync(created.Endpoint.Id)).ConsecutiveFailures.ShouldBe(0);
    }

    /// <summary>Estado de la (única) fila de entrega de un endpoint.</summary>
    private async Task<string?> DeliveryStatusAsync(Guid endpointId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM webhook_deliveries WHERE endpoint_id = {endpointId} ORDER BY created_at DESC LIMIT 1")
            .SingleOrDefaultAsync();
    }

    /// <summary>Dispara el pump hasta que se cumpla la condición o se agote el margen.</summary>
    private async Task EventuallyAsync(Func<Task<bool>> condition, int attempts = 10)
    {
        for (var i = 0; i < attempts; i++)
        {
            await PumpAsync();
            if (await condition())
                return;
            await Task.Delay(100);
        }

        throw new Shouldly.ShouldAssertException("La condición no se cumplió tras varios ticks del pump.");
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

    [RequiresDockerFact]
    public async Task Ping_delivers_a_synthetic_event_inline_and_returns_the_result()
    {
        using var receiver = new WireMockFixture();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(204));

        await RegisterAndActAsTenantAsync("140999000");
        var created = await CreateEndpointAsync($"{receiver.BaseUrl}/hook", "ecf.rejected");

        var ping = await LeerAsync<WebhookPingResultDto>(
            await Client.PostAsync($"/api/v1/webhooks/{created.Endpoint.Id}/ping", content: null));

        ping!.Delivered.ShouldBeTrue();
        ping.StatusCode.ShouldBe(204);

        var body = receiver.Server.LogEntries.ShouldHaveSingleItem().RequestMessage!.Body!;
        body.ShouldContain("webhook.ping");
    }

    [RequiresDockerFact]
    public async Task Deliveries_log_records_each_attempt()
    {
        using var receiver = new WireMockFixture();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var tenant = await RegisterAndActAsTenantAsync("141000111");
        var created = await CreateEndpointAsync($"{receiver.BaseUrl}/hook", "ecf.accepted");

        await EnqueueAsync(tenant, WebhookEventType.EcfAccepted, new { id = "d" });
        await PumpAsync();

        var log = await LeerAsync<PagedResult<WebhookDeliveryDto>>(
            await Client.GetAsync($"/api/v1/webhooks/{created.Endpoint.Id}/deliveries"));

        var entry = log!.Items.ShouldHaveSingleItem();
        entry.EventType.ShouldBe("ecf.accepted");
        entry.Status.ShouldBe("delivered");
        entry.LastStatusCode.ShouldBe(200);
    }

    /// <summary>Adelanta el <c>next_attempt_at</c> de las filas pendientes. Devuelve cuántas.</summary>
    private async Task<int> ForceDueAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.ExecuteSqlRawAsync(
            "UPDATE webhook_deliveries SET next_attempt_at = now() - interval '1 minute' WHERE status = 'pending'");
    }
}
