using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;

namespace NovaFE.Infrastructure.Ecf;

// <Totales>, su desglose <ImpuestosAdicionales>, y el bloque <OtraMoneda>.
internal sealed partial class EcfXmlSerializer
{
    private static void WriteTotales(EcfElementWriter w, EcfDocument doc, EcfXmlProfile p)
    {
        var t = doc.Totals;

        using (w.Element("Totales"))
        {
            w.MoneyOpt("MontoGravadoTotal", t.MontoGravadoTotal);
            w.MoneyOpt("MontoGravadoI1", t.MontoGravadoI1);
            w.MoneyOpt("MontoGravadoI2", t.MontoGravadoI2);
            w.MoneyOpt("MontoGravadoI3", t.MontoGravadoI3);
            w.MoneyOpt("MontoExento", t.MontoExento);

            if (t.MontoGravadoI1 > 0m) w.El("ITBIS1", EcfXmlFormat.RateIndicator(ItbisRate.Eighteen.Rate));
            if (t.MontoGravadoI2 > 0m) w.El("ITBIS2", EcfXmlFormat.RateIndicator(ItbisRate.Sixteen.Rate));
            if (t.MontoGravadoI3 > 0m) w.El("ITBIS3", EcfXmlFormat.RateIndicator(ItbisRate.Zero.Rate));

            w.MoneyOpt("TotalITBIS", t.TotalItbis);
            if (t.MontoGravadoI1 > 0m) w.Money("TotalITBIS1", t.Itbis1);
            if (t.MontoGravadoI2 > 0m) w.Money("TotalITBIS2", t.Itbis2);
            if (t.MontoGravadoI3 > 0m) w.Money("TotalITBIS3", t.Itbis3);

            w.MoneyOpt("MontoImpuestoAdicional", t.MontoImpuestoAdicional);
            WriteImpuestosAdicionales(w, doc.Lines, p.IscBreakdownAmounts);

            w.Money("MontoTotal", t.MontoTotal);

            if (doc.Header.NonInvoiceableAmount != 0m)
            {
                w.Money("MontoNoFacturable", t.MontoNoFacturable);
                w.Money("MontoPeriodo", t.MontoPeriodo);
            }

            WriteValorPagarYRetenciones(w, t, p.Retention);
        }
    }

    /// <summary>
    /// <c>&lt;ImpuestosAdicionales&gt;</c> — el desglose de
    /// <see cref="EcfLine.AdditionalTaxDetail"/> de todas las líneas, agrupado por
    /// código (Tabla I). Es una proyección para el XML; no lleva regla de negocio.
    /// </summary>
    private static void WriteImpuestosAdicionales(
        EcfElementWriter w, IReadOnlyList<EcfLine> lines, bool iscAmounts)
    {
        var groups = lines
            .Where(line => line.AdditionalTaxDetail is { Count: > 0 })
            .SelectMany(line => line.AdditionalTaxDetail!)
            .GroupBy(tax => tax.Code, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();

        if (groups.Count == 0)
            return;

        using (w.Element("ImpuestosAdicionales"))
        {
            foreach (var group in groups)
            {
                using (w.Element("ImpuestoAdicional"))
                {
                    w.El("TipoImpuesto", group.Key);
                    w.El("TasaImpuestoAdicional", EcfXmlFormat.Percent(group.Max(tax => tax.Rate)));
                    if (iscAmounts)
                    {
                        w.MoneyOpt("MontoImpuestoSelectivoConsumoEspecifico", group.Sum(tax => tax.IscEspecifico));
                        w.MoneyOpt("MontoImpuestoSelectivoConsumoAdvalorem", group.Sum(tax => tax.IscAdvalorem));
                    }

                    w.MoneyOpt("OtrosImpuestosAdicionales", group.Sum(tax => tax.Otros));
                }
            }
        }
    }

    /// <summary>
    /// <c>&lt;ValorPagar&gt;</c> = <c>MontoTotal − retenciones − descuentos Norma 10-07</c>,
    /// y los totales retenidos. El tipo 47 (<see cref="RetentionShape.IsrOnly"/>)
    /// siempre lo emite; el resto solo si hay algo que restar.
    /// </summary>
    private static void WriteValorPagarYRetenciones(EcfElementWriter w, EcfTotals t, RetentionShape retention)
    {
        var reductions = t.TotalItbisWithheld + t.TotalIsrWithheld + t.Norma1007Discount;

        if (retention == RetentionShape.IsrOnly)
        {
            w.Money("ValorPagar", t.MontoTotal - reductions);
            w.Money("TotalISRRetencion", t.TotalIsrWithheld);
            return;
        }

        if (reductions <= 0m)
            return;

        w.Money("ValorPagar", t.MontoTotal - reductions);
        w.MoneyOpt("TotalITBISRetenido", t.TotalItbisWithheld);
        w.MoneyOpt("TotalISRRetencion", t.TotalIsrWithheld);
    }

    /// <summary>
    /// <c>&lt;OtraMoneda&gt;</c> del encabezado — emite el subconjunto de
    /// <c>*OtraMoneda</c> que corresponde al <see cref="TotalsShape"/> del tipo.
    /// </summary>
    private static void WriteOtraMoneda(EcfElementWriter w, EcfForeignCurrency? fx, TotalsShape shape)
    {
        if (fx is null)
            return;

        var t = fx.Totals;

        using (w.Element("OtraMoneda"))
        {
            w.El("TipoMoneda", fx.Currency.Name);
            w.El("TipoCambio", EcfXmlFormat.UnitPrice(fx.ExchangeRate));

            switch (shape)
            {
                case TotalsShape.ExemptOnly:
                    w.MoneyOpt("MontoExentoOtraMoneda", t.MontoExento);
                    break;

                case TotalsShape.ZeroRate:
                    w.MoneyOpt("MontoGravadoTotalOtraMoneda", t.MontoGravadoTotal);
                    w.MoneyOpt("MontoGravado3OtraMoneda", t.MontoGravadoI3);
                    w.MoneyOpt("TotalITBISOtraMoneda", t.TotalItbis);
                    w.MoneyOpt("TotalITBIS3OtraMoneda", t.TotalItbis3);
                    break;

                default:
                    w.MoneyOpt("MontoGravadoTotalOtraMoneda", t.MontoGravadoTotal);
                    w.MoneyOpt("MontoGravado1OtraMoneda", t.MontoGravadoI1);
                    w.MoneyOpt("MontoGravado2OtraMoneda", t.MontoGravadoI2);
                    w.MoneyOpt("MontoGravado3OtraMoneda", t.MontoGravadoI3);
                    w.MoneyOpt("MontoExentoOtraMoneda", t.MontoExento);
                    w.MoneyOpt("TotalITBISOtraMoneda", t.TotalItbis);
                    w.MoneyOpt("TotalITBIS1OtraMoneda", t.TotalItbis1);
                    w.MoneyOpt("TotalITBIS2OtraMoneda", t.TotalItbis2);
                    w.MoneyOpt("TotalITBIS3OtraMoneda", t.TotalItbis3);
                    break;
            }

            w.MoneyOpt("MontoTotalOtraMoneda", t.MontoTotal);
        }
    }
}
