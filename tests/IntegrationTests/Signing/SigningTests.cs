using NovaFE.Application.Signing.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.IntegrationTests.Fixtures;
using NovaFE.Service.Common;
using Microsoft.Extensions.DependencyInjection;

namespace NovaFE.IntegrationTests.Signing;

public sealed class SigningTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string SampleEcf =
        "<ECF xmlns=\"https://dgii.gov.do/etecf\"><Encabezado><Version>1.0</Version></Encabezado></ECF>";

    [RequiresDockerFact]
    public async Task Signs_with_the_uploaded_certificate_and_the_result_verifies()
    {
        const string rnc = "130862346";
        var tenantId = await RegisterAndActAsTenantAsync(rnc);

        (await Client.PostAsync("/api/v1/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: rnc), TestPkcs12.DefaultPassword, "Test")))
            .EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);
        var signer = scope.ServiceProvider.GetRequiredService<ICertificateSigner>();
        var xmlSigner = scope.ServiceProvider.GetRequiredService<IXmlSigner>();

        var result = await signer.SignAsync(SampleEcf, DgiiEnvironment.Test);

        result.IsError.ShouldBeFalse();
        result.Value.SecurityCode.Length.ShouldBe(6);
        result.Value.Xml.ShouldContain("<Signature");
        xmlSigner.Verify(result.Value.Xml).ShouldBeTrue();
    }

    [RequiresDockerFact]
    public async Task Signing_fails_when_the_tenant_has_no_certificate_for_the_environment()
    {
        var tenantId = await RegisterTenantAsync("130000099");

        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);
        var signer = scope.ServiceProvider.GetRequiredService<ICertificateSigner>();

        var result = await signer.SignAsync(SampleEcf, DgiiEnvironment.Production);

        result.FirstError.Code.ShouldBe("Certificate.NoActiveCertificate");
    }
}
