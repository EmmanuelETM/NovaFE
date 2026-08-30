using ErrorOr;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// Errores de negocio al armar el documento e-CF. <c>code</c> en inglés;
/// descripción en español.
/// </summary>
public static class EcfErrors
{
    public static Error NoLines => Error.Validation(
        code: "Ecf.NoLines",
        description: "El comprobante no tiene líneas de detalle.");

    public static Error TooManyLines(int count, int max) => Error.Validation(
        code: "Ecf.TooManyLines",
        description: $"El comprobante tiene {count} líneas; el máximo para este tipo es {max}.");

    public static Error NonContiguousLineNumbers => Error.Validation(
        code: "Ecf.NonContiguousLineNumbers",
        description: "Los números de línea deben ir de 1 a N, sin saltos ni repetidos.");

    public static Error EncfTypeMismatch(int encfType, int documentType) => Error.Validation(
        code: "Ecf.EncfTypeMismatch",
        description: $"El e-NCF es del tipo {encfType} pero el comprobante es del tipo {documentType}.");

    public static Error SequenceExpiryRequired => Error.Validation(
        code: "Ecf.SequenceExpiryRequired",
        description: "Este tipo de comprobante necesita fecha de vencimiento de secuencia.");

    public static Error SequenceExpiryNotApplicable => Error.Validation(
        code: "Ecf.SequenceExpiryNotApplicable",
        description: "Los tipos 32 y 34 no llevan fecha de vencimiento de secuencia.");

    public static Error ReferenceRequired(int documentType) => Error.Validation(
        code: "Ecf.ReferenceRequired",
        description: $"El tipo {documentType} (Nota de Crédito/Débito) necesita la sección de referencia al comprobante modificado.");

    public static Error ReferenceNotApplicable(int documentType) => Error.Validation(
        code: "Ecf.ReferenceNotApplicable",
        description: $"El tipo {documentType} no lleva sección de referencia.");

    public static Error BuyerIdentificationRequired(int documentType) => Error.Validation(
        code: "Ecf.BuyerIdentificationRequired",
        description: documentType switch
        {
            32 => "La Factura de Consumo con monto total ≥ DOP 250,000 debe identificar al comprador (RNC/cédula, o Identificador Extranjero si es extranjero).",
            33 or 34 => "La Nota de Crédito/Débito debe identificar al comprador porque su monto llega a DOP 250,000 o modifica un comprobante que lo identifica.",
            _ => $"El tipo {documentType} necesita el RNC o cédula del comprador.",
        });

    public static Error IncomeTypeRequired => Error.Validation(
        code: "Ecf.IncomeTypeRequired",
        description: "El tipo de ingresos es obligatorio para este comprobante.");

    public static Error RetentionRequired(int lineNumber) => Error.Validation(
        code: "Ecf.RetentionRequired",
        description: $"La línea {lineNumber} del Comprobante de Compras (tipo 41) necesita el área de retención (agente de retención o percepción).");

    public static Error RetentionNotApplicable(int documentType) => Error.Validation(
        code: "Ecf.RetentionNotApplicable",
        description: $"El tipo {documentType} no lleva área de retención en las líneas de detalle.");

    public static Error OnlyExemptLinesAllowed(int documentType) => Error.Validation(
        code: "Ecf.OnlyExemptLinesAllowed",
        description: $"El tipo {documentType} solo admite líneas exentas de ITBIS.");

    public static Error OnlyZeroRatedLinesAllowed(int documentType) => Error.Validation(
        code: "Ecf.OnlyZeroRatedLinesAllowed",
        description: $"El tipo {documentType} (Exportaciones) solo admite líneas con ITBIS a tasa cero (indicador de facturación 3).");

    public static Error LineAdjustmentsNotApplicable(int documentType) => Error.Validation(
        code: "Ecf.LineAdjustmentsNotApplicable",
        description: $"Las líneas del tipo {documentType} no admiten descuentos, recargos ni otros impuestos adicionales.");

    public static Error ItbisRetentionNotApplicable(int documentType) => Error.Validation(
        code: "Ecf.ItbisRetentionNotApplicable",
        description: $"El tipo {documentType} solo retiene ISR; el área de retención no lleva monto de ITBIS.");

    public static Error BlockNotApplicable(string block, int documentType) => Error.Validation(
        code: "Ecf.BlockNotApplicable",
        description: $"El tipo {documentType} no admite el bloque {block}.");

    public static Error ExportFieldsOnlyForExports(string block) => Error.Validation(
        code: "Ecf.ExportFieldsOnlyForExports",
        description: $"Los campos de exportación del bloque {block} solo aplican al tipo 46 (Exportaciones).");

    public static Error TransportForPagosExteriorIsDestinationOnly => Error.Validation(
        code: "Ecf.TransportForPagosExteriorIsDestinationOnly",
        description: "El tipo 47 (Pagos al Exterior) solo admite el país de destino en el bloque Transporte.");

    public static Error InvalidExchangeRate => Error.Validation(
        code: "Ecf.InvalidExchangeRate",
        description: "El tipo de cambio debe ser mayor que cero.");

    public static Error LineForeignCurrencyWithoutHeader => Error.Validation(
        code: "Ecf.LineForeignCurrencyWithoutHeader",
        description: "Una línea trae montos en otra moneda pero el encabezado no declara el bloque OtraMoneda.");

    public static Error TooManyGlobalAdjustments => Error.Validation(
        code: "Ecf.TooManyGlobalAdjustments",
        description: "La Sección D admite hasta 20 descuentos o recargos globales.");

    public static Error NonContiguousGlobalAdjustmentLines => Error.Validation(
        code: "Ecf.NonContiguousGlobalAdjustmentLines",
        description: "Los números de línea de la Sección D deben ir de 1 a N, sin saltos ni repetidos.");

    public static Error Norma1007NotApplicable(int documentType) => Error.Validation(
        code: "Ecf.Norma1007NotApplicable",
        description: $"El Indicador Norma 10-07 solo aplica en el tipo {documentType} para descuentos globales sobre la tasa 1 (18 %) — y solo en los tipos 31/32/33/34/45.");

    public static Error InvalidAdditionalTaxCode(int lineNumber) => Error.Validation(
        code: "Ecf.InvalidAdditionalTaxCode",
        description: $"La línea {lineNumber} trae un impuesto adicional con código fuera de la Tabla I (001–039) o con tasa ≤ 0.");

    public static Error AdditionalTaxDetailMismatch(int lineNumber) => Error.Validation(
        code: "Ecf.AdditionalTaxDetailMismatch",
        description: $"El desglose de impuestos adicionales de la línea {lineNumber} no coincide con el monto agregado.");

    public static Error TooManySubtotals => Error.Validation(
        code: "Ecf.TooManySubtotals",
        description: "La sección Subtotales admite hasta 20 filas.");

    public static Error TooManyPages => Error.Validation(
        code: "Ecf.TooManyPages",
        description: "La sección Paginación admite hasta 1000 páginas.");

    public static Error NonInvoiceableAmountNotApplicable(int documentType) => Error.Validation(
        code: "Ecf.NonInvoiceableAmountNotApplicable",
        description: $"El tipo {documentType} no admite monto no facturable.");

    public static Error CreditNoteDueDateInThePast => Error.Validation(
        code: "Ecf.CreditNoteDueDateInThePast",
        description: "La fecha del comprobante modificado no puede ser posterior a la emisión de la nota.");
}
