using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Application.Signing.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.IntegrationTests.Fixtures;
using NovaFE.Service.Common;
using NovaFE.Service.DevTools;

namespace NovaFE.IntegrationTests.Ecf;

/// <summary>
/// <see cref="IEcfSigner"/> de punta a punta: serializa un <c>EcfDocument</c> del
/// catálogo de ejemplos, lo firma con un certificado real subido al vault, y el
/// resultado verifica criptográficamente y valida contra el XSD oficial.
/// </summary>
public sealed class EcfSigningTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private const string Rnc = "130862346";

    [RequiresDockerFact]
    public async Task Signs_a_credit_note_and_the_result_verifies_and_is_xsd_valid()
    {
        var (signer, xmlSigner) = await ArrangeSignerAsync();

        var result = await signer.SignAsync(
            EcfSampleCatalog.Find("credito-fiscal")!.Document, DgiiEnvironment.TestEcf);

        result.IsError.ShouldBeFalse();
        var signed = result.Value;

        signed.EcfXml.ShouldContain("<Signature");
        signed.SecurityCode.Length.ShouldBe(6);
        signed.SecurityCode.ShouldBe(signed.SignatureValue[..6]);
        signed.DocumentHash.Length.ShouldBe(64);
        signed.SubmitsRfce.ShouldBeFalse();
        signed.RfceXml.ShouldBeNull();

        // El e-CF firmado ya pasó el XSD dentro del signer; acá confirmamos la firma.
        xmlSigner.Verify(signed.EcfXml).ShouldBeTrue();
    }

    [RequiresDockerFact]
    public async Task A_low_amount_consumo_yields_a_signed_rfce_bound_to_the_ecf()
    {
        var (signer, xmlSigner) = await ArrangeSignerAsync();

        var result = await signer.SignAsync(
            EcfSampleCatalog.Find("consumo")!.Document, DgiiEnvironment.TestEcf);

        result.IsError.ShouldBeFalse();
        var signed = result.Value;

        signed.SubmitsRfce.ShouldBeTrue();
        signed.RfceXml.ShouldNotBeNull();
        signed.RfceXml.ShouldContain($"<CodigoSeguridadeCF>{signed.SecurityCode}</CodigoSeguridadeCF>");
        signed.RfceXml.ShouldNotContain("<DetallesItems>");

        // Tanto el e-CF completo como el resumen quedan firmados y verifican.
        xmlSigner.Verify(signed.EcfXml).ShouldBeTrue();
        xmlSigner.Verify(signed.RfceXml).ShouldBeTrue();
    }

    [RequiresDockerFact]
    public async Task Signing_fails_when_the_tenant_has_no_certificate()
    {
        var tenantId = await RegisterTenantAsync("130000099");

        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);
        var signer = scope.ServiceProvider.GetRequiredService<IEcfSigner>();

        var result = await signer.SignAsync(
            EcfSampleCatalog.Find("credito-fiscal")!.Document, DgiiEnvironment.Production);

        result.FirstError.Code.ShouldBe("Certificate.NoActiveCertificate");
    }

    private async Task<(IEcfSigner Signer, IXmlSigner XmlSigner)> ArrangeSignerAsync()
    {
        var tenantId = await RegisterAndActAsTenantAsync(Rnc);

        (await Client.PostAsync("/api/v1.0/certificates",
            CertificateForm(TestPkcs12.Generate(holderIdentifier: Rnc), TestPkcs12.DefaultPassword, "TestEcf")))
            .EnsureSuccessStatusCode();

        var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);

        return (
            scope.ServiceProvider.GetRequiredService<IEcfSigner>(),
            scope.ServiceProvider.GetRequiredService<IXmlSigner>());
    }
}
