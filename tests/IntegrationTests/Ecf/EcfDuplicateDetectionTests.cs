using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Webhooks.Delivery;
using NovaFE.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace NovaFE.IntegrationTests.Ecf;

/// <summary>
/// Detección de e-CF duplicados por huella (comprador con RNC/cédula + tipo +
/// monto + fecha + ambiente). Exige RNC del comprador — el caso real que motiva
/// la regla: facturas de consumo bajo DOP 250,000 sin identificar al comprador
/// (p. ej. varias personas pagando el mismo monto en minutos) nunca deben
/// marcarse como duplicadas entre sí.
/// </summary>
public sealed class EcfDuplicateDetectionTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string Rnc = "130862346";
    private static readonly int[] SequenceTypes = [31, 32];
    private static readonly string[] Phones = ["809-555-0100"];

    private Task<int> WebhookPumpAsync()
        => Factory.Services.GetRequiredService<IWebhookDeliveryPump>().RunOnceAsync();

    private async Task ArrangeTenantAsync()
    {
        var tenantId = await RegisterAndActAsTenantAsync(Rnc);

        (await Client.PutAsJsonAsync($"/api/v1/tenants/{tenantId}/emitter-profile", new
        {
            address = "Av. 27 de Febrero 100",
            municipality = "010100",
            province = "010000",
            phones = Phones,
            email = "facturacion@almax.do",
            economicActivity = "Comercio",
            defaultEnvironment = "Test",
        })).EnsureSuccessStatusCode();

        (await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: Rnc), TestPkcs12.DefaultPassword, "Test")))
            .EnsureSuccessStatusCode();

        foreach (var typeCode in SequenceTypes)
        {
            (await Client.PostAsJsonAsync("/api/v1/sequences", new
            {
                environment = "Test", type = typeCode, series = "E", rangeFrom = 1, rangeTo = 100,
            })).EnsureSuccessStatusCode();
        }
    }

    private static object CreditoFiscal(string buyerRnc) => new
    {
        type = 31,
        incomeType = "01",
        buyer = new { name = "Mi Cliente SRL", rnc = buyerRnc },
        payment = new
        {
            condition = "credit",
            dueDate = "15-03-2026",
            methods = new[] { new { type = "check_transfer", amount = 2360m } },
        },
        lines = new[] { new { name = "Consultoria", kind = "service", quantity = 1, unitPrice = 2000m, itbisRate = 1, unitOfMeasure = "43" } },
    };

    /// <summary>Consumo sin identificar al comprador — no obligatorio bajo DOP 250,000.</summary>
    private static object ConsumoSinComprador() => new
    {
        type = 32,
        incomeType = "01",
        payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 1180m } } },
        lines = new[] { new { name = "Inscripción", kind = "service", quantity = 1, unitPrice = 1000m, itbisRate = 1, unitOfMeasure = "43" } },
    };

    private Task<HttpResponseMessage> SetModeAsync(string mode) =>
        Client.PutAsJsonAsync("/api/v1/platform-settings/ecf.duplicate_detection_mode", new { value = mode });

    [RequiresDockerFact]
    public async Task Off_by_default_never_blocks_a_repeated_buyer_and_amount()
    {
        await ArrangeTenantAsync();

        (await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal("131880681")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal("131880681")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [RequiresDockerFact]
    public async Task Blocking_mode_rejects_the_second_invoice_with_the_same_fingerprint()
    {
        await ArrangeTenantAsync();
        (await SetModeAsync("bloquear")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal("131880681")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal("131880681"));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.Content.ReadAsStringAsync()).ShouldContain("mismo comprobante");
    }

    [RequiresDockerFact]
    public async Task Blocking_mode_allows_a_different_buyer_with_the_same_amount()
    {
        await ArrangeTenantAsync();
        (await SetModeAsync("bloquear")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal("131880681")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal("101880682")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [RequiresDockerFact]
    public async Task Consumer_invoices_without_a_buyer_rnc_are_never_flagged_even_in_blocking_mode()
    {
        // El caso real que motiva la regla: varias facturas de consumo por el
        // mismo monto, en minutos, sin RNC (no es obligatorio bajo DOP 250,000) —
        // no hay huella confiable sin identificador del comprador.
        await ArrangeTenantAsync();
        (await SetModeAsync("bloquear")).StatusCode.ShouldBe(HttpStatusCode.OK);

        for (var i = 0; i < 5; i++)
        {
            (await Client.PostAsJsonAsync("/api/v1/ecf", ConsumoSinComprador()))
                .StatusCode.ShouldBe(HttpStatusCode.Created);
        }
    }

    [RequiresDockerFact]
    public async Task Observing_mode_issues_normally_and_delivers_a_duplicate_suspected_webhook()
    {
        await ArrangeTenantAsync();
        (await SetModeAsync("observar")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var receiver = new WireMockFixture();
        receiver.Server.Given(Request.Create().WithPath("/hook").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));
        string[] all = ["ecf.*"];
        (await Client.PostAsJsonAsync("/api/v1/webhooks", new { url = $"{receiver.BaseUrl}/hook", events = all }))
            .EnsureSuccessStatusCode();

        (await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal("131880681")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal("131880681"));
        second.StatusCode.ShouldBe(HttpStatusCode.Created);

        await EventuallyAsync(
            () => Task.FromResult(receiver.Server.LogEntries
                .Any(e => e.RequestMessage!.Headers!["X-NovaFE-Event"][0] == "ecf.duplicate_suspected")),
            tick: WebhookPumpAsync);
    }
}
