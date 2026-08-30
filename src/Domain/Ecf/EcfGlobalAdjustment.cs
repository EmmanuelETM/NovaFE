using NovaFE.Domain.Common;
using NovaFE.Domain.Fiscal;

namespace NovaFE.Domain.Ecf;

/// <summary><c>&lt;TipoAjuste&gt;</c> de la Sección D: <b>D</b> = descuento, <b>R</b> = recargo.</summary>
public sealed record AdjustmentKind(int Id, string Name, string Code) : Enumeration<AdjustmentKind>(Id, Name)
{
    public static readonly AdjustmentKind Discount = new(1, nameof(Discount), "D");

    public static readonly AdjustmentKind Surcharge = new(2, nameof(Surcharge), "R");
}

/// <summary>
/// Una línea de la Sección D (<c>&lt;DescuentoORecargo&gt;</c>) — descuento o recargo
/// <b>global</b> que afecta al total del e-CF, sin detallar ítem por ítem. Sección
/// **condicional** (obligatoriedad 2) en todos los tipos menos 43 y 47. Hasta 20.
/// <para>
/// El motor fiscal lo aplica al bucket que indica <see cref="AffectsRate"/> y
/// recalcula el ITBIS de ese bucket (ver <see cref="EcfGlobalAdjustmentInput"/>).
/// </para>
/// </summary>
/// <param name="Line"><c>&lt;NumeroLinea&gt;</c> — secuencial 1..N.</param>
/// <param name="Kind"><c>&lt;TipoAjuste&gt;</c>.</param>
/// <param name="AffectsRate"><c>&lt;IndicadorFacturacionDescuentooRecargo&gt;</c> — 1/2/3/4.</param>
/// <param name="Amount"><c>&lt;MontoDescuentooRecargo&gt;</c> — ≥ 0, en DOP.</param>
/// <param name="Norma1007"><c>&lt;IndicadorNorma1007&gt;</c> = 1. Solo descuentos a la tasa 1 (18 %).</param>
/// <param name="Description"><c>&lt;DescripcionDescuentooRecargo&gt;</c>.</param>
/// <param name="Percentage"><c>&lt;ValorDescuentooRecargo&gt;</c> — el % (informativo; el monto igual es obligatorio).</param>
/// <param name="AmountOtherCurrency"><c>&lt;MontoDescuentooRecargoOtraMoneda&gt;</c>.</param>
public sealed record EcfGlobalAdjustment(
    int Line,
    AdjustmentKind Kind,
    ItbisRate AffectsRate,
    decimal Amount,
    bool Norma1007 = false,
    string? Description = null,
    decimal? Percentage = null,
    decimal? AmountOtherCurrency = null);
