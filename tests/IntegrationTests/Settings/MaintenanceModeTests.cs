using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Settings;

public sealed class MaintenanceModeTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string ToggleUrl = "/api/v1/platform-settings/platform.maintenance_mode";
    private const string MessageUrl = "/api/v1/platform-settings/platform.maintenance_message";

    // maintenance_mode es sensible → `confirm: true`.
    private Task<HttpResponseMessage> SetMaintenanceAsync(bool on) =>
        Client.PutAsJsonAsync(ToggleUrl, new { value = on ? "true" : "false", confirm = true });

    private Task<HttpResponseMessage> SetMaintenanceMessageAsync(string message) =>
        Client.PutAsJsonAsync(MessageUrl, new { value = message });

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

    [RequiresDockerFact]
    public async Task A_custom_message_replaces_the_default_503_detail()
    {
        await SetMaintenanceMessageAsync("Ventana programada hasta las 15:00.");
        await SetMaintenanceAsync(true);

        var response = await Client.GetAsync("/api/v1/tenants");

        (await response.Content.ReadAsStringAsync()).ShouldContain("Ventana programada hasta las 15:00.");
    }

    [RequiresDockerFact]
    public async Task An_empty_message_falls_back_to_the_default_text()
    {
        await SetMaintenanceMessageAsync(string.Empty);
        await SetMaintenanceAsync(true);

        var response = await Client.GetAsync("/api/v1/tenants");

        (await response.Content.ReadAsStringAsync()).ShouldContain("en mantenimiento. Intenta de nuevo");
    }
}
