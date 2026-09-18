using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.IntegrationTests.Fixtures;
using NovaFE.Service.Common;
using NovaFE.Service.DevTools;

namespace NovaFE.IntegrationTests.Finance;

/// <summary>
/// <c>GET /api/v1/finance/summary</c> — resumen fiscal del lado ventas.
/// Persiste comprobantes directo por repositorio (mismo atajo que
/// <c>IssuedEcfPersistenceTests</c>): no hace falta certificado ni secuencia
/// real para probar la agregación, solo un <see cref="IssuedEcf"/> firmado.
/// </summary>
public sealed class GetFiscalSummaryEndpointTests(DatabaseFixture database) : IntegrationTestBase(database)
{
    private static readonly DateOnly IssueDate = EcfSampleCatalog.Find("credito-fiscal")!.Document.Header.IssueDate;

    private static SignedEcf Signed(EcfDocument document) => new(
        SignedAt: EcfSampleCatalog.SignedAt,
        EcfXml: $"<ECF><enc>{document.Header.Encf.Value}</enc><Signature/></ECF>",
        RfceXml: document.QualifiesForRfce ? "<RFCE><Signature/></RFCE>" : null,
        SignatureValue: "aB3xZ9KkLlMmNnOo",
        SecurityCode: "aB3xZ9",
        DocumentHash: new string('d', 64),
        QrUrl: "https://ecf.dgii.gov.do/testecf/consultatimbre?x=1");

    private async Task<IssuedEcf> IssueAsync(Guid tenantId, string slug)
    {
        var document = EcfSampleCatalog.Find(slug)!.Document;
        var ecf = IssuedEcf.FromSigned(document, Signed(document), DgiiEnvironment.Test);

        await using var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(tenantId);
        await scope.ServiceProvider.GetRequiredService<IEcfRepository>().AddAsync(ecf);

        return ecf;
    }

    [RequiresDockerFact]
    public async Task Sums_the_invoiced_amount_and_itbis_across_types()
    {
        var tenantId = await RegisterTenantAsync($"1{Random.Shared.NextInt64(10_000_000, 99_999_999)}");
        var creditoFiscal = await IssueAsync(tenantId, "credito-fiscal");
        var notaCredito = await IssueAsync(tenantId, "nota-credito");

        ActAs(tenantId);
        var response = await Client.GetAsync(
            $"/api/v1/finance/summary?from={IssueDate:yyyy-MM-dd}&to={IssueDate:yyyy-MM-dd}");
        response.EnsureSuccessStatusCode();

        var summary = await LeerAsync<SummaryResponse>(response);
        summary!.TotalCount.ShouldBe(2);
        summary.TotalInvoiced.ShouldBe(creditoFiscal.Totals.MontoTotal + notaCredito.Totals.MontoTotal);
        summary.TotalItbis.ShouldBe(creditoFiscal.Totals.TotalItbis + notaCredito.Totals.TotalItbis);
        summary.TotalCreditNoteAmount.ShouldBe(notaCredito.Totals.MontoTotal);
        summary.TotalDebitNoteAmount.ShouldBe(0m);
        summary.NetCreditDebitEffect.ShouldBe(-notaCredito.Totals.MontoTotal);

        var byType = summary.ByType.ToDictionary(t => t.Type);
        byType[31].Count.ShouldBe(1);
        byType[31].TotalAmount.ShouldBe(creditoFiscal.Totals.MontoTotal);
        byType[34].Count.ShouldBe(1);
        byType[34].TotalAmount.ShouldBe(notaCredito.Totals.MontoTotal);
    }

    [RequiresDockerFact]
    public async Task Excludes_ecf_outside_the_requested_range()
    {
        var tenantId = await RegisterTenantAsync($"1{Random.Shared.NextInt64(10_000_000, 99_999_999)}");
        await IssueAsync(tenantId, "credito-fiscal");

        ActAs(tenantId);
        var outsideRange = IssueDate.AddYears(-1);
        var response = await Client.GetAsync(
            $"/api/v1/finance/summary?from={outsideRange:yyyy-MM-dd}&to={outsideRange:yyyy-MM-dd}");
        response.EnsureSuccessStatusCode();

        var summary = await LeerAsync<SummaryResponse>(response);
        summary!.TotalCount.ShouldBe(0);
        summary.TotalInvoiced.ShouldBe(0m);
        summary.ByType.ShouldBeEmpty();
    }

    [RequiresDockerFact]
    public async Task Rejects_an_end_date_before_the_start_date()
    {
        var tenantId = await RegisterTenantAsync($"1{Random.Shared.NextInt64(10_000_000, 99_999_999)}");

        ActAs(tenantId);
        var response = await Client.GetAsync(
            $"/api/v1/finance/summary?from={IssueDate:yyyy-MM-dd}&to={IssueDate.AddDays(-1):yyyy-MM-dd}");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    private sealed record SummaryResponse(
        DateOnly From,
        DateOnly To,
        int TotalCount,
        decimal TotalInvoiced,
        decimal TotalItbis,
        decimal TotalItbis1,
        decimal TotalItbis2,
        decimal TotalItbis3,
        decimal TotalExempt,
        decimal TotalItbisWithheld,
        decimal TotalIsrWithheld,
        decimal TotalCreditNoteAmount,
        decimal TotalDebitNoteAmount,
        decimal NetCreditDebitEffect,
        IReadOnlyList<ByTypeResponse> ByType);

    private sealed record ByTypeResponse(int Type, string TypeName, int Count, decimal TotalAmount, decimal TotalItbis);
}
