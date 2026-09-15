using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace NovaFE.IntegrationTests.Ecf;

/// <summary><c>GET /api/v1/ecf/{id}/trackids</c> (Módulo 10) de punta a punta.</summary>
public sealed class EcfTrackIdsEndpointTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string Rnc = "130445601";
    private static readonly string[] Phones = ["809-555-0100"];

    private const string SeedXml =
        "<SemillaModel><valor>0xabc123</valor><fecha>2026-08-30T10:00:00-04:00</fecha></SemillaModel>";

    // El caso de uso pide un token nuevo (Módulo 10, sin caché propia) —
    // el flujo de autenticación real de la DGII hay que stubearlo igual que
    // EcfSubmissionFlowTests, aunque esta prueba no envíe nada a la DGII.
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

    private async Task<Guid> IssueOneAsync(string? dgiiEcfBaseUrl = null)
    {
        var overrides = new Dictionary<string, string?>
        {
            ["EcfSubmission:Enabled"] = "false",
            ["EcfSubmission:SyncWaitBudgetSeconds"] = "0",
        };
        if (dgiiEcfBaseUrl is not null)
            overrides["Dgii:EcfBaseUrl"] = dgiiEcfBaseUrl;

        Reconfigure(overrides);

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

        var post = await Client.PostAsJsonAsync("/api/v1/ecf", new
        {
            type = 31,
            incomeType = "01",
            buyer = new { name = "Mi Cliente SRL", rnc = "131880681" },
            payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 2360m } } },
            lines = new[] { new { name = "Consultoria", kind = "service", quantity = 1, unitPrice = 2000m, itbisRate = 1, unitOfMeasure = "43" } },
        });
        post.EnsureSuccessStatusCode();

        return (await LeerAsync<IdResponse>(post))!.Id;
    }

    [RequiresDockerFact]
    public async Task Returns_every_trackid_the_dgii_has_for_the_comprobante()
    {
        using var dgii = new WireMockFixture();
        StubAuth(dgii.Server);
        dgii.Server
            .Given(Request.Create().WithPath("/testecf/consultatrackids/api/trackids/consulta").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new[]
            {
                new { trackId = "TRACK-1", estado = "Rechazado", fechaRecepcion = (string?)null },
                new { trackId = "TRACK-2", estado = "Aceptado", fechaRecepcion = (string?)null },
            }));

        var id = await IssueOneAsync(dgii.BaseUrl);

        var response = await Client.GetAsync($"/api/v1/ecf/{id}/trackids");
        response.EnsureSuccessStatusCode();

        var trackIds = await LeerAsync<List<TrackIdView>>(response);
        trackIds!.Count.ShouldBe(2);
        trackIds[0].TrackId.ShouldBe("TRACK-1");
        trackIds[1].Estado.ShouldBe("Aceptado");
    }

    private sealed record TrackIdView(string TrackId, string Estado, DateTimeOffset? FechaRecepcion);
}
