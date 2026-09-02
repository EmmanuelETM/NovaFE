namespace NovaFE.Application.Ecf.Representation;

/// <summary>
/// Proyección de un comprobante emitido lista para pintar la <b>Representación
/// Impresa</b> (Módulo 9). Se arma del <c>&lt;ECF&gt;</c> firmado que ya se guarda
/// (fuente única de los datos del documento) más el código de seguridad, la URL
/// del timbre QR y el estado frente a la DGII, que viven en la fila
/// <c>issued_ecf</c>.
/// </summary>
public sealed record RepresentationModel(
    RepresentationDocumentInfo Document,
    RepresentationParty Issuer,
    RepresentationParty? Buyer,
    RepresentationPayment Payment,
    IReadOnlyList<RepresentationLine> Lines,
    RepresentationTotals Totals,
    RepresentationReference? Reference,
    RepresentationVerification Verification,
    RepresentationDgiiStatus? Dgii,
    string? ContingencyNotice = null);

/// <summary>Identidad fiscal del comprobante (bloque <c>IdDoc</c> + fechas).</summary>
/// <param name="TypeCode">Código de dos dígitos (31, 32, …).</param>
/// <param name="TypeName">Nombre del tipo tal como lo llama la DGII.</param>
/// <param name="Encf">e-NCF de 13 caracteres.</param>
/// <param name="IssueDate">Fecha de emisión.</param>
/// <param name="SequenceExpiresOn">Vencimiento de la secuencia; ausente en los tipos 32 y 34.</param>
/// <param name="InternalNumber">Número de factura interna (<c>NumeroFacturaInterna</c>), si el emisor lo puso.</param>
/// <param name="IncomeType">Etiqueta del tipo de ingreso, si el comprobante lo lleva.</param>
/// <param name="Currency">Moneda de facturación (<c>TipoMoneda</c>) cuando no es DOP; <c>null</c> si es DOP.</param>
/// <param name="ExchangeRate">Tipo de cambio a DOP, cuando hay <see cref="Currency"/>.</param>
/// <param name="SignedAtText">El <c>&lt;FechaHoraFirma&gt;</c> tal cual va en el XML (hora dominicana, <c>dd-MM-yyyy HH:mm:ss</c>).</param>
public sealed record RepresentationDocumentInfo(
    string TypeCode,
    string TypeName,
    string Encf,
    DateOnly IssueDate,
    DateOnly? SequenceExpiresOn,
    string? InternalNumber,
    string? IncomeType,
    string? Currency,
    decimal? ExchangeRate,
    string SignedAtText);

/// <summary>Un participante del comprobante (emisor o comprador).</summary>
public sealed record RepresentationParty(
    string Name,
    string? Rnc,
    string? ForeignId,
    string? TradeName,
    string? Address,
    string? Municipality,
    string? Province,
    IReadOnlyList<string> Phones,
    string? Email,
    string? EconomicActivity,
    string? Contact)
{
    /// <summary>RNC/cédula, o el identificador extranjero, o <c>null</c>.</summary>
    public string? TaxId => Rnc ?? ForeignId;
}

/// <summary>Condición y formas de pago.</summary>
/// <param name="ConditionLabel">"Contado" / "Crédito" / "Gratuito".</param>
public sealed record RepresentationPayment(
    string? ConditionLabel,
    DateOnly? DueDate,
    IReadOnlyList<RepresentationPaymentMethod> Methods);

/// <param name="Label">Forma de pago en texto ("Efectivo", "Cheque/Transferencia/Depósito", …).</param>
public sealed record RepresentationPaymentMethod(string Label, decimal Amount);

/// <summary>Una línea de detalle.</summary>
/// <param name="Kind">"Bien" / "Servicio".</param>
/// <param name="TaxLabel">"18%" / "16%" / "0%" / "Exento" — según el <c>IndicadorFacturacion</c>.</param>
/// <param name="TaxRate">Tasa aplicada (0.18 / 0.16 / 0) para derivar el importe con impuesto.</param>
/// <param name="Amount">
/// <c>&lt;MontoItem&gt;</c>: el neto de la línea (cantidad × precio − descuento + recargo),
/// <b>antes</b> del ITBIS.
/// </param>
public sealed record RepresentationLine(
    int Number,
    string Name,
    string? Kind,
    decimal Quantity,
    string? UnitOfMeasure,
    decimal UnitPrice,
    decimal? Discount,
    decimal? Surcharge,
    string? TaxLabel,
    decimal TaxRate,
    decimal Amount,
    decimal? ItbisWithheld,
    decimal? IsrWithheld)
{
    /// <summary>Importe de la línea <b>con</b> el ITBIS que le corresponde — lo que se paga por ese ítem.</summary>
    public decimal GrossAmount => Math.Round(Amount * (1m + TaxRate), 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Totalizadores del encabezado. Todos anulables salvo <see cref="MontoTotal"/>:
/// la RI solo pinta la fila que trae valor.
/// </summary>
public sealed record RepresentationTotals(
    decimal? MontoGravadoTotal,
    decimal? MontoGravadoI1,
    decimal? MontoGravadoI2,
    decimal? MontoGravadoI3,
    decimal? MontoExento,
    decimal? Itbis1,
    decimal? Itbis2,
    decimal? Itbis3,
    decimal? TotalItbis,
    decimal? MontoImpuestoAdicional,
    decimal? TotalItbisWithheld,
    decimal? TotalIsrWithheld,
    decimal MontoTotal,
    decimal? AmountDue);

/// <summary>Referencia al comprobante que modifica una nota de crédito o débito.</summary>
public sealed record RepresentationReference(
    string ModifiedNcf,
    DateOnly? ModifiedDate,
    string? Reason);

/// <summary>El timbre: código de seguridad visible y la URL del QR (<see cref="Domain.Ecf.EcfVerificationUrl"/>).</summary>
public sealed record RepresentationVerification(string SecurityCode, string QrUrl)
{
    public static readonly RepresentationVerification None = new(string.Empty, string.Empty);
}

/// <summary>Estado del comprobante frente a la DGII, para el sello de la RI.</summary>
/// <param name="Status">El estado interno (<c>signed</c>, <c>accepted</c>, …).</param>
/// <param name="Code">Código de la DGII (1/2/3/4), si ya resolvió.</param>
/// <param name="Text">El <c>estado</c> textual de la DGII, si lo devolvió.</param>
public sealed record RepresentationDgiiStatus(string Status, int? Code, string? Text, string? TrackId);
