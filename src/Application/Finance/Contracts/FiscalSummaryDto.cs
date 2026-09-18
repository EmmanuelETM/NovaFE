namespace NovaFE.Application.Finance.Contracts;

/// <summary>
/// Resumen fiscal del lado ventas (lo que el contribuyente <b>emitió</b>) para un
/// rango de fechas — visibilidad interna, no un formato de envío de la DGII
/// (606/607/608): ese mapeo exacto queda para cuando se consiga el spec real.
/// Un e-CF <c>rejected</c> no cuenta en ningún total: nunca quedó facturado
/// ante la DGII.
/// </summary>
public sealed record FiscalSummaryDto(
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
    IReadOnlyList<FiscalSummaryByTypeDto> ByType);

/// <summary>Totales de un tipo de e-CF puntual dentro del rango.</summary>
public sealed record FiscalSummaryByTypeDto(
    int Type,
    string TypeName,
    int Count,
    decimal TotalAmount,
    decimal TotalItbis);
