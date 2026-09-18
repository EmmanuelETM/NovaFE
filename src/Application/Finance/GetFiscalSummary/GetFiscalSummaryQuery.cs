namespace NovaFE.Application.Finance.GetFiscalSummary;

/// <summary>Resumen fiscal del tenant actual entre dos fechas (inclusive), por fecha de emisión.</summary>
public sealed record GetFiscalSummaryQuery(DateOnly From, DateOnly To);
