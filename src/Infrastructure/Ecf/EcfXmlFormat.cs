using System.Globalization;

namespace NovaFE.Infrastructure.Ecf;

/// <summary>
/// Formato numérico y de fechas exacto que exige el XSD de la DGII: punto
/// decimal, sin separador de miles, sin notación científica, sin ceros de más.
/// </summary>
internal static class EcfXmlFormat
{
    /// <summary>Montos, ITBIS, impuestos, descuentos en el <c>&lt;ECF&gt;</c>: hasta 2 decimales.</summary>
    public static string Money(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Montos en el <c>&lt;RFCE&gt;</c>: su XSD (<c>Decimal18D2…</c>) exige
    /// <b>exactamente</b> 2 decimales cuando hay parte fraccionaria — no acepta
    /// "191.3", sí "191.30".
    /// </summary>
    public static string Money2(decimal value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return rounded == Math.Truncate(rounded)
            ? rounded.ToString("0", CultureInfo.InvariantCulture)
            : rounded.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary><c>PrecioUnitarioItem</c>, <c>TipoCambio</c>: hasta 4 decimales.</summary>
    public static string UnitPrice(decimal value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary><c>Subcantidad</c>: hasta 3 decimales.</summary>
    public static string Subquantity(decimal value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Porcentajes de descuento/recargo y tasas adicionales: hasta 2 decimales.</summary>
    public static string Percent(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary><c>&lt;ITBIS1&gt;</c>/<c>&lt;ITBIS2&gt;</c>/<c>&lt;ITBIS3&gt;</c> — la tasa como entero (18, 16, 0).</summary>
    public static string RateIndicator(decimal rate) =>
        Math.Round(rate * 100m, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>Fechas de documento: <c>dd-MM-yyyy</c>.</summary>
    public static string Date(DateOnly date) =>
        date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
}
