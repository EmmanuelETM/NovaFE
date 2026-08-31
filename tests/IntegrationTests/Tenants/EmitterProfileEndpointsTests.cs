using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Tenants;

public sealed class EmitterProfileEndpointsTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private async Task<Guid> RegisterTenantAsync()
    {
        var register = await Client.PostAsJsonAsync("/api/v1.0/tenants", new
        {
            rnc = "132786262",
            legalName = "Acme SRL",
            plan = "Business",
        });
        register.EnsureSuccessStatusCode();
        return (await LeerAsync<IdResponse>(register))!.Id;
    }

    private static object ProfileBody(string address = "Av. 27 de Febrero 100", string environment = "TestEcf") => new
    {
        address,
        municipality = "010100",
        province = "01",
        phones = new[] { "809-555-0100", "809-555-0101" },
        email = "facturacion@acme.do",
        economicActivity = "Comercio al por menor",
        defaultEnvironment = environment,
    };

    [RequiresDockerFact]
    public async Task Put_creates_then_get_returns_the_profile()
    {
        var tenantId = await RegisterTenantAsync();

        var put = await Client.PutAsJsonAsync($"/api/v1.0/tenants/{tenantId}/emitter-profile", ProfileBody());
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var get = await Client.GetAsync($"/api/v1.0/tenants/{tenantId}/emitter-profile");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await LeerAsync<EmitterProfileResponse>(get);
        profile!.TenantId.ShouldBe(tenantId);
        profile.Address.ShouldBe("Av. 27 de Febrero 100");
        profile.Phones.ShouldBe(["809-555-0100", "809-555-0101"]);
        profile.DefaultEnvironment.ShouldBe("TestEcf");
    }

    [RequiresDockerFact]
    public async Task Put_is_an_upsert()
    {
        var tenantId = await RegisterTenantAsync();

        (await Client.PutAsJsonAsync($"/api/v1.0/tenants/{tenantId}/emitter-profile", ProfileBody())).EnsureSuccessStatusCode();
        (await Client.PutAsJsonAsync($"/api/v1.0/tenants/{tenantId}/emitter-profile",
            ProfileBody(address: "Calle Nueva 5", environment: "Production"))).EnsureSuccessStatusCode();

        var profile = await LeerAsync<EmitterProfileResponse>(
            await Client.GetAsync($"/api/v1.0/tenants/{tenantId}/emitter-profile"));

        profile!.Address.ShouldBe("Calle Nueva 5");
        profile.DefaultEnvironment.ShouldBe("Production");
    }

    [RequiresDockerFact]
    public async Task Get_returns_400_when_not_configured()
    {
        var tenantId = await RegisterTenantAsync();

        var get = await Client.GetAsync($"/api/v1.0/tenants/{tenantId}/emitter-profile");

        get.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task Put_rejects_an_unknown_tenant_with_404()
    {
        var put = await Client.PutAsJsonAsync(
            $"/api/v1.0/tenants/{Guid.NewGuid()}/emitter-profile", ProfileBody());

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [RequiresDockerFact]
    public async Task Put_rejects_a_blank_address_with_400()
    {
        var tenantId = await RegisterTenantAsync();

        var put = await Client.PutAsJsonAsync(
            $"/api/v1.0/tenants/{tenantId}/emitter-profile", ProfileBody(address: ""));

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed record EmitterProfileResponse(
        Guid Id,
        Guid TenantId,
        string Address,
        string? Municipality,
        string? Province,
        string[] Phones,
        string? Email,
        string? EconomicActivity,
        string DefaultEnvironment,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);
}
