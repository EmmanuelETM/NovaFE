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
        int? creditNoteIndicator)
    {
        Type = type;
        Header = header;
        Lines = lines;
        Reference = reference;
        Calculation = calculation;
        CreditNoteIndicator = creditNoteIndicator;
    }

    public EcfType Type { get; }

    public EcfHeader Header { get; }

    public IReadOnlyList<EcfLine> Lines { get; }

    public EcfReference? Reference { get; }

    /// <summary>Totales del encabezado y montos por línea, calculados por Módulo 6.</summary>
    public EcfCalculationResult Calculation { get; }

    /// <summary><c>&lt;IndicadorNotaCredito&gt;</c> (0 / 1) para el tipo 34; null para el resto.</summary>
    public int? CreditNoteIndicator { get; }

    public EcfTotals Totals => Calculation.Totals;

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
            header.NonInvoiceableAmount);
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

        return new EcfDocument(type, header, lines, reference, calculation.Value, creditNoteIndicator);
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

        return errors;
    }

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

    private static EcfLineInput ToLineInput(EcfLine line, bool headerPricesIncludeTax) =>
        new(
            LineNumber: line.Number,
            Rate: line.Rate,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            Discount: line.Discount,
            Surcharge: line.Surcharge,
            PriceIncludesTax: line.PriceIncludesTax ?? headerPricesIncludeTax,
            AdditionalTaxes: line.AdditionalTaxes,
            ItbisWithheld: line.Retention?.ItbisWithheld ?? 0m,
            IsrWithheld: line.Retention?.IsrWithheld ?? 0m);
}
