using ErrorOr;

namespace NovaFE.Domain.Fiscal;

/// <summary>
/// Errores del motor de cálculo fiscal. <c>code</c> en inglés (estable);
/// descripción en español (la consume quien llama a la API).
/// </summary>
public static class FiscalErrors
{
    public static Error NoLines => Error.Validation(
        code: "Fiscal.NoLines",
        description: "El comprobante no tiene líneas de detalle.");

    public static Error InvalidLineNumber(int lineNumber) => Error.Validation(
        code: "Fiscal.InvalidLineNumber",
        description: $"El número de línea '{lineNumber}' no es válido: debe ser mayor o igual a 1.");

    public static Error DuplicateLineNumber(int lineNumber) => Error.Validation(
        code: "Fiscal.DuplicateLineNumber",
        description: $"El número de línea '{lineNumber}' está repetido.");

    public static Error NegativeQuantity(int lineNumber) => Error.Validation(
        code: "Fiscal.NegativeQuantity",
        description: $"La cantidad de la línea {lineNumber} no puede ser negativa.");

    public static Error NegativeUnitPrice(int lineNumber) => Error.Validation(
        code: "Fiscal.NegativeUnitPrice",
        description: $"El precio unitario de la línea {lineNumber} no puede ser negativo.");

    public static Error NegativeAdjustment(int lineNumber) => Error.Validation(
        code: "Fiscal.NegativeAdjustment",
        description: $"El descuento, el recargo y los impuestos adicionales de la línea {lineNumber} no pueden ser negativos.");

    public static Error NegativeGlobalAdjustment => Error.Validation(
        code: "Fiscal.NegativeGlobalAdjustment",
        description: "El monto de un descuento o recargo global no puede ser negativo.");

    public static Error GlobalAdjustmentExceedsBucket => Error.Validation(
        code: "Fiscal.GlobalAdjustmentExceedsBucket",
        description: "Un descuento global supera el monto gravado o exento al que se aplica.");

    public static Error NegativeRetention(int lineNumber) => Error.Validation(
        code: "Fiscal.NegativeRetention",
        description: $"El ITBIS o el ISR retenido de la línea {lineNumber} no puede ser negativo.");

    public static Error NegativeLineAmount(int lineNumber) => Error.Validation(
        code: "Fiscal.NegativeLineAmount",
        description: $"El monto de la línea {lineNumber} da negativo: el descuento no puede superar al valor de la línea.");

    public static Error CreditNoteBeforeOriginal => Error.Validation(
        code: "Fiscal.CreditNoteBeforeOriginal",
        description: "La fecha de la nota de crédito no puede ser anterior a la del comprobante que modifica.");

    public static Error CreditNoteTotalExceedsOriginal(decimal creditNoteTotal, decimal originalTotal) => Error.Validation(
        code: "Fiscal.CreditNoteTotalExceedsOriginal",
        description: $"El monto total de la nota de crédito ({creditNoteTotal}) no puede superar al del comprobante modificado ({originalTotal}).");
}
