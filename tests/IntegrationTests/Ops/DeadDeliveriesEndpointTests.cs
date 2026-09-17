using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Webhooks;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Webhooks;
using NovaFE.Infrastructure.Persistence.EfCore;
using NovaFE.IntegrationTests.Fixtures;
using NovaFE.Service.Common;

namespace NovaFE.IntegrationTests.Ops;

/// <summary>
/// <c>GET /ops/dead-deliveries</c> — vista cross-tenant para el operador (Fase 5).
/// <c>webhook_deliveries</c> es tabla de sistema sin RLS: la prueba clave es que
/// una sola llamada trae filas de tenants distintos.
/// </summary>
public sealed class DeadDeliveriesEndpointTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private async Task<Guid> CreateEndpointAsync(Guid tenantId, string url)
    {
        ActAs(tenantId);
        var response = await Client.PostAsJsonAsync(
            "/api/v1/webhooks", new { url, events = new[] { "ecf.accepted" }, description = "t" });
        response.EnsureSuccessStatusCode();
        return (await LeerAsync<WebhookEndpointCreatedDto>(response))!.Endpoint.Id;
    }

    private async Task EnqueueAsync(Guid tenantId, string eventType, object data)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);
        var envelope = WebhookEvent.Create(eventType, data, DateTimeOffset.UtcNow);
        await scope.ServiceProvider.GetRequiredService<IWebhookOutbox>().EnqueueAsync(envelope, tenantId);
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
    public async Task Lists_dead_deliveries_from_two_different_tenants_in_one_call()
    {
        var tenantA = await RegisterTenantAsync("142111222");
        var tenantB = await RegisterTenantAsync("142333444");

        var endpointA = await CreateEndpointAsync(tenantA, "http://localhost:9/hook-a");
        await EnqueueAsync(tenantA, WebhookEventType.EcfAccepted, new { id = "a" });
        await ForceDeadAsync(endpointA);

        var endpointB = await CreateEndpointAsync(tenantB, "http://localhost:9/hook-b");
        await EnqueueAsync(tenantB, WebhookEventType.EcfAccepted, new { id = "b" });
        await ForceDeadAsync(endpointB);

        var result = await LeerAsync<PagedResult<DeadWebhookDeliveryDto>>(
            await Client.GetAsync("/api/v1/ops/dead-deliveries"));

        result!.Items.Select(i => i.TenantId).ShouldBe([tenantB, tenantA]);
        result.Items.ShouldAllBe(i => i.EventType == "ecf.accepted" && i.LastStatusCode == 500);
    }
}
