using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Fiscal;

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

        if (RequiresBuyerRnc(type) && header.Buyer.Rnc is null && header.Buyer.ForeignId is null)
            errors.Add(EcfErrors.BuyerRncRequired(type.Id));

        if (RequiresIncomeType(type) && string.IsNullOrWhiteSpace(header.IncomeType))
            errors.Add(EcfErrors.IncomeTypeRequired);

        return errors;
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

    // Obligatoriedad del RNC comprador — subconjunto para v1 (tipo 31). El resto
    // se completa al agregar cada tipo.
    private static bool RequiresBuyerRnc(EcfType type) =>
        type == EcfType.CreditoFiscal
        || type == EcfType.NotaDebito
        || type == EcfType.NotaCredito
        || type == EcfType.Compras
        || type == EcfType.RegimenesEspeciales
        || type == EcfType.Gubernamental;

    private static bool RequiresIncomeType(EcfType type) =>
        type == EcfType.CreditoFiscal
        || type == EcfType.NotaDebito
        || type == EcfType.NotaCredito
        || type == EcfType.Compras
        || type == EcfType.Gubernamental
        || type == EcfType.Exportaciones
        || type == EcfType.PagosExterior;

    private static EcfLineInput ToLineInput(EcfLine line, bool headerPricesIncludeTax) =>
        new(
            LineNumber: line.Number,
            Rate: line.Rate,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            Discount: line.Discount,
            Surcharge: line.Surcharge,
            PriceIncludesTax: line.PriceIncludesTax ?? headerPricesIncludeTax,
            AdditionalTaxes: line.AdditionalTaxes);
}
