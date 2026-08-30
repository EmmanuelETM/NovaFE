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
/// Área de retención de una línea de detalle (<c>&lt;Retencion&gt;</c>). Obligatoria
/// en el tipo 41 (Comprobante de Compras), donde el emisor actúa como agente de
/// retención frente a un proveedor informal.
/// <para>
/// Los montos los trae calculados el cliente según las normas de retención de la
/// DGII (Norma 07-2007 y otras: 30 %/100 % de ITBIS, 10 %/2 % de ISR según el
/// servicio). El motor fiscal no deriva las tasas: solo suma
/// <see cref="ItbisWithheld"/> e <see cref="IsrWithheld"/> en
/// <c>&lt;TotalITBISRetenido&gt;</c> y <c>&lt;TotalISRRetencion&gt;</c>.
/// </para>
/// </summary>
/// <param name="Agent"><c>&lt;IndicadorAgenteRetencionoPercepcion&gt;</c>.</param>
/// <param name="ItbisWithheld"><c>&lt;MontoITBISRetenido&gt;</c> — ≥ 0.</param>
/// <param name="IsrWithheld"><c>&lt;MontoISRRetenido&gt;</c> — ≥ 0.</param>
public sealed record EcfLineRetention(
    RetentionAgent Agent,
    decimal ItbisWithheld = 0m,
    decimal IsrWithheld = 0m);
