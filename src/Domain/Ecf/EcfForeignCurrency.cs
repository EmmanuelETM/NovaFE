using NovaFE.Domain.Common;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// <c>&lt;TipoMoneda&gt;</c> — Tabla II de la DGII (código ISO de 3 letras).
/// <see cref="Enumeration{T}.Name"/> es el código que va al XML.
/// </summary>
public sealed record CurrencyCode(int Id, string Name) : Enumeration<CurrencyCode>(Id, Name)
{
    public static readonly CurrencyCode BRL = new(1, "BRL");
    public static readonly CurrencyCode CAD = new(2, "CAD");
    public static readonly CurrencyCode CHF = new(3, "CHF");
    public static readonly CurrencyCode CHY = new(4, "CHY");
    public static readonly CurrencyCode XDR = new(5, "XDR");
    public static readonly CurrencyCode DKK = new(6, "DKK");
    public static readonly CurrencyCode EUR = new(7, "EUR");
    public static readonly CurrencyCode GBP = new(8, "GBP");
    public static readonly CurrencyCode JPY = new(9, "JPY");
    public static readonly CurrencyCode NOK = new(10, "NOK");
    public static readonly CurrencyCode SCP = new(11, "SCP");
    public static readonly CurrencyCode SEK = new(12, "SEK");
    public static readonly CurrencyCode USD = new(13, "USD");
    public static readonly CurrencyCode VEF = new(14, "VEF");
    public static readonly CurrencyCode HTG = new(15, "HTG");
    public static readonly CurrencyCode MXN = new(16, "MXN");
    public static readonly CurrencyCode COP = new(17, "COP");
}

/// <summary>
/// <c>&lt;OtraMoneda&gt;</c> del encabezado — el comprobante facturado en divisa.
/// Bloque <b>condicional</b> a que la facturación sea en moneda extranjera
/// (obligatoriedad 2, todos los tipos). <b>Passthrough:</b> el cliente trae los
/// montos ya convertidos; el motor solo hace un cross-check contra
/// <c>MontoTotal_DOP / TipoCambio</c> (nunca rechaza).
/// <para>
/// El almacenamiento y la lógica interna siguen en DOP; <c>OtraMoneda</c> es solo
/// un espejo para la DGII y la Representación Impresa.
/// </para>
/// </summary>
/// <param name="Currency"><c>&lt;TipoMoneda&gt;</c>.</param>
/// <param name="ExchangeRate"><c>&lt;TipoCambio&gt;</c> — DOP por unidad de divisa, hasta 4 decimales, &gt; 0.</param>
/// <param name="Totals">Los totalizadores del encabezado en divisa (el subconjunto que aplica al tipo).</param>
public sealed record EcfForeignCurrency(
    CurrencyCode Currency,
    decimal ExchangeRate,
    EcfForeignCurrencyTotals Totals);

/// <summary>
/// Totalizadores del encabezado en divisa (<c>*OtraMoneda</c>). Cada tipo de e-CF
/// emite el subconjunto que corresponde a su <c>&lt;Totales&gt;</c>: los tipos con
/// ITBIS completo usan todos; 43/44/47 solo <see cref="MontoExento"/> y
/// <see cref="MontoTotal"/>; el 46 solo el bucket a tasa 0. Los campos que no
/// aplican al tipo se dejan en null.
/// </summary>
public sealed record EcfForeignCurrencyTotals(
    decimal? MontoGravadoTotal = null,
    decimal? MontoGravadoI1 = null,
    decimal? MontoGravadoI2 = null,
    decimal? MontoGravadoI3 = null,
    decimal? MontoExento = null,
    decimal? TotalItbis = null,
    decimal? TotalItbis1 = null,
    decimal? TotalItbis2 = null,
    decimal? TotalItbis3 = null,
    decimal? MontoTotal = null);

/// <summary>
/// <c>&lt;OtraMonedaDetalle&gt;</c> de una línea — precio y montos en divisa.
/// Passthrough. Igual en los diez tipos.
/// </summary>
/// <param name="UnitPrice"><c>&lt;PrecioOtraMoneda&gt;</c>.</param>
/// <param name="Discount"><c>&lt;DescuentoOtraMoneda&gt;</c>.</param>
/// <param name="Surcharge"><c>&lt;RecargoOtraMoneda&gt;</c>.</param>
/// <param name="LineAmount"><c>&lt;MontoItemOtraMoneda&gt;</c>.</param>
public sealed record EcfLineForeignCurrency(
    decimal? UnitPrice = null,
    decimal? Discount = null,
    decimal? Surcharge = null,
    decimal? LineAmount = null);

/// <summary>
/// Resultado del cross-check de <c>&lt;OtraMoneda&gt;</c>: compara el
/// <c>MontoTotal</c> declarado en divisa contra <c>MontoTotal_DOP / TipoCambio</c>.
/// Informativo — <b>nunca</b> es motivo de rechazo.
/// </summary>
public sealed record EcfForeignCurrencyCheck(
    decimal ExpectedFromDop,
    decimal Declared,
    decimal Difference,
    bool WithinTolerance);
