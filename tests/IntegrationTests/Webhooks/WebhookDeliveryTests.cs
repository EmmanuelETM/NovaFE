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
        await EventuallyAsync(
            () => Task.FromResult(receiver.Server.LogEntries.Count > 0),
            tick: PumpAsync);

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
        await EventuallyAsync(
            async () => (await GetEndpointAsync(created.Endpoint.Id)).ConsecutiveFailures == 1,
            tick: PumpAsync);
        (await DeliveryStatusAsync(created.Endpoint.Id)).ShouldBe("pending");

        // Ahora el receptor responde 200; se adelanta el next_attempt_at.
        receiver.Server.Reset();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));
        (await ForceDueAsync()).ShouldBe(1);

        // El worker corre cada pocos segundos en producción; acá se dispara a mano.
        // No se asume que la primera pasada reclama la fila (jitter de tiempos).
        await EventuallyAsync(
            async () => await DeliveryStatusAsync(created.Endpoint.Id) == "delivered",
            tick: PumpAsync);

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
        await EventuallyAsync(
            async () => await DeliveryStatusAsync(created.Endpoint.Id) == "delivered",
            tick: PumpAsync);

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

    /// <summary>Marca la (única) fila de entrega del endpoint como muerta. Devuelve su id.</summary>
    private async Task<Guid> ForceDeadAsync(Guid endpointId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var id = await db.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM webhook_deliveries WHERE endpoint_id = {endpointId} ORDER BY created_at DESC LIMIT 1")
            .SingleAsync();

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE webhook_deliveries SET status = 'dead', attempts = 5, last_status_code = 500 WHERE id = {id}");

        return id;
    }

    [RequiresDockerFact]
    public async Task Retrying_a_dead_delivery_requeues_it_and_a_later_pump_delivers_it()
    {
        using var receiver = new WireMockFixture();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));

        var tenant = await RegisterAndActAsTenantAsync("141222333");
        var created = await CreateEndpointAsync($"{receiver.BaseUrl}/hook", "ecf.accepted");

        await EnqueueAsync(tenant, WebhookEventType.EcfAccepted, new { id = "dead-one" });
        var deliveryId = await ForceDeadAsync(created.Endpoint.Id);

        // Sin haber reintentado, no queda nada para reclamar: el pump no toca la fila muerta.
        (await PumpAsync()).ShouldBe(0);
        (await DeliveryStatusAsync(created.Endpoint.Id)).ShouldBe("dead");

        (await Client.PostAsync($"/api/v1/webhooks/{created.Endpoint.Id}/deliveries/{deliveryId}/retry", content: null))
            .StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);
        (await DeliveryStatusAsync(created.Endpoint.Id)).ShouldBe("pending");

        receiver.Server.Reset();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        await EventuallyAsync(
            async () => await DeliveryStatusAsync(created.Endpoint.Id) == "delivered",
            tick: PumpAsync);
    }

    [RequiresDockerFact]
    public async Task Retrying_a_delivery_that_is_not_dead_is_a_404()
    {
        var tenant = await RegisterAndActAsTenantAsync("141444555");
        var created = await CreateEndpointAsync("http://localhost:9/hook", "ecf.accepted");

        await EnqueueAsync(tenant, WebhookEventType.EcfAccepted, new { id = "still-pending" });

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pendingId = await db.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM webhook_deliveries WHERE endpoint_id = {created.Endpoint.Id} ORDER BY created_at DESC LIMIT 1")
            .SingleAsync();

        (await Client.PostAsync($"/api/v1/webhooks/{created.Endpoint.Id}/deliveries/{pendingId}/retry", content: null))
            .StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [RequiresDockerFact]
    public async Task An_operator_can_list_a_tenants_endpoints_and_retry_a_dead_delivery()
    {
        using var receiver = new WireMockFixture();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var tenant = await RegisterAndActAsTenantAsync("141666777");
        var created = await CreateEndpointAsync($"{receiver.BaseUrl}/hook", "ecf.accepted");

        await EnqueueAsync(tenant, WebhookEventType.EcfAccepted, new { id = "op-view" });
        var deliveryId = await ForceDeadAsync(created.Endpoint.Id);

        var list = await LeerAsync<List<WebhookEndpointDto>>(
            await Client.GetAsync($"/api/v1/tenants/{tenant}/webhooks"));
        list!.ShouldHaveSingleItem().Id.ShouldBe(created.Endpoint.Id);

        var log = await LeerAsync<PagedResult<WebhookDeliveryDto>>(
            await Client.GetAsync($"/api/v1/tenants/{tenant}/webhooks/{created.Endpoint.Id}/deliveries"));
        log!.Items.ShouldHaveSingleItem().Status.ShouldBe("dead");

        (await Client.PostAsync(
                $"/api/v1/tenants/{tenant}/webhooks/{created.Endpoint.Id}/deliveries/{deliveryId}/retry", content: null))
            .StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);
        (await DeliveryStatusAsync(created.Endpoint.Id)).ShouldBe("pending");

        await EventuallyAsync(
            async () => await DeliveryStatusAsync(created.Endpoint.Id) == "delivered",
            tick: PumpAsync);
    }

    [RequiresDockerFact]
    public async Task A_human_without_the_operator_role_cannot_reach_the_operator_webhook_routes()
    {
        var tenant = await RegisterTenantAsync("141888999");

        var orgResponse = await Client.PostAsJsonAsync(
            "/api/v1/organizations",
            new { name = "Acme", slug = "acme-webhooks-ops", plan = "Developer", ownerEmail = "owner@acme-webhooks-ops.do" });
        orgResponse.EnsureSuccessStatusCode();

        Reconfigure(new Dictionary<string, string?> { ["Security:AdminApiKey"] = "s3cr3t-operator" });
        Client.DefaultRequestHeaders.Add("X-Internal-Key", ApiFactory.InternalApiKey);
        Client.DefaultRequestHeaders.Add("X-Acting-User", "auth-owner");
        Client.DefaultRequestHeaders.Add("X-Acting-Email", "owner@acme-webhooks-ops.do");

        var response = await Client.GetAsync($"/api/v1/tenants/{tenant}/webhooks");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);
    }
}
