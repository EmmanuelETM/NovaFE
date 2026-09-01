using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Ecf.Submission;
using NovaFE.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace NovaFE.IntegrationTests.Ecf;

/// <summary>
/// El envío a la DGII de punta a punta contra un WireMock: <c>POST /ecf</c> firma,
/// encola e intenta resolver inline; el worker (vía <c>pump.RunOnceAsync</c>)
/// termina lo que quede pendiente.
/// </summary>
public sealed class EcfSubmissionFlowTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string Rnc = "130862346";
    private static readonly string[] Phones = ["809-555-0100"];

    private const string SeedXml =
        "<SemillaModel><valor>0xabc123</valor><fecha>2026-08-30T10:00:00-04:00</fecha></SemillaModel>";

    private static readonly DateTimeOffset ReceivedAt =
        new(2026, 8, 30, 10, 5, 0, TimeSpan.FromHours(-4));

    private static void StubAuth(WireMockServer dgii)
    {
        dgii.Given(Request.Create().WithPath("/testecf/autenticacion/api/autenticacion/semilla").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/xml").WithBody(SeedXml));
        dgii.Given(Request.Create().WithPath("/testecf/autenticacion/api/autenticacion/validarsemilla").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                token = "dgii-token",
                expira = DateTimeOffset.UtcNow.AddHours(1),
                expedido = DateTimeOffset.UtcNow,
            }));
    }

    private static void StubReception(WireMockServer dgii, string trackId = "TRACK-1")
        => dgii.Given(Request.Create().WithPath("/testecf/recepcion/api/facturaselectronicas").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { trackId, error = "", mensaje = "" }));

    private static void StubResult(WireMockServer dgii, int codigo, string estado)
        => dgii.Given(Request.Create().WithPath("/testecf/consultaresultado/api/consultas/estado").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                trackId = "TRACK-1",
                codigo,
                estado,
                secuenciaUtilizada = codigo != 2,
                fechaRecepcion = ReceivedAt,
                mensajes = Array.Empty<object>(),
            }));

    private async Task<Guid> ArrangeTenantAsync()
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
            defaultEnvironment = "TestEcf",
        })).EnsureSuccessStatusCode();

        (await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: Rnc), TestPkcs12.DefaultPassword, "TestEcf")))
            .EnsureSuccessStatusCode();

        (await Client.PostAsJsonAsync("/api/v1/sequences", new
        {
            environment = "TestEcf", type = 31, series = "E", rangeFrom = 1, rangeTo = 100,
        })).EnsureSuccessStatusCode();

        return tenantId;
    }

    private static object CreditoFiscal() => new
    {
        type = 31,
        incomeType = "01",
        buyer = new { name = "Mi Cliente SRL", rnc = "131880681" },
        payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 2360m } } },
        lines = new[] { new { name = "Consultoria", kind = "service", quantity = 1, unitPrice = 2000m, itbisRate = 1, unitOfMeasure = "43" } },
    };

    private Task<int> PumpAsync()
        => Factory.Services.GetRequiredService<IEcfSubmissionPump>().RunOnceAsync();

    [RequiresDockerFact]
    public async Task A_fast_dgii_resolves_the_comprobante_inside_the_post_response()
    {
        using var dgii = new WireMockFixture();
        StubAuth(dgii.Server);
        StubReception(dgii.Server);
        StubResult(dgii.Server, codigo: 4, estado: "Aceptado Condicional");

        Reconfigure(new Dictionary<string, string?>
        {
            ["Dgii:EcfBaseUrl"] = dgii.BaseUrl,
            ["EcfSubmission:Enabled"] = "false",
            ["EcfSubmission:SyncWaitBudgetSeconds"] = "8",
            ["EcfSubmission:InlinePollDelayMillis"] = "50",
        });
        await ArrangeTenantAsync();

        var post = await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal());
        post.StatusCode.ShouldBe(HttpStatusCode.Created, await post.Content.ReadAsStringAsync());

        var issued = await LeerAsync<EcfResponse>(post);
        issued!.Status.ShouldBe("accepted_conditional");
        issued.Dgii.ShouldNotBeNull();
        issued.Dgii.TrackId.ShouldBe("TRACK-1");
        issued.Dgii.StatusCode.ShouldBe(4);
        issued.Dgii.Status.ShouldBe("Aceptado Condicional");
        issued.Dgii.SubmittedAt.ShouldNotBeNull();
        issued.Dgii.ReceivedAt.ShouldBe(ReceivedAt);
        issued.Dgii.ProcessedAt.ShouldNotBeNull();
    }

    [RequiresDockerFact]
    public async Task A_slow_dgii_returns_submitted_and_the_worker_finishes_it()
    {
        using var dgii = new WireMockFixture();
        StubAuth(dgii.Server);
        StubReception(dgii.Server);
        StubResult(dgii.Server, codigo: 3, estado: "En Proceso");

        Reconfigure(new Dictionary<string, string?>
        {
            ["Dgii:EcfBaseUrl"] = dgii.BaseUrl,
            ["EcfSubmission:Enabled"] = "false",
            ["EcfSubmission:SyncWaitBudgetSeconds"] = "10",
            ["EcfSubmission:MaxInlinePolls"] = "2",
            ["EcfSubmission:InlinePollDelayMillis"] = "50",
            ["EcfSubmission:FirstPollDelaySeconds"] = "0",
        });
        await ArrangeTenantAsync();

        var post = await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal());
        var issued = await LeerAsync<EcfResponse>(post);
        issued!.Status.ShouldBe("submitted");

        // La DGII ya resolvió; el worker lo recoge.
        dgii.Server.ResetMappings();
        StubAuth(dgii.Server);
        StubResult(dgii.Server, codigo: 1, estado: "Aceptado");

        (await PumpAsync()).ShouldBe(1);

        var get = await LeerAsync<EcfResponse>(await Client.GetAsync($"/api/v1/ecf/{issued.Id}"));
        get!.Status.ShouldBe("accepted");
    }

    [RequiresDockerFact]
    public async Task A_rejection_is_recorded_with_its_messages()
    {
        using var dgii = new WireMockFixture();
        StubAuth(dgii.Server);
        StubReception(dgii.Server);
        StubResult(dgii.Server, codigo: 2, estado: "Rechazado");

        Reconfigure(new Dictionary<string, string?>
        {
            ["Dgii:EcfBaseUrl"] = dgii.BaseUrl,
            ["EcfSubmission:Enabled"] = "false",
            ["EcfSubmission:SyncWaitBudgetSeconds"] = "8",
            ["EcfSubmission:InlinePollDelayMillis"] = "50",
        });
        await ArrangeTenantAsync();

        var issued = await LeerAsync<EcfResponse>(await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal()));

        issued!.Status.ShouldBe("rejected");
    }

    [RequiresDockerFact]
    public async Task A_failed_submission_can_be_retried()
    {
        using var dgii = new WireMockFixture();
        StubAuth(dgii.Server);
        // El gateway acepta pero no devuelve TrackId → 'failed' sin reintentos.
        dgii.Server.Given(Request.Create().WithPath("/testecf/recepcion/api/facturaselectronicas").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                trackId = "", error = "XML_INVALIDO", mensaje = "No cumple el XSD",
            }));

        Reconfigure(new Dictionary<string, string?>
        {
            ["Dgii:EcfBaseUrl"] = dgii.BaseUrl,
            ["EcfSubmission:Enabled"] = "false",
            ["EcfSubmission:SyncWaitBudgetSeconds"] = "0",
            ["EcfSubmission:FirstPollDelaySeconds"] = "0",
        });
        await ArrangeTenantAsync();

        var issued = await LeerAsync<EcfResponse>(await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal()));
        issued!.Status.ShouldBe("signed");

        await PumpAsync();
        (await LeerAsync<EcfResponse>(await Client.GetAsync($"/api/v1/ecf/{issued.Id}")))!.Status.ShouldBe("failed");

        // Ahora la DGII responde y el operador reintenta.
        dgii.Server.ResetMappings();
        StubAuth(dgii.Server);
        StubReception(dgii.Server);
        StubResult(dgii.Server, codigo: 1, estado: "Aceptado");

        var retry = await Client.PostAsync($"/api/v1/ecf/{issued.Id}/retry", content: null);
        retry.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await LeerAsync<EcfResponse>(retry))!.Status.ShouldBe("signed");

        await PumpAsync();   // envío → submitted, poll +0s
        await PumpAsync();   // poll → accepted
        (await LeerAsync<EcfResponse>(await Client.GetAsync($"/api/v1/ecf/{issued.Id}")))!.Status.ShouldBe("accepted");
    }

    [RequiresDockerFact]
    public async Task Retrying_a_comprobante_that_is_not_failed_is_a_conflict()
    {
        Reconfigure(new Dictionary<string, string?>
        {
            ["EcfSubmission:Enabled"] = "false",
            ["EcfSubmission:SyncWaitBudgetSeconds"] = "0",
        });
        await ArrangeTenantAsync();

        var issued = await LeerAsync<EcfResponse>(await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal()));

        var retry = await Client.PostAsync($"/api/v1/ecf/{issued!.Id}/retry", content: null);
        retry.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private sealed record EcfResponse(Guid Id, string Status, string Encf, DgiiExchange? Dgii);

    private sealed record DgiiExchange(
        string? TrackId, string? Status, int? StatusCode, bool? SequenceUsed,
        DateTimeOffset? SubmittedAt, DateTimeOffset? ReceivedAt, DateTimeOffset? ProcessedAt);
}
