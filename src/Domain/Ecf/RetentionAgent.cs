using NovaFE.Domain.Common;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// <c>&lt;IndicadorAgenteRetencionoPercepcion&gt;</c> del área de retención del
/// detalle: <b>1 = Retención, 2 = Percepción</b> (verificado contra
/// <c>e-CF 41 v.1.0.xsd</c>, <c>IndicadorAgenteRetencionoPercepcionType</c>).
/// </summary>
public sealed record RetentionAgent(int Id, string Name) : Enumeration<RetentionAgent>(Id, Name)
{
    /// <summary>1 — el emisor retiene ITBIS/ISR al pagar (Comprobante de Compras).</summary>
    public static readonly RetentionAgent Withholding = new(1, nameof(Withholding));

    /// <summary>2 — el emisor percibe ITBIS/ISR por adelantado.</summary>
    public static readonly RetentionAgent Perception = new(2, nameof(Perception));
}

/// <summary>
/// Área de retención de una línea de detalle (<c>&lt;Retencion&gt;</c>). Aplica a
/// los tipos 41 (Compras) y 47 (Pagos al Exterior), donde el emisor actúa como
/// agente de retención frente al proveedor.
/// <para>
/// <b>Los montos los calcula y los presenta el cliente</b>, por línea. El motor
/// fiscal no deriva porcentajes: solo suma <see cref="ItbisWithheld"/> e
/// <see cref="IsrWithheld"/> en <c>&lt;TotalITBISRetenido&gt;</c> y
/// <c>&lt;TotalISRRetencion&gt;</c>. El ITBIS de la línea sí es conocido, pero el
/// porcentaje retenido (30 %/100 %) depende de la clasificación del proveedor; la
/// tasa de ISR varía por la naturaleza del pago (2 %, 5 %, 10 %, y 27 % exclusivo
/// del tipo 47). Ver <c>docs/fiscal.md</c> § Retenciones.
/// </para>
/// </summary>
/// <param name="Agent"><c>&lt;IndicadorAgenteRetencionoPercepcion&gt;</c>.</param>
/// <param name="ItbisWithheld"><c>&lt;MontoITBISRetenido&gt;</c> — ≥ 0.</param>
/// <param name="IsrWithheld"><c>&lt;MontoISRRetenido&gt;</c> — ≥ 0.</param>
public sealed record EcfLineRetention(
    RetentionAgent Agent,
    decimal ItbisWithheld = 0m,
    decimal IsrWithheld = 0m);
