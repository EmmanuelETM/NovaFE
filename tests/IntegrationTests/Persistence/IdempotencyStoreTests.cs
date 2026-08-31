using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Common.Interfaces;
using NovaFE.IntegrationTests.Fixtures;
using NovaFE.Service.Common;

namespace NovaFE.IntegrationTests.Persistence;

public sealed class IdempotencyStoreTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private async Task<(IIdempotencyStore Store, AsyncServiceScope Scope)> StoreAsync(Guid tenantId)
    {
        var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);
        return (scope.ServiceProvider.GetRequiredService<IIdempotencyStore>(), scope);
    }

    [RequiresDockerFact]
    public async Task A_new_key_proceeds_then_replays_the_same_body()
    {
        var tenant = Guid.CreateVersion7();
        var resource = Guid.CreateVersion7();

        var (store, scope) = await StoreAsync(tenant);
        await using (scope)
        {
            (await store.BeginAsync(tenant, "key-1", "hashA")).Decision.ShouldBe(IdempotencyDecision.Proceed);

            // Antes de completar: la misma clave está "en curso".
            (await store.BeginAsync(tenant, "key-1", "hashA")).Decision.ShouldBe(IdempotencyDecision.InProgress);

            await store.CompleteAsync(tenant, "key-1", resource);

            var replay = await store.BeginAsync(tenant, "key-1", "hashA");
            replay.Decision.ShouldBe(IdempotencyDecision.Replay);
            replay.EcfId.ShouldBe(resource);
        }
    }

    [RequiresDockerFact]
    public async Task The_same_key_with_a_different_body_is_a_conflict()
    {
        var tenant = Guid.CreateVersion7();

        var (store, scope) = await StoreAsync(tenant);
        await using (scope)
        {
            await store.BeginAsync(tenant, "key-2", "hashA");
            await store.CompleteAsync(tenant, "key-2", Guid.CreateVersion7());

            (await store.BeginAsync(tenant, "key-2", "hashB")).Decision.ShouldBe(IdempotencyDecision.Conflict);
        }
    }

    [RequiresDockerFact]
    public async Task Keys_are_scoped_per_tenant()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        var (storeA, scopeA) = await StoreAsync(tenantA);
        await using (scopeA)
            await storeA.BeginAsync(tenantA, "shared", "hashA");

        var (storeB, scopeB) = await StoreAsync(tenantB);
        await using (scopeB)
            (await storeB.BeginAsync(tenantB, "shared", "hashZ")).Decision.ShouldBe(IdempotencyDecision.Proceed);
    }
}
