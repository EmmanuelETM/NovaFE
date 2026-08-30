namespace NovaFE.Domain.Fiscal;

/// <summary>
/// Descuento o recargo <b>global</b> (Sección D del Formato, <c>&lt;DescuentoORecargo&gt;</c>)
/// para el motor de cálculo. Solo los datos que afectan a los totalizadores; los
/// campos de descripción/moneda van en el modelo del dominio.
/// <para>
/// El motor lo aplica <b>mecánicamente</b> al bucket que indica
/// <see cref="AffectsRate"/>: un descuento resta, un recargo suma, y si el bucket
/// es gravado se recalcula su ITBIS sobre la nueva base. La excepción es la
/// <b>Norma 10-07</b> (solo descuentos a la tasa 1 / 18 %): no rebaja la base ni el
/// ITBIS, solo el valor a pagar (Formato notas 12 y 27).
/// </para>
/// </summary>
/// <param name="IsDiscount"><c>true</c> = <c>&lt;TipoAjuste&gt;D&lt;/TipoAjuste&gt;</c> (descuento); <c>false</c> = recargo.</param>
/// <param name="AffectsRate">
/// <c>&lt;IndicadorFacturacionDescuentooRecargo&gt;</c> — a qué bucket afecta
/// (1 = 18 %, 2 = 16 %, 3 = 0 %, 4 = exento).
/// </param>
/// <param name="Amount"><c>&lt;MontoDescuentooRecargo&gt;</c> — ≥ 0.</param>
/// <param name="Norma1007"><c>&lt;IndicadorNorma1007&gt;</c> = 1. Solo aplica a descuentos sobre la tasa 1.</param>
public sealed record EcfGlobalAdjustmentInput(
    bool IsDiscount,
    ItbisRate AffectsRate,
    decimal Amount,
    bool Norma1007 = false);
