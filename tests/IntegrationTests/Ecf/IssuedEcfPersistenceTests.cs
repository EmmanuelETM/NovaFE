using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.IntegrationTests.Fixtures;
using NovaFE.Service.Common;
using NovaFE.Service.DevTools;

namespace NovaFE.IntegrationTests.Ecf;

/// <summary>
/// El agregado <see cref="IssuedEcf"/> persiste y se relee (jsonb de totales,
/// value objects) y respeta el aislamiento por tenant.
/// </summary>
public sealed class IssuedEcfPersistenceTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private static SignedEcf Signed(EcfDocument document) => new(
        SignedAt: EcfSampleCatalog.SignedAt,
        EcfXml: $"<ECF><enc>{document.Header.Encf.Value}</enc><Signature/></ECF>",
        RfceXml: document.QualifiesForRfce ? "<RFCE><Signature/></RFCE>" : null,
        SignatureValue: "aB3xZ9KkLlMmNnOo",
        SecurityCode: "aB3xZ9",
        DocumentHash: new string('d', 64),
        QrUrl: "https://ecf.dgii.gov.do/testecf/consultatimbre?x=1");

    private async Task<(Guid TenantId, IssuedEcf Ecf)> PersistSampleAsync(string slug = "credito-fiscal")
    {
        var tenantId = await RegisterTenantAsync($"1{Random.Shared.NextInt64(10_000_000, 99_999_999)}");
        var document = EcfSampleCatalog.Find(slug)!.Document;
        var ecf = IssuedEcf.FromSigned(document, Signed(document), DgiiEnvironment.Test);

        await using var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);
        await scope.ServiceProvider.GetRequiredService<IEcfRepository>().AddAsync(ecf);

        return (tenantId, ecf);
    }

    [RequiresDockerFact]
    public async Task Round_trips_through_ef_and_dapper()
    {
        var (tenantId, ecf) = await PersistSampleAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);

        var reloaded = await scope.ServiceProvider.GetRequiredService<IEcfRepository>().GetByIdAsync(ecf.Id);
        reloaded.ShouldNotBeNull();
        reloaded.Encf.ShouldBe(ecf.Encf);
        reloaded.Totals.MontoTotal.ShouldBe(ecf.Totals.MontoTotal);
        reloaded.Status.ShouldBe(EcfStatus.Signed);

        var dto = await scope.ServiceProvider.GetRequiredService<IEcfReadRepository>().GetByIdAsync(ecf.Id, tenantId);
        dto.ShouldNotBeNull();
        dto.Status.ShouldBe("signed");
        dto.Encf.ShouldBe(ecf.Encf.Value);
        dto.Type.ShouldBe(31);
        dto.ToleranceWarning.ShouldBeNull();

        var xml = await scope.ServiceProvider.GetRequiredService<IEcfReadRepository>()
            .GetXmlAsync(ecf.Id, tenantId, rfce: false);
        xml.ShouldNotBeNull();
        xml.ShouldContain("<ECF>");
    }

    [RequiresDockerFact]
    public async Task Find_by_internal_number_and_list_are_tenant_scoped()
    {
        var (tenantA, ecfA) = await PersistSampleAsync();
        var (tenantB, _) = await PersistSampleAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var reads = scope.ServiceProvider.GetRequiredService<IEcfReadRepository>();

        var listA = await reads.ListAsync(tenantA, new());
        listA.TotalCount.ShouldBe(1);
        listA.Items.ShouldContain(i => i.Id == ecfA.Id);

        var listB = await reads.ListAsync(tenantB, new());
        listB.Items.ShouldNotContain(i => i.Id == ecfA.Id);

        (await reads.GetByIdAsync(ecfA.Id, tenantB)).ShouldBeNull();
    }

    [RequiresDockerFact]
    public async Task A_low_amount_consumo_keeps_the_signed_rfce()
    {
        var (tenantId, ecf) = await PersistSampleAsync("consumo");

        await using var scope = Factory.Services.CreateAsyncScope();
        var reads = scope.ServiceProvider.GetRequiredService<IEcfReadRepository>();

        var dto = await reads.GetByIdAsync(ecf.Id, tenantId);
        dto!.SubmitsRfce.ShouldBeTrue();

        var rfce = await reads.GetXmlAsync(ecf.Id, tenantId, rfce: true);
        rfce.ShouldNotBeNull();
        rfce.ShouldContain("<RFCE>");
    }
}
