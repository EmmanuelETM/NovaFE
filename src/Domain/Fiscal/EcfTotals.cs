namespace NovaFE.Domain.Fiscal;

/// <summary>
/// Totalizadores del Encabezado del e-CF (Formato e-CF §A / RFCE campos 14-30).
/// Todos en escala de dinero (2 decimales).
/// <para>
/// Cuadratura (verificada contra el Formato e-CF v1.0, oct 2025 — no contra el
/// borrador del Plan Técnico, que incluía <c>MontoNoFacturable</c> dentro de
/// <c>MontoTotal</c> por error):
/// </para>
/// <code>
/// MontoGravadoTotal      = MontoGravadoI1 + MontoGravadoI2 + MontoGravadoI3
/// TotalItbis             = Itbis1 + Itbis2 + Itbis3
/// MontoImpuestoAdicional = TotalImpuestoSelectivoConsumo + TotalOtrosImpuestosAdicionales
/// MontoTotal             = MontoGravadoTotal + MontoExento + TotalItbis + MontoImpuestoAdicional
/// MontoPeriodo           = MontoTotal + MontoNoFacturable      (MontoNoFacturable puede ser negativo)
/// </code>
/// </summary>
/// <param name="MontoGravadoI1">Base gravada a 18 %.</param>
/// <param name="MontoGravadoI2">Base gravada a 16 %.</param>
/// <param name="MontoGravadoI3">Base gravada a 0 % (con crédito fiscal).</param>
/// <param name="MontoGravadoTotal">Suma de <see cref="MontoGravadoI1"/> + I2 + I3.</param>
/// <param name="Itbis1">ITBIS al 18 %.</param>
/// <param name="Itbis2">ITBIS al 16 %.</param>
/// <param name="Itbis3">ITBIS al 0 % (siempre 0; el campo existe por simetría).</param>
/// <param name="TotalItbis">Suma de <see cref="Itbis1"/> + I2 + I3.</param>
/// <param name="MontoExento">Total de líneas exentas (<c>IndicadorFacturacion = 4</c>).</param>
/// <param name="TotalImpuestoSelectivoConsumo">ISC de alcoholes y cigarrillos. Slice aparte; hoy 0.</param>
/// <param name="TotalOtrosImpuestosAdicionales">Propina Legal, CDT, ISC de servicios, Primera Placa…</param>
/// <param name="MontoImpuestoAdicional">Suma de ISC + otros impuestos adicionales.</param>
/// <param name="MontoTotal">Total del comprobante.</param>
/// <param name="MontoNoFacturable">Montos no facturables (reembolsos, propina voluntaria…). Puede ser negativo.</param>
/// <param name="MontoPeriodo"><see cref="MontoTotal"/> + <see cref="MontoNoFacturable"/>. Puede ser negativo.</param>
public sealed record EcfTotals(
    decimal MontoGravadoI1,
    decimal MontoGravadoI2,
    decimal MontoGravadoI3,
    decimal MontoGravadoTotal,
    decimal Itbis1,
    decimal Itbis2,
    decimal Itbis3,
    decimal TotalItbis,
    decimal MontoExento,
    decimal TotalImpuestoSelectivoConsumo,
    decimal TotalOtrosImpuestosAdicionales,
    decimal MontoImpuestoAdicional,
    decimal MontoTotal,
    decimal MontoNoFacturable,
    decimal MontoPeriodo);
