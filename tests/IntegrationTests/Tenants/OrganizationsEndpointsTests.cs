using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Tenants;

/// <summary>
/// Organizaciones (Fase 2 de la jerarquía User -&gt; Organization -&gt; Tenant):
/// alta, membresía self-service, asociación de tenants y suspensión en
/// cascada. Ver <c>docs/multi-tenancy-hierarchy.md</c>.
/// </summary>
public sealed class OrganizationsEndpointsTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private async Task<Guid> RegisterOrganizationAsync(string name, string slug, string? ownerEmail = null)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/organizations", new { name, slug, plan = "Developer", ownerEmail });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await LeerAsync<IdResponse>(response))!.Id;
    }

    private void ActAsHuman(string authUserId, string email)
    {
        foreach (var h in new[] { "X-API-Key", "X-Tenant-Id", "X-Internal-Key", "X-Acting-User", "X-Acting-Email" })
            Client.DefaultRequestHeaders.Remove(h);

        Client.DefaultRequestHeaders.Add("X-Internal-Key", ApiFactory.InternalApiKey);
        Client.DefaultRequestHeaders.Add("X-Acting-User", authUserId);
        Client.DefaultRequestHeaders.Add("X-Acting-Email", email);
    }

    [RequiresDockerFact]
    public async Task Register_then_get_returns_the_organization()
    {
        var id = await RegisterOrganizationAsync("Acme Group", "acme-group");

        var get = await Client.GetAsync($"/api/v1/organizations/{id}");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await LeerAsync<OrganizationDetailResponse>(get);
        detail!.Name.ShouldBe("Acme Group");
        detail.Slug.ShouldBe("acme-group");
        detail.Plan.ShouldBe("Developer");
        detail.Status.ShouldBe("Active");
    }

    [RequiresDockerFact]
    public async Task Register_rejects_a_duplicate_slug_with_409()
    {
        await RegisterOrganizationAsync("Acme", "acme-dup");

        var response = await Client.PostAsJsonAsync(
            "/api/v1/organizations", new { name = "Acme 2", slug = "acme-dup", plan = "Developer" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [RequiresDockerFact]
    public async Task Register_with_owner_email_provisions_the_user_and_adds_them_as_owner()
    {
        var orgId = await RegisterOrganizationAsync("Acme", "acme-owner", ownerEmail: "owner@acme.do");

        ActAsHuman("auth-owner", "owner@acme.do");
        var members = await LeerAsync<OrganizationMemberResponse[]>(
            await Client.GetAsync($"/api/v1/organizations/{orgId}/members"));

        var owner = members!.ShouldHaveSingleItem();
        owner.Email.ShouldBe("owner@acme.do");
        owner.Role.ShouldBe("owner");
    }

    [RequiresDockerFact]
    public async Task Assign_tenant_then_list_shows_it()
    {
        var orgId = await RegisterOrganizationAsync("Acme", "acme-tenants", ownerEmail: "owner@acme-tenants.do");
        var tenantId = await RegisterTenantAsync("130444555");

        var assign = await Client.PostAsync($"/api/v1/organizations/{orgId}/tenants/{tenantId}", null);
        assign.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Self-service: el listado lo pide un miembro autenticado, no el operador implícito.
        ActAsHuman("auth-owner", "owner@acme-tenants.do");
        var list = await Client.GetAsync($"/api/v1/organizations/{orgId}/tenants");
        list.StatusCode.ShouldBe(HttpStatusCode.OK, await list.Content.ReadAsStringAsync());

        var tenants = await LeerAsync<PagedResponse<TenantSummaryResponse>>(list);

        tenants!.Items.ShouldHaveSingleItem().Id.ShouldBe(tenantId);
    }

    [RequiresDockerFact]
    public async Task Unassign_tenant_then_list_no_longer_shows_it()
    {
        var orgId = await RegisterOrganizationAsync("Acme", "acme-unassign", ownerEmail: "owner@acme-unassign.do");
        var tenantId = await RegisterTenantAsync("130444556");

        (await Client.PostAsync($"/api/v1/organizations/{orgId}/tenants/{tenantId}", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var unassign = await Client.DeleteAsync($"/api/v1/organizations/{orgId}/tenants/{tenantId}");
        unassign.StatusCode.ShouldBe(HttpStatusCode.NoContent, await unassign.Content.ReadAsStringAsync());

        ActAsHuman("auth-owner", "owner@acme-unassign.do");
        var list = await LeerAsync<PagedResponse<TenantSummaryResponse>>(
            await Client.GetAsync($"/api/v1/organizations/{orgId}/tenants"));

        list!.Items.ShouldBeEmpty();
    }

    [RequiresDockerFact]
    public async Task Assigning_an_already_owned_tenant_to_another_organization_is_a_409()
    {
        var orgId = await RegisterOrganizationAsync("Acme", "acme-move-a");
        var otherOrgId = await RegisterOrganizationAsync("Other", "acme-move-b");
        var tenantId = await RegisterTenantAsync("130444558");

        (await Client.PostAsync($"/api/v1/organizations/{orgId}/tenants/{tenantId}", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var move = await Client.PostAsync($"/api/v1/organizations/{otherOrgId}/tenants/{tenantId}", null);

        move.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [RequiresDockerFact]
    public async Task Unassigning_a_tenant_from_the_wrong_organization_is_a_409()
    {
        var orgId = await RegisterOrganizationAsync("Acme", "acme-unassign-wrong");
        var otherOrgId = await RegisterOrganizationAsync("Other", "other-unassign-wrong");
        var tenantId = await RegisterTenantAsync("130444557");

        (await Client.PostAsync($"/api/v1/organizations/{orgId}/tenants/{tenantId}", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var unassign = await Client.DeleteAsync($"/api/v1/organizations/{otherOrgId}/tenants/{tenantId}");

        unassign.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [RequiresDockerFact]
    public async Task The_organization_audit_log_shows_actions_taken_on_it()
    {
        var orgId = await RegisterOrganizationAsync("Acme", "acme-org-audit");

        (await Client.PatchAsJsonAsync($"/api/v1/organizations/{orgId}/plan", new { plan = "Enterprise" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var log = await LeerAsync<PagedResponse<AuditLogRowResponse>>(
            await Client.GetAsync($"/api/v1/organizations/{orgId}/audit-log"));

        log!.Items.ShouldContain(row => row.Path.Contains($"{orgId}/plan") && row.HttpMethod == "PATCH");
    }

    [RequiresDockerFact]
    public async Task Suspending_the_organization_blocks_authentication_for_its_tenants()
    {
        var orgId = await RegisterOrganizationAsync("Acme", "acme-suspend");

        var setup = await Client.PostAsJsonAsync("/api/v1/dev/sandbox", new { });
        setup.StatusCode.ShouldBe(HttpStatusCode.OK, await setup.Content.ReadAsStringAsync());
        var sandbox = await LeerAsync<SandboxResponse>(setup);

        (await Client.PostAsync($"/api/v1/organizations/{orgId}/tenants/{sandbox!.TenantId}", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Client.DefaultRequestHeaders.Add("X-API-Key", sandbox.ApiKey);
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.OK);

        Client.DefaultRequestHeaders.Remove("X-API-Key");
        (await Client.PostAsync($"/api/v1/organizations/{orgId}/suspend", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Client.DefaultRequestHeaders.Add("X-API-Key", sandbox.ApiKey);
        (await Client.GetAsync("/api/v1/ecf")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task An_owner_can_manage_members_but_a_plain_member_cannot()
    {
        var orgId = await RegisterOrganizationAsync("Acme", "acme-roles", ownerEmail: "owner@acme.do");
        await RegisterOrganizationAsync("Other", "other-org", ownerEmail: "member@acme.do");
        await RegisterOrganizationAsync("Third", "third-org", ownerEmail: "third@acme.do");

        ActAsHuman("auth-owner", "owner@acme.do");
        var addMember = await Client.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/members", new { email = "member@acme.do", role = "member" });
        addMember.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Un member no puede agregar a un tercero.
        ActAsHuman("auth-member", "member@acme.do");
        var denied = await Client.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/members", new { email = "third@acme.do", role = "member" });
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // El owner sí puede.
        ActAsHuman("auth-owner", "owner@acme.do");
        var allowed = await Client.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/members", new { email = "third@acme.do", role = "member" });
        allowed.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [RequiresDockerFact]
    public async Task Inviting_a_brand_new_email_provisions_the_user_in_the_same_step()
    {
        var orgId = await RegisterOrganizationAsync("Acme", "acme-auto-provision", ownerEmail: "owner@acme-auto.do");

        ActAsHuman("auth-owner", "owner@acme-auto.do");
        var invite = await Client.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/members",
            new { email = "nunca-antes-visto@acme-auto.do", role = "member" });

        invite.StatusCode.ShouldBe(HttpStatusCode.Created, await invite.Content.ReadAsStringAsync());

        var member = await LeerAsync<OrganizationMemberResponse>(invite);
        member!.Email.ShouldBe("nunca-antes-visto@acme-auto.do");
        member.Role.ShouldBe("member");

        // Ya puede autenticarse como cualquier otro usuario de la plataforma.
        ActAsHuman("auth-nuevo", "nunca-antes-visto@acme-auto.do");
        (await Client.GetAsync($"/api/v1/organizations/{orgId}/members")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [RequiresDockerFact]
    public async Task A_member_can_list_the_organizations_tenants_but_an_outsider_cannot()
    {
        var orgId = await RegisterOrganizationAsync("Acme", "acme-tenants-self-service", ownerEmail: "owner@acme.do");
        await RegisterOrganizationAsync("Other", "other-org-member", ownerEmail: "member@acme.do");
        await RegisterOrganizationAsync("Third", "other-org-outsider", ownerEmail: "outsider@acme.do");
        var tenantId = await RegisterTenantAsync("130444556");

        ActAsHuman("auth-owner", "owner@acme.do");
        var assign = await Client.PostAsync($"/api/v1/organizations/{orgId}/tenants/{tenantId}", null);
        assign.StatusCode.ShouldBe(HttpStatusCode.NoContent, await assign.Content.ReadAsStringAsync());
        var addMember = await Client.PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/members", new { email = "member@acme.do", role = "member" });
        addMember.StatusCode.ShouldBe(HttpStatusCode.Created, await addMember.Content.ReadAsStringAsync());

        // Un miembro cualquiera (no solo owner/admin) puede listar los tenants de su organización.
        ActAsHuman("auth-member", "member@acme.do");
        var asMember = await Client.GetAsync($"/api/v1/organizations/{orgId}/tenants");
        asMember.StatusCode.ShouldBe(HttpStatusCode.OK, await asMember.Content.ReadAsStringAsync());
        (await LeerAsync<PagedResponse<TenantSummaryResponse>>(asMember))!
            .Items.ShouldHaveSingleItem().Id.ShouldBe(tenantId);

        // Alguien sin membresía en esta organización no puede.
        ActAsHuman("auth-outsider", "outsider@acme.do");
        (await Client.GetAsync($"/api/v1/organizations/{orgId}/tenants"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [RequiresDockerFact]
    public async Task The_tenant_grid_shows_environment_certificate_and_last_ecf()
    {
        var orgId = await RegisterOrganizationAsync("Acme", "acme-tenant-grid", ownerEmail: "owner@acme-grid.do");

        var setup = await Client.PostAsJsonAsync("/api/v1/dev/sandbox", new { });
        setup.StatusCode.ShouldBe(HttpStatusCode.OK, await setup.Content.ReadAsStringAsync());
        var sandbox = await LeerAsync<SandboxResponse>(setup);

        (await Client.PostAsync($"/api/v1/organizations/{orgId}/tenants/{sandbox!.TenantId}", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        ActAsHuman("auth-owner", "owner@acme-grid.do");
        var beforeIssuance = await LeerAsync<PagedResponse<TenantSummaryResponse>>(
            await Client.GetAsync($"/api/v1/organizations/{orgId}/tenants"));
        var row = beforeIssuance!.Items.ShouldHaveSingleItem();

        // El sandbox ya deja perfil de emisor + certificado activo, pero
        // todavía ningún e-CF.
        row.DefaultEnvironment.ShouldBe("Test");
        row.CertificateExpiresAt.ShouldNotBeNull();
        row.LastEcfIssuedAt.ShouldBeNull();

        Client.DefaultRequestHeaders.Add("X-API-Key", sandbox.ApiKey);
        var issue = await Client.PostAsJsonAsync("/api/v1/ecf", new
        {
            type = 31,
            incomeType = "01",
            buyer = new { name = "Cliente de Prueba SRL", rnc = "131880681" },
            payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 2360m } } },
            lines = new[]
            {
                new { name = "Consultoría", kind = "service", quantity = 1, unitPrice = 2000m, itbisRate = 1, unitOfMeasure = "43" },
            },
        });
        issue.StatusCode.ShouldBe(HttpStatusCode.Created, await issue.Content.ReadAsStringAsync());
        Client.DefaultRequestHeaders.Remove("X-API-Key");

        ActAsHuman("auth-owner", "owner@acme-grid.do");
        var afterIssuance = await LeerAsync<PagedResponse<TenantSummaryResponse>>(
            await Client.GetAsync($"/api/v1/organizations/{orgId}/tenants"));

        afterIssuance!.Items.ShouldHaveSingleItem().LastEcfIssuedAt.ShouldNotBeNull();
    }

    [RequiresDockerFact]
    public async Task Operator_can_change_the_plan_but_a_plain_member_cannot()
    {
        const string adminKeyHeader = "X-Admin-Key";
        var orgId = await RegisterOrganizationAsync("Acme", "acme-plan", ownerEmail: "owner@acme-plan.do");

        // Con la clave de operador configurada, un miembro autenticado como humano
        // (sin rol admin_sistema) no pasa la política Operator.
        Reconfigure(new Dictionary<string, string?> { ["Security:AdminApiKey"] = "s3cr3t-operator" });
        ActAsHuman("auth-owner", "owner@acme-plan.do");
        var denied = await Client.PatchAsJsonAsync($"/api/v1/organizations/{orgId}/plan", new { plan = "Enterprise" });
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        foreach (var h in new[] { "X-Internal-Key", "X-Acting-User", "X-Acting-Email" })
            Client.DefaultRequestHeaders.Remove(h);
        Client.DefaultRequestHeaders.Add(adminKeyHeader, "s3cr3t-operator");

        var changed = await Client.PatchAsJsonAsync($"/api/v1/organizations/{orgId}/plan", new { plan = "Enterprise" });
        changed.StatusCode.ShouldBe(HttpStatusCode.NoContent, await changed.Content.ReadAsStringAsync());

        var detail = await LeerAsync<OrganizationDetailResponse>(await Client.GetAsync($"/api/v1/organizations/{orgId}"));
        detail!.Plan.ShouldBe("Enterprise");
    }

    [RequiresDockerFact]
    public async Task Changing_the_plan_to_an_unknown_value_is_rejected_with_400()
    {
        var orgId = await RegisterOrganizationAsync("Acme", "acme-plan-invalid");

        var response = await Client.PatchAsJsonAsync($"/api/v1/organizations/{orgId}/plan", new { plan = "NoExiste" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed record OrganizationDetailResponse(Guid Id, string Name, string Slug, string Plan, string Status);

    private sealed record OrganizationMemberResponse(Guid PlatformUserId, string Email, string Role);

    private sealed record TenantSummaryResponse(
        Guid Id,
        string Rnc,
        string LegalName,
        string Status,
        string? DefaultEnvironment,
        DateTimeOffset? CertificateExpiresAt,
        DateTimeOffset? LastEcfIssuedAt);

    private sealed record PagedResponse<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);

    private sealed record SandboxResponse(Guid TenantId, string ApiKey);

    private sealed record AuditLogRowResponse(Guid Id, string HttpMethod, string Path, int StatusCode);
}
