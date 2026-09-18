using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Security;

/// <summary>
/// Alta y baja de usuarios del dashboard por el operador (slice D):
/// <c>POST/GET/DELETE /tenants/{id}/users</c> y <c>/operator-users</c>.
/// El entorno de pruebas es Development, así que la política de operador la
/// satisface el handler AdminKey por defecto; el objetivo acá es el flujo
/// aprovisionar → autenticar → revocar.
/// </summary>
public sealed class PlatformUserProvisioningTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private async Task<Guid> OnboardTenantAsync()
    {
        var setup = await Client.PostAsJsonAsync("/api/v1/dev/sandbox", new { });
        setup.StatusCode.ShouldBe(HttpStatusCode.OK, await setup.Content.ReadAsStringAsync());
        return (await LeerAsync<SandboxResponse>(setup))!.TenantId;
    }

    private void ActAsHuman(string authUserId, string email)
    {
        foreach (var h in new[] { "X-API-Key", "X-Admin-Key", "X-Tenant-Id", "X-Internal-Key", "X-Acting-User", "X-Acting-Email" })
            Client.DefaultRequestHeaders.Remove(h);
        Client.DefaultRequestHeaders.Add("X-Internal-Key", ApiFactory.InternalApiKey);
        Client.DefaultRequestHeaders.Add("X-Acting-User", authUserId);
        Client.DefaultRequestHeaders.Add("X-Acting-Email", email);
    }

    private void AsOperator()
    {
        foreach (var h in new[] { "X-API-Key", "X-Admin-Key", "X-Tenant-Id", "X-Internal-Key", "X-Acting-User", "X-Acting-Email" })
            Client.DefaultRequestHeaders.Remove(h);
    }

    [RequiresDockerFact]
    public async Task Provision_then_authenticate_then_revoke()
    {
        var tenantId = await OnboardTenantAsync();

        var provision = await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/users",
            new { email = "Nuevo.Empleado@Cliente.DO", role = "emisor" });
        provision.StatusCode.ShouldBe(HttpStatusCode.Created, await provision.Content.ReadAsStringAsync());

        var created = await LeerAsync<UserView>(provision);
        created!.Email.ShouldBe("nuevo.empleado@cliente.do"); // normalizado
        created.Role.ShouldBe("emisor");
        created.AuthLinked.ShouldBeFalse();

        var list = await LeerAsync<UserView[]>(await Client.GetAsync($"/api/v1/tenants/{tenantId}/users"));
        list!.ShouldContain(u => u.Id == created.Id);

        // Ahora la persona entra por el dashboard.
        ActAsHuman("auth-nuevo", "nuevo.empleado@cliente.do");
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // El operador la revoca.
        AsOperator();
        (await Client.DeleteAsync($"/api/v1/tenants/{tenantId}/users/{created.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Revocar de nuevo: conflicto.
        (await Client.DeleteAsync($"/api/v1/tenants/{tenantId}/users/{created.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // La revocación es de ESTE tenant (TenantMember), no global
        // (PlatformUser) — la persona sigue pudiendo autenticar (no queda
        // "borrada" de la plataforma), pero sin tenant activo no puede
        // operar acá.
        ActAsHuman("auth-nuevo", "nuevo.empleado@cliente.do");
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [RequiresDockerFact]
    public async Task Revoking_a_user_from_one_tenant_does_not_affect_their_access_to_another()
    {
        var tenantA = await OnboardTenantAsync();
        var tenantB = await OnboardTenantAsync();
        const string email = "multi@cliente.do";

        (await Client.PostAsJsonAsync($"/api/v1/tenants/{tenantA}/users", new { email, role = "emisor" }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        var provisionB = await Client.PostAsJsonAsync($"/api/v1/tenants/{tenantB}/users", new { email, role = "consultor" });
        var userB = await LeerAsync<UserView>(provisionB);

        AsOperator();
        (await Client.DeleteAsync($"/api/v1/tenants/{tenantA}/users/{userB!.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Sigue activo en B — la revocación de A no se filtró.
        ActAsHuman("auth-multi", email);
        Client.DefaultRequestHeaders.Add("X-Acting-Tenant-Id", tenantB.ToString());
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Pero ya no en A.
        Client.DefaultRequestHeaders.Remove("X-Acting-Tenant-Id");
        Client.DefaultRequestHeaders.Add("X-Acting-Tenant-Id", tenantA.ToString());
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task Provisioning_a_tenant_user_rejects_admin_sistema()
    {
        var tenantId = await OnboardTenantAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/users",
            new { email = "x@cliente.do", role = "admin_sistema" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task Provisioning_the_same_email_twice_is_a_conflict()
    {
        var tenantId = await OnboardTenantAsync();
        var body = new { email = "dupe@cliente.do", role = "consultor" };

        (await Client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/users", body))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/users", body))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [RequiresDockerFact]
    public async Task Provisioning_the_same_email_for_a_different_tenant_succeeds()
    {
        var tenantA = await OnboardTenantAsync();
        var tenantB = await OnboardTenantAsync();

        var provisionA = await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantA}/users",
            new { email = "multi@cliente.do", role = "admin_tenant" });
        provisionA.StatusCode.ShouldBe(HttpStatusCode.Created, await provisionA.Content.ReadAsStringAsync());
        var userA = await LeerAsync<UserView>(provisionA);

        var provisionB = await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantB}/users",
            new { email = "multi@cliente.do", role = "consultor" });
        provisionB.StatusCode.ShouldBe(HttpStatusCode.Created, await provisionB.Content.ReadAsStringAsync());
        var userB = await LeerAsync<UserView>(provisionB);

        // Misma cuenta (mismo PlatformUser), con acceso a los dos tenants.
        userB!.Id.ShouldBe(userA!.Id);

        var listA = await LeerAsync<UserView[]>(await Client.GetAsync($"/api/v1/tenants/{tenantA}/users"));
        listA!.ShouldContain(u => u.Id == userA.Id);

        var listB = await LeerAsync<UserView[]>(await Client.GetAsync($"/api/v1/tenants/{tenantB}/users"));
        listB!.ShouldContain(u => u.Id == userA.Id);
    }

    [RequiresDockerFact]
    public async Task Provisioning_for_an_unknown_tenant_is_not_found()
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{Guid.CreateVersion7()}/users",
            new { email = "x@cliente.do", role = "emisor" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [RequiresDockerFact]
    public async Task An_operator_can_be_provisioned_and_then_authenticates()
    {
        Reconfigure(new Dictionary<string, string?>
        {
            ["Security:AdminApiKey"] = "s3cr3t-operator",
            ["Security:InternalApiKey"] = ApiFactory.InternalApiKey,
        });

        Client.DefaultRequestHeaders.Add("X-Admin-Key", "s3cr3t-operator");
        var provision = await Client.PostAsJsonAsync("/api/v1/operator-users", new { email = "boss@nemus.do" });
        provision.StatusCode.ShouldBe(HttpStatusCode.Created, await provision.Content.ReadAsStringAsync());

        var created = await LeerAsync<UserView>(provision);
        created!.Role.ShouldBe("admin_sistema");
        created.TenantId.ShouldBeNull();

        var operators = await LeerAsync<UserView[]>(await Client.GetAsync("/api/v1/operator-users"));
        operators!.ShouldContain(u => u.Email == "boss@nemus.do");

        // Entra por el dashboard y pasa la política de operador.
        ActAsHuman("auth-boss", "boss@nemus.do");
        (await Client.GetAsync("/api/v1/tenants")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [RequiresDockerFact]
    public async Task A_tenant_user_id_cannot_be_revoked_through_the_operator_users_route()
    {
        var tenantId = await OnboardTenantAsync();
        var provision = await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/users",
            new { email = "scoped@cliente.do", role = "emisor" });
        var created = await LeerAsync<UserView>(provision);

        // La ruta de operadores solo revoca operadores (tenant_id nulo).
        (await Client.DeleteAsync($"/api/v1/operator-users/{created!.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed record SandboxResponse(Guid TenantId, string ApiKey);

    private sealed record UserView(
        Guid Id, string Email, string Role, Guid? TenantId, bool AuthLinked, DateTimeOffset? RevokedAt);
}
