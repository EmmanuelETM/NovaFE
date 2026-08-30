using NovaFE.Domain.Ecf;

namespace NovaFE.Infrastructure.Ecf;

// Secciones posteriores a </DetallesItems>, en el orden del XSD:
// Subtotales → DescuentosORecargos → Paginacion → InformacionReferencia.
internal sealed partial class EcfXmlSerializer
{
    /// <summary><c>&lt;Subtotales&gt;</c> — passthrough informativo (no toca ningún total).</summary>
    private static void WriteSubtotales(EcfElementWriter w, EcfDocument doc, TotalsShape shape)
    {
        if (doc.Header.Subtotals is not { Count: > 0 } subtotals)
            return;

        using (w.Element("Subtotales"))
        {
            foreach (var s in subtotals)
            {
                using (w.Element("Subtotal"))
                {
                    w.Opt("NumeroSubTotal", s.Number);
                    w.Opt("DescripcionSubtotal", s.Description);
                    w.Opt("Orden", s.Order);

                    if (shape == TotalsShape.Full)
                    {
                        w.MoneyOpt("SubTotalMontoGravadoTotal", s.MontoGravadoTotal);
                        w.MoneyOpt("SubTotalMontoGravadoI1", s.MontoGravadoI1);
                        w.MoneyOpt("SubTotalMontoGravadoI2", s.MontoGravadoI2);
                        w.MoneyOpt("SubTotalMontoGravadoI3", s.MontoGravadoI3);
                        w.MoneyOpt("SubTotaITBIS", s.TotalItbis);
                        w.MoneyOpt("SubTotaITBIS1", s.Itbis1);
                        w.MoneyOpt("SubTotaITBIS2", s.Itbis2);
                        w.MoneyOpt("SubTotaITBIS3", s.Itbis3);
                    }
                    else if (shape == TotalsShape.ZeroRate)
                    {
                        w.MoneyOpt("SubTotalMontoGravadoTotal", s.MontoGravadoTotal);
                        w.MoneyOpt("SubTotalMontoGravadoI3", s.MontoGravadoI3);
                        w.MoneyOpt("SubTotaITBIS", s.TotalItbis);
                        w.MoneyOpt("SubTotaITBIS3", s.Itbis3);
                    }

                    w.MoneyOpt("SubTotalImpuestoAdicional", s.MontoImpuestoAdicional);
                    w.MoneyOpt("SubTotalExento", s.MontoExento);
                    w.MoneyOpt("MontoSubTotal", s.Amount);
                    w.Opt("Lineas", s.Lines);
                }
            }
        }
    }

    /// <summary>
    /// <c>&lt;DescuentosORecargos&gt;</c> (Sección D). El motor fiscal ya aplicó los
    /// montos a los buckets; aquí solo se listan.
    /// </summary>
    private static void WriteDescuentosORecargos(EcfElementWriter w, EcfDocument doc)
    {
        if (doc.Header.GlobalAdjustments is not { Count: > 0 } adjustments)
            return;

        using (w.Element("DescuentosORecargos"))
        {
            foreach (var adj in adjustments.OrderBy(a => a.Line))
            {
                using (w.Element("DescuentoORecargo"))
                {
                    w.El("NumeroLinea", adj.Line);
                    w.El("TipoAjuste", adj.Kind.Code);
                    if (adj.Norma1007)
                        w.El("IndicadorNorma1007", "1");
                    w.Opt("DescripcionDescuentooRecargo", adj.Description);

                    if (adj.Percentage is { } percentage and > 0m)
                    {
                        w.El("TipoValor", "%");
                        w.El("ValorDescuentooRecargo", EcfXmlFormat.Percent(percentage));
                    }
                    else
                    {
                        w.El("TipoValor", "$");
                    }

                    w.Money("MontoDescuentooRecargo", adj.Amount);
                    w.MoneyOpt("MontoDescuentooRecargoOtraMoneda", adj.AmountOtherCurrency);
                    w.El("IndicadorFacturacionDescuentooRecargo", adj.AffectsRate.Id);
                }
            }
        }
    }

    /// <summary><c>&lt;Paginacion&gt;</c> — passthrough presentacional para la RI.</summary>
    private static void WritePaginacion(EcfElementWriter w, EcfDocument doc, TotalsShape shape)
    {
        if (doc.Header.Pagination is not { Count: > 0 } pages)
            return;

        using (w.Element("Paginacion"))
        {
            foreach (var page in pages)
            {
                using (w.Element("Pagina"))
                {
                    w.Opt("PaginaNo", page.Number);
                    w.Opt("NoLineaDesde", page.LineFrom);
                    w.Opt("NoLineaHasta", page.LineTo);

                    if (shape == TotalsShape.Full)
                    {
                        w.MoneyOpt("SubtotalMontoGravadoPagina", page.MontoGravadoTotal);
                        w.MoneyOpt("SubtotalMontoGravado1Pagina", page.MontoGravadoI1);
                        w.MoneyOpt("SubtotalMontoGravado2Pagina", page.MontoGravadoI2);
                        w.MoneyOpt("SubtotalMontoGravado3Pagina", page.MontoGravadoI3);
                        w.MoneyOpt("SubtotalExentoPagina", page.MontoExento);
                        w.MoneyOpt("SubtotalItbisPagina", page.TotalItbis);
                        w.MoneyOpt("SubtotalItbis1Pagina", page.Itbis1);
                        w.MoneyOpt("SubtotalItbis2Pagina", page.Itbis2);
                        w.MoneyOpt("SubtotalItbis3Pagina", page.Itbis3);
                    }
                    else if (shape == TotalsShape.ZeroRate)
                    {
                        w.MoneyOpt("SubtotalMontoGravadoPagina", page.MontoGravadoTotal);
                        w.MoneyOpt("SubtotalMontoGravado3Pagina", page.MontoGravadoI3);
                        w.MoneyOpt("SubtotalItbisPagina", page.TotalItbis);
                        w.MoneyOpt("SubtotalItbis3Pagina", page.Itbis3);
                    }
                    else
                    {
                        w.MoneyOpt("SubtotalExentoPagina", page.MontoExento);
                    }

                    w.MoneyOpt("SubtotalImpuestoAdicionalPagina", page.MontoImpuestoAdicional);
                    if (page.IscEspecifico is not null || page.OtrosImpuestos is not null)
                    {
                        using (w.Element("SubtotalImpuestoAdicional"))
                        {
                            w.MoneyOpt("SubtotalImpuestoSelectivoConsumoEspecificoPagina", page.IscEspecifico);
                            w.MoneyOpt("SubtotalOtrosImpuesto", page.OtrosImpuestos);
                        }
                    }

                    w.MoneyOpt("MontoSubtotalPagina", page.Amount);
                    w.MoneyOpt("SubtotalMontoNoFacturablePagina", page.NonInvoiceableAmount);
                }
            }
        }
    }

    private static void WriteInformacionReferencia(EcfElementWriter w, EcfReference? reference)
    {
        if (reference is null)
            return;

        using (w.Element("InformacionReferencia"))
        {
            w.Opt("NCFModificado", reference.ModifiedNcf);
            w.Opt("RNCOtroContribuyente", reference.OtherIssuerRnc);
            w.El("FechaNCFModificado", EcfXmlFormat.Date(reference.ModifiedNcfDate));
            w.El("CodigoModificacion", reference.Code.Id);
        }
    }
}
