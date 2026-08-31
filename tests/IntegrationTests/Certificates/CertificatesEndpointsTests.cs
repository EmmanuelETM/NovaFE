using System.Net;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.IntegrationTests.Fixtures;
using NovaFE.Service.Common;
using Microsoft.Extensions.DependencyInjection;

namespace NovaFE.IntegrationTests.Certificates;

public sealed class CertificatesEndpointsTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    [RequiresDockerFact]
    public async Task Upload_then_get_returns_the_certificate_metadata()
    {
        const string rnc = "130862346";
        await RegisterAndActAsTenantAsync(rnc);

        var upload = await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: rnc), TestPkcs12.DefaultPassword, "TestEcf"));

        upload.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await LeerAsync<IdResponse>(upload))!.Id;

        var get = await Client.GetAsync($"/api/v1/certificates/{id}");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);

        var certificate = await LeerAsync<CertificateResponse>(get);
        certificate!.HolderIdentifier.ShouldBe(rnc);
        certificate.Environment.ShouldBe("TestEcf");
        certificate.Status.ShouldBe("Active");
        certificate.Thumbprint.ShouldNotBeNullOrWhiteSpace();
    }

    [RequiresDockerFact]
    public async Task Upload_rejects_a_certificate_whose_holder_is_not_the_tenant_rnc()
    {
        await RegisterAndActAsTenantAsync("130000001");

        var response = await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: "999999999"), TestPkcs12.DefaultPassword, "TestEcf"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [RequiresDockerFact]
    public async Task Upload_rejects_a_second_active_certificate_for_the_same_environment()
    {
        const string rnc = "130000002";
        await RegisterAndActAsTenantAsync(rnc);

        (await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: rnc), TestPkcs12.DefaultPassword, "TestEcf")))
            .EnsureSuccessStatusCode();

        var second = await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: rnc), TestPkcs12.DefaultPassword, "TestEcf"));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [RequiresDockerFact]
    public async Task Revoke_then_upload_a_replacement_for_the_same_environment_succeeds()
    {
        const string rnc = "130000003";
        await RegisterAndActAsTenantAsync(rnc);

        var first = await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: rnc), TestPkcs12.DefaultPassword, "TestEcf"));
        first.EnsureSuccessStatusCode();
        var firstId = (await LeerAsync<IdResponse>(first))!.Id;

        var revoke = await Client.PostAsync($"/api/v1/certificates/{firstId}/revoke", content: null);
        revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var replacement = await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: rnc), TestPkcs12.DefaultPassword, "TestEcf"));

        replacement.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [RequiresDockerFact]
    public async Task Certificates_are_isolated_between_tenants()
    {
        var tenantA = await RegisterTenantAsync("130000010");
        var tenantB = await RegisterTenantAsync("130000011");

        ActAs(tenantA);
        var upload = await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: "130000010"), TestPkcs12.DefaultPassword, "TestEcf"));
        upload.EnsureSuccessStatusCode();
        var certificateId = (await LeerAsync<IdResponse>(upload))!.Id;

        ActAs(tenantB);

        var listB = await Client.GetAsync("/api/v1/certificates");
        listB.EnsureSuccessStatusCode();
        (await LeerAsync<CertificateResponse[]>(listB))!.ShouldBeEmpty();

        var getB = await Client.GetAsync($"/api/v1/certificates/{certificateId}");
        getB.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [RequiresDockerFact]
    public async Task Uploaded_pkcs12_round_trips_through_the_vault()
    {
        var tenantId = await RegisterTenantAsync("130111333");
        var original = TestPkcs12.Generate(holderIdentifier: "130111333");

        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);
        var vault = scope.ServiceProvider.GetRequiredService<ICertificateVault>();

        var reference = await vault.StoreAsync(original, "pw-abc");

        using var secret = await vault.RetrieveAsync(reference);
        secret.Pkcs12.ShouldBe(original);
        secret.Password.ShouldBe("pw-abc");
    }

    [RequiresDockerFact]
    public async Task Certificate_endpoints_require_a_tenant_header()
    {
        var response = await Client.GetAsync("/api/v1/certificates");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed record CertificateResponse(
        Guid Id, string Environment, string HolderIdentifier, string Subject, string Issuer,
        string Thumbprint, DateTimeOffset ValidFrom, DateTimeOffset ValidTo, string Status,
        DateTimeOffset? RevokedAt, DateTimeOffset CreatedAt);
}
