using System.Net;
using System.Net.Http.Json;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Webhooks;

public sealed class WebhookEndpointsEndpointsTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    // IP pública literal (example.com) — pasa el guard sin resolver DNS.
    private const string PublicUrl = "http://93.184.215.14/hook";

    private static object Body(string url, params string[] events) => new { url, events, description = "ERP" };

    [RequiresDockerFact]
    public async Task Create_then_get_and_list_return_the_endpoint_without_the_secret()
    {
        await RegisterAndActAsTenantAsync("130111222");

        var create = await Client.PostAsJsonAsync("/api/v1/webhooks", Body(PublicUrl, "ecf.accepted", "ecf.rejected"));
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await LeerAsync<WebhookEndpointCreatedDto>(create);
        created!.Secret.ShouldStartWith("whsec_");
        created.Endpoint.Url.ShouldBe(PublicUrl);
        created.Endpoint.Events.ShouldBe(["ecf.accepted", "ecf.rejected"], ignoreOrder: true);

        var get = await Client.GetAsync($"/api/v1/webhooks/{created.Endpoint.Id}");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await get.Content.ReadAsStringAsync();
        raw.ShouldNotContain("whsec_");
        raw.ShouldNotContain("secret");

        var list = await LeerAsync<List<WebhookEndpointDto>>(await Client.GetAsync("/api/v1/webhooks"));
        list!.ShouldHaveSingleItem().Id.ShouldBe(created.Endpoint.Id);
    }

    [RequiresDockerFact]
    public async Task Endpoints_are_scoped_per_tenant()
    {
        var a = await RegisterTenantAsync("130333444");
        var b = await RegisterTenantAsync("130555666");

        ActAs(a);
        await Client.PostAsJsonAsync("/api/v1/webhooks", Body(PublicUrl, "ecf.accepted"));

        ActAs(b);
        var list = await LeerAsync<List<WebhookEndpointDto>>(await Client.GetAsync("/api/v1/webhooks"));
        list!.ShouldBeEmpty();
    }

    [RequiresDockerFact]
    public async Task Rejects_a_url_that_resolves_to_a_private_or_link_local_address()
    {
        await RegisterAndActAsTenantAsync("130777888");

        foreach (var url in new[] { "http://127.0.0.1/x", "http://10.0.0.5/x", "http://169.254.169.254/latest/meta-data" })
        {
            var response = await Client.PostAsJsonAsync("/api/v1/webhooks", Body(url, "ecf.accepted"));
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"url {url} debería rechazarse");
        }
    }

    [RequiresDockerFact]
    public async Task Rejects_an_unknown_event_type()
    {
        await RegisterAndActAsTenantAsync("130999000");

        var response = await Client.PostAsJsonAsync("/api/v1/webhooks", Body(PublicUrl, "ecf.exploded"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task Enforces_the_per_tenant_limit()
    {
        Reconfigure(new Dictionary<string, string?> { ["Webhooks:MaxEndpointsPerTenant"] = "2" });
        await RegisterAndActAsTenantAsync("131000111");

        (await Client.PostAsJsonAsync("/api/v1/webhooks", Body(PublicUrl, "ecf.accepted"))).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync("/api/v1/webhooks", Body(PublicUrl, "ecf.rejected"))).EnsureSuccessStatusCode();

        var third = await Client.PostAsJsonAsync("/api/v1/webhooks", Body(PublicUrl, "ecf.review"));
        third.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [RequiresDockerFact]
    public async Task Patch_updates_events_and_toggles_enabled()
    {
        await RegisterAndActAsTenantAsync("131222333");
        var created = await LeerAsync<WebhookEndpointCreatedDto>(
            await Client.PostAsJsonAsync("/api/v1/webhooks", Body(PublicUrl, "ecf.accepted")));

        string[] wildcard = ["ecf.*"];
        var patch = await Client.PatchAsJsonAsync(
            $"/api/v1/webhooks/{created!.Endpoint.Id}",
            new { events = wildcard, enabled = false });
        patch.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await LeerAsync<WebhookEndpointDto>(patch);
        updated!.Events.ShouldBe(["ecf.*"]);
        updated.Enabled.ShouldBeFalse();
    }

    [RequiresDockerFact]
    public async Task Rotate_secret_returns_a_new_one_and_delete_removes_the_endpoint()
    {
        await RegisterAndActAsTenantAsync("131444555");
        var created = await LeerAsync<WebhookEndpointCreatedDto>(
            await Client.PostAsJsonAsync("/api/v1/webhooks", Body(PublicUrl, "ecf.accepted")));

        var rotated = await LeerAsync<WebhookSecretDto>(
            await Client.PostAsync($"/api/v1/webhooks/{created!.Endpoint.Id}/rotate-secret", content: null));
        rotated!.Secret.ShouldStartWith("whsec_");
        rotated.Secret.ShouldNotBe(created.Secret);

        (await Client.DeleteAsync($"/api/v1/webhooks/{created.Endpoint.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/v1/webhooks/{created.Endpoint.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
