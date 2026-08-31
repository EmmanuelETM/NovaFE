namespace NovaFE.Application.Ecf.Contracts;

// Sub-estructuras del payload de emisión (POST /api/v1.0/ecf). Los campos "enum"
// aceptan el nombre ("credit", "check_transfer") o el código DGII ("2"); el mapper
// los resuelve. Los montos de línea que no sean el ITBIS los trae el cliente ya
// calculados (retención, impuestos adicionales, OtraMoneda).

/// <summary>Bloque <c>&lt;Comprador&gt;</c>. <c>Rnc</c> y <c>ForeignId</c> son excluyentes.</summary>
public sealed record EcfBuyerPayload(
    string? Name = null,
    string? Rnc = null,
    string? ForeignId = null,
    string? Email = null,
    string? Contact = null,
    string? Address = null,
    string? Municipality = null,
    string? Province = null,
    string? AdditionalInfo = null);

/// <summary>Bloque de pago del encabezado.</summary>
public sealed record EcfPaymentPayload(
    string Condition = "cash",
    DateOnly? DueDate = null,
    IReadOnlyList<EcfPaymentMethodPayload>? Methods = null);

public sealed record EcfPaymentMethodPayload(string Type, decimal Amount);

/// <summary>Una línea de <c>&lt;DetallesItems&gt;</c>.</summary>
public sealed record EcfLinePayload(
    string Name,
    string Kind = "service",
    decimal Quantity = 1m,
    decimal UnitPrice = 0m,
    int ItbisRate = 1,
    string? UnitOfMeasure = null,
    string? Description = null,
    decimal Discount = 0m,
    decimal Surcharge = 0m,
    bool? PriceIncludesTax = null,
    decimal? DeclaredAmount = null,
    IReadOnlyList<EcfItemCodePayload>? Codes = null,
    EcfLineRetentionPayload? Retention = null,
    IReadOnlyList<EcfAdditionalTaxPayload>? AdditionalTaxes = null);

public sealed record EcfItemCodePayload(string Type, string Value);

/// <summary>Área <c>&lt;Retencion&gt;</c> de la línea (tipos 41 y 47). Los montos los calcula el cliente.</summary>
public sealed record EcfLineRetentionPayload(
    string Agent = "withholding",
    decimal ItbisWithheld = 0m,
    decimal IsrWithheld = 0m);

/// <summary>Desglose de impuestos adicionales por código de la Tabla I (001–039).</summary>
public sealed record EcfAdditionalTaxPayload(
    string Code,
    decimal Rate = 0m,
    decimal IscEspecifico = 0m,
    decimal IscAdvalorem = 0m,
    decimal Otros = 0m);

/// <summary>Sección <c>&lt;InformacionReferencia&gt;</c> — Notas de Crédito/Débito y reemplazos.</summary>
public sealed record EcfReferencePayload(
    string ModifiedNcf,
    DateOnly ModifiedNcfDate,
    string ModificationCode = "corrects_amounts",
    string? OtherIssuerRnc = null);

/// <summary>Un descuento o recargo global (Sección D).</summary>
public sealed record EcfGlobalAdjustmentPayload(
    int Line,
    string Kind = "discount",
    int AffectsItbisRate = 1,
    decimal Amount = 0m,
    bool Norm1007 = false,
    string? Description = null,
    decimal? Percentage = null);

/// <summary>Facturación en divisa (<c>&lt;OtraMoneda&gt;</c>). El cliente trae los montos ya convertidos.</summary>
public sealed record EcfForeignCurrencyPayload(
    string Currency,
    decimal ExchangeRate,
    EcfForeignCurrencyTotalsPayload Totals);

public sealed record EcfForeignCurrencyTotalsPayload(
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

/// <summary>Datos de embarque (<c>&lt;InformacionesAdicionales&gt;</c>). Passthrough.</summary>
public sealed record EcfShippingPayload(
    DateOnly? ShipmentDate = null,
    string? ShipmentNumber = null,
    string? ContainerNumber = null,
    string? ReferenceNumber = null,
    decimal? GrossWeight = null,
    decimal? NetWeight = null,
    string? GrossWeightUnit = null,
    string? NetWeightUnit = null,
    decimal? PackageCount = null,
    string? PackageUnit = null,
    decimal? Volume = null,
    string? VolumeUnit = null,
    EcfExportPayload? Export = null);

/// <summary>Datos de exportación (solo tipo 46).</summary>
public sealed record EcfExportPayload(
    string? LoadingPortName = null,
    string? DeliveryTerms = null,
    decimal? TotalFob = null,
    decimal? Insurance = null,
    decimal? Freight = null,
    decimal? OtherCharges = null,
    decimal? TotalCif = null,
    string? CustomsRegime = null,
    string? DeparturePortName = null,
    string? UnloadingPortName = null);

/// <summary>Datos de transporte (<c>&lt;Transporte&gt;</c>). Passthrough.</summary>
public sealed record EcfTransportPayload(
    string? Driver = null,
    string? TransportDocument = null,
    string? VehicleId = null,
    string? Plate = null,
    string? Route = null,
    string? Zone = null,
    string? DeliveryNote = null,
    string? Via = null,
    string? OriginCountry = null,
    string? DestinationAddress = null,
    string? DestinationCountry = null,
    string? CarrierRnc = null,
    string? CarrierName = null,
    string? VoyageNumber = null);

/// <summary>Subtotal informativo para la RI (Sección C). No afecta la base imponible.</summary>
public sealed record EcfSubtotalPayload(
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

/// <summary>Página de la RI (<c>&lt;Paginacion&gt;</c>).</summary>
public sealed record EcfPagePayload(
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

/// <summary>Texto libre para la Representación Impresa.</summary>
public sealed record EcfAdditionalInfoPayload(string? Issuer = null, string? Buyer = null);

/// <summary>
/// Totales que calculó el cliente (en DOP). Se comparan con el cálculo de NovaFE
/// (tolerancia RF-06.6); el valor de NovaFE es el que va al XML. Todo opcional.
/// </summary>
public sealed record EcfDeclaredTotalsPayload(
    decimal? MontoGravadoTotal = null,
    decimal? MontoExento = null,
    decimal? TotalItbis = null,
    decimal? MontoImpuestoAdicional = null,
    decimal? MontoTotal = null);
