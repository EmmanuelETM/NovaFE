using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Fiscal;
using NovaFE.Domain.Sequences;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// Un comprobante fiscal electrónico listo para serializar a XML (Módulo 2).
/// Se construye con <see cref="Create"/>, que valida la estructura y calcula
/// todos los totales con el motor fiscal (Módulo 6). Un <see cref="EcfDocument"/>
/// construido siempre está cuadrado.
/// <para>
/// No incluye <c>&lt;FechaHoraFirma&gt;</c> ni <c>&lt;Signature&gt;</c>: los agrega
/// el serializador con la hora de firma y Módulo 3.
/// </para>
/// </summary>
public sealed class EcfDocument
{
    /// <summary>Máximo de líneas de detalle (1000; 10 000 para el tipo 32 &lt; DOP 250 k).</summary>
    public const int MaxLines = 1000;

    /// <summary>
    /// Umbral de la DGII (DOP) que separa la Factura de Consumo de "bajo monto".
    /// Manda la identificación del comprador (tipo 32 y las NC/ND que modifican un
    /// 32 ≥ este monto) y el ruteo a RFCE.
    /// </summary>
    public const decimal ConsumerIdentificationThreshold = 250_000m;

    private EcfDocument(
        EcfType type,
        EcfHeader header,
        IReadOnlyList<EcfLine> lines,
        EcfReference? reference,
        EcfCalculationResult calculation,
        int? creditNoteIndicator,
        EcfForeignCurrencyCheck? foreignCurrencyCheck)
    {
        Type = type;
        Header = header;
        Lines = lines;
        Reference = reference;
        Calculation = calculation;
        CreditNoteIndicator = creditNoteIndicator;
        ForeignCurrencyCheck = foreignCurrencyCheck;
    }

    public EcfType Type { get; }

    public EcfHeader Header { get; }

    public IReadOnlyList<EcfLine> Lines { get; }

    public EcfReference? Reference { get; }

    /// <summary>Totales del encabezado y montos por línea, calculados por Módulo 6.</summary>
    public EcfCalculationResult Calculation { get; }

    /// <summary><c>&lt;IndicadorNotaCredito&gt;</c> (0 / 1) para el tipo 34; null para el resto.</summary>
    public int? CreditNoteIndicator { get; }

    /// <summary>
    /// Cross-check de <c>&lt;OtraMoneda&gt;</c> (<c>MontoTotal</c> declarado en divisa
    /// vs. <c>MontoTotal_DOP / TipoCambio</c>). Null si el comprobante no lleva divisa.
    /// Informativo — no bloquea la emisión.
    /// </summary>
    public EcfForeignCurrencyCheck? ForeignCurrencyCheck { get; }

    public EcfTotals Totals => Calculation.Totals;

    /// <summary>
    /// El tipo 32 con <c>MontoTotal &lt; DOP 250 000</c> se envía a la DGII como
    /// <b>RFCE</b> (resumen), no como <c>&lt;ECF&gt;</c> completo (RF-02.6). El
    /// <c>&lt;ECF&gt;</c> igual se genera y se guarda localmente.
    /// </summary>
    public bool QualifiesForRfce =>
        Type == EcfType.Consumo && Totals.MontoTotal < ConsumerIdentificationThreshold;

    public static ErrorOr<EcfDocument> Create(
        EcfType type,
        EcfHeader header,
        IReadOnlyList<EcfLine> lines,
        EcfReference? reference = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(lines);

        var structure = ValidateStructure(type, header, lines, reference);
        if (structure.Count > 0)
            return structure;

        var calculation = EcfCalculator.Calculate(
            [.. lines.Select(line => ToLineInput(line, header.PricesIncludeTax))],
            header.NonInvoiceableAmount,
            header.GlobalAdjustments is { } adjustments
                ? [.. adjustments.Select(ToAdjustmentInput)]
                : null);
        if (calculation.IsError)
            return calculation.Errors;

        // La identificación del comprador depende del monto total (tipos 32/33/34),
        // así que se valida después de calcular.
        if (RequiresBuyerIdentification(type, reference, calculation.Value.Totals.MontoTotal)
            && header.Buyer.Rnc is null
            && string.IsNullOrWhiteSpace(header.Buyer.ForeignId))
        {
            return EcfErrors.BuyerIdentificationRequired(type.Id);
        }

        int? creditNoteIndicator = null;
        if (type == EcfType.NotaCredito && reference is not null)
        {
            var indicator = Fiscal.CreditNoteIndicator.For(reference.ModifiedNcfDate, header.IssueDate);
            if (indicator.IsError)
                return EcfErrors.CreditNoteDueDateInThePast;

            creditNoteIndicator = indicator.Value.Value;
        }

        var fxCheck = CheckForeignCurrency(header.ForeignCurrency, calculation.Value.Totals.MontoTotal, lines.Count);

        return new EcfDocument(
            type, header, lines, reference, calculation.Value, creditNoteIndicator, fxCheck);
    }

    /// <summary>
    /// Compara el <c>MontoTotal</c> declarado en divisa con
    /// <c>MontoTotal_DOP / TipoCambio</c>. Tolerancia: 1 unidad de divisa por línea
    /// (misma filosofía que la cuadratura — nunca rechaza).
    /// </summary>
    private static EcfForeignCurrencyCheck? CheckForeignCurrency(
        EcfForeignCurrency? fx, decimal montoTotalDop, int lineCount)
    {
        if (fx is null || fx.Totals.MontoTotal is not { } declared)
            return null;

        var expected = decimal.Round(montoTotalDop / fx.ExchangeRate, 2, MidpointRounding.AwayFromZero);
        var difference = Math.Abs(expected - declared);
        return new EcfForeignCurrencyCheck(expected, declared, difference, difference <= Math.Max(1m, lineCount));
    }

    private static List<Error> ValidateStructure(
        EcfType type,
        EcfHeader header,
        IReadOnlyList<EcfLine> lines,
        EcfReference? reference)
    {
        var errors = new List<Error>();

        if (header.Encf.TypeCode != type.Id)
            errors.Add(EcfErrors.EncfTypeMismatch(header.Encf.TypeCode, type.Id));

        if (lines.Count == 0)
            errors.Add(EcfErrors.NoLines);
        else if (lines.Count > MaxLines)
            errors.Add(EcfErrors.TooManyLines(lines.Count, MaxLines));
        else if (!AreContiguous(lines))
            errors.Add(EcfErrors.NonContiguousLineNumbers);

        if (type.HasSequenceExpiry && header.SequenceExpiresOn is null)
            errors.Add(EcfErrors.SequenceExpiryRequired);
        if (!type.HasSequenceExpiry && header.SequenceExpiresOn is not null)
            errors.Add(EcfErrors.SequenceExpiryNotApplicable);

        var needsReference = type == EcfType.NotaCredito || type == EcfType.NotaDebito;
        if (needsReference && reference is null)
            errors.Add(EcfErrors.ReferenceRequired(type.Id));

        if (RequiresIncomeType(type) && string.IsNullOrWhiteSpace(header.IncomeType))
            errors.Add(EcfErrors.IncomeTypeRequired);

        // Regímenes Especiales (44), Gastos Menores (43) y Pagos al Exterior (47):
        // sus XSD no tienen campos gravados ni de ITBIS en <Totales> — todo va a
        // <MontoExento> (Formato nota 50).
        if ((type == EcfType.RegimenesEspeciales
                || type == EcfType.GastosMenores
                || type == EcfType.PagosExterior)
            && lines.Any(line => !line.Rate.IsExempt))
            errors.Add(EcfErrors.OnlyExemptLinesAllowed(type.Id));

        // Exportaciones (46): toda línea va a tasa 0 % (Formato nota 51); su
        // <Totales> solo tiene el bucket I3.
        if (type == EcfType.Exportaciones && lines.Any(line => line.Rate != ItbisRate.Zero))
            errors.Add(EcfErrors.OnlyZeroRatedLinesAllowed(type.Id));

        ValidateSimpleLineDocument(type, header, lines, errors);
        ValidateRetention(type, lines, errors);
        ValidateTransversalBlocks(type, header, lines, errors);

        return errors;
    }

    /// <summary>
    /// Obligatoriedad de los bloques transversales del encabezado:
    /// <list type="bullet">
    ///   <item><c>&lt;InformacionesAdicionales&gt;</c>: opcional en 31/32/33/34/44/45/46; no aplica a 41/43/47.</item>
    ///   <item><c>&lt;Transporte&gt;</c>: opcional en 31/32/33/34/44/45/46/47; no aplica a 41/43. El 47 solo admite <c>PaisDestino</c>.</item>
    ///   <item>Los campos de exportación (FOB/CIF, vía, país, transportista) son solo del tipo 46.</item>
    /// </list>
    /// </summary>
    private static void ValidateTransversalBlocks(
        EcfType type, EcfHeader header, IReadOnlyList<EcfLine> lines, List<Error> errors)
    {
        var isExport = type == EcfType.Exportaciones;

        if (header.ForeignCurrency is { } fx)
        {
            if (fx.ExchangeRate <= 0m)
                errors.Add(EcfErrors.InvalidExchangeRate);
        }
        else if (lines.Any(line => line.ForeignCurrency is not null))
        {
            errors.Add(EcfErrors.LineForeignCurrencyWithoutHeader);
        }

        if (header.GlobalAdjustments is { Count: > 0 } adjustments)
            ValidateGlobalAdjustments(type, adjustments, errors);

        ValidateAdditionalTaxDetail(type, lines, errors);

        if (header.Subtotals is { Count: > 20 })
            errors.Add(EcfErrors.TooManySubtotals);
        if (header.Pagination is { Count: > 1000 })
            errors.Add(EcfErrors.TooManyPages);

        if (header.Shipping is { } shipping)
        {
            if (type == EcfType.Compras || type == EcfType.GastosMenores || type == EcfType.PagosExterior)
                errors.Add(EcfErrors.BlockNotApplicable("InformacionesAdicionales", type.Id));
            else if (shipping.Export is not null && !isExport)
                errors.Add(EcfErrors.ExportFieldsOnlyForExports("InformacionesAdicionales"));
        }

        if (header.Transport is { } transport)
        {
            if (type == EcfType.Compras || type == EcfType.GastosMenores)
            {
                errors.Add(EcfErrors.BlockNotApplicable("Transporte", type.Id));
            }
            else if (!isExport && HasExportTransportFields(transport))
            {
                errors.Add(EcfErrors.ExportFieldsOnlyForExports("Transporte"));
            }
            else if (type == EcfType.PagosExterior && HasNonDestinationTransportFields(transport))
            {
                errors.Add(EcfErrors.TransportForPagosExteriorIsDestinationOnly);
            }
        }
    }

    /// <summary>
    /// Sección D (<c>&lt;DescuentosORecargos&gt;</c>): no aplica a 43/47; hasta 20
    /// líneas con <c>NumeroLinea</c> secuencial; el <c>IndicadorNorma1007</c> solo en
    /// 31/32/33/34/45 y solo para descuentos a la tasa 1.
    /// </summary>
    private static void ValidateGlobalAdjustments(
        EcfType type, IReadOnlyList<EcfGlobalAdjustment> adjustments, List<Error> errors)
    {
        if (type == EcfType.GastosMenores || type == EcfType.PagosExterior)
        {
            errors.Add(EcfErrors.BlockNotApplicable("DescuentosORecargos", type.Id));
            return;
        }

        if (adjustments.Count > 20)
            errors.Add(EcfErrors.TooManyGlobalAdjustments);

        var expected = 1;
        foreach (var line in adjustments.Select(a => a.Line).OrderBy(n => n))
        {
            if (line != expected++)
            {
                errors.Add(EcfErrors.NonContiguousGlobalAdjustmentLines);
                break;
            }
        }

        var norma1007Allowed = type == EcfType.CreditoFiscal
            || type == EcfType.Consumo
            || type == EcfType.NotaDebito
            || type == EcfType.NotaCredito
            || type == EcfType.Gubernamental;

        foreach (var adj in adjustments.Where(a => a.Norma1007))
        {
            if (!norma1007Allowed
                || adj.Kind != AdjustmentKind.Discount
                || adj.AffectsRate != ItbisRate.Eighteen)
            {
                errors.Add(EcfErrors.Norma1007NotApplicable(type.Id));
                break;
            }
        }
    }

    /// <summary>
    /// El desglose <c>&lt;ImpuestosAdicionales&gt;</c> solo existe en el XSD de
    /// 31/32/33/34/44/45. Los códigos deben ser de la Tabla I y el desglose debe
    /// cuadrar con el <c>AdditionalTaxes</c> agregado de la línea.
    /// </summary>
    private static void ValidateAdditionalTaxDetail(
        EcfType type, IReadOnlyList<EcfLine> lines, List<Error> errors)
    {
        var anyDetail = lines.Any(line => line.AdditionalTaxDetail is { Count: > 0 });
        if (!anyDetail)
            return;

        var supported = type == EcfType.CreditoFiscal
            || type == EcfType.Consumo
            || type == EcfType.NotaDebito
            || type == EcfType.NotaCredito
            || type == EcfType.RegimenesEspeciales
            || type == EcfType.Gubernamental;

        if (!supported)
        {
            errors.Add(EcfErrors.BlockNotApplicable("ImpuestosAdicionales", type.Id));
            return;
        }

        foreach (var line in lines.Where(l => l.AdditionalTaxDetail is { Count: > 0 }))
        {
            if (line.AdditionalTaxDetail!.Any(tax => !EcfAdditionalTax.IsValidCode(tax.Code) || tax.Rate <= 0m))
                errors.Add(EcfErrors.InvalidAdditionalTaxCode(line.Number));

            var detailSum = line.AdditionalTaxDetail!.Sum(tax => tax.Amount);
            if (line.AdditionalTaxes > 0m && Math.Abs(line.AdditionalTaxes - detailSum) > 1m)
                errors.Add(EcfErrors.AdditionalTaxDetailMismatch(line.Number));
        }
    }

    private static bool HasExportTransportFields(EcfTransport t) =>
        t.Via is not null
        || !string.IsNullOrWhiteSpace(t.OriginCountry)
        || !string.IsNullOrWhiteSpace(t.DestinationAddress)
        || !string.IsNullOrWhiteSpace(t.CarrierRnc)
        || !string.IsNullOrWhiteSpace(t.CarrierName)
        || !string.IsNullOrWhiteSpace(t.VoyageNumber);

    private static bool HasNonDestinationTransportFields(EcfTransport t) =>
        !string.IsNullOrWhiteSpace(t.Driver)
        || !string.IsNullOrWhiteSpace(t.TransportDocument)
        || !string.IsNullOrWhiteSpace(t.VehicleId)
        || !string.IsNullOrWhiteSpace(t.Plate)
        || !string.IsNullOrWhiteSpace(t.Route)
        || !string.IsNullOrWhiteSpace(t.Zone)
        || !string.IsNullOrWhiteSpace(t.DeliveryNote)
        || HasExportTransportFields(t);

    /// <summary>
    /// Los tipos 43 (Gastos Menores) y 47 (Pagos al Exterior) son reducidos: sus
    /// líneas no admiten descuentos, recargos ni otros impuestos, y el encabezado
    /// no lleva monto no facturable (nada de eso existe en sus XSD).
    /// </summary>
    private static void ValidateSimpleLineDocument(
        EcfType type, EcfHeader header, IReadOnlyList<EcfLine> lines, List<Error> errors)
    {
        if (type != EcfType.GastosMenores && type != EcfType.PagosExterior)
            return;

        if (lines.Any(line => line.Discount > 0m || line.Surcharge > 0m || line.AdditionalTaxes > 0m))
            errors.Add(EcfErrors.LineAdjustmentsNotApplicable(type.Id));

        if (header.NonInvoiceableAmount != 0m)
            errors.Add(EcfErrors.NonInvoiceableAmountNotApplicable(type.Id));
    }

    /// <summary>
    /// Los tipos 41 (Compras) y 47 (Pagos al Exterior) exigen el área de retención
    /// en cada línea; el 47 solo retiene ISR (su <c>&lt;Retencion&gt;</c> no tiene
    /// campo de ITBIS). Los demás tipos soportados en v1 no la llevan.
    /// </summary>
    private static void ValidateRetention(EcfType type, IReadOnlyList<EcfLine> lines, List<Error> errors)
    {
        if (type == EcfType.Compras || type == EcfType.PagosExterior)
        {
            foreach (var line in lines.Where(line => line.Retention is null))
                errors.Add(EcfErrors.RetentionRequired(line.Number));

            if (type == EcfType.PagosExterior
                && lines.Any(line => line.Retention is { ItbisWithheld: > 0m }))
                errors.Add(EcfErrors.ItbisRetentionNotApplicable(type.Id));
        }
        else if (lines.Any(line => line.Retention is not null))
        {
            errors.Add(EcfErrors.RetentionNotApplicable(type.Id));
        }
    }

    private static bool AreContiguous(IReadOnlyList<EcfLine> lines)
    {
        var expected = 1;
        foreach (var number in lines.Select(line => line.Number).OrderBy(number => number))
        {
            if (number != expected)
                return false;
            expected++;
        }

        return true;
    }

    /// <summary>
    /// Si el comprobante debe identificar al comprador (RNC/cédula o Identificador
    /// Extranjero). Reglas de la DGII:
    /// <list type="bullet">
    ///   <item>31, 41, 44, 45: siempre (estructural, <c>RNCComprador</c> es obligatorio en su XSD).</item>
    ///   <item>32: solo si el monto total ≥ <see cref="ConsumerIdentificationThreshold"/>
    ///   (si el comprador es extranjero, va por Identificador Extranjero).</item>
    ///   <item>33/34: si su propio monto ya llega al umbral, o si modifican un e-CF de un
    ///   tipo que identifica al comprador. Si modifican un 32 de monto desconocido, lo
    ///   decide la capa de aplicación con el e-CF original a la vista.</item>
    ///   <item>43, 46, 47: no lo valida el dominio todavía (se completa al agregar el tipo).</item>
    /// </list>
    /// </summary>
    private static bool RequiresBuyerIdentification(EcfType type, EcfReference? reference, decimal montoTotal)
    {
        if (type == EcfType.CreditoFiscal
            || type == EcfType.Compras
            || type == EcfType.RegimenesEspeciales
            || type == EcfType.Gubernamental)
            return true;

        if (type == EcfType.Consumo)
            return montoTotal >= ConsumerIdentificationThreshold;

        if (type == EcfType.NotaCredito || type == EcfType.NotaDebito)
        {
            if (montoTotal >= ConsumerIdentificationThreshold)
                return true;

            if (reference is not null
                && Encf.Create(reference.ModifiedNcf) is { IsError: false } modified)
                return IdentifiesBuyer(modified.Value.Type);

            return false;
        }

        return false;
    }

    private static bool IdentifiesBuyer(EcfType modifiedType) =>
        modifiedType == EcfType.CreditoFiscal
        || modifiedType == EcfType.NotaDebito
        || modifiedType == EcfType.NotaCredito
        || modifiedType == EcfType.Compras
        || modifiedType == EcfType.RegimenesEspeciales
        || modifiedType == EcfType.Gubernamental
        || modifiedType == EcfType.PagosExterior;

    // Los tipos 41 (Compras) y 47 (Pagos al Exterior) NO llevan <TipoIngresos> —
    // sus XSD ni siquiera lo admiten.
    private static bool RequiresIncomeType(EcfType type) =>
        type == EcfType.CreditoFiscal
        || type == EcfType.Consumo
        || type == EcfType.NotaDebito
        || type == EcfType.NotaCredito
        || type == EcfType.RegimenesEspeciales
        || type == EcfType.Gubernamental
        || type == EcfType.Exportaciones;

    private static EcfGlobalAdjustmentInput ToAdjustmentInput(EcfGlobalAdjustment adj) =>
        new(
            IsDiscount: adj.Kind == AdjustmentKind.Discount,
            AffectsRate: adj.AffectsRate,
            Amount: adj.Amount,
            Norma1007: adj.Norma1007);

    private static EcfLineInput ToLineInput(EcfLine line, bool headerPricesIncludeTax) =>
        new(
            LineNumber: line.Number,
            Rate: line.Rate,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            Discount: line.Discount,
            Surcharge: line.Surcharge,
            PriceIncludesTax: line.PriceIncludesTax ?? headerPricesIncludeTax,
            // Si viene el desglose por código, es la fuente de verdad del monto.
            AdditionalTaxes: line.AdditionalTaxDetail is { Count: > 0 } detail
                ? detail.Sum(tax => tax.Amount)
                : line.AdditionalTaxes,
            SuppliedLineAmount: line.DeclaredAmount,
            ItbisWithheld: line.Retention?.ItbisWithheld ?? 0m,
            IsrWithheld: line.Retention?.IsrWithheld ?? 0m);
}
