using ErrorOr;
using NovaFE.Domain.Common;

namespace NovaFE.Domain.Fiscal;

/// <summary>
/// <c>&lt;IndicadorNotaCredito&gt;</c> — solo en la Nota de Crédito electrónica
/// (tipo 34). Regla de los 30 días (Formato e-CF v1.0, oct 2025):
/// <list type="bullet">
///   <item><b>0</b> — la NC se emite dentro de los 30 días de la fecha de emisión
///   del comprobante que modifica; el comprador conserva el derecho a la
///   devolución del ITBIS.</item>
///   <item><b>1</b> — se emite después de los 30 días; sin derecho a devolución.</item>
/// </list>
/// <para>
/// ⚠️ El borrador del Plan Técnico decía 1 / 2; el valor correcto verificado
/// contra el Formato e-CF y el contexto de DGII es <b>0 / 1</b>.
/// </para>
/// </summary>
public sealed record CreditNoteIndicator(int Value, string Name)
    : Enumeration<CreditNoteIndicator>(Value, Name)
{
    /// <summary>Ventana de 30 días para la regla, contada en días de calendario.</summary>
    public const int WindowDays = 30;

    /// <summary>0 — dentro de los 30 días; conserva el derecho a devolución de ITBIS.</summary>
    public static readonly CreditNoteIndicator WithinThirtyDays = new(0, nameof(WithinThirtyDays));

    /// <summary>1 — después de los 30 días.</summary>
    public static readonly CreditNoteIndicator AfterThirtyDays = new(1, nameof(AfterThirtyDays));

    /// <summary>
    /// Determina el indicador a partir de las fechas de emisión (calendario
    /// dominicano). La diferencia se cuenta en días de calendario: exactamente 30
    /// días todavía cuenta como "dentro".
    /// </summary>
    public static ErrorOr<CreditNoteIndicator> For(DateOnly originalIssueDate, DateOnly creditNoteIssueDate)
    {
        var days = creditNoteIssueDate.DayNumber - originalIssueDate.DayNumber;

        if (days < 0)
            return FiscalErrors.CreditNoteBeforeOriginal;

        return days <= WindowDays ? WithinThirtyDays : AfterThirtyDays;
    }
}
