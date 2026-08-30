using NovaFE.Domain.Ecf;

namespace NovaFE.Infrastructure.Ecf;

// <DetallesItems> y todo lo que va dentro de un <Item>.
internal sealed partial class EcfXmlSerializer
{
    private static void WriteDetalles(EcfElementWriter w, EcfDocument doc, EcfXmlProfile p)
    {
        var lineAmounts = doc.Calculation.Lines.ToDictionary(line => line.LineNumber);

        using (w.Element("DetallesItems"))
        {
            foreach (var line in doc.Lines.OrderBy(line => line.Number))
            {
                using (w.Element("Item"))
                {
                    w.El("NumeroLinea", line.Number);
                    WriteCodigosItem(w, line.Codes);
                    w.El("IndicadorFacturacion", line.Rate.Id);
                    WriteRetencion(w, line.Retention, p.Retention);
                    w.Opt("NombreItem", line.Name);
                    w.El("IndicadorBienoServicio", line.Kind.Id);
                    w.Opt("DescripcionItem", line.Description);
                    w.Money("CantidadItem", line.Quantity);
                    w.Opt("UnidadMedida", line.UnitOfMeasure);
                    WriteLineDetails(w, line.Details, p.Mining);
                    w.El("PrecioUnitarioItem", EcfXmlFormat.UnitPrice(line.UnitPrice));
                    w.MoneyOpt("DescuentoMonto", line.Discount);
                    w.MoneyOpt("RecargoMonto", line.Surcharge);
                    WriteTablaImpuestoAdicional(w, line.AdditionalTaxDetail);
                    WriteOtraMonedaDetalle(w, line.ForeignCurrency);
                    w.Money("MontoItem", lineAmounts[line.Number].LineAmount);
                }
            }
        }
    }

    private static void WriteCodigosItem(EcfElementWriter w, IReadOnlyList<EcfItemCode>? codes)
    {
        if (codes is not { Count: > 0 })
            return;

        using (w.Element("TablaCodigosItem"))
        {
            foreach (var code in codes.Take(5))
            {
                using (w.Element("CodigosItem"))
                {
                    w.Opt("TipoCodigo", code.Type);
                    w.Opt("CodigoItem", code.Value);
                }
            }
        }
    }

    /// <summary>
    /// <c>&lt;Retencion&gt;</c> del detalle. <see cref="RetentionShape.IsrOnly"/>
    /// (tipo 47) emite <c>&lt;MontoISRRetenido&gt;</c> siempre (aunque sea 0) y no
    /// tiene <c>&lt;MontoITBISRetenido&gt;</c>.
    /// </summary>
    private static void WriteRetencion(EcfElementWriter w, EcfLineRetention? retention, RetentionShape shape)
    {
        if (retention is null)
            return;

        using (w.Element("Retencion"))
        {
            w.El("IndicadorAgenteRetencionoPercepcion", retention.Agent.Id);

            if (shape == RetentionShape.IsrOnly)
            {
                w.Money("MontoISRRetenido", retention.IsrWithheld);
                return;
            }

            w.MoneyOpt("MontoITBISRetenido", retention.ItbisWithheld);
            w.MoneyOpt("MontoISRRetenido", retention.IsrWithheld);
        }
    }

    /// <summary>
    /// Campos opcionales del <c>&lt;Item&gt;</c> entre <c>UnidadMedida</c> y
    /// <c>PrecioUnitarioItem</c>. El bloque <c>&lt;Mineria&gt;</c> solo se emite si
    /// <paramref name="miningAllowed"/> (tipos 32/33/34/46).
    /// </summary>
    private static void WriteLineDetails(EcfElementWriter w, EcfLineDetails? d, bool miningAllowed)
    {
        if (d is null)
            return;

        w.MoneyOpt("CantidadReferencia", d.ReferenceQuantity);
        w.Opt("UnidadReferencia", d.ReferenceUnit);

        if (d.Subquantities is { Count: > 0 } subquantities)
        {
            using (w.Element("TablaSubcantidad"))
            {
                foreach (var sub in subquantities.Take(5))
                {
                    using (w.Element("SubcantidadItem"))
                    {
                        w.El("Subcantidad", EcfXmlFormat.Subquantity(sub.Quantity));
                        w.Opt("CodigoSubcantidad", sub.UnitCode);
                    }
                }
            }
        }

        w.MoneyOpt("GradosAlcohol", d.AlcoholDegrees);
        w.MoneyOpt("PrecioUnitarioReferencia", d.ReferenceUnitPrice);
        w.Opt("FechaElaboracion", d.Elaboration);
        w.Opt("FechaVencimientoItem", d.ItemExpiry);

        if (miningAllowed && d.Mining is { } mining)
        {
            using (w.Element("Mineria"))
            {
                w.MoneyOpt("PesoNetoKilogramo", mining.NetWeightKilogram);
                w.MoneyOpt("PesoNetoMineria", mining.NetWeightMining);
                w.Opt("TipoAfiliacion", mining.AffiliationType);
                w.Opt("Liquidacion", mining.Settlement);
            }
        }
    }

    /// <summary><c>&lt;TablaImpuestoAdicional&gt;</c> de la línea — solo los códigos.</summary>
    private static void WriteTablaImpuestoAdicional(EcfElementWriter w, IReadOnlyList<EcfAdditionalTax>? detail)
    {
        if (detail is not { Count: > 0 })
            return;

        using (w.Element("TablaImpuestoAdicional"))
        {
            foreach (var tax in detail.Take(2))
            {
                using (w.Element("ImpuestoAdicional"))
                    w.El("TipoImpuesto", tax.Code);
            }
        }
    }

    /// <summary><c>&lt;OtraMonedaDetalle&gt;</c> — precio y montos de la línea en divisa.</summary>
    private static void WriteOtraMonedaDetalle(EcfElementWriter w, EcfLineForeignCurrency? fx)
    {
        if (fx is null)
            return;

        using (w.Element("OtraMonedaDetalle"))
        {
            if (fx.UnitPrice is { } unitPrice and > 0m)
                w.El("PrecioOtraMoneda", EcfXmlFormat.UnitPrice(unitPrice));
            w.MoneyOpt("DescuentoOtraMoneda", fx.Discount);
            w.MoneyOpt("RecargoOtraMoneda", fx.Surcharge);
            w.MoneyOpt("MontoItemOtraMoneda", fx.LineAmount);
        }
    }
}
