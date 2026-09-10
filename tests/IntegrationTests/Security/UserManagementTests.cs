using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Security;

/// <summary>
/// Cambio de rol y reactivación de usuarios del dashboard por el operador:
/// <c>PATCH /tenants/{id}/users/{userId}</c> y
/// <c>POST .../users/{userId}/reinstate</c> (+ <c>/operator-users</c>).
/// </summary>
public sealed class UserManagementTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private async Task<Guid> OnboardTenantAsync()
    {
        var setup = await Client.PostAsJsonAsync("/api/v1/dev/sandbox", new { });
        setup.StatusCode.ShouldBe(HttpStatusCode.OK, await setup.Content.ReadAsStringAsync());
        return (await LeerAsync<SandboxResponse>(setup))!.TenantId;
    }

    private async Task<UserView> ProvisionTenantUserAsync(Guid tenantId, string email, string role)
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/users", new { email, role });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await LeerAsync<UserView>(response))!;
    }

    [RequiresDockerFact]
    public async Task Change_role_updates_the_user()
    {
        var tenantId = await OnboardTenantAsync();
        var user = await ProvisionTenantUserAsync(tenantId, "e@cliente.do", "emisor");

        var patch = await Client.PatchAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/users/{user.Id}", new { role = "consultor" });
        patch.StatusCode.ShouldBe(HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        (await LeerAsync<UserView>(patch))!.Role.ShouldBe("consultor");

        var list = await LeerAsync<UserView[]>(await Client.GetAsync($"/api/v1/tenants/{tenantId}/users"));
        list!.Single(u => u.Id == user.Id).Role.ShouldBe("consultor");
    }

    [RequiresDockerFact]
    public async Task Change_role_to_admin_sistema_is_rejected()
    {
        var tenantId = await OnboardTenantAsync();
        var user = await ProvisionTenantUserAsync(tenantId, "e@cliente.do", "emisor");

        var patch = await Client.PatchAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/users/{user.Id}", new { role = "admin_sistema" });

        patch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task Change_role_through_another_tenant_scope_is_not_found()
    {
        var tenantId = await OnboardTenantAsync();
        var user = await ProvisionTenantUserAsync(tenantId, "e@cliente.do", "emisor");

        var patch = await Client.PatchAsJsonAsync(
            $"/api/v1/tenants/{Guid.CreateVersion7()}/users/{user.Id}", new { role = "consultor" });

        patch.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [RequiresDockerFact]
    public async Task Revoke_then_reinstate_restores_access()
    {
        var tenantId = await OnboardTenantAsync();
        var user = await ProvisionTenantUserAsync(tenantId, "e@cliente.do", "emisor");

        (await Client.DeleteAsync($"/api/v1/tenants/{tenantId}/users/{user.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var reinstate = await Client.PostAsync(
            $"/api/v1/tenants/{tenantId}/users/{user.Id}/reinstate", content: null);
        reinstate.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var list = await LeerAsync<UserView[]>(await Client.GetAsync($"/api/v1/tenants/{tenantId}/users"));
        list!.Single(u => u.Id == user.Id).RevokedAt.ShouldBeNull();

        // Reactivar de nuevo: conflicto (idempotencia estricta).
        (await Client.PostAsync($"/api/v1/tenants/{tenantId}/users/{user.Id}/reinstate", content: null))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private sealed record SandboxResponse(Guid TenantId, string ApiKey);

    private sealed record UserView(
        Guid Id, string Email, string Role, Guid? TenantId, bool AuthLinked, DateTimeOffset? RevokedAt);
}
