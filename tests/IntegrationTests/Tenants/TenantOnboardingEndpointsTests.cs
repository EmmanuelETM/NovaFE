using System.Net;
using System.Net.Http.Json;
using NovaFE.IntegrationTests.Fixtures;

namespace NovaFE.IntegrationTests.Tenants;

/// <summary>
/// Los endpoints <c>...ForTenant</c> de <c>CertificatesController</c> y
/// <c>SequencesController</c>: resuelven el candado de onboarding — un
/// operador puede cargar certificado y registrar secuencia de un tenant que
/// todavía no tiene ninguna API key propia (no puede tenerla: para acuñar la
/// primera hace falta certificado + secuencia).
/// </summary>
public sealed class TenantOnboardingEndpointsTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    [RequiresDockerFact]
    public async Task Operator_uploads_certificate_and_registers_sequence_without_acting_as_the_tenant()
    {
        var tenantId = await RegisterTenantAsync("130862350");
        // Nunca se llama ActAs: el cliente sigue autenticado como operador.

        var upload = await Client.PostAsync(
            $"/api/v1/tenants/{tenantId}/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: "130862350"), TestPkcs12.DefaultPassword, "Test"));
        upload.StatusCode.ShouldBe(HttpStatusCode.Created);

        var register = await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/sequences",
            new { environment = "Test", type = 31, series = "E", rangeFrom = 1L, rangeTo = 100L });
        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        var certificates = await Client.GetAsync($"/api/v1/tenants/{tenantId}/certificates");
        certificates.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LeerAsync<CertificateSummary[]>(certificates))!.ShouldHaveSingleItem();

        var sequences = await Client.GetAsync($"/api/v1/tenants/{tenantId}/sequences");
        sequences.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LeerAsync<SequenceSummary[]>(sequences))!.ShouldHaveSingleItem();
    }

    [RequiresDockerFact]
    public async Task Minting_an_api_key_only_succeeds_after_the_operator_sets_up_certificate_and_sequence()
    {
        var tenantId = await RegisterTenantAsync("130862351");

        (await Client.PutAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/emitter-profile",
            new { address = "Calle Test 123", defaultEnvironment = "Test" }))
            .EnsureSuccessStatusCode();

        var tooSoon = await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/api-keys", new { role = "admin_tenant", environment = "Test" });
        tooSoon.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await Client.PostAsync(
            $"/api/v1/tenants/{tenantId}/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: "130862351"), TestPkcs12.DefaultPassword, "Test")))
            .EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/sequences",
            new { environment = "Test", type = 31, series = "E", rangeFrom = 1L, rangeTo = 100L }))
            .EnsureSuccessStatusCode();

        var ready = await Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/api-keys", new { role = "admin_tenant", environment = "Test" });
        ready.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private sealed record CertificateSummary(Guid Id);

    private sealed record SequenceSummary(Guid Id);
}
