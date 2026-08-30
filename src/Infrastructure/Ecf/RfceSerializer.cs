using System.Text;
using System.Xml;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;

namespace NovaFE.Infrastructure.Ecf;

/// <summary>
/// Serializador del <c>&lt;RFCE&gt;</c> (Resumen de Factura de Consumo Electrónica).
/// Su XSD (<c>RFCE-32-v1.0.xsd</c>) es un formato aparte, mucho más chico que el
/// <c>&lt;ECF&gt;</c>: raíz distinta, solo <c>&lt;Encabezado&gt;</c> (IdDoc, Emisor
/// reducido, Comprador reducido, Totales sin indicadores de tasa) y un
/// <c>&lt;CodigoSeguridadeCF&gt;</c>. Sin <c>&lt;DetallesItems&gt;</c> ni
/// <c>&lt;FechaHoraFirma&gt;</c>. Comparte con el e-CF el <see cref="EcfElementWriter"/>
/// y <see cref="EcfXml"/>; el resto es propio porque es otro documento.
/// </summary>
internal sealed class RfceSerializer : IRfceSerializer
{
    /// <summary><c>&lt;CodigoSeguridadeCF&gt;</c> son exactamente 6 caracteres (XSD <c>.{6}</c>).</summary>
    public const int SecurityCodeLength = 6;

    public string Serialize(EcfDocument document, string securityCode)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(securityCode);

        if (document.Type != EcfType.Consumo)
            throw new ArgumentException("El RFCE solo aplica al tipo 32 (Factura de Consumo).", nameof(document));
        if (securityCode.Length != SecurityCodeLength)
            throw new ArgumentException(
                $"El código de seguridad del e-CF debe tener {SecurityCodeLength} caracteres.", nameof(securityCode));

        var buffer = new StringBuilder();

        using (var xmlWriter = XmlWriter.Create(buffer, EcfXml.WriterSettings))
        {
            var w = new EcfElementWriter(xmlWriter, EcfXmlFormat.Money2);
            using (w.Element("RFCE"))
            using (w.Element("Encabezado"))
            {
                w.El("Version", "1.0");
                WriteIdDoc(w, document.Header);
                WriteEmisor(w, document.Header.Issuer, document.Header.IssueDate);
                WriteComprador(w, document.Header.Buyer);
                WriteTotales(w, document);
                w.El("CodigoSeguridadeCF", securityCode);
            }
        }

        return EcfXml.Finish(buffer.ToString());
    }

    private static void WriteIdDoc(EcfElementWriter w, EcfHeader h)
    {
        using (w.Element("IdDoc"))
        {
            w.El("TipoeCF", EcfType.Consumo.Id);
            w.El("eNCF", h.Encf.Value);
            w.El("TipoIngresos", h.IncomeType);
            w.El("TipoPago", h.Payment.Condition.Id);
            WriteFormasPago(w, h.Payment.Methods);
        }
    }

    private static void WriteFormasPago(EcfElementWriter w, IReadOnlyList<EcfPaymentMethod> methods)
    {
        if (methods.Count == 0)
            return;

        using (w.Element("TablaFormasPago"))
        {
            foreach (var method in methods)
            {
                using (w.Element("FormaDePago"))
                {
                    w.El("FormaPago", method.Method.Id);
                    w.Money("MontoPago", method.Amount);
                }
            }
        }
    }

    private static void WriteEmisor(EcfElementWriter w, EcfIssuer issuer, DateOnly issueDate)
    {
        using (w.Element("Emisor"))
        {
            w.El("RNCEmisor", issuer.Rnc.Value);
            w.Opt("RazonSocialEmisor", issuer.Name);
            w.El("FechaEmision", EcfXmlFormat.Date(issueDate));
        }
    }

    private static void WriteComprador(EcfElementWriter w, EcfBuyer buyer)
    {
        using (w.Element("Comprador"))
        {
            if (buyer.Rnc is { } rnc)
                w.El("RNCComprador", rnc.Value);
            w.Opt("IdentificadorExtranjero", buyer.ForeignId);
            w.Opt("RazonSocialComprador", buyer.Name);
        }
    }

    private static void WriteTotales(EcfElementWriter w, EcfDocument doc)
    {
        var t = doc.Totals;

        using (w.Element("Totales"))
        {
            w.MoneyOpt("MontoGravadoTotal", t.MontoGravadoTotal);
            w.MoneyOpt("MontoGravadoI1", t.MontoGravadoI1);
            w.MoneyOpt("MontoGravadoI2", t.MontoGravadoI2);
            w.MoneyOpt("MontoGravadoI3", t.MontoGravadoI3);
            w.MoneyOpt("MontoExento", t.MontoExento);
            w.MoneyOpt("TotalITBIS", t.TotalItbis);
            if (t.MontoGravadoI1 > 0m) w.Money("TotalITBIS1", t.Itbis1);
            if (t.MontoGravadoI2 > 0m) w.Money("TotalITBIS2", t.Itbis2);
            if (t.MontoGravadoI3 > 0m) w.Money("TotalITBIS3", t.Itbis3);

            w.MoneyOpt("MontoImpuestoAdicional", t.MontoImpuestoAdicional);
            WriteImpuestosAdicionales(w, doc.Lines);

            w.Money("MontoTotal", t.MontoTotal);

            if (doc.Header.NonInvoiceableAmount != 0m)
            {
                w.Money("MontoNoFacturable", t.MontoNoFacturable);
                w.Money("MontoPeriodo", t.MontoPeriodo);
            }
        }
    }

    /// <summary>
    /// <c>&lt;ImpuestosAdicionales&gt;</c> del RFCE — como en el e-CF pero <b>sin</b>
    /// <c>&lt;TasaImpuestoAdicional&gt;</c> (su XSD no la tiene).
    /// </summary>
    private static void WriteImpuestosAdicionales(EcfElementWriter w, IReadOnlyList<EcfLine> lines)
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
                    w.MoneyOpt("MontoImpuestoSelectivoConsumoEspecifico", group.Sum(tax => tax.IscEspecifico));
                    w.MoneyOpt("MontoImpuestoSelectivoConsumoAdvalorem", group.Sum(tax => tax.IscAdvalorem));
                    w.MoneyOpt("OtrosImpuestosAdicionales", group.Sum(tax => tax.Otros));
                }
            }
        }
    }
}
