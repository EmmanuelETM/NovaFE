using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Finance.GetFiscalSummary;
using NovaFE.Application.Finance.Interfaces;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Finance;

public class GetFiscalSummaryUseCaseTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 1, 31);

    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IFinanceReadRepository _finance = Substitute.For<IFinanceReadRepository>();

    public GetFiscalSummaryUseCaseTests()
    {
        _currentTenant.TenantId.Returns(TenantId);
    }

    private GetFiscalSummaryUseCase Sut() =>
        new(LoggerFactory, new GetFiscalSummaryQueryValidator(), _currentTenant, _finance);

    [Fact]
    public async Task Assembles_the_summary_from_the_two_repository_calls()
    {
        _finance.GetTotalsAsync(TenantId, From, To, Arg.Any<CancellationToken>())
            .Returns(new FiscalTotalsLookup(
                TotalCount: 10,
                TotalInvoiced: 100_000m,
                TotalItbis: 18_000m,
                TotalItbis1: 18_000m,
                TotalItbis2: 0m,
                TotalItbis3: 0m,
                TotalExempt: 0m,
                TotalItbisWithheld: 1_000m,
                TotalIsrWithheld: 500m,
                TotalCreditNoteAmount: 3_000m,
                TotalDebitNoteAmount: 1_000m));

        _finance.GetTotalsByTypeAsync(TenantId, From, To, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FiscalTypeTotalsLookup>)
            [
                new FiscalTypeTotalsLookup(31, 8, 90_000m, 16_200m),
                new FiscalTypeTotalsLookup(34, 2, 3_000m, 540m),
            ]);

        var result = await Sut().Execute(new GetFiscalSummaryQuery(From, To));

        result.IsError.ShouldBeFalse();
        var summary = result.Value;
        summary.From.ShouldBe(From);
        summary.To.ShouldBe(To);
        summary.TotalCount.ShouldBe(10);
        summary.TotalInvoiced.ShouldBe(100_000m);
        summary.NetCreditDebitEffect.ShouldBe(-2_000m); // 1,000 (débito) - 3,000 (crédito)
        summary.ByType.Count.ShouldBe(2);
        summary.ByType[0].Type.ShouldBe(31);
        summary.ByType[0].TypeName.ShouldBe("Factura de Crédito Fiscal Electrónica");
        summary.ByType[1].TypeName.ShouldBe("Nota de Crédito Electrónica");
    }

    [Fact]
    public async Task Rejects_a_range_where_the_end_is_before_the_start()
    {
        var result = await Sut().Execute(new GetFiscalSummaryQuery(To, From));

        result.IsError.ShouldBeTrue();
        await _finance.DidNotReceive().GetTotalsAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_when_the_tenant_cannot_be_resolved()
    {
        _currentTenant.TenantId.Returns((Guid?)null);

        var result = await Sut().Execute(new GetFiscalSummaryQuery(From, To));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Auth.TenantNotResolved");
    }
}
