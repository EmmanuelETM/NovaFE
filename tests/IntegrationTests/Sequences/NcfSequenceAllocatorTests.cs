using System.Net.Http.Json;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.IntegrationTests.Fixtures;
using NovaFE.Service.Common;
using Microsoft.Extensions.DependencyInjection;

namespace NovaFE.IntegrationTests.Sequences;

public sealed class NcfSequenceAllocatorTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    [RequiresDockerFact]
    public async Task Concurrent_allocations_never_hand_out_the_same_number()
    {
        const int concurrency = 40;
        var tenantId = await RegisterAndActAsTenantAsync("130900001");

        (await Client.PostAsJsonAsync("/api/v1.0/sequences",
                new { environment = "TestEcf", type = 31, series = "E", rangeFrom = 1L, rangeTo = 1000L }))
            .EnsureSuccessStatusCode();

        var tasks = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
        {
            using var scope = Factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);
            var allocator = scope.ServiceProvider.GetRequiredService<INcfSequenceAllocator>();

            return await allocator.AllocateAsync(DgiiEnvironment.TestEcf, EcfType.CreditoFiscal);
        }));

        var results = await Task.WhenAll(tasks);

        results.ShouldAllBe(r => !r.IsError);
        results.Select(r => r.Value.Encf.Value).Distinct().Count().ShouldBe(concurrency);
        results.Select(r => r.Value.Encf.Sequential).OrderBy(n => n).ShouldBe(Enumerable.Range(1, concurrency).Select(n => (long)n));
    }

    [RequiresDockerFact]
    public async Task Allocation_stops_when_the_range_is_exhausted()
    {
        var tenantId = await RegisterAndActAsTenantAsync("130900002");

        (await Client.PostAsJsonAsync("/api/v1.0/sequences",
                new { environment = "TestEcf", type = 33, series = "E", rangeFrom = 1L, rangeTo = 2L }))
            .EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);
        var allocator = scope.ServiceProvider.GetRequiredService<INcfSequenceAllocator>();

        (await allocator.AllocateAsync(DgiiEnvironment.TestEcf, EcfType.NotaDebito)).IsError.ShouldBeFalse();
        (await allocator.AllocateAsync(DgiiEnvironment.TestEcf, EcfType.NotaDebito)).IsError.ShouldBeFalse();

        var exhausted = await allocator.AllocateAsync(DgiiEnvironment.TestEcf, EcfType.NotaDebito);
        exhausted.IsError.ShouldBeTrue();
        exhausted.FirstError.Code.ShouldBe("Sequence.AllRangesExhausted");
    }

    [RequiresDockerFact]
    public async Task Allocation_spills_into_the_next_series_once_the_first_is_exhausted()
    {
        var tenantId = await RegisterAndActAsTenantAsync("130900003");

        (await Client.PostAsJsonAsync("/api/v1.0/sequences",
                new { environment = "TestEcf", type = 31, series = "E", rangeFrom = 1L, rangeTo = 1L }))
            .EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync("/api/v1.0/sequences",
                new { environment = "TestEcf", type = 31, series = "F", rangeFrom = 1L, rangeTo = 5L }))
            .EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);
        var allocator = scope.ServiceProvider.GetRequiredService<INcfSequenceAllocator>();

        (await allocator.AllocateAsync(DgiiEnvironment.TestEcf, EcfType.CreditoFiscal)).Value.Encf.Value.ShouldBe("E310000000001");
        (await allocator.AllocateAsync(DgiiEnvironment.TestEcf, EcfType.CreditoFiscal)).Value.Encf.Value.ShouldBe("F310000000001");
    }
}
