using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Security;

/// <summary>
/// Autenticación de la API por API key (Módulo 14). El entorno de las pruebas es
/// <c>Development</c>, así que <c>X-Tenant-Id</c> sin credencial sigue funcionando;
/// estas pruebas ejercen explícitamente el camino de la API key y el de operador.
/// </summary>
public sealed class ApiKeyAuthTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string ApiKeyHeader = "X-API-Key";
    private const string AdminKeyHeader = "X-Admin-Key";

    /// <summary>Deja un contribuyente listo (perfil + cert + secuencias) y devuelve su tenant + API key de Test.</summary>
    private async Task<(Guid TenantId, string Token)> OnboardAsync()
    {
        var setup = await Client.PostAsJsonAsync("/api/v1/dev/sandbox", new { });
        setup.StatusCode.ShouldBe(HttpStatusCode.OK, await setup.Content.ReadAsStringAsync());

        var sandbox = await LeerAsync<SandboxResponse>(setup);
        sandbox!.ApiKey.ShouldStartWith("sk_nfe_test_");
        return (sandbox.TenantId, sandbox.ApiKey);
    }

    private void UseApiKey(string? token)
    {
        Client.DefaultRequestHeaders.Remove(ApiKeyHeader);
        Client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        if (token is not null)
            Client.DefaultRequestHeaders.Add(ApiKeyHeader, token);
    }

    [RequiresDockerFact]
    public async Task A_valid_api_key_authenticates_a_tenant_request()
    {
        var (_, token) = await OnboardAsync();
        UseApiKey(token);

        var response = await Client.GetAsync("/api/v1/ecf");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [RequiresDockerFact]
    public async Task An_api_key_of_Test_issues_in_Test()
    {
        var (_, token) = await OnboardAsync();
        UseApiKey(token);

        var post = await Client.PostAsJsonAsync("/api/v1/ecf", new
        {
            type = 31,
            incomeType = "01",
            buyer = new { name = "Cliente SRL", rnc = "131880681" },
            payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 2360m } } },
            lines = new[] { new { name = "Consultoría", kind = "service", quantity = 1, unitPrice = 2000m, itbisRate = 1 } },
        });
        post.StatusCode.ShouldBe(HttpStatusCode.Created, await post.Content.ReadAsStringAsync());

        var issued = await LeerAsync<EcfResponse>(post);
        // El timbre QR lleva el segmento del ambiente de la key.
        issued!.QrUrl.ShouldContain("/testecf/");
    }

    [RequiresDockerFact]
    public async Task A_request_with_no_credential_is_401()
    {
        await OnboardAsync();
        UseApiKey(null);

        var response = await Client.GetAsync("/api/v1/ecf");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task A_malformed_api_key_is_401()
    {
        await OnboardAsync();
        UseApiKey("not-a-real-key");

        var response = await Client.GetAsync("/api/v1/ecf");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task A_revoked_api_key_stops_authenticating()
    {
        var (tenantId, token) = await OnboardAsync();

        UseApiKey(token);
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Recupera el id de la key y revócala (recurso de operador; en test no exige admin key).
        Client.DefaultRequestHeaders.Remove(ApiKeyHeader);
        var keys = await LeerAsync<ApiKeyView[]>(await Client.GetAsync($"/api/v1/tenants/{tenantId}/api-keys"));
        var keyId = keys!.Single().Id;

        var revoke = await Client.DeleteAsync($"/api/v1/tenants/{tenantId}/api-keys/{keyId}");
        revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        UseApiKey(token);
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task Repeated_failed_attempts_lock_out_the_caller()
    {
        var (_, token) = await OnboardAsync();

        UseApiKey("sk_nfe_test_wrong_wrong_wrong_wrong_wrong_x");
        for (var i = 0; i < 5; i++)
            (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Con el origen bloqueado, ni siquiera la credencial buena pasa.
        UseApiKey(token);
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task Minting_a_key_for_an_unready_environment_is_a_400()
    {
        var tenantId = await RegisterTenantAsync("130999888");

        var create = await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/api-keys", new { label = "Prod", environment = "Production" });

        create.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task The_dev_tenant_header_still_works_in_the_test_environment()
    {
        var tenantId = await RegisterTenantAsync("131234567");
        ActAs(tenantId);

        var response = await Client.GetAsync("/api/v1/ecf");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [RequiresDockerFact]
    public async Task Operator_endpoints_require_the_admin_key_when_it_is_configured()
    {
        Reconfigure(new Dictionary<string, string?> { ["Security:AdminApiKey"] = "s3cr3t-operator" });

        var withoutKey = await Client.PostAsJsonAsync("/api/v1/tenants", new
        {
            rnc = "130111222",
            legalName = "Sin Llave SRL",
            plan = "Business",
        });
        withoutKey.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        Client.DefaultRequestHeaders.Add(AdminKeyHeader, "s3cr3t-operator");
        var withKey = await Client.PostAsJsonAsync("/api/v1/tenants", new
        {
            rnc = "130111222",
            legalName = "Con Llave SRL",
            plan = "Business",
        });
        withKey.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private sealed record SandboxResponse(Guid TenantId, string ApiKey);

    private sealed record ApiKeyView(Guid Id, Guid TenantId, string Prefix, string Label, string Environment);

    private sealed record EcfResponse(Guid Id, string Encf, string QrUrl);
}
