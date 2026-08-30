namespace NovaFE.Domain.Ecf;

/// <summary>
/// Un impuesto adicional de una línea, desglosado por código (Tabla I de la DGII,
/// <c>001</c>…<c>039</c>). Alimenta el bloque <c>&lt;ImpuestosAdicionales&gt;</c> de
/// <c>&lt;Totales&gt;</c> y la <c>&lt;TablaImpuestoAdicional&gt;</c> de la línea.
/// <para>
/// <b>Passthrough:</b> el cliente trae los montos ya calculados; el motor solo los
/// agrupa por código. La derivación del ISC específico desde
/// <c>GradosAlcohol</c>/<c>CantidadReferencia</c> es un slice posterior
/// (ver <c>docs/fiscal.md</c>). El total de estos montos debe coincidir con el
/// <see cref="EcfLine.AdditionalTaxes"/> agregado de la línea.
/// </para>
/// </summary>
/// <param name="Code"><c>&lt;TipoImpuesto&gt;</c> — código de la Tabla I (<c>"001"</c>…<c>"039"</c>).</param>
/// <param name="Rate"><c>&lt;TasaImpuestoAdicional&gt;</c> — % del impuesto (p. ej. propina 10, CDT 2).</param>
/// <param name="IscEspecifico"><c>&lt;MontoImpuestoSelectivoConsumoEspecifico&gt;</c> — ISC por volumen; solo alcoholes/cigarrillos.</param>
/// <param name="IscAdvalorem"><c>&lt;MontoImpuestoSelectivoConsumoAdvalorem&gt;</c> — ISC ad valorem.</param>
/// <param name="Otros"><c>&lt;OtrosImpuestosAdicionales&gt;</c> — el resto (propina, CDT, seguros, telecom, placa).</param>
public sealed record EcfAdditionalTax(
    string Code,
    decimal Rate = 0m,
    decimal IscEspecifico = 0m,
    decimal IscAdvalorem = 0m,
    decimal Otros = 0m)
{
    /// <summary>Códigos válidos de la Tabla I: <c>"001"</c>…<c>"039"</c>.</summary>
    public static bool IsValidCode(string code) =>
        code is { Length: 3 }
        && int.TryParse(code, out var n)
        && n is >= 1 and <= 39;

    /// <summary>Suma que aporta este código al <c>&lt;MontoImpuestoAdicional&gt;</c>.</summary>
    public decimal Amount => IscEspecifico + IscAdvalorem + Otros;
}

/// <summary>
/// Un código de impuesto adicional agregado a lo largo de todas las líneas — una
/// fila de <c>&lt;ImpuestosAdicionales&gt;</c> en <c>&lt;Totales&gt;</c>.
/// </summary>
public sealed record EcfAdditionalTaxGroup(
    string Code,
    decimal Rate,
    decimal IscEspecifico,
    decimal IscAdvalorem,
    decimal Otros);

/// <summary>
/// <c>&lt;GradosAlcohol&gt;</c>, <c>&lt;CantidadReferencia&gt;</c>, <c>&lt;Mineria&gt;</c> y demás
/// campos opcionales del <c>&lt;Item&gt;</c> que hoy son passthrough puro. Se
/// intercalan en el orden del XSD entre <c>UnidadMedida</c> y <c>PrecioUnitarioItem</c>.
/// </summary>
/// <param name="ReferenceQuantity"><c>&lt;CantidadReferencia&gt;</c>.</param>
/// <param name="ReferenceUnit"><c>&lt;UnidadReferencia&gt;</c> — código Tabla IV.</param>
/// <param name="Subquantities"><c>&lt;TablaSubcantidad&gt;</c> — hasta 5.</param>
/// <param name="AlcoholDegrees"><c>&lt;GradosAlcohol&gt;</c>.</param>
/// <param name="ReferenceUnitPrice"><c>&lt;PrecioUnitarioReferencia&gt;</c>.</param>
/// <param name="Elaboration"><c>&lt;FechaElaboracion&gt;</c>.</param>
/// <param name="ItemExpiry"><c>&lt;FechaVencimientoItem&gt;</c>.</param>
/// <param name="Mining"><c>&lt;Mineria&gt;</c> — solo tipos 32/33/34/46.</param>
public sealed record EcfLineDetails(
    decimal? ReferenceQuantity = null,
    string? ReferenceUnit = null,
    IReadOnlyList<EcfSubquantity>? Subquantities = null,
    decimal? AlcoholDegrees = null,
    decimal? ReferenceUnitPrice = null,
    DateOnly? Elaboration = null,
    DateOnly? ItemExpiry = null,
    EcfMining? Mining = null);

/// <summary><c>&lt;SubcantidadItem&gt;</c> — una subcantidad con su unidad.</summary>
/// <param name="Quantity"><c>&lt;Subcantidad&gt;</c> — hasta 3 decimales.</param>
/// <param name="UnitCode"><c>&lt;CodigoSubcantidad&gt;</c> — código Tabla IV.</param>
public sealed record EcfSubquantity(decimal Quantity, string UnitCode);

/// <summary>
/// <c>&lt;Mineria&gt;</c> — datos de liquidación minera. Solo tipos 32/33/34/46.
/// Passthrough.
/// </summary>
/// <param name="NetWeightKilogram"><c>&lt;PesoNetoKilogramo&gt;</c>.</param>
/// <param name="NetWeightMining"><c>&lt;PesoNetoMineria&gt;</c>.</param>
/// <param name="AffiliationType"><c>&lt;TipoAfiliacion&gt;</c> — 1 afiliada / 2 no afiliada.</param>
/// <param name="Settlement"><c>&lt;Liquidacion&gt;</c> — 1 provisional / 2 final.</param>
public sealed record EcfMining(
    decimal? NetWeightKilogram = null,
    decimal? NetWeightMining = null,
    int? AffiliationType = null,
    int? Settlement = null);
