namespace NovaFE.Domain.Fiscal;

/// <summary>
/// Resultado del cálculo de una línea. <see cref="LineAmount"/> es
/// <c>&lt;MontoItem&gt;</c>; según la tasa, el monto cae en la base gravada o en
/// el exento, y lleva o no ITBIS.
/// </summary>
/// <param name="LineNumber">Correlativo de la línea.</param>
/// <param name="BillingIndicator"><c>&lt;IndicadorFacturacion&gt;</c> (1–4).</param>
/// <param name="LineAmount"><c>&lt;MontoItem&gt;</c> = redondeo₂(precio × cantidad − descuento + recargo).</param>
/// <param name="TaxableBase">Parte gravada de la línea (0 si es exenta).</param>
/// <param name="TaxAmount">ITBIS de la línea (0 para tasa 0 % y para exenta).</param>
/// <param name="ExemptAmount">Parte exenta de la línea (= <see cref="LineAmount"/> si la tasa es Exento, si no 0).</param>
/// <param name="AdditionalTaxes">Otros impuestos adicionales acumulados de la línea.</param>
public sealed record EcfLineResult(
    int LineNumber,
    int BillingIndicator,
    decimal LineAmount,
    decimal TaxableBase,
    decimal TaxAmount,
    decimal ExemptAmount,
    decimal AdditionalTaxes);
