using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Settings;

public sealed class MaintenanceModeTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string ToggleUrl = "/api/v1/platform-settings/platform.maintenance_mode";

    private Task<HttpResponseMessage> SetMaintenanceAsync(bool on) =>
        Client.PutAsJsonAsync(ToggleUrl, new { value = on ? "true" : "false" });

    [RequiresDockerFact]
    public async Task Blocks_regular_traffic_with_503_but_keeps_health_and_settings_reachable()
    {
        (await Client.GetAsync("/api/v1/tenants")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await SetMaintenanceAsync(true);

        (await Client.GetAsync("/api/v1/tenants")).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await Client.GetAsync("/health")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Client.GetAsync("/api/v1/platform-settings")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [RequiresDockerFact]
    public async Task Turning_it_off_restores_traffic()
    {
        await SetMaintenanceAsync(true);
        (await Client.GetAsync("/api/v1/tenants")).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        await SetMaintenanceAsync(false);
        (await Client.GetAsync("/api/v1/tenants")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [RequiresDockerFact]
    public async Task The_503_carries_a_retry_after_header_and_problem_details()
    {
        await SetMaintenanceAsync(true);

        var response = await Client.GetAsync("/api/v1/tenants");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        response.Headers.RetryAfter.ShouldNotBeNull();
        (await response.Content.ReadAsStringAsync()).ShouldContain("mantenimiento");
    }
}
