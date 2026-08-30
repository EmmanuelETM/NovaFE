namespace NovaFE.Domain.Fiscal;

/// <summary>
/// Diferencia entre el <c>&lt;MontoItem&gt;</c> que calculó el motor y el que trajo
/// el cliente, para una línea.
/// </summary>
/// <param name="LineNumber">Correlativo de la línea.</param>
/// <param name="Calculated">MontoItem calculado por el motor.</param>
/// <param name="Supplied">MontoItem suministrado por el cliente.</param>
/// <param name="Difference">Valor absoluto de la diferencia.</param>
/// <param name="WithinLineTolerance">La diferencia de la línea es ≤ 1 (RF-06.6).</param>
public sealed record EcfLineToleranceDiff(
    int LineNumber,
    decimal Calculated,
    decimal Supplied,
    decimal Difference,
    bool WithinLineTolerance);

/// <summary>
/// Análisis de tolerancia de cuadratura (RF-06.6, corregido). La tolerancia por
/// línea es ±1 y la global es la <b>cantidad de líneas</b> del e-CF.
/// <para>
/// <b>Esto nunca es motivo de rechazo local.</b> Si se excede la tolerancia, la
/// DGII marca el e-CF como <i>aceptado condicional</i>; el sistema solo lo
/// anticipa para avisar al cliente y de todos modos envía el comprobante.
/// </para>
/// </summary>
/// <param name="LineDiffs">Diferencia por línea (solo las líneas con MontoItem suministrado).</param>
/// <param name="TotalDifference">Suma de las diferencias absolutas por línea.</param>
/// <param name="GlobalTolerance">Tolerancia global = cantidad de líneas del e-CF.</param>
/// <param name="WithinTolerance"><see cref="TotalDifference"/> ≤ <see cref="GlobalTolerance"/> y cada línea ≤ 1.</param>
public sealed record EcfToleranceReport(
    IReadOnlyList<EcfLineToleranceDiff> LineDiffs,
    decimal TotalDifference,
    int GlobalTolerance,
    bool WithinTolerance)
{
    /// <summary>Si la DGII devolvería "aceptado condicional" por cuadratura.</summary>
    public bool ExpectConditionalAcceptance => !WithinTolerance;
}
