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

    public static Error CreditNoteDueDateInThePast => Error.Validation(
        code: "Ecf.CreditNoteDueDateInThePast",
        description: "La fecha del comprobante modificado no puede ser posterior a la emisión de la nota.");
}
