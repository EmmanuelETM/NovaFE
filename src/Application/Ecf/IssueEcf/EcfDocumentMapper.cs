using ErrorOr;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;
using NovaFE.Domain.Sequences;

namespace NovaFE.Application.Ecf.IssueEcf;

/// <summary>
/// Traduce el payload curado (<see cref="IssueEcfCommand"/>) al modelo fiscal del
/// dominio (<see cref="EcfDocument"/>). El bloque <c>&lt;Emisor&gt;</c> ya viene
/// armado (<see cref="EcfIssuerFactory"/>); el e-NCF y su vencimiento vienen de la
/// asignación de secuencia (Módulo 7). Los errores de forma vuelven como
/// <see cref="Error.Validation"/>; las invariantes las verifica
/// <see cref="EcfDocument.Create"/>.
/// </summary>
internal static class EcfDocumentMapper
{
    public static ErrorOr<EcfDocument> ToDocument(
        IssueEcfCommand command,
        EcfType type,
        Encf encf,
        DateOnly? sequenceExpiresOn,
        EcfIssuer issuer,
        DateOnly issueDate)
    {
        var buyer = MapBuyer(command.Buyer, command.AdditionalInfo?.Buyer);
        if (buyer.IsError)
            return buyer.Errors;

        var payment = MapPayment(command.Payment);
        if (payment.IsError)
            return payment.Errors;

        var lines = new List<EcfLine>(command.Lines.Count);
        for (var i = 0; i < command.Lines.Count; i++)
        {
            var line = MapLine(command.Lines[i], i + 1);
            if (line.IsError)
                return line.Errors;
            lines.Add(line.Value);
        }

        var reference = MapReference(command.Reference);
        if (reference.IsError)
            return reference.Errors;

        var adjustments = MapAdjustments(command.GlobalAdjustments);
        if (adjustments.IsError)
            return adjustments.Errors;

        var foreignCurrency = MapForeignCurrency(command.ForeignCurrency);
        if (foreignCurrency.IsError)
            return foreignCurrency.Errors;

        var transport = MapTransport(command.Transport);
        if (transport.IsError)
            return transport.Errors;

        var header = new EcfHeader(
            Encf: encf,
            SequenceExpiresOn: sequenceExpiresOn,
            IssueDate: issueDate,
            IncomeType: command.IncomeType?.Trim() ?? string.Empty,
            PricesIncludeTax: command.PricesIncludeTax,
            Issuer: issuer,
            Buyer: buyer.Value,
            Payment: payment.Value,
            DeferredDelivery: command.DeferredDelivery,
            NonInvoiceableAmount: command.NonInvoiceableAmount,
            ForeignCurrency: foreignCurrency.Value,
            Shipping: MapShipping(command.Shipping),
            Transport: transport.Value,
            GlobalAdjustments: adjustments.Value.Count > 0 ? adjustments.Value : null,
            Subtotals: MapSubtotals(command.Subtotals),
            Pagination: MapPagination(command.Pagination));

        return EcfDocument.Create(type, header, lines, reference.Value);
    }

    // --- comprador / pago -----------------------------------------------

    private static ErrorOr<EcfBuyer> MapBuyer(EcfBuyerPayload? buyer, string? additionalInfo)
    {
        if (buyer is null)
            return new EcfBuyer("Consumidor Final");

        Rnc? rnc = null;
        if (!string.IsNullOrWhiteSpace(buyer.Rnc))
        {
            var parsed = Rnc.Create(buyer.Rnc);
            if (parsed.IsError)
                return parsed.Errors;
            rnc = parsed.Value;
        }

        return new EcfBuyer(
            Name: Clean(buyer.Name) ?? "Consumidor Final",
            Rnc: rnc,
            ForeignId: Clean(buyer.ForeignId),
            Contact: Clean(buyer.Contact),
            Email: Clean(buyer.Email),
            Address: Clean(buyer.Address),
            Municipality: Clean(buyer.Municipality),
            Province: Clean(buyer.Province),
            AdditionalInfo: Clean(additionalInfo));
    }

    private static ErrorOr<EcfPayment> MapPayment(EcfPaymentPayload payment)
    {
        if (EcfPayloadEnum.Resolve<PaymentCondition>(payment.Condition) is not { } condition)
            return Bad($"Condición de pago desconocida: '{payment.Condition}'.");

        var methods = new List<EcfPaymentMethod>();
        foreach (var method in payment.Methods ?? [])
        {
            if (EcfPayloadEnum.Resolve<PaymentMethodType>(method.Type) is not { } resolved)
                return Bad($"Forma de pago desconocida: '{method.Type}'.");
            methods.Add(new EcfPaymentMethod(resolved, method.Amount));
        }

        return new EcfPayment(condition, payment.DueDate, methods);
    }

    // --- líneas --------------------------------------------------------

    private static ErrorOr<EcfLine> MapLine(EcfLinePayload line, int number)
    {
        if (ItbisRate.FromIndicatorOrDefault(line.ItbisRate) is not { } rate)
            return Bad($"Línea {number}: indicador de facturación desconocido: {line.ItbisRate}.");
        if (EcfPayloadEnum.Resolve<ItemKind>(line.Kind) is not { } kind)
            return Bad($"Línea {number}: indicador bien/servicio desconocido: '{line.Kind}'.");

        EcfLineRetention? retention = null;
        if (line.Retention is { } r)
        {
            if (EcfPayloadEnum.Resolve<RetentionAgent>(r.Agent) is not { } agent)
                return Bad($"Línea {number}: agente de retención desconocido: '{r.Agent}'.");
            retention = new EcfLineRetention(agent, r.ItbisWithheld, r.IsrWithheld);
        }

        IReadOnlyList<EcfAdditionalTax>? additionalTaxes = line.AdditionalTaxes
            ?.Select(tax => new EcfAdditionalTax(tax.Code, tax.Rate, tax.IscEspecifico, tax.IscAdvalorem, tax.Otros))
            .ToList();

        IReadOnlyList<EcfItemCode>? codes = line.Codes
            ?.Select(code => new EcfItemCode(code.Type, code.Value))
            .ToList();

        return new EcfLine(
            Number: number,
            Rate: rate,
            Name: line.Name,
            Kind: kind,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            Description: Clean(line.Description),
            UnitOfMeasure: Clean(line.UnitOfMeasure),
            Discount: line.Discount,
            Surcharge: line.Surcharge,
            PriceIncludesTax: line.PriceIncludesTax,
            Codes: codes,
            Retention: retention,
            AdditionalTaxDetail: additionalTaxes,
            DeclaredAmount: line.DeclaredAmount);
    }

    // --- referencia / Sección D / divisa / transporte ------------------

    private static ErrorOr<EcfReference?> MapReference(EcfReferencePayload? reference)
    {
        if (reference is null)
            return (EcfReference?)null;

        if (EcfPayloadEnum.Resolve<ModificationCode>(reference.ModificationCode) is not { } code)
            return Bad($"Código de modificación desconocido: '{reference.ModificationCode}'.");

        return new EcfReference(reference.ModifiedNcf, reference.ModifiedNcfDate, code, Clean(reference.OtherIssuerRnc));
    }

    private static ErrorOr<List<EcfGlobalAdjustment>> MapAdjustments(
        IReadOnlyList<EcfGlobalAdjustmentPayload>? adjustments)
    {
        var mapped = new List<EcfGlobalAdjustment>();

        foreach (var adjustment in adjustments ?? [])
        {
            if (EcfPayloadEnum.Resolve<AdjustmentKind>(adjustment.Kind) is not { } kind)
                return Bad($"Tipo de ajuste global desconocido: '{adjustment.Kind}'.");
            if (ItbisRate.FromIndicatorOrDefault(adjustment.AffectsItbisRate) is not { } affects)
                return Bad($"Ajuste global de la línea {adjustment.Line}: tasa de ITBIS desconocida: {adjustment.AffectsItbisRate}.");

            mapped.Add(new EcfGlobalAdjustment(
                adjustment.Line, kind, affects, adjustment.Amount,
                adjustment.Norm1007, Clean(adjustment.Description), adjustment.Percentage));
        }

        return mapped;
    }

    private static ErrorOr<EcfForeignCurrency?> MapForeignCurrency(EcfForeignCurrencyPayload? fx)
    {
        if (fx is null)
            return (EcfForeignCurrency?)null;

        if (EcfPayloadEnum.Resolve<CurrencyCode>(fx.Currency) is not { } currency)
            return Bad($"Moneda desconocida: '{fx.Currency}'.");

        var t = fx.Totals;
        return new EcfForeignCurrency(currency, fx.ExchangeRate, new EcfForeignCurrencyTotals(
            MontoGravadoTotal: t.MontoGravadoTotal,
            MontoGravadoI1: t.MontoGravadoI1,
            MontoGravadoI2: t.MontoGravadoI2,
            MontoGravadoI3: t.MontoGravadoI3,
            MontoExento: t.MontoExento,
            TotalItbis: t.TotalItbis,
            TotalItbis1: t.TotalItbis1,
            TotalItbis2: t.TotalItbis2,
            TotalItbis3: t.TotalItbis3,
            MontoTotal: t.MontoTotal));
    }

    private static ErrorOr<EcfTransport?> MapTransport(EcfTransportPayload? transport)
    {
        if (transport is null)
            return (EcfTransport?)null;

        TransportVia? via = null;
        if (!string.IsNullOrWhiteSpace(transport.Via))
        {
            if (EcfPayloadEnum.Resolve<TransportVia>(transport.Via) is not { } resolved)
                return Bad($"Vía de transporte desconocida: '{transport.Via}'.");
            via = resolved;
        }

        return new EcfTransport(
            Driver: Clean(transport.Driver),
            TransportDocument: Clean(transport.TransportDocument),
            VehicleId: Clean(transport.VehicleId),
            Plate: Clean(transport.Plate),
            Route: Clean(transport.Route),
            Zone: Clean(transport.Zone),
            DeliveryNote: Clean(transport.DeliveryNote),
            Via: via,
            OriginCountry: Clean(transport.OriginCountry),
            DestinationAddress: Clean(transport.DestinationAddress),
            DestinationCountry: Clean(transport.DestinationCountry),
            CarrierRnc: Clean(transport.CarrierRnc),
            CarrierName: Clean(transport.CarrierName),
            VoyageNumber: Clean(transport.VoyageNumber));
    }

    private static EcfShippingInfo? MapShipping(EcfShippingPayload? shipping)
    {
        if (shipping is null)
            return null;

        var export = shipping.Export is { } e
            ? new EcfExportDetails(
                LoadingPortName: Clean(e.LoadingPortName),
                DeliveryTerms: Clean(e.DeliveryTerms),
                TotalFob: e.TotalFob,
                Insurance: e.Insurance,
                Freight: e.Freight,
                OtherCharges: e.OtherCharges,
                TotalCif: e.TotalCif,
                CustomsRegime: Clean(e.CustomsRegime),
                DeparturePortName: Clean(e.DeparturePortName),
                UnloadingPortName: Clean(e.UnloadingPortName))
            : null;

        return new EcfShippingInfo(
            ShipmentDate: shipping.ShipmentDate,
            ShipmentNumber: Clean(shipping.ShipmentNumber),
            ContainerNumber: Clean(shipping.ContainerNumber),
            ReferenceNumber: Clean(shipping.ReferenceNumber),
            GrossWeight: shipping.GrossWeight,
            NetWeight: shipping.NetWeight,
            GrossWeightUnit: Clean(shipping.GrossWeightUnit),
            NetWeightUnit: Clean(shipping.NetWeightUnit),
            PackageCount: shipping.PackageCount,
            PackageUnit: Clean(shipping.PackageUnit),
            Volume: shipping.Volume,
            VolumeUnit: Clean(shipping.VolumeUnit),
            Export: export);
    }

    private static IReadOnlyList<EcfSubtotal>? MapSubtotals(IReadOnlyList<EcfSubtotalPayload>? subtotals)
        => subtotals is null || subtotals.Count == 0
            ? null
            : [.. subtotals.Select(s => new EcfSubtotal(
                s.Number, Clean(s.Description), s.Order, s.MontoGravadoTotal,
                s.MontoGravadoI1, s.MontoGravadoI2, s.MontoGravadoI3, s.TotalItbis,
                s.Itbis1, s.Itbis2, s.Itbis3, s.MontoImpuestoAdicional, s.MontoExento, s.Amount, s.Lines))];

    private static IReadOnlyList<EcfPage>? MapPagination(IReadOnlyList<EcfPagePayload>? pages)
        => pages is null || pages.Count == 0
            ? null
            : [.. pages.Select(p => new EcfPage(
                p.Number, p.LineFrom, p.LineTo, p.MontoGravadoTotal, p.MontoGravadoI1,
                p.MontoGravadoI2, p.MontoGravadoI3, p.MontoExento, p.TotalItbis, p.Itbis1,
                p.Itbis2, p.Itbis3, p.MontoImpuestoAdicional, p.IscEspecifico, p.OtrosImpuestos,
                p.Amount, p.NonInvoiceableAmount))];

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Error Bad(string message)
        => Error.Validation("Ecf.InvalidPayload", message);
}
