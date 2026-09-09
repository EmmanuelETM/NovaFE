using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Users;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Security;

/// <summary>
/// Autenticación de humanos del dashboard (esquema <c>InternalKey</c>, slices B/C).
/// El BFF de Next presenta <c>X-Internal-Key</c> + <c>X-Acting-User</c>/
/// <c>X-Acting-Email</c>; la API resuelve un <c>PlatformUser</c> y emite los
/// mismos claims que una API key.
/// </summary>
public sealed class HumanAuthTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private async Task<Guid> OnboardTenantAsync()
    {
        var setup = await Client.PostAsJsonAsync("/api/v1/dev/sandbox", new { });
        setup.StatusCode.ShouldBe(HttpStatusCode.OK, await setup.Content.ReadAsStringAsync());
        return (await LeerAsync<SandboxResponse>(setup))!.TenantId;
    }

    private async Task<Guid> ProvisionAsync(PlatformUser user)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPlatformUserRepository>();
        await repo.AddAsync(user);
        return user.Id;
    }

    private Task<Guid> ProvisionTenantUserAsync(Guid tenantId, string email, PlatformRole role)
        => ProvisionAsync(PlatformUser.CreateTenantUser(email, tenantId, role).Value);

    private Task<Guid> ProvisionOperatorAsync(string email)
        => ProvisionAsync(PlatformUser.CreateOperator(email).Value);

    private async Task RevokeAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPlatformUserRepository>();
        var user = await repo.GetAsync(userId);
        user!.Revoke(DateTimeOffset.UtcNow);
        await repo.UpdateAsync(user);
    }

    private async Task<string?> AuthUserIdOfAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPlatformUserRepository>();
        return (await repo.GetAsync(userId))!.AuthUserId;
    }

    private void ActAsHuman(string? authUserId, string email, string? internalKey = ApiFactory.InternalApiKey)
    {
        foreach (var h in new[] { "X-API-Key", "X-Tenant-Id", "X-Internal-Key", "X-Acting-User", "X-Acting-Email" })
            Client.DefaultRequestHeaders.Remove(h);

        if (internalKey is not null)
            Client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
        if (authUserId is not null)
            Client.DefaultRequestHeaders.Add("X-Acting-User", authUserId);
        Client.DefaultRequestHeaders.Add("X-Acting-Email", email);
    }

    [RequiresDockerFact]
    public async Task A_provisioned_tenant_user_passes_the_read_policy()
    {
        var tenantId = await OnboardTenantAsync();
        await ProvisionTenantUserAsync(tenantId, "consultor@cliente.do", PlatformRole.Consultor);

        ActAsHuman(authUserId: "auth-consultor", "consultor@cliente.do");
        var response = await Client.GetAsync("/api/v1/ecf");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [RequiresDockerFact]
    public async Task A_consultor_cannot_issue()
    {
        var tenantId = await OnboardTenantAsync();
        await ProvisionTenantUserAsync(tenantId, "consultor@cliente.do", PlatformRole.Consultor);

        ActAsHuman("auth-consultor", "consultor@cliente.do");
        var response = await Client.PostAsJsonAsync("/api/v1/ecf", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [RequiresDockerFact]
    public async Task An_unprovisioned_email_is_unauthorized()
    {
        await OnboardTenantAsync();

        ActAsHuman("auth-ghost", "ghost@nowhere.do");
        var response = await Client.GetAsync("/api/v1/ecf");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task A_revoked_user_is_unauthorized()
    {
        var tenantId = await OnboardTenantAsync();
        var userId = await ProvisionTenantUserAsync(tenantId, "revoked@cliente.do", PlatformRole.Emisor);
        await RevokeAsync(userId);

        ActAsHuman("auth-revoked", "revoked@cliente.do");
        var response = await Client.GetAsync("/api/v1/ecf");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task Acting_headers_without_the_internal_key_cannot_spoof()
    {
        var tenantId = await OnboardTenantAsync();
        await ProvisionTenantUserAsync(tenantId, "real@cliente.do", PlatformRole.AdminTenant);

        ActAsHuman("auth-real", "real@cliente.do", internalKey: null);
        var response = await Client.GetAsync("/api/v1/ecf");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task A_wrong_internal_key_is_unauthorized()
    {
        var tenantId = await OnboardTenantAsync();
        await ProvisionTenantUserAsync(tenantId, "real@cliente.do", PlatformRole.AdminTenant);

        ActAsHuman("auth-real", "real@cliente.do", internalKey: "not-the-key");
        var response = await Client.GetAsync("/api/v1/ecf");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task The_first_login_links_the_auth_user_id()
    {
        var tenantId = await OnboardTenantAsync();
        var userId = await ProvisionTenantUserAsync(tenantId, "firstlogin@cliente.do", PlatformRole.Emisor);
        (await AuthUserIdOfAsync(userId)).ShouldBeNull();

        ActAsHuman("auth-first-login", "firstlogin@cliente.do");
        var response = await Client.GetAsync("/api/v1/ecf");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await AuthUserIdOfAsync(userId)).ShouldBe("auth-first-login");
    }

    [RequiresDockerFact]
    public async Task A_human_operator_passes_the_operator_policy_and_a_tenant_user_does_not()
    {
        // Con la admin key configurada, el handler AdminKey deja de autenticar
        // solo (en dev autentica siempre): así se prueba el rol del camino InternalKey.
        Reconfigure(new Dictionary<string, string?>
        {
            ["Security:AdminApiKey"] = "s3cr3t-operator",
            ["Security:InternalApiKey"] = ApiFactory.InternalApiKey,
        });

        var tenantId = await OnboardTenantAsync();
        await ProvisionTenantUserAsync(tenantId, "admin@cliente.do", PlatformRole.AdminTenant);
        await ProvisionOperatorAsync("ops@nemus.do");

        ActAsHuman("auth-tenant-admin", "admin@cliente.do");
        (await Client.GetAsync("/api/v1/tenants")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        ActAsHuman("auth-operator", "ops@nemus.do");
        (await Client.GetAsync("/api/v1/tenants")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [RequiresDockerFact]
    public async Task Users_me_returns_the_profile_of_a_human()
    {
        var tenantId = await OnboardTenantAsync();
        await ProvisionTenantUserAsync(tenantId, "emisor@cliente.do", PlatformRole.Emisor);

        ActAsHuman("auth-emisor", "emisor@cliente.do");
        var response = await Client.GetAsync("/api/v1/users/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var me = await LeerAsync<ProfileResponse>(response);
        me!.Email.ShouldBe("emisor@cliente.do");
        me.Role.ShouldBe("emisor");
        me.TenantId.ShouldBe(tenantId);
        me.TenantName.ShouldNotBeNullOrWhiteSpace();
    }

    [RequiresDockerFact]
    public async Task Users_me_synthesizes_a_profile_for_the_dev_header()
    {
        var tenantId = await OnboardTenantAsync();
        ActAs(tenantId);

        var response = await Client.GetAsync("/api/v1/users/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var me = await LeerAsync<ProfileResponse>(response);
        me!.Role.ShouldBe("admin_tenant");
        me.TenantId.ShouldBe(tenantId);
    }

    private sealed record SandboxResponse(Guid TenantId, string ApiKey);

    private sealed record ProfileResponse(string Id, string? Email, string Role, Guid? TenantId, string? TenantName);
}
