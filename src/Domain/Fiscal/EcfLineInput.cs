namespace NovaFE.Domain.Fiscal;

/// <summary>
/// Datos de una línea de detalle que necesita el motor de cálculo (RF-06). Es la
/// entrada mínima: cantidad, precio, ajustes de línea y cómo se declara el ITBIS.
/// </summary>
/// <param name="LineNumber"><c>&lt;NumeroLinea&gt;</c> — correlativo ≥ 1.</param>
/// <param name="Rate">Tasa de ITBIS de la línea (<c>&lt;IndicadorFacturacion&gt;</c>).</param>
/// <param name="Quantity"><c>&lt;CantidadItem&gt;</c> — ≥ 0.</param>
/// <param name="UnitPrice"><c>&lt;PrecioUnitarioItem&gt;</c> — ≥ 0, hasta 4 decimales.</param>
/// <param name="Discount"><c>&lt;DescuentoMonto&gt;</c> — suma de subdescuentos de la línea, ≥ 0.</param>
/// <param name="Surcharge"><c>&lt;RecargoMonto&gt;</c> — suma de subrecargos de la línea, ≥ 0.</param>
/// <param name="PriceIncludesTax">
/// <c>&lt;IndicadorMontoGravado&gt;</c>: <c>true</c> (= 1) si el precio ya trae el
/// ITBIS y hay que extraer la base; <c>false</c> (= 0) si el ITBIS va por encima.
/// </param>
/// <param name="AdditionalTaxes">
/// Suma de "otros impuestos adicionales" de la línea que el cliente ya trae
/// calculados (p. ej. Propina Legal, CDT). Se acumulan en
/// <c>&lt;MontoImpuestoAdicional&gt;</c> y en <c>&lt;MontoTotal&gt;</c>; <b>no</b>
/// entran a la base del ITBIS. El ISC de alcoholes y cigarrillos (que sí integra
/// la base) es un slice aparte.
/// </param>
/// <param name="SuppliedLineAmount">
/// <c>&lt;MontoItem&gt;</c> tal como lo calculó el sistema del cliente, si se
/// quiere el chequeo de tolerancia (RF-06.6). Null = no comparar.
/// </param>
/// <param name="ItbisWithheld">
/// <c>&lt;MontoITBISRetenido&gt;</c> de la línea, ya calculado por el cliente. Se
/// acumula en <c>&lt;TotalITBISRetenido&gt;</c>; no afecta a <c>&lt;MontoTotal&gt;</c>.
/// </param>
/// <param name="IsrWithheld">
/// <c>&lt;MontoISRRetenido&gt;</c> de la línea, ya calculado por el cliente. Se
/// acumula en <c>&lt;TotalISRRetencion&gt;</c>; no afecta a <c>&lt;MontoTotal&gt;</c>.
/// </param>
public sealed record EcfLineInput(
    int LineNumber,
    ItbisRate Rate,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount = 0m,
    decimal Surcharge = 0m,
    bool PriceIncludesTax = false,
    decimal AdditionalTaxes = 0m,
    decimal? SuppliedLineAmount = null,
    decimal ItbisWithheld = 0m,
    decimal IsrWithheld = 0m);
