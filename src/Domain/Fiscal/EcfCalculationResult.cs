namespace NovaFE.Domain.Fiscal;

/// <summary>
/// Salida completa del motor de cálculo: el resultado por línea, los
/// totalizadores del Encabezado y el análisis de tolerancia de cuadratura.
/// </summary>
public sealed record EcfCalculationResult(
    IReadOnlyList<EcfLineResult> Lines,
    EcfTotals Totals,
    EcfToleranceReport Tolerance);
