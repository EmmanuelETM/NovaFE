namespace NovaFE.Application.Finance.Interfaces;

/// <summary>Read side (Dapper) del resumen fiscal — agregación sobre <c>issued_ecf</c>.</summary>
public interface IFinanceReadRepository
{
    /// <summary>Totales del rango, sin desglosar por tipo de e-CF.</summary>
    Task<FiscalTotalsLookup> GetTotalsAsync(
        Guid tenantId, DateOnly from, DateOnly through, CancellationToken ct = default);

    /// <summary>Conteo y montos por tipo de e-CF, dentro del mismo rango.</summary>
    Task<IReadOnlyList<FiscalTypeTotalsLookup>> GetTotalsByTypeAsync(
        Guid tenantId, DateOnly from, DateOnly through, CancellationToken ct = default);
}

/// <summary>Totales agregados del rango. Un e-CF <c>rejected</c> no se cuenta.</summary>
public sealed record FiscalTotalsLookup(
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
    decimal TotalDebitNoteAmount);

/// <summary>Totales de un tipo de e-CF puntual dentro del rango.</summary>
public sealed record FiscalTypeTotalsLookup(int Type, int Count, decimal TotalAmount, decimal TotalItbis);
