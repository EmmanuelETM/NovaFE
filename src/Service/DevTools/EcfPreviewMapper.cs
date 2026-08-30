using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;
using NovaFE.Domain.Sequences;

namespace NovaFE.Service.DevTools;

/// <summary>
/// Traduce el <see cref="EcfPreviewRequest"/> (forma cruda del preview) a un
/// <see cref="EcfDocument"/> del dominio. Los errores de forma vuelven como
/// <see cref="Error.Validation"/>; los del dominio los devuelve
/// <see cref="EcfDocument.Create"/>.
/// </summary>
internal static class EcfPreviewMapper
{
    public static ErrorOr<EcfDocument> ToDocument(EcfPreviewRequest request)
    {
        if (TryEnum<EcfType>(request.Type) is not { } ecfType)
            return Bad($"Tipo de e-CF desconocido: {request.Type}.");

        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(DateTime.Today);

        var encf = request.Encf is { Length: > 0 } rawEncf
            ? Encf.Create(rawEncf)
            : Encf.Build('E', ecfType.Id, 42);
        if (encf.IsError)
            return encf.Errors;

        var issuerRnc = Rnc.Create(request.Issuer.Rnc);
        if (issuerRnc.IsError)
            return issuerRnc.Errors;

        var buyer = MapBuyer(request.Buyer);
        if (buyer.IsError)
            return buyer.Errors;

        var payment = MapPayment(request.Payment);
        if (payment.IsError)
            return payment.Errors;

        var lines = new List<EcfLine>(request.Lines.Count);
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = MapLine(request.Lines[i], i + 1);
            if (line.IsError)
                return line.Errors;
            lines.Add(line.Value);
        }

        var adjustmentsResult = MapAdjustments(request.GlobalAdjustments);
        if (adjustmentsResult.IsError)
            return adjustmentsResult.Errors;
        var adjustments = adjustmentsResult.Value.Count > 0 ? adjustmentsResult.Value : null;

        var foreignCurrency = MapForeignCurrency(request.ForeignCurrency);
        if (foreignCurrency.IsError)
            return foreignCurrency.Errors;

        var reference = MapReference(request.Reference);
        if (reference.IsError)
            return reference.Errors;

        var header = new EcfHeader(
            Encf: encf.Value,
            SequenceExpiresOn: request.SequenceExpiresOn
                ?? (ecfType.HasSequenceExpiry ? new DateOnly(issueDate.Year + 1, 12, 31) : null),
            IssueDate: issueDate,
            IncomeType: request.IncomeType,
            PricesIncludeTax: request.PricesIncludeTax,
            Issuer: new EcfIssuer(issuerRnc.Value, request.Issuer.Name, request.Issuer.Address ?? "—", Email: request.Issuer.Email),
            Buyer: buyer.Value,
            Payment: payment.Value,
            NonInvoiceableAmount: request.NonInvoiceableAmount,
            ForeignCurrency: foreignCurrency.Value,
            GlobalAdjustments: adjustments);

        return EcfDocument.Create(ecfType, header, lines, reference.Value);
    }

    private static ErrorOr<EcfBuyer> MapBuyer(PreviewBuyer buyer)
    {
        Rnc? rnc = null;
        if (buyer.Rnc is { Length: > 0 } raw)
        {
            var parsed = Rnc.Create(raw);
            if (parsed.IsError)
                return parsed.Errors;
            rnc = parsed.Value;
        }

        return new EcfBuyer(buyer.Name, rnc, buyer.ForeignId, Email: buyer.Email);
    }

    private static ErrorOr<EcfPayment> MapPayment(PreviewPayment payment)
    {
        if (TryEnum<PaymentCondition>(payment.Condition) is not { } condition)
            return Bad($"Condición de pago desconocida: {payment.Condition}.");

        var methods = new List<EcfPaymentMethod>();
        foreach (var m in payment.Methods ?? [])
        {
            if (TryEnum<PaymentMethodType>(m.Method) is not { } method)
                return Bad($"Forma de pago desconocida: {m.Method}.");
            methods.Add(new EcfPaymentMethod(method, m.Amount));
        }

        return new EcfPayment(condition, payment.DueDate, methods);
    }

    private static ErrorOr<EcfLine> MapLine(PreviewLine line, int fallbackNumber)
    {
        if (TryEnum<ItbisRate>(line.Rate) is not { } rate)
            return Bad($"Indicador de facturación desconocido: {line.Rate}.");
        if (TryEnum<ItemKind>(line.Kind) is not { } kind)
            return Bad($"Indicador bien/servicio desconocido: {line.Kind}.");

        EcfLineRetention? retention = null;
        if (line.Retention is { } r)
        {
            if (TryEnum<RetentionAgent>(r.Agent) is not { } agent)
                return Bad($"Agente de retención desconocido: {r.Agent}.");
            retention = new EcfLineRetention(agent, r.ItbisWithheld, r.IsrWithheld);
        }

        IReadOnlyList<EcfAdditionalTax>? detail = line.AdditionalTaxDetail
            ?.Select(t => new EcfAdditionalTax(t.Code, t.Rate, t.IscEspecifico, t.IscAdvalorem, t.Otros))
            .ToList();

        return new EcfLine(
            Number: line.Number ?? fallbackNumber,
            Rate: rate,
            Name: line.Name,
            Kind: kind,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            Description: line.Description,
            UnitOfMeasure: line.UnitOfMeasure,
            Discount: line.Discount,
            Surcharge: line.Surcharge,
            AdditionalTaxes: line.AdditionalTaxes,
            Retention: retention,
            AdditionalTaxDetail: detail);
    }

    private static ErrorOr<List<EcfGlobalAdjustment>> MapAdjustments(IReadOnlyList<PreviewAdjustment>? adjustments)
    {
        var mapped = new List<EcfGlobalAdjustment>();
        foreach (var a in adjustments ?? [])
        {
            if (TryEnum<AdjustmentKind>(a.Kind) is not { } kind)
                return Bad($"Tipo de ajuste desconocido: {a.Kind}.");
            if (TryEnum<ItbisRate>(a.AffectsRate) is not { } affects)
                return Bad($"Indicador de facturación del ajuste desconocido: {a.AffectsRate}.");
            mapped.Add(new EcfGlobalAdjustment(a.Line, kind, affects, a.Amount, a.Norma1007, a.Description, a.Percentage));
        }

        return mapped;
    }

    private static ErrorOr<EcfForeignCurrency?> MapForeignCurrency(PreviewForeignCurrency? fx)
    {
        if (fx is null)
            return (EcfForeignCurrency?)null;

        var currency = CurrencyCode.GetAll().FirstOrDefault(c => string.Equals(c.Name, fx.Currency, StringComparison.OrdinalIgnoreCase));
        if (currency is null)
            return Bad($"Moneda desconocida: {fx.Currency}.");

        var t = fx.Totals;
        return new EcfForeignCurrency(currency, fx.ExchangeRate, new EcfForeignCurrencyTotals(
            t.MontoGravadoTotal, t.MontoGravadoI1, t.MontoGravadoI2, t.MontoGravadoI3, t.MontoExento,
            t.TotalItbis, t.TotalItbis1, t.TotalItbis2, t.TotalItbis3, t.MontoTotal));
    }

    private static ErrorOr<EcfReference?> MapReference(PreviewReference? reference)
    {
        if (reference is null)
            return (EcfReference?)null;

        if (TryEnum<ModificationCode>(reference.Code) is not { } code)
            return Bad($"Código de modificación desconocido: {reference.Code}.");

        return new EcfReference(reference.ModifiedNcf, reference.ModifiedNcfDate, code, reference.OtherIssuerRnc);
    }

    private static T? TryEnum<T>(int value) where T : Enumeration<T> =>
        Enumeration<T>.GetAll().FirstOrDefault(item => item.Id == value);

    private static Error Bad(string message) => Error.Validation("EcfPreview.BadRequest", message);
}
