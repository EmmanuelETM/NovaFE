using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Security;

/// <summary>
/// Registro de auditoría inmutable (RF-14.4). Cada petición a un endpoint
/// <c>[Authorize]</c> deja una fila, con éxito o no.
/// </summary>
public sealed class AuditLoggingTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string ApiKeyHeader = "X-API-Key";

    private async Task<(Guid TenantId, string Token)> OnboardAsync()
    {
        var setup = await Client.PostAsJsonAsync("/api/v1/dev/sandbox", new { });
        setup.StatusCode.ShouldBe(HttpStatusCode.OK, await setup.Content.ReadAsStringAsync());

        var sandbox = await LeerAsync<SandboxResponse>(setup);
        return (sandbox!.TenantId, sandbox.ApiKey);
    }

    private void UseApiKey(string token) => Client.DefaultRequestHeaders.Add(ApiKeyHeader, token);

    [RequiresDockerFact]
    public async Task A_successful_authenticated_request_is_audited()
    {
        var (tenantId, token) = await OnboardAsync();
        UseApiKey(token);

        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await LeerAsync<AuditPage>(
            await Client.GetAsync($"/api/v1/tenants/{tenantId}/audit-log"));
        var row = page!.Items.ShouldHaveSingleItem();

        row.TenantId.ShouldBe(tenantId);
        row.HttpMethod.ShouldBe("GET");
        row.Path.ShouldBe("/api/v1/ecf");
        row.StatusCode.ShouldBe(200);
        row.Succeeded.ShouldBeTrue();
        row.ActorRole.ShouldBe("admin_tenant");
        row.Actor.ShouldStartWith("apikey:");
    }

    [RequiresDockerFact]
    public async Task A_request_denied_by_role_is_still_audited()
    {
        var (tenantId, _) = await OnboardAsync();
        var mint = await Client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/api-keys", new { role = "consultor" });
        var consultorToken = (await LeerAsync<ApiKeyCreatedView>(mint))!.Token;

        UseApiKey(consultorToken);
        (await Client.PostAsJsonAsync("/api/v1/ecf", new { type = 31 })).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        var page = await LeerAsync<AuditPage>(
            await Client.GetAsync($"/api/v1/tenants/{tenantId}/audit-log"));
        var denied = page!.Items.Single(i => i.HttpMethod == "POST" && i.Path == "/api/v1/ecf");

        denied.StatusCode.ShouldBe(403);
        denied.Succeeded.ShouldBeFalse();
    }

    private sealed record SandboxResponse(Guid TenantId, string ApiKey);

    private sealed record ApiKeyCreatedView(string Token);

    private sealed record AuditRow(
        Guid TenantId, string Actor, string? ActorRole, string HttpMethod, string Path, int StatusCode, bool Succeeded);

    private sealed record AuditPage(IReadOnlyList<AuditRow> Items, int TotalCount);
}
