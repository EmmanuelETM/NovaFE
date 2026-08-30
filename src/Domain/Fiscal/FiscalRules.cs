using ErrorOr;

namespace NovaFE.Domain.Fiscal;

/// <summary>
/// Reglas fiscales sueltas que no son cálculo pero sí dominio: se devuelven, no
/// se lanzan.
/// </summary>
public static class FiscalRules
{
    /// <summary>
    /// El <c>&lt;MontoTotal&gt;</c> de una Nota de Crédito (tipo 34) no puede
    /// superar al del comprobante que modifica (Formato e-CF / RF-02.10). La
    /// comparación es a escala de dinero.
    /// </summary>
    public static ErrorOr<Success> CreditNoteTotalWithinOriginal(decimal creditNoteTotal, decimal originalTotal)
    {
        var creditNote = EcfRounding.Money(creditNoteTotal);
        var original = EcfRounding.Money(originalTotal);

        return creditNote > original
            ? FiscalErrors.CreditNoteTotalExceedsOriginal(creditNote, original)
            : Result.Success;
    }
}
