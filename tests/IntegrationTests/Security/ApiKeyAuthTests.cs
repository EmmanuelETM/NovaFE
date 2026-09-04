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

    private async Task<(Guid TenantId, Guid KeyId, string Token)> MintKeyAsync(string rnc = "130862346")
    {
        var tenantId = await RegisterTenantAsync(rnc);

        var create = await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/api-keys", new { label = "Pruebas" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());

        var created = await LeerAsync<ApiKeyCreatedResponse>(create);
        created!.Token.ShouldStartWith("nfe_");
        return (tenantId, created.Key.Id, created.Token);
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
        var (_, _, token) = await MintKeyAsync();
        UseApiKey(token);

        var response = await Client.GetAsync("/api/v1/ecf");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [RequiresDockerFact]
    public async Task A_request_with_no_credential_is_401()
    {
        await MintKeyAsync();
        UseApiKey(null);

        var response = await Client.GetAsync("/api/v1/ecf");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task A_malformed_api_key_is_401()
    {
        await MintKeyAsync();
        UseApiKey("not-a-real-key");

        var response = await Client.GetAsync("/api/v1/ecf");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task A_revoked_api_key_stops_authenticating()
    {
        var (tenantId, keyId, token) = await MintKeyAsync();

        UseApiKey(token);
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // La revocación es un recurso de operador; en el entorno de test no exige admin key.
        Client.DefaultRequestHeaders.Remove(ApiKeyHeader);
        var revoke = await Client.DeleteAsync($"/api/v1/tenants/{tenantId}/api-keys/{keyId}");
        revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        UseApiKey(token);
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task Repeated_failed_attempts_lock_out_the_caller()
    {
        var (_, _, token) = await MintKeyAsync();

        UseApiKey("nfe_wrong_wrong_wrong_wrong_wrong_wrong_x");
        for (var i = 0; i < 5; i++)
            (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Con el origen bloqueado, ni siquiera la credencial buena pasa.
        UseApiKey(token);
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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

    private sealed record ApiKeyCreatedResponse(ApiKeyView Key, string Token);

    private sealed record ApiKeyView(Guid Id, Guid TenantId, string Prefix, string Label);
}
