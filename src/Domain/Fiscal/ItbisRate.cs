using NovaFE.Domain.Common;

namespace NovaFE.Domain.Fiscal;

/// <summary>
/// Tasa de ITBIS de una línea de detalle, identificada por
/// <c>&lt;IndicadorFacturacion&gt;</c> (Formato e-CF, detalle campo 3 / RF-06.2):
/// <list type="bullet">
///   <item>1 — 18 %</item>
///   <item>2 — 16 %</item>
///   <item>3 — 0 % (exento <b>con</b> derecho a crédito fiscal; va en <c>MontoGravadoI3</c>)</item>
///   <item>4 — Exento (<b>sin</b> crédito fiscal; va en <c>MontoExento</c>, no lleva ITBIS)</item>
/// </list>
/// La tasa 3 y la 4 se parecen (ITBIS 0) pero se totalizan distinto: la 3 es
/// gravada, la 4 es exenta.
/// </summary>
public sealed record ItbisRate(int Id, string Name, decimal Rate, bool IsExempt)
    : Enumeration<ItbisRate>(Id, Name)
{
    /// <summary>18 % — <c>IndicadorFacturacion = 1</c>.</summary>
    public static readonly ItbisRate Eighteen = new(1, nameof(Eighteen), 0.18m, IsExempt: false);

    /// <summary>16 % — <c>IndicadorFacturacion = 2</c>.</summary>
    public static readonly ItbisRate Sixteen = new(2, nameof(Sixteen), 0.16m, IsExempt: false);

    /// <summary>0 % gravado (con crédito fiscal) — <c>IndicadorFacturacion = 3</c>.</summary>
    public static readonly ItbisRate Zero = new(3, nameof(Zero), 0m, IsExempt: false);

    /// <summary>Exento sin crédito fiscal — <c>IndicadorFacturacion = 4</c>.</summary>
    public static readonly ItbisRate Exempt = new(4, nameof(Exempt), 0m, IsExempt: true);

    /// <summary>La tasa cuyo <c>IndicadorFacturacion</c> es <paramref name="indicator"/>, o null si no es 1–4.</summary>
    public static ItbisRate? FromIndicatorOrDefault(int indicator) =>
        GetAll().FirstOrDefault(rate => rate.Id == indicator);
}
