using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Sequences;

public sealed class SequencesEndpointsTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private static object Range(
        string environment = "TestEcf",
        int type = 31,
        string series = "E",
        long rangeFrom = 1,
        long rangeTo = 100)
        => new { environment, type, series, rangeFrom, rangeTo };

    [RequiresDockerFact]
    public async Task Register_then_get_returns_the_range_with_derived_stock()
    {
        await RegisterAndActAsTenantAsync("130862346");

        var register = await Client.PostAsJsonAsync("/api/v1/sequences", Range(type: 31, rangeFrom: 1, rangeTo: 20));
        register.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await LeerAsync<IdResponse>(register))!.Id;

        var get = await Client.GetAsync($"/api/v1/sequences/{id}");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);

        var view = await LeerAsync<SequenceResponse>(get);
        view!.Type.ShouldBe(31);
        view.Series.ShouldBe("E");
        view.RangeFrom.ShouldBe(1);
        view.RangeTo.ShouldBe(20);
        view.Next.ShouldBe(1);
        view.Capacity.ShouldBe(20);
        view.Remaining.ShouldBe(20);
        view.Active.ShouldBeTrue();
        view.ExpiresOn.ShouldNotBeNull();
        (view.ExpiresOn.Value.Month, view.ExpiresOn.Value.Day).ShouldBe((12, 31));
    }

    [RequiresDockerFact]
    public async Task Consumo_ranges_have_no_expiry()
    {
        await RegisterAndActAsTenantAsync("130000032");

        var register = await Client.PostAsJsonAsync("/api/v1/sequences", Range(type: 32));
        register.EnsureSuccessStatusCode();
        var id = (await LeerAsync<IdResponse>(register))!.Id;

        var view = await LeerAsync<SequenceResponse>(await Client.GetAsync($"/api/v1/sequences/{id}"));
        view!.ExpiresOn.ShouldBeNull();
    }

    [RequiresDockerFact]
    public async Task Register_rejects_a_future_authorization_date_with_400()
    {
        await RegisterAndActAsTenantAsync("130000037");

        var response = await Client.PostAsJsonAsync("/api/v1/sequences", new
        {
            environment = "TestEcf", type = 31, series = "E", rangeFrom = 1L, rangeTo = 10L,
            authorizedOn = new DateOnly(2099, 12, 31),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task Register_rejects_a_cert_ecf_range_that_does_not_start_at_one_with_400()
    {
        await RegisterAndActAsTenantAsync("130000038");

        var response = await Client.PostAsJsonAsync("/api/v1/sequences",
            Range(environment: "CertEcf", type: 31, series: "E", rangeFrom: 5, rangeTo: 100));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task Register_rejects_a_second_active_range_for_the_same_series()
    {
        await RegisterAndActAsTenantAsync("130000033");

        (await Client.PostAsJsonAsync("/api/v1/sequences", Range(type: 31, series: "E")))
            .EnsureSuccessStatusCode();

        var second = await Client.PostAsJsonAsync("/api/v1/sequences", Range(type: 31, series: "E"));
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [RequiresDockerFact]
    public async Task Register_allows_a_second_series_for_the_same_type()
    {
        await RegisterAndActAsTenantAsync("130000034");

        (await Client.PostAsJsonAsync("/api/v1/sequences", Range(type: 31, series: "E")))
            .EnsureSuccessStatusCode();

        var otherSeries = await Client.PostAsJsonAsync("/api/v1/sequences", Range(type: 31, series: "F"));
        otherSeries.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [RequiresDockerFact]
    public async Task Allocate_hands_out_the_next_number_and_advances_the_pointer()
    {
        await RegisterAndActAsTenantAsync("130000035");
        (await Client.PostAsJsonAsync("/api/v1/sequences", Range(type: 31, series: "E", rangeFrom: 1, rangeTo: 10)))
            .EnsureSuccessStatusCode();

        var first = await LeerAsync<AllocatedResponse>(
            await Client.PostAsJsonAsync("/api/v1/sequences/allocate", new { environment = "TestEcf", type = 31 }));
        var second = await LeerAsync<AllocatedResponse>(
            await Client.PostAsJsonAsync("/api/v1/sequences/allocate", new { environment = "TestEcf", type = 31 }));

        first!.Encf.ShouldBe("E310000000001");
        second!.Encf.ShouldBe("E310000000002");
    }

    [RequiresDockerFact]
    public async Task Allocate_fails_when_the_tenant_has_no_range_for_the_type()
    {
        await RegisterAndActAsTenantAsync("130000036");

        var response = await Client.PostAsJsonAsync(
            "/api/v1/sequences/allocate", new { environment = "TestEcf", type = 41 });

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [RequiresDockerFact]
    public async Task Ranges_are_isolated_between_tenants()
    {
        var tenantA = await RegisterTenantAsync("130000040");
        var tenantB = await RegisterTenantAsync("130000041");

        ActAs(tenantA);
        var register = await Client.PostAsJsonAsync("/api/v1/sequences", Range(type: 31));
        register.EnsureSuccessStatusCode();
        var id = (await LeerAsync<IdResponse>(register))!.Id;

        ActAs(tenantB);

        var listB = await Client.GetAsync("/api/v1/sequences");
        listB.EnsureSuccessStatusCode();
        (await LeerAsync<SequenceResponse[]>(listB))!.ShouldBeEmpty();

        (await Client.GetAsync($"/api/v1/sequences/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [RequiresDockerFact]
    public async Task Sequence_endpoints_require_a_credential()
        => (await Client.GetAsync("/api/v1/sequences")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    private sealed record SequenceResponse(
        Guid Id, string Environment, int Type, string TypeName, string Series,
        long RangeFrom, long RangeTo, long Next, long Capacity, long Remaining,
        DateOnly? ExpiresOn, bool Active, bool IsLowStock, DateTimeOffset CreatedAt);

    private sealed record AllocatedResponse(string Encf, int Type, string Series, long Sequential);
}
