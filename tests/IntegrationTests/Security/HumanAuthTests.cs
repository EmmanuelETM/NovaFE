using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Tenants;
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

    private async Task<Guid> ProvisionTenantUserAsync(Guid tenantId, string email, PlatformRole role)
    {
        var user = PlatformUser.CreateTenantUser(email, tenantId, role).Value;
        var userId = await ProvisionAsync(user);

        // Fase 2: el login del dashboard resuelve el tenant/rol desde
        // tenant_members, no desde PlatformUser.TenantId/Role.
        using var scope = Factory.Services.CreateScope();
        var members = scope.ServiceProvider.GetRequiredService<ITenantMemberRepository>();
        await members.AddAsync(TenantMember.Create(tenantId, userId, role).Value);

        return userId;
    }

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
    public async Task Acting_tenant_header_lets_a_multi_tenant_user_switch()
    {
        var tenantA = await OnboardTenantAsync();
        var tenantB = await OnboardTenantAsync();

        var userId = await ProvisionTenantUserAsync(tenantA, "multi@cliente.do", PlatformRole.AdminTenant);

        using (var scope = Factory.Services.CreateScope())
        {
            var members = scope.ServiceProvider.GetRequiredService<ITenantMemberRepository>();
            await members.AddAsync(TenantMember.Create(tenantB, userId, PlatformRole.Consultor).Value);
        }

        ActAsHuman("auth-multi", "multi@cliente.do");
        Client.DefaultRequestHeaders.Add("X-Acting-Tenant-Id", tenantA.ToString());
        var meA = await LeerAsync<ProfileResponse>(await Client.GetAsync("/api/v1/users/me"));
        meA!.TenantId.ShouldBe(tenantA);
        meA.Role.ShouldBe("admin_tenant");

        Client.DefaultRequestHeaders.Remove("X-Acting-Tenant-Id");
        Client.DefaultRequestHeaders.Add("X-Acting-Tenant-Id", tenantB.ToString());
        var meB = await LeerAsync<ProfileResponse>(await Client.GetAsync("/api/v1/users/me"));
        meB!.TenantId.ShouldBe(tenantB);
        meB.Role.ShouldBe("consultor");
    }

    [RequiresDockerFact]
    public async Task Acting_tenant_header_for_a_tenant_without_access_is_unauthorized()
    {
        var tenantA = await OnboardTenantAsync();
        var tenantB = await OnboardTenantAsync();
        await ProvisionTenantUserAsync(tenantA, "onlya@cliente.do", PlatformRole.AdminTenant);

        ActAsHuman("auth-onlya", "onlya@cliente.do");
        Client.DefaultRequestHeaders.Add("X-Acting-Tenant-Id", tenantB.ToString());
        var response = await Client.GetAsync("/api/v1/users/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task Users_me_lists_a_directly_accessible_tenant_outside_any_organization()
    {
        var tenantId = await OnboardTenantAsync();
        await ProvisionTenantUserAsync(tenantId, "directo@cliente.do", PlatformRole.AdminTenant);

        ActAsHuman("auth-directo", "directo@cliente.do");
        var me = await LeerAsync<ProfileResponse>(await Client.GetAsync("/api/v1/users/me"));

        me!.DirectTenants.ShouldHaveSingleItem().TenantId.ShouldBe(tenantId);
    }

    [RequiresDockerFact]
    public async Task Users_me_does_not_duplicate_a_tenant_already_covered_by_organization_membership()
    {
        var register = await Client.PostAsJsonAsync(
            "/api/v1/organizations",
            new { name = "Acme", slug = "acme-direct-and-org", plan = "Developer", ownerEmail = "owner@acme-direct.do" });
        register.StatusCode.ShouldBe(HttpStatusCode.Created, await register.Content.ReadAsStringAsync());
        var orgId = (await LeerAsync<IdResponse>(register))!.Id;

        ActAsHuman("auth-owner-direct", "owner@acme-direct.do");
        var members = await LeerAsync<OrganizationMemberResponse[]>(
            await Client.GetAsync($"/api/v1/organizations/{orgId}/members"));
        var ownerId = members!.ShouldHaveSingleItem().PlatformUserId;

        // Asociar el tenant es una acción de operador — vuelve al atajo de
        // dev (sin cabeceras de identidad) antes de llamarla.
        foreach (var h in new[] { "X-API-Key", "X-Tenant-Id", "X-Internal-Key", "X-Acting-User", "X-Acting-Email" })
            Client.DefaultRequestHeaders.Remove(h);

        var tenantId = await OnboardTenantAsync();
        (await Client.PostAsync($"/api/v1/organizations/{orgId}/tenants/{tenantId}", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // El dueño de la organización, además, tiene una fila directa para
        // ese mismo tenant — no debería listarse dos veces.
        using (var scope = Factory.Services.CreateScope())
        {
            var tenantMembers = scope.ServiceProvider.GetRequiredService<ITenantMemberRepository>();
            await tenantMembers.AddAsync(TenantMember.Create(tenantId, ownerId, PlatformRole.AdminTenant).Value);
        }

        ActAsHuman("auth-owner-direct", "owner@acme-direct.do");
        var me = await LeerAsync<ProfileResponse>(await Client.GetAsync("/api/v1/users/me"));

        me!.DirectTenants.ShouldBeEmpty();
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

    private sealed record ProfileResponse(
        string Id,
        string? Email,
        string Role,
        Guid? TenantId,
        string? TenantName,
        IReadOnlyList<TenantEntryResponse> DirectTenants);

    private sealed record TenantEntryResponse(Guid TenantId, string TenantName, string Role);

    private sealed record OrganizationMemberResponse(Guid PlatformUserId, string Email, string Role);
}
