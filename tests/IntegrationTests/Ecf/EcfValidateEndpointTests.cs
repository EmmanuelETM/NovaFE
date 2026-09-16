using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Ecf;

/// <summary>
/// <c>POST /api/v1/ecf/validate</c>: corre la matriz de validación real sin
/// emitir nada — ni certificado ni secuencia registrada hacen falta.
/// </summary>
public sealed class EcfValidateEndpointTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string Rnc = "130445602";
    private static readonly string[] Phones = ["809-555-0100"];

    private async Task ArrangeTenantWithProfileOnlyAsync()
    {
        var tenantId = await RegisterAndActAsTenantAsync(Rnc);

        (await Client.PutAsJsonAsync($"/api/v1/tenants/{tenantId}/emitter-profile", new
        {
            address = "Av. 27 de Febrero 100", municipality = "010100", province = "010000",
            phones = Phones, email = "f@x.do", economicActivity = "Comercio", defaultEnvironment = "Test",
        })).EnsureSuccessStatusCode();

        // A propósito: sin certificado, sin secuencia registrada — validar no
        // debería necesitar ninguno de los dos.
    }

    [RequiresDockerFact]
    public async Task A_valid_payload_returns_the_calculated_totals_and_persists_nothing()
    {
        await ArrangeTenantWithProfileOnlyAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/ecf/validate", new
        {
            type = 31,
            issueDate = "10-01-2026",
            incomeType = "01",
            buyer = new { name = "Mi Cliente SRL", rnc = "131880681" },
            payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 2360m } } },
            lines = new[] { new { name = "Consultoria", kind = "service", quantity = 1, unitPrice = 2000m, itbisRate = 1, unitOfMeasure = "43" } },
        });
        response.EnsureSuccessStatusCode();

        var result = await LeerAsync<ValidateResultView>(response);
        result!.MontoTotal.ShouldBe(2360m);
        result.TotalItbis.ShouldBe(360m);
        result.SampleEncf.ShouldStartWith("E31");
        result.SequenceExpiresOnEstimate.ShouldBe(new DateOnly(2027, 12, 31));

        var list = await LeerAsync<ListView>(await Client.GetAsync("/api/v1/ecf"));
        list!.TotalCount.ShouldBe(0);
    }

    [RequiresDockerFact]
    public async Task A_malformed_type_is_a_400_before_touching_the_tenant()
    {
        await ArrangeTenantWithProfileOnlyAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/ecf/validate", new { type = 99 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task A_credito_fiscal_without_a_buyer_rnc_fails_the_structural_matrix()
    {
        await ArrangeTenantWithProfileOnlyAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/ecf/validate", new
        {
            type = 31,
            incomeType = "01",
            payment = new { condition = "cash", methods = new[] { new { type = "cash", amount = 2360m } } },
            lines = new[] { new { name = "Consultoria", kind = "service", quantity = 1, unitPrice = 2000m, itbisRate = 1, unitOfMeasure = "43" } },
        });

        ((int)response.StatusCode).ShouldBeGreaterThanOrEqualTo(400);

        var list = await LeerAsync<ListView>(await Client.GetAsync("/api/v1/ecf"));
        list!.TotalCount.ShouldBe(0);
    }

    private sealed record ValidateResultView(
        int Type, string SampleEncf, decimal MontoTotal, decimal TotalItbis, DateOnly? SequenceExpiresOnEstimate);

    private sealed record ListView(int TotalCount);
}
