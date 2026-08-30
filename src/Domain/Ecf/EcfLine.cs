using NovaFE.Domain.Fiscal;

namespace NovaFE.Domain.Ecf;

/// <summary>Par Tipo/Código de <c>&lt;TablaCodigosItem&gt;</c> (GTIN, código interno…).</summary>
public sealed record EcfItemCode(string Type, string Value);

/// <summary>
/// Una línea de <c>&lt;DetallesItems&gt;</c>. Trae los datos que da el cliente; los
/// montos calculados (<c>&lt;MontoItem&gt;</c>, base gravada, ITBIS) los produce
/// <see cref="Fiscal.EcfCalculator"/> y viven en el <see cref="EcfDocument"/>.
/// </summary>
/// <param name="Number"><c>&lt;NumeroLinea&gt;</c> — 1…1000, sin saltos.</param>
/// <param name="Rate"><c>&lt;IndicadorFacturacion&gt;</c>.</param>
/// <param name="Name"><c>&lt;NombreItem&gt;</c>.</param>
/// <param name="Kind"><c>&lt;IndicadorBienoServicio&gt;</c>.</param>
/// <param name="Quantity"><c>&lt;CantidadItem&gt;</c> — ≥ 0.</param>
/// <param name="UnitPrice"><c>&lt;PrecioUnitarioItem&gt;</c> — ≥ 0, hasta 4 decimales.</param>
/// <param name="Description"><c>&lt;DescripcionItem&gt;</c>.</param>
/// <param name="UnitOfMeasure"><c>&lt;UnidadMedida&gt;</c> — código Tabla IV.</param>
/// <param name="Discount"><c>&lt;DescuentoMonto&gt;</c> — suma de subdescuentos, ≥ 0.</param>
/// <param name="Surcharge"><c>&lt;RecargoMonto&gt;</c> — suma de subrecargos, ≥ 0.</param>
/// <param name="PriceIncludesTax">Sobrescribe el <c>IndicadorMontoGravado</c> del encabezado.</param>
/// <param name="AdditionalTaxes">Suma de "otros impuestos adicionales" ya calculados (Propina, CDT…).</param>
/// <param name="Codes"><c>&lt;TablaCodigosItem&gt;</c> — hasta 5.</param>
/// <param name="Retention"><c>&lt;Retencion&gt;</c> — área de retención de la línea; obligatoria en el tipo 41.</param>
/// <param name="ForeignCurrency"><c>&lt;OtraMonedaDetalle&gt;</c> — precio y montos de la línea en divisa.</param>
public sealed record EcfLine(
    int Number,
    ItbisRate Rate,
    string Name,
    ItemKind Kind,
    decimal Quantity,
    decimal UnitPrice,
    string? Description = null,
    string? UnitOfMeasure = null,
    decimal Discount = 0m,
    decimal Surcharge = 0m,
    bool? PriceIncludesTax = null,
    decimal AdditionalTaxes = 0m,
    IReadOnlyList<EcfItemCode>? Codes = null,
    EcfLineRetention? Retention = null,
    EcfLineForeignCurrency? ForeignCurrency = null);
