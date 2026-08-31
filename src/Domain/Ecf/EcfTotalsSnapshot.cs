using NovaFE.Domain.Fiscal;

namespace NovaFE.Domain.Ecf;

/// <summary>
/// Los totales del comprobante tal como se persisten y se devuelven por la API —
/// una vista curada de <see cref="EcfTotals"/> (el resultado completo del motor
/// fiscal vive en el XML firmado).
/// </summary>
public sealed record EcfTotalsSnapshot(
    decimal MontoGravadoTotal,
    decimal MontoGravadoI1,
    decimal MontoGravadoI2,
    decimal MontoGravadoI3,
    decimal MontoExento,
    decimal TotalItbis,
    decimal TotalItbis1,
    decimal TotalItbis2,
    decimal TotalItbis3,
    decimal MontoImpuestoAdicional,
    decimal MontoTotal,
    decimal MontoNoFacturable,
    decimal MontoPeriodo,
    decimal TotalItbisRetenido,
    decimal TotalIsrRetencion,
    /// <summary>
    /// ISC específico de alcoholes/cigarrillos, ya incluido en
    /// <see cref="MontoImpuestoAdicional"/>. Se expone aparte porque integra la base
    /// del ITBIS: explica por qué <see cref="TotalItbis"/> no es
    /// <see cref="MontoGravadoTotal"/> × tasa.
    /// </summary>
    decimal TotalImpuestoSelectivoConsumo = 0m)
{
    public static EcfTotalsSnapshot From(EcfTotals t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new EcfTotalsSnapshot(
            MontoGravadoTotal: t.MontoGravadoTotal,
            MontoGravadoI1: t.MontoGravadoI1,
            MontoGravadoI2: t.MontoGravadoI2,
            MontoGravadoI3: t.MontoGravadoI3,
            MontoExento: t.MontoExento,
            TotalItbis: t.TotalItbis,
            TotalItbis1: t.Itbis1,
            TotalItbis2: t.Itbis2,
            TotalItbis3: t.Itbis3,
            MontoImpuestoAdicional: t.MontoImpuestoAdicional,
            MontoTotal: t.MontoTotal,
            MontoNoFacturable: t.MontoNoFacturable,
            MontoPeriodo: t.MontoPeriodo,
            TotalItbisRetenido: t.TotalItbisWithheld,
            TotalIsrRetencion: t.TotalIsrWithheld,
            TotalImpuestoSelectivoConsumo: t.TotalImpuestoSelectivoConsumo);
    }
}
