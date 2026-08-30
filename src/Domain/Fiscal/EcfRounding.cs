namespace NovaFE.Domain.Fiscal;

/// <summary>
/// Redondeo de la DGII para los campos numéricos del e-CF (Informe Técnico e-CF,
/// RF-06.1). Regla: se mira el dígito de la posición siguiente a la que se
/// conserva; si es <b>≥ 5</b> se incrementa el último dígito conservado, si es
/// <b>&lt; 5</b> se trunca. Es exactamente el redondeo "mitad hacia afuera del
/// cero" (<see cref="MidpointRounding.AwayFromZero"/>) a la escala del campo.
/// <para>
/// Se trabaja siempre en <see cref="decimal"/>: la aritmética de <c>double</c> no
/// es exacta para montos y produciría diferencias de centavos.
/// </para>
/// </summary>
public static class EcfRounding
{
    /// <summary>Escala de los campos de dinero, ITBIS, impuestos, descuentos y recargos.</summary>
    public const int MoneyScale = 2;

    /// <summary>Escala de <c>PrecioUnitarioItem</c>, <c>PrecioUnitarioItemOtraMoneda</c> y <c>TipoCambio</c>.</summary>
    public const int UnitPriceScale = 4;

    /// <summary>Escala de <c>Subcantidad</c>.</summary>
    public const int SubquantityScale = 3;

    /// <summary>Redondea a la escala de dinero (2 decimales).</summary>
    public static decimal Money(decimal value) => AtScale(value, MoneyScale);

    /// <summary>Redondea a la escala de precio unitario / tipo de cambio (4 decimales).</summary>
    public static decimal UnitPrice(decimal value) => AtScale(value, UnitPriceScale);

    /// <summary>Redondea a la escala de subcantidad (3 decimales).</summary>
    public static decimal Subquantity(decimal value) => AtScale(value, SubquantityScale);

    /// <summary>Redondea <paramref name="value"/> a <paramref name="decimals"/> con la regla de la DGII.</summary>
    public static decimal AtScale(decimal value, int decimals)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decimals, 28);

        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }
}
