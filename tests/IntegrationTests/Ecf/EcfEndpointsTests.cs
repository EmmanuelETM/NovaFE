using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Ecf;

/// <summary>
/// <c>POST /api/v1/ecf</c> de punta a punta: asigna secuencia → arma → calcula →
/// firma (con un certificado real subido al vault) → persiste. Sin tocar la DGII.
/// </summary>
public sealed class EcfEndpointsTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string Rnc = "130862346";
    private static readonly int[] SequenceTypes = [31, 32];
    private static readonly string[] Phones = ["809-555-0100"];

    private async Task ArrangeTenantAsync()
    {
        var tenantId = await RegisterAndActAsTenantAsync(Rnc);

        (await Client.PutAsJsonAsync($"/api/v1/tenants/{tenantId}/emitter-profile", new
        {
            address = "Av. 27 de Febrero 100",
            municipality = "010100",  // Santo Domingo de Guzmán (Tabla III)
            province = "010000",      // Distrito Nacional
            phones = Phones,
            email = "facturacion@almax.do",
            economicActivity = "Comercio",
            defaultEnvironment = "TestEcf",
        })).EnsureSuccessStatusCode();

        (await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: Rnc), TestPkcs12.DefaultPassword, "TestEcf")))
            .EnsureSuccessStatusCode();

        foreach (var typeCode in SequenceTypes)
        {
            (await Client.PostAsJsonAsync("/api/v1/sequences", new
            {
                environment = "TestEcf",
                type = typeCode,
                series = "E",
                rangeFrom = 1,
                rangeTo = 100,
            })).EnsureSuccessStatusCode();
        }
    }

    private static object CreditoFiscal(string? internalNumber = null) => new
    {
        type = 31,
        incomeType = "01",
        internalNumber,
        buyer = new { name = "Mi Cliente SRL", rnc = "131880681" },
        payment = new
        {
            condition = "credit",
            dueDate = "15-03-2026",
            methods = new[] { new { type = "check_transfer", amount = 2360m } },
        },
        lines = new[] { new { name = "Consultoria", kind = "service", quantity = 1, unitPrice = 2000m, itbisRate = 1, unitOfMeasure = "43" } },
    };

    [RequiresDockerFact]
    public async Task Issues_a_credit_note_then_get_returns_it()
    {
        await ArrangeTenantAsync();

        var post = await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal());
        post.StatusCode.ShouldBe(HttpStatusCode.Created, await post.Content.ReadAsStringAsync());

        var issued = await LeerAsync<EcfResponse>(post);
        issued!.Status.ShouldBe("signed");
        issued.Encf.ShouldBe("E310000000001");
        issued.Totals.MontoTotal.ShouldBe(2360m);
        issued.QrUrl.ShouldContain("/testecf/consultatimbre?");
        issued.SubmitsRfce.ShouldBeFalse();

        var get = await Client.GetAsync($"/api/v1/ecf/{issued.Id}");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LeerAsync<EcfResponse>(get))!.Encf.ShouldBe("E310000000001");

        var xml = await (await Client.GetAsync($"/api/v1/ecf/{issued.Id}/xml")).Content.ReadAsStringAsync();
        xml.ShouldContain("<TipoeCF>31</TipoeCF>");
        xml.ShouldContain("<Signature");
    }

    [RequiresDockerFact]
    public async Task A_low_amount_consumo_is_issued_as_rfce()
    {
        await ArrangeTenantAsync();

        var post = await Client.PostAsJsonAsync("/api/v1/ecf", new
        {
            type = 32,
            incomeType = "01",
            payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 1180m } } },
            lines = new[] { new { name = "Almuerzo", kind = "good", quantity = 1, unitPrice = 1000m, itbisRate = 1, unitOfMeasure = "43" } },
        });

        post.StatusCode.ShouldBe(HttpStatusCode.Created);
        var issued = await LeerAsync<EcfResponse>(post);
        issued!.SubmitsRfce.ShouldBeTrue();
        issued.QrUrl.ShouldContain("fc.dgii.gov.do/testecf/consultatimbrefc");

        var rfce = await (await Client.GetAsync($"/api/v1/ecf/{issued.Id}/xml?rfce=true")).Content.ReadAsStringAsync();
        rfce.ShouldContain("<RFCE>");
    }

    [RequiresDockerFact]
    public async Task The_same_idempotency_key_replays_the_original()
    {
        await ArrangeTenantAsync();

        Client.DefaultRequestHeaders.Add("Idempotency-Key", "abc-123");

        var first = await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal());
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        var firstId = (await LeerAsync<EcfResponse>(first))!.Id;

        var second = await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal());
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LeerAsync<EcfResponse>(second))!.Id.ShouldBe(firstId);
    }

    [RequiresDockerFact]
    public async Task A_repeated_internal_number_returns_the_existing_ecf()
    {
        await ArrangeTenantAsync();

        var first = await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal(internalNumber: "FAC-2026-1"));
        var firstId = (await LeerAsync<EcfResponse>(first))!.Id;

        var second = await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal(internalNumber: "FAC-2026-1"));
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LeerAsync<EcfResponse>(second))!.Id.ShouldBe(firstId);
    }

    [RequiresDockerFact]
    public async Task Issuing_without_an_emitter_profile_is_a_400()
    {
        await RegisterAndActAsTenantAsync("131111111");

        var post = await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal());

        post.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task An_invalid_payload_is_a_400()
    {
        await ArrangeTenantAsync();

        var post = await Client.PostAsJsonAsync("/api/v1/ecf", new { type = 31, lines = Array.Empty<object>() });

        post.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task Specific_isc_on_a_line_raises_the_itbis_and_is_broken_out()
    {
        await ArrangeTenantAsync();

        var post = await Client.PostAsJsonAsync("/api/v1/ecf", new
        {
            type = 31,
            incomeType = "01",
            buyer = new { name = "Bar El Rincón SRL", rnc = "131880681" },
            payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 1928.12m } } },
            lines = new[]
            {
                new
                {
                    name = "Ron añejo 0.75L",
                    kind = "good",
                    quantity = 1,
                    unitPrice = 1000m,
                    itbisRate = 1,
                    unitOfMeasure = "43",
                    additionalTaxes = new[] { new { code = "014", rate = 10m, iscEspecifico = 634m } },
                },
            },
        });

        post.StatusCode.ShouldBe(HttpStatusCode.Created, await post.Content.ReadAsStringAsync());
        var totals = (await LeerAsync<EcfResponse>(post))!.Totals;

        totals.MontoGravadoI1.ShouldBe(1000m);                     // sin el ISC
        totals.TotalItbis.ShouldBe(294.12m);                       // (1000 + 634) * 0.18
        totals.TotalImpuestoSelectivoConsumo.ShouldBe(634m);
        totals.MontoTotal.ShouldBe(1928.12m);                      // 1000 + 294.12 + 634
    }

    [RequiresDockerFact]
    public async Task List_returns_the_tenant_issued_ecf()
    {
        await ArrangeTenantAsync();

        await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal());
        await Client.PostAsJsonAsync("/api/v1/ecf", CreditoFiscal());

        var page = await LeerAsync<PagedResponse<EcfSummaryResponse>>(await Client.GetAsync("/api/v1/ecf?pageSize=10"));

        page!.TotalCount.ShouldBe(2);
        page.Items.ShouldAllBe(e => e.Type == 31 && e.Status == "signed");
    }

    private sealed record EcfResponse(
        Guid Id, string Status, string Encf, int Type, bool SubmitsRfce, string QrUrl, TotalsResponse Totals);

    private sealed record TotalsResponse(
        decimal MontoTotal, decimal TotalItbis, decimal MontoGravadoI1, decimal TotalImpuestoSelectivoConsumo);

    private sealed record EcfSummaryResponse(Guid Id, string Status, string Encf, int Type, decimal MontoTotal);

    private sealed record PagedResponse<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);
}
