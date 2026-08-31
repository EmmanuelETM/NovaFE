using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Tenants;

public sealed class TenantsEndpointsTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    [RequiresDockerFact]
    public async Task Register_then_get_returns_the_tenant()
    {
        var register = await Client.PostAsJsonAsync("/api/v1/tenants", new
        {
            rnc = "101672919",
            legalName = "Acme SRL",
            tradeName = "Acme",
            plan = "Developer",
        });

        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await LeerAsync<IdResponse>(register);
        created!.Id.ShouldNotBe(Guid.Empty);

        var get = await Client.GetAsync($"/api/v1/tenants/{created.Id}");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await LeerAsync<TenantDetailResponse>(get);
        detail!.Rnc.ShouldBe("101672919");
        detail.LegalName.ShouldBe("Acme SRL");
        detail.TradeName.ShouldBe("Acme");
        detail.Plan.ShouldBe("Developer");
        detail.Status.ShouldBe("Active");
    }

    [RequiresDockerFact]
    public async Task Timestamps_are_serialized_in_dominican_time()
    {
        var register = await Client.PostAsJsonAsync("/api/v1/tenants", new
        {
            rnc = "131234567",
            legalName = "Zona SRL",
            plan = "Business",
        });
        var id = (await LeerAsync<IdResponse>(register))!.Id;

        var body = await (await Client.GetAsync($"/api/v1/tenants/{id}")).Content.ReadAsStringAsync();

        body.ShouldContain("-04:00");
        body.ShouldNotContain("+00:00");
        body.ShouldNotContain("Z\""); // ninguna fecha en UTC
    }

    [RequiresDockerFact]
    public async Task Get_unknown_tenant_returns_404()
    {
        var get = await Client.GetAsync($"/api/v1/tenants/{Guid.NewGuid()}");

        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [RequiresDockerFact]
    public async Task Register_rejects_a_duplicate_rnc_with_409()
    {
        var body = new { rnc = "130111222", legalName = "First", plan = "Business" };

        (await Client.PostAsJsonAsync("/api/v1/tenants", body)).EnsureSuccessStatusCode();

        var second = await Client.PostAsJsonAsync("/api/v1/tenants", body);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [RequiresDockerFact]
    public async Task Register_rejects_a_malformed_rnc_with_400()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/tenants", new
        {
            rnc = "abc",
            legalName = "X",
            plan = "Developer",
        });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task List_paginates_and_filters_by_search()
    {
        foreach (var i in Enumerable.Range(1, 3))
        {
            (await Client.PostAsJsonAsync("/api/v1/tenants", new
            {
                rnc = $"13000000{i}",
                legalName = $"Contoso {i}",
                plan = "Developer",
            })).EnsureSuccessStatusCode();
        }

        (await Client.PostAsJsonAsync("/api/v1/tenants", new
        {
            rnc = "140000001",
            legalName = "Unrelated",
            plan = "Developer",
        })).EnsureSuccessStatusCode();

        var res = await Client.GetAsync("/api/v1/tenants?page=1&pageSize=2&search=Contoso");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await LeerAsync<PagedResponse<TenantSummaryResponse>>(res);
        page!.TotalCount.ShouldBe(3);
        page.Items.Count().ShouldBe(2);
        page.Items.ShouldAllBe(t => t.LegalName.StartsWith("Contoso"));
    }

    private sealed record TenantDetailResponse(
        Guid Id, string Rnc, string LegalName, string? TradeName, string Plan, string Status, DateTimeOffset CreatedAt);

    private sealed record TenantSummaryResponse(
        Guid Id, string Rnc, string LegalName, string Plan, string Status);

    private sealed record PagedResponse<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);
}
