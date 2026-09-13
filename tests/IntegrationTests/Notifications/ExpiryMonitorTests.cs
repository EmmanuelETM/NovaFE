using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Notifications;
using NovaFE.Application.Webhooks.Delivery;
using NovaFE.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace NovaFE.IntegrationTests.Notifications;

public sealed class ExpiryMonitorTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private static readonly string[] Phones = ["809-555-0100"];

    private Task<int> MonitorPumpAsync()
        => Factory.Services.GetRequiredService<IExpiryMonitorPump>().RunOnceAsync();

    private Task<int> WebhookPumpAsync()
        => Factory.Services.GetRequiredService<IWebhookDeliveryPump>().RunOnceAsync();

    private async Task ArrangeAsync(string rnc, WireMockFixture receiver)
    {
        var tenantId = await RegisterAndActAsTenantAsync(rnc);

        (await Client.PutAsJsonAsync($"/api/v1/tenants/{tenantId}/emitter-profile", new
        {
            address = "Av. 27 de Febrero 100", municipality = "010100", province = "010000",
            phones = Phones, email = "f@x.do", economicActivity = "Comercio", defaultEnvironment = "Test",
        })).EnsureSuccessStatusCode();

        // Certificado que vence en 20 días → cruza los umbrales 90 y 30.
        (await Client.PostAsync("/api/v1/certificates", CertificateForm(
            TestPkcs12.Generate(holderIdentifier: rnc, notAfter: DateTimeOffset.UtcNow.AddDays(20)),
            TestPkcs12.DefaultPassword, "Test"))).EnsureSuccessStatusCode();

        // Rango autorizado en 2023 → ExpiresOn 2024-12-31, ya vencido.
        (await Client.PostAsJsonAsync("/api/v1/sequences", new
        {
            environment = "Test", type = 31, series = "E", rangeFrom = 1, rangeTo = 100,
            authorizedOn = "2023-01-01",
        })).EnsureSuccessStatusCode();

        string[] all = ["certificate.*", "sequence.*"];
        (await Client.PostAsJsonAsync("/api/v1/webhooks", new { url = $"{receiver.BaseUrl}/hook", events = all }))
            .EnsureSuccessStatusCode();
    }

    [RequiresDockerFact]
    public async Task Emits_certificate_expiring_and_sequence_expired_then_dedups()
    {
        using var receiver = new WireMockFixture();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        await ArrangeAsync("130445566", receiver);

        await MonitorPumpAsync();
        await EventuallyAsync(
            () => Task.FromResult(receiver.Server.LogEntries.Count >= 2),
            tick: WebhookPumpAsync);

        var types = receiver.Server.LogEntries
            .Select(e => e.RequestMessage!.Headers!["X-NovaFE-Event"][0])
            .ToList();

        types.ShouldContain("certificate.expiring");
        types.ShouldContain("sequence.expired");

        // Segunda pasada: los avisos ya se registraron → nada nuevo que entregar.
        receiver.Server.Reset();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        await MonitorPumpAsync();
        await WebhookPumpAsync();
        receiver.Server.LogEntries.ShouldBeEmpty();
    }

    [RequiresDockerFact]
    public async Task A_custom_threshold_changes_what_gets_reported_as_expiring()
    {
        using var receiver = new WireMockFixture();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        await ArrangeAsync("130445567", receiver);

        // El certificado vence en 20 días: con el único umbral configurado en 5,
        // ya no lo cruza (el default 90/30/15/7 sí lo cruza — ver el otro test).
        (await Client.PutAsJsonAsync(
            "/api/v1/platform-settings/notifications.certificate_expiry_thresholds_days", new { value = "5" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        await MonitorPumpAsync();
        // Drena el outbox por completo (a lo sumo un par de eventos posibles
        // acá) — no alcanza con esperar "al menos 1": lo que se prueba es que
        // el segundo NO aparece, así que hay que agotar la cola primero.
        for (var i = 0; i < 5; i++)
            await WebhookPumpAsync();

        var types = receiver.Server.LogEntries
            .Select(e => e.RequestMessage!.Headers!["X-NovaFE-Event"][0])
            .ToList();

        types.ShouldContain("sequence.expired");
        types.ShouldNotContain("certificate.expiring");
    }
}
