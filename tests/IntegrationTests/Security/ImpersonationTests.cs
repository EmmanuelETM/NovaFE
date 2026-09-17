using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Security;

/// <summary>
/// Fase 5: un operador (<c>admin_sistema</c>) puede ver la API como otro
/// usuario para dar soporte (<c>X-Impersonate-User-Id</c>), pero solo leer —
/// <c>ImpersonationReadOnlyFilter</c> bloquea cualquier escritura mientras el
/// claim <c>impersonated_by</c> esté presente. Ver docs/human-auth.md.
/// </summary>
public sealed class ImpersonationTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string AdminKey = "s3cr3t-operator";

    private void AsOperator()
    {
        foreach (var h in new[] { "X-API-Key", "X-Admin-Key", "X-Tenant-Id", "X-Internal-Key", "X-Acting-User", "X-Acting-Email", "X-Impersonate-User-Id" })
            Client.DefaultRequestHeaders.Remove(h);
        Client.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);
    }

    private void ActAsHuman(string authUserId, string email, Guid? impersonate = null)
    {
        foreach (var h in new[] { "X-API-Key", "X-Admin-Key", "X-Tenant-Id", "X-Internal-Key", "X-Acting-User", "X-Acting-Email", "X-Impersonate-User-Id" })
            Client.DefaultRequestHeaders.Remove(h);
        Client.DefaultRequestHeaders.Add("X-Internal-Key", ApiFactory.InternalApiKey);
        Client.DefaultRequestHeaders.Add("X-Acting-User", authUserId);
        Client.DefaultRequestHeaders.Add("X-Acting-Email", email);
        if (impersonate is { } id)
            Client.DefaultRequestHeaders.Add("X-Impersonate-User-Id", id.ToString());
    }

    private async Task<(Guid TenantId, string ApiKey)> OnboardTenantAsync()
    {
        var setup = await Client.PostAsJsonAsync("/api/v1/dev/sandbox", new { });
        setup.StatusCode.ShouldBe(HttpStatusCode.OK, await setup.Content.ReadAsStringAsync());
        var sandbox = await LeerAsync<SandboxResponse>(setup);
        return (sandbox!.TenantId, sandbox.ApiKey);
    }

    private async Task<Guid> ProvisionOperatorAsync(string email)
    {
        AsOperator();
        var response = await Client.PostAsJsonAsync("/api/v1/operator-users", new { email });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await LeerAsync<UserView>(response))!.Id;
    }

    private async Task<Guid> ProvisionTenantUserAsync(Guid tenantId, string email, string role)
    {
        AsOperator();
        var response = await Client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/users", new { email, role });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await LeerAsync<UserView>(response))!.Id;
    }

    [RequiresDockerFact]
    public async Task An_operator_impersonating_can_read_but_a_write_is_blocked()
    {
        Reconfigure(new Dictionary<string, string?>
        {
            ["Security:AdminApiKey"] = AdminKey,
            ["Security:InternalApiKey"] = ApiFactory.InternalApiKey,
        });

        await ProvisionOperatorAsync("boss@nemus.do");
        var (tenantId, _) = await OnboardTenantAsync();
        var targetId = await ProvisionTenantUserAsync(tenantId, "empleado@cliente.do", "admin_tenant");

        ActAsHuman("auth-boss", "boss@nemus.do", impersonate: targetId);

        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var blocked = await Client.PostAsJsonAsync(
            "/api/v1/webhooks", new { url = "http://93.184.215.14/hook", events = new[] { "ecf.accepted" } });
        blocked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [RequiresDockerFact]
    public async Task The_audit_log_row_carries_both_identities_during_impersonation()
    {
        Reconfigure(new Dictionary<string, string?>
        {
            ["Security:AdminApiKey"] = AdminKey,
            ["Security:InternalApiKey"] = ApiFactory.InternalApiKey,
        });

        var operatorId = await ProvisionOperatorAsync("boss2@nemus.do");
        var (tenantId, _) = await OnboardTenantAsync();
        var targetId = await ProvisionTenantUserAsync(tenantId, "empleado2@cliente.do", "admin_tenant");

        ActAsHuman("auth-boss2", "boss2@nemus.do", impersonate: targetId);
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.OK);

        AsOperator();
        var log = await LeerAsync<PagedResponse<AuditRowView>>(
            await Client.GetAsync($"/api/v1/tenants/{tenantId}/audit-log"));

        var row = log!.Items.ShouldHaveSingleItem();
        row.Actor.ShouldBe($"user:{targetId}");
        row.ImpersonatedBy.ShouldBe($"user:{operatorId}");
    }

    [RequiresDockerFact]
    public async Task A_non_operator_cannot_use_the_impersonation_header()
    {
        Reconfigure(new Dictionary<string, string?>
        {
            ["Security:AdminApiKey"] = AdminKey,
            ["Security:InternalApiKey"] = ApiFactory.InternalApiKey,
        });

        var (tenantId, _) = await OnboardTenantAsync();
        var actorId = await ProvisionTenantUserAsync(tenantId, "plain@cliente.do", "consultor");
        var otherId = await ProvisionTenantUserAsync(tenantId, "other@cliente.do", "admin_tenant");

        // El header lo ignora: sigue siendo él mismo, un consultor, no admin_tenant.
        ActAsHuman("auth-plain", "plain@cliente.do", impersonate: otherId);
        var response = await Client.PostAsJsonAsync(
            "/api/v1/webhooks", new { url = "http://93.184.215.14/hook", events = new[] { "ecf.accepted" } });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [RequiresDockerFact]
    public async Task Impersonating_another_operator_is_rejected()
    {
        Reconfigure(new Dictionary<string, string?>
        {
            ["Security:AdminApiKey"] = AdminKey,
            ["Security:InternalApiKey"] = ApiFactory.InternalApiKey,
        });

        await ProvisionOperatorAsync("boss3@nemus.do");
        var otherOperatorId = await ProvisionOperatorAsync("boss4@nemus.do");

        ActAsHuman("auth-boss3", "boss3@nemus.do", impersonate: otherOperatorId);

        (await Client.GetAsync("/api/v1/tenants")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed record SandboxResponse(Guid TenantId, string ApiKey);

    private sealed record UserView(Guid Id, string Email, string Role, Guid? TenantId);

    private sealed record AuditRowView(Guid Id, string Actor, string? ActorRole, string? ImpersonatedBy);

    private sealed record PagedResponse<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);
}
