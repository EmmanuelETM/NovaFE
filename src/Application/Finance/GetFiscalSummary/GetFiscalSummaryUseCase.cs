using System.Globalization;
using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Finance.Contracts;
using NovaFE.Application.Finance.Interfaces;
using NovaFE.Domain.Common;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Finance.GetFiscalSummary;

/// <summary>
/// Resumen fiscal del tenant actual (lado ventas): total facturado, ITBIS por
/// tasa, retenciones, efecto neto de notas de crédito/débito, desglose por
/// tipo de e-CF. Ver docs/finance.md.
/// </summary>
public sealed class GetFiscalSummaryUseCase(
    ILoggerFactory loggerFactory,
    IValidator<GetFiscalSummaryQuery> validator,
    ICurrentTenant currentTenant,
    IFinanceReadRepository finance)
    : QueryUseCase<GetFiscalSummaryQuery, FiscalSummaryDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<FiscalSummaryDto>> ExecuteCore(
        GetFiscalSummaryQuery request, CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var totals = await finance.GetTotalsAsync(tenantId, request.From, request.To, ct);
        var byType = await finance.GetTotalsByTypeAsync(tenantId, request.From, request.To, ct);

        return new FiscalSummaryDto(
            request.From,
            request.To,
            totals.TotalCount,
            totals.TotalInvoiced,
            totals.TotalItbis,
            totals.TotalItbis1,
            totals.TotalItbis2,
            totals.TotalItbis3,
            totals.TotalExempt,
            totals.TotalItbisWithheld,
            totals.TotalIsrWithheld,
            totals.TotalCreditNoteAmount,
            totals.TotalDebitNoteAmount,
            NetCreditDebitEffect: totals.TotalDebitNoteAmount - totals.TotalCreditNoteAmount,
            ByType: [.. byType.Select(t => new FiscalSummaryByTypeDto(
                t.Type,
                EcfType.FromCodeOrDefault(t.Type)?.DisplayName ?? t.Type.ToString(CultureInfo.InvariantCulture),
                t.Count,
                t.TotalAmount,
                t.TotalItbis))]);
    }
}
