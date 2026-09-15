using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Contingency;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Application.Webhooks.Delivery;
using NovaFE.Domain.Settings;
using NovaFE.Infrastructure.Persistence.EfCore;
using NovaFE.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace NovaFE.IntegrationTests.Contingency;

/// <summary>
/// M11 Tipo 1: el monitor prende <c>platform.contingency_mode</c> solo cuando el
/// outbox de envío a la DGII lleva demasiado estancado, y avisa a los tenants
/// suscritos — <c>PUT /api/v1/platform-settings/platform.contingency_mode</c>
/// (manual) sigue siendo el otro camino, sin código nuevo que probar ahí.
/// </summary>
public sealed class ContingencyMonitorPumpTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string Rnc = "130445599";
    private static readonly string[] Phones = ["809-555-0100"];

    private Task<ContingencyTransition?> PumpAsync()
        => Factory.Services.GetRequiredService<IContingencyMonitorPump>().RunOnceAsync();

    private async Task IssueOneStuckSubmitAsync()
    {
        Reconfigure(new Dictionary<string, string?>
        {
            ["EcfSubmission:Enabled"] = "false",
            ["EcfSubmission:SyncWaitBudgetSeconds"] = "0",
        });

        var tenantId = await RegisterAndActAsTenantAsync(Rnc);

        (await Client.PutAsJsonAsync($"/api/v1/tenants/{tenantId}/emitter-profile", new
        {
            address = "Av. 27 de Febrero 100", municipality = "010100", province = "010000",
            phones = Phones, email = "f@x.do", economicActivity = "Comercio", defaultEnvironment = "Test",
        })).EnsureSuccessStatusCode();

        (await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: Rnc), TestPkcs12.DefaultPassword, "Test")))
            .EnsureSuccessStatusCode();

        (await Client.PostAsJsonAsync("/api/v1/sequences", new
        {
            environment = "Test", type = 31, series = "E", rangeFrom = 1, rangeTo = 100,
        })).EnsureSuccessStatusCode();

        (await Client.PostAsJsonAsync("/api/v1/ecf", new
        {
            type = 31,
            incomeType = "01",
            buyer = new { name = "Mi Cliente SRL", rnc = "131880681" },
            payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 2360m } } },
            lines = new[]
            {
                new { name = "Consultoria", kind = "service", quantity = 1, unitPrice = 2000m, itbisRate = 1, unitOfMeasure = "43" },
            },
        })).EnsureSuccessStatusCode();

        // La fila queda 'pending' (worker apagado). La antigüedad real es de
        // milisegundos — se adelanta a mano, mismo truco que WebhookDeliveryTests.
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE ecf_submission_outbox SET created_at = now() - interval '10 minutes' WHERE status = 'pending'");
    }

    private bool ContingencyModeIsActive()
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ISettingsReader>().GetValue(SettingDefinitions.ContingencyMode);
    }

    [RequiresDockerFact]
    public async Task Activates_and_notifies_a_subscribed_tenant_when_the_outbox_is_stuck()
    {
        using var receiver = new WireMockFixture();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        await IssueOneStuckSubmitAsync();

        (await Client.PostAsJsonAsync("/api/v1/webhooks", new { url = $"{receiver.BaseUrl}/hook", events = new[] { "contingency.*" } }))
            .EnsureSuccessStatusCode();

        var transition = await PumpAsync();

        transition.ShouldNotBeNull();
        transition.EventType.ShouldBe("contingency.activated");
        ContingencyModeIsActive().ShouldBeTrue();

        await EventuallyAsync(
            () => Task.FromResult(receiver.Server.LogEntries.Count >= 1),
            tick: () => Factory.Services.GetRequiredService<IWebhookDeliveryPump>().RunOnceAsync());

        receiver.Server.LogEntries.ShouldHaveSingleItem()
            .RequestMessage!.Headers!["X-NovaFE-Event"][0].ShouldBe("contingency.activated");
    }

    [RequiresDockerFact]
    public async Task A_healthy_outbox_never_activates_contingency()
    {
        Reconfigure(new Dictionary<string, string?>());

        var transition = await PumpAsync();

        transition.ShouldBeNull();
        ContingencyModeIsActive().ShouldBeFalse();
    }

    [RequiresDockerFact]
    public async Task Manual_activation_via_the_platform_settings_endpoint_still_works()
    {
        Reconfigure(new Dictionary<string, string?>());
        await RegisterAndActAsTenantAsync("130445600");

        var response = await Client.PutAsJsonAsync(
            "/api/v1/platform-settings/platform.contingency_mode", new { value = "true", confirm = true });

        response.EnsureSuccessStatusCode();
        ContingencyModeIsActive().ShouldBeTrue();
    }
}
