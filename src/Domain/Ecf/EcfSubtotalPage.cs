namespace NovaFE.Domain.Ecf;

/// <summary>
/// Una fila de <c>&lt;Subtotales&gt;</c> — agrupación **informativa** para la
/// Representación Impresa. El Formato es explícito: no aumenta ni disminuye la base
/// del impuesto ni modifica los totalizadores. <b>Passthrough puro</b> — el cliente
/// arma la agrupación, nosotros la emitimos. Cada tipo emite el subconjunto de
/// campos que corresponde a su <c>&lt;Totales&gt;</c>.
/// </summary>
public sealed record EcfSubtotal(
    int? Number = null,
    string? Description = null,
    int? Order = null,
    decimal? MontoGravadoTotal = null,
    decimal? MontoGravadoI1 = null,
    decimal? MontoGravadoI2 = null,
    decimal? MontoGravadoI3 = null,
    decimal? TotalItbis = null,
    decimal? Itbis1 = null,
    decimal? Itbis2 = null,
    decimal? Itbis3 = null,
    decimal? MontoImpuestoAdicional = null,
    decimal? MontoExento = null,
    decimal? Amount = null,
    int? Lines = null);

/// <summary>
/// Una página de <c>&lt;Paginacion&gt;</c> — qué líneas de detalle van en cada página
/// de la Representación Impresa, con sus subtotales. <b>Passthrough puro</b>. Cada
/// tipo emite el subconjunto de campos que corresponde a su <c>&lt;Totales&gt;</c>.
/// </summary>
public sealed record EcfPage(
    int? Number = null,
    int? LineFrom = null,
    int? LineTo = null,
    decimal? MontoGravadoTotal = null,
    decimal? MontoGravadoI1 = null,
    decimal? MontoGravadoI2 = null,
    decimal? MontoGravadoI3 = null,
    decimal? MontoExento = null,
    decimal? TotalItbis = null,
    decimal? Itbis1 = null,
    decimal? Itbis2 = null,
    decimal? Itbis3 = null,
    decimal? MontoImpuestoAdicional = null,
    decimal? IscEspecifico = null,
    decimal? OtrosImpuestos = null,
    decimal? Amount = null,
    decimal? NonInvoiceableAmount = null);
