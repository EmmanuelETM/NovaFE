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

        (await MonitorPumpAsync()).ShouldBe(1);
        await WebhookPumpAsync();

        var types = receiver.Server.LogEntries
            .Select(e => e.RequestMessage!.Headers!["X-NovaFE-Event"][0])
            .ToList();

        types.ShouldContain("certificate.expiring");
        types.ShouldContain("sequence.expired");

        // Segunda pasada: nada nuevo que entregar.
        receiver.Server.Reset();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        await MonitorPumpAsync();
        (await WebhookPumpAsync()).ShouldBe(0);
        receiver.Server.LogEntries.ShouldBeEmpty();
    }
}
