using System.Text;
using System.Xml;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;

namespace NovaFE.Infrastructure.Ecf;

/// <summary>
/// Serializador del <c>&lt;ECF&gt;</c> (Módulo 2). El orden de los elementos sale
/// del XSD oficial de cada tipo; los opcionales sin valor se omiten (RF-02.5).
/// <para>
/// v1: los diez tipos (31–34, 41, 43–47) con IdDoc, Emisor, Comprador, Totales,
/// DetallesItems, InformacionReferencia, Retencion, InformacionesAdicionales,
/// Transporte y OtraMoneda (+ OtraMonedaDetalle). Faltan: Subtotales,
/// DescuentosORecargos, Paginación, el desglose de ImpuestosAdicionales y el
/// formato reducido RFCE (tipo 32 &lt; DOP 250 k). Ver <c>docs/ecf-xml.md</c>.
/// </para>
/// </summary>
internal sealed class EcfXmlSerializer : IEcfXmlSerializer
{
    private static readonly XmlWriterSettings _settings = new()
    {
        OmitXmlDeclaration = true,
        Indent = false,
        NewLineHandling = NewLineHandling.None,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    public string Serialize(EcfDocument document, DateTimeOffset signedAt)
    {
        ArgumentNullException.ThrowIfNull(document);

        var buffer = new StringBuilder();
        using (var writer = XmlWriter.Create(buffer, _settings))
        {
            writer.WriteStartElement("ECF");
            WriteEncabezado(writer, document);
            WriteDetalles(writer, document);
            WriteInformacionReferencia(writer, document.Reference);
            El(writer, "FechaHoraFirma", DominicanTimeZone.ToDateTimeString(signedAt));
            writer.WriteEndElement();
        }

        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + EscapeDgii(buffer.ToString());
    }

    // ---- Encabezado --------------------------------------------------------

    private static void WriteEncabezado(XmlWriter w, EcfDocument doc)
    {
        var h = doc.Header;

        w.WriteStartElement("Encabezado");
        El(w, "Version", "1.0");

        WriteIdDoc(w, doc);

        w.WriteStartElement("Emisor");
        El(w, "RNCEmisor", h.Issuer.Rnc.Value);
        Text(w, "RazonSocialEmisor", h.Issuer.Name);
        Text(w, "NombreComercial", h.Issuer.TradeName);
        Text(w, "Sucursal", h.Issuer.Branch);
        Text(w, "DireccionEmisor", h.Issuer.Address);
        Text(w, "Municipio", h.Issuer.Municipality);
        Text(w, "Provincia", h.Issuer.Province);
        WritePhones(w, h.Issuer.Phones);
        Text(w, "CorreoEmisor", h.Issuer.Email);
        Text(w, "ActividadEconomica", h.Issuer.EconomicActivity);
        Text(w, "CodigoVendedor", h.Issuer.SellerCode);
        Text(w, "NumeroFacturaInterna", h.Issuer.InternalInvoiceNumber);
        Text(w, "InformacionAdicionalEmisor", h.Issuer.AdditionalInfo);
        El(w, "FechaEmision", EcfXmlFormat.Date(h.IssueDate));
        w.WriteEndElement(); // Emisor

        // El tipo 43 (Gastos Menores) no tiene bloque <Comprador>: es un gasto
        // propio del emisor. El tipo 47 (Pagos al Exterior) lo tiene reducido:
        // solo identificador extranjero y razón social.
        if (doc.Type == EcfType.PagosExterior)
        {
            w.WriteStartElement("Comprador");
            Opt(w, "IdentificadorExtranjero", h.Buyer.ForeignId);
            Text(w, "RazonSocialComprador", h.Buyer.Name);
            w.WriteEndElement(); // Comprador
        }
        else if (doc.Type != EcfType.GastosMenores)
        {
            w.WriteStartElement("Comprador");
            if (h.Buyer.Rnc is { } buyerRnc)
                El(w, "RNCComprador", buyerRnc.Value);
            Opt(w, "IdentificadorExtranjero", h.Buyer.ForeignId);
            Text(w, "RazonSocialComprador", h.Buyer.Name);
            Text(w, "ContactoComprador", h.Buyer.Contact);
            Text(w, "CorreoComprador", h.Buyer.Email);
            Text(w, "DireccionComprador", h.Buyer.Address);
            Text(w, "MunicipioComprador", h.Buyer.Municipality);
            Text(w, "ProvinciaComprador", h.Buyer.Province);
            Text(w, "InformacionAdicionalComprador", h.Buyer.AdditionalInfo);
            w.WriteEndElement(); // Comprador
        }

        WriteInformacionesAdicionales(w, doc);
        WriteTransporte(w, doc);
        WriteTotales(w, doc);
        WriteOtraMoneda(w, doc);
        w.WriteEndElement(); // Encabezado
    }

    /// <summary>
    /// <c>&lt;OtraMoneda&gt;</c> del encabezado. Emite el subconjunto de
    /// <c>*OtraMoneda</c> que corresponde al <c>&lt;Totales&gt;</c> del tipo: los
    /// tipos con ITBIS completo usan todos; 43/44/47 solo exento + total; el 46 solo
    /// el bucket a tasa 0.
    /// </summary>
    private static void WriteOtraMoneda(XmlWriter w, EcfDocument doc)
    {
        if (doc.Header.ForeignCurrency is not { } fx)
            return;

        var t = fx.Totals;
        var exemptOnly = doc.Type == EcfType.GastosMenores
            || doc.Type == EcfType.RegimenesEspeciales
            || doc.Type == EcfType.PagosExterior;
        var zeroRateOnly = doc.Type == EcfType.Exportaciones;

        w.WriteStartElement("OtraMoneda");
        El(w, "TipoMoneda", fx.Currency.Name);
        El(w, "TipoCambio", EcfXmlFormat.UnitPrice(fx.ExchangeRate));

        if (exemptOnly)
        {
            Amount(w, "MontoExentoOtraMoneda", t.MontoExento);
        }
        else if (zeroRateOnly)
        {
            Amount(w, "MontoGravadoTotalOtraMoneda", t.MontoGravadoTotal);
            Amount(w, "MontoGravado3OtraMoneda", t.MontoGravadoI3);
            Amount(w, "TotalITBISOtraMoneda", t.TotalItbis);
            Amount(w, "TotalITBIS3OtraMoneda", t.TotalItbis3);
        }
        else
        {
            Amount(w, "MontoGravadoTotalOtraMoneda", t.MontoGravadoTotal);
            Amount(w, "MontoGravado1OtraMoneda", t.MontoGravadoI1);
            Amount(w, "MontoGravado2OtraMoneda", t.MontoGravadoI2);
            Amount(w, "MontoGravado3OtraMoneda", t.MontoGravadoI3);
            Amount(w, "MontoExentoOtraMoneda", t.MontoExento);
            Amount(w, "TotalITBISOtraMoneda", t.TotalItbis);
            Amount(w, "TotalITBIS1OtraMoneda", t.TotalItbis1);
            Amount(w, "TotalITBIS2OtraMoneda", t.TotalItbis2);
            Amount(w, "TotalITBIS3OtraMoneda", t.TotalItbis3);
        }

        Amount(w, "MontoTotalOtraMoneda", t.MontoTotal);
        w.WriteEndElement();
    }

    // ---- InformacionesAdicionales / Transporte (bloques transversales) ---

    /// <summary>
    /// <c>&lt;InformacionesAdicionales&gt;</c> (datos de embarque). Los campos de
    /// exportación (FOB/CIF, puertos) se intercalan entre <c>NumeroReferencia</c> y
    /// <c>PesoBruto</c>, solo para el tipo 46.
    /// </summary>
    private static void WriteInformacionesAdicionales(XmlWriter w, EcfDocument doc)
    {
        if (doc.Header.Shipping is not { } s)
            return;

        w.WriteStartElement("InformacionesAdicionales");
        Opt(w, "FechaEmbarque", s.ShipmentDate is { } d ? EcfXmlFormat.Date(d) : null);
        Opt(w, "NumeroEmbarque", s.ShipmentNumber);
        Opt(w, "NumeroContenedor", s.ContainerNumber);
        Opt(w, "NumeroReferencia", s.ReferenceNumber);

        if (doc.Type == EcfType.Exportaciones && s.Export is { } e)
        {
            Opt(w, "NombrePuertoEmbarque", e.LoadingPortName);
            Opt(w, "CondicionesEntrega", e.DeliveryTerms);
            Amount(w, "TotalFob", e.TotalFob);
            Amount(w, "Seguro", e.Insurance);
            Amount(w, "Flete", e.Freight);
            Amount(w, "OtrosGastos", e.OtherCharges);
            Amount(w, "TotalCif", e.TotalCif);
            Opt(w, "RegimenAduanero", e.CustomsRegime);
            Opt(w, "NombrePuertoSalida", e.DeparturePortName);
            Opt(w, "NombrePuertoDesembarque", e.UnloadingPortName);
        }

        Amount(w, "PesoBruto", s.GrossWeight);
        Amount(w, "PesoNeto", s.NetWeight);
        Opt(w, "UnidadPesoBruto", s.GrossWeightUnit);
        Opt(w, "UnidadPesoNeto", s.NetWeightUnit);
        Amount(w, "CantidadBulto", s.PackageCount);
        Opt(w, "UnidadBulto", s.PackageUnit);
        Amount(w, "VolumenBulto", s.Volume);
        Opt(w, "UnidadVolumen", s.VolumeUnit);
        w.WriteEndElement();
    }

    /// <summary>
    /// <c>&lt;Transporte&gt;</c>. El tipo 46 antepone vía/país/compañía transportista;
    /// el tipo 47 solo lleva <c>PaisDestino</c>; el resto, los campos básicos.
    /// </summary>
    private static void WriteTransporte(XmlWriter w, EcfDocument doc)
    {
        if (doc.Header.Transport is not { } t)
            return;

        w.WriteStartElement("Transporte");

        if (doc.Type == EcfType.Exportaciones)
        {
            Opt(w, "ViaTransporte", t.Via?.Code);
            Opt(w, "PaisOrigen", t.OriginCountry);
            Opt(w, "DireccionDestino", t.DestinationAddress);
            Opt(w, "PaisDestino", t.DestinationCountry);
            Opt(w, "RNCIdentificacionCompaniaTransportista", t.CarrierRnc);
            Opt(w, "NombreCompaniaTransportista", t.CarrierName);
            Opt(w, "NumeroViaje", t.VoyageNumber);
        }
        else if (doc.Type == EcfType.PagosExterior)
        {
            // El XSD del 47 solo admite <PaisDestino> dentro de <Transporte>.
            Opt(w, "PaisDestino", t.DestinationCountry);
            w.WriteEndElement();
            return;
        }

        Opt(w, "Conductor", t.Driver);
        Opt(w, "DocumentoTransporte", t.TransportDocument);
        Opt(w, "Ficha", t.VehicleId);
        Opt(w, "Placa", t.Plate);
        Opt(w, "RutaTransporte", t.Route);
        Opt(w, "ZonaTransporte", t.Zone);
        Opt(w, "NumeroAlbaran", t.DeliveryNote);
        w.WriteEndElement();
    }

    /// <summary>
    /// <c>&lt;IdDoc&gt;</c>. Variaciones por tipo:
    /// <list type="bullet">
    ///   <item>34 (Nota de Crédito): <c>&lt;IndicadorNotaCredito&gt;</c> (0/1, obligatorio)
    ///   en vez de <c>&lt;FechaVencimientoSecuencia&gt;</c>; su XSD no admite <c>&lt;TablaFormasPago&gt;</c>.</item>
    ///   <item>41 (Compras) y 47 (Pagos al Exterior): sus XSD no admiten
    ///   <c>&lt;TipoIngresos&gt;</c> ni <c>&lt;IndicadorEnvioDiferido&gt;</c>.</item>
    ///   <item>44 (Regímenes Especiales), 46 (Exportaciones) y 47 (Pagos al Exterior):
    ///   sus XSD no admiten <c>&lt;IndicadorMontoGravado&gt;</c>.</item>
    ///   <item>43 (Gastos Menores): IdDoc mínimo — solo <c>TipoeCF</c>, <c>eNCF</c>,
    ///   <c>FechaVencimientoSecuencia</c> y <c>TipoPago</c>.</item>
    /// </list>
    /// </summary>
    private static void WriteIdDoc(XmlWriter w, EcfDocument doc)
    {
        var h = doc.Header;
        var isCreditNote = doc.Type == EcfType.NotaCredito;
        var omitsIncomeType = doc.Type == EcfType.Compras || doc.Type == EcfType.PagosExterior;
        var omitsTaxedIndicator = doc.Type == EcfType.RegimenesEspeciales
            || doc.Type == EcfType.Exportaciones
            || doc.Type == EcfType.PagosExterior;

        w.WriteStartElement("IdDoc");
        El(w, "TipoeCF", Int(doc.Type.Id));
        El(w, "eNCF", h.Encf.Value);

        if (doc.Type == EcfType.GastosMenores)
        {
            El(w, "FechaVencimientoSecuencia", EcfXmlFormat.Date(h.SequenceExpiresOn!.Value));
            El(w, "TipoPago", Int(h.Payment.Condition.Id));
            w.WriteEndElement(); // IdDoc
            return;
        }

        if (isCreditNote)
            El(w, "IndicadorNotaCredito", Int(doc.CreditNoteIndicator ?? 0));
        else
            Opt(w, "FechaVencimientoSecuencia", h.SequenceExpiresOn is { } exp ? EcfXmlFormat.Date(exp) : null);

        if (h.DeferredDelivery && !omitsIncomeType)
            El(w, "IndicadorEnvioDiferido", "1");
        if (!omitsTaxedIndicator)
            El(w, "IndicadorMontoGravado", h.PricesIncludeTax ? "1" : "0");
        if (!omitsIncomeType)
            El(w, "TipoIngresos", h.IncomeType);
        El(w, "TipoPago", Int(h.Payment.Condition.Id));
        Opt(w, "FechaLimitePago", h.Payment.DueDate is { } due ? EcfXmlFormat.Date(due) : null);
        if (!isCreditNote)
            WriteFormasPago(w, h.Payment.Methods);

        w.WriteEndElement(); // IdDoc
    }

    private static void WriteFormasPago(XmlWriter w, IReadOnlyList<EcfPaymentMethod> methods)
    {
        if (methods.Count == 0)
            return;

        w.WriteStartElement("TablaFormasPago");
        foreach (var method in methods)
        {
            w.WriteStartElement("FormaDePago");
            El(w, "FormaPago", method.Method.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            El(w, "MontoPago", EcfXmlFormat.Money(method.Amount));
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    private static void WritePhones(XmlWriter w, IReadOnlyList<string>? phones)
    {
        if (phones is null || phones.Count == 0)
            return;

        w.WriteStartElement("TablaTelefonoEmisor");
        foreach (var phone in phones.Take(3))
            Text(w, "TelefonoEmisor", phone);
        w.WriteEndElement();
    }

    private static void WriteTotales(XmlWriter w, EcfDocument doc)
    {
        var t = doc.Totals;

        w.WriteStartElement("Totales");

        if (t.MontoGravadoTotal > 0m)
            El(w, "MontoGravadoTotal", EcfXmlFormat.Money(t.MontoGravadoTotal));
        RateBucket(w, "MontoGravadoI1", t.MontoGravadoI1);
        RateBucket(w, "MontoGravadoI2", t.MontoGravadoI2);
        RateBucket(w, "MontoGravadoI3", t.MontoGravadoI3);
        if (t.MontoExento > 0m)
            El(w, "MontoExento", EcfXmlFormat.Money(t.MontoExento));

        if (t.MontoGravadoI1 > 0m) El(w, "ITBIS1", EcfXmlFormat.RateIndicator(ItbisRate.Eighteen.Rate));
        if (t.MontoGravadoI2 > 0m) El(w, "ITBIS2", EcfXmlFormat.RateIndicator(ItbisRate.Sixteen.Rate));
        if (t.MontoGravadoI3 > 0m) El(w, "ITBIS3", EcfXmlFormat.RateIndicator(ItbisRate.Zero.Rate));

        if (t.TotalItbis > 0m)
            El(w, "TotalITBIS", EcfXmlFormat.Money(t.TotalItbis));
        if (t.MontoGravadoI1 > 0m) El(w, "TotalITBIS1", EcfXmlFormat.Money(t.Itbis1));
        if (t.MontoGravadoI2 > 0m) El(w, "TotalITBIS2", EcfXmlFormat.Money(t.Itbis2));
        if (t.MontoGravadoI3 > 0m) El(w, "TotalITBIS3", EcfXmlFormat.Money(t.Itbis3));

        if (t.MontoImpuestoAdicional > 0m)
            El(w, "MontoImpuestoAdicional", EcfXmlFormat.Money(t.MontoImpuestoAdicional));

        El(w, "MontoTotal", EcfXmlFormat.Money(t.MontoTotal));

        if (doc.Header.NonInvoiceableAmount != 0m)
        {
            El(w, "MontoNoFacturable", EcfXmlFormat.Money(t.MontoNoFacturable));
            El(w, "MontoPeriodo", EcfXmlFormat.Money(t.MontoPeriodo));
        }

        // Retenciones: el valor neto a pagar y los totales retenidos.
        if (doc.Type == EcfType.PagosExterior)
        {
            // El 47 siempre lleva retención de ISR (obligatoria por línea).
            El(w, "ValorPagar", EcfXmlFormat.Money(t.MontoTotal - t.TotalIsrWithheld));
            El(w, "TotalISRRetencion", EcfXmlFormat.Money(t.TotalIsrWithheld));
        }
        else
        {
            var retenido = t.TotalItbisWithheld + t.TotalIsrWithheld;
            if (retenido > 0m)
            {
                El(w, "ValorPagar", EcfXmlFormat.Money(t.MontoTotal - retenido));
                if (t.TotalItbisWithheld > 0m)
                    El(w, "TotalITBISRetenido", EcfXmlFormat.Money(t.TotalItbisWithheld));
                if (t.TotalIsrWithheld > 0m)
                    El(w, "TotalISRRetencion", EcfXmlFormat.Money(t.TotalIsrWithheld));
            }
        }

        w.WriteEndElement();

        void RateBucket(XmlWriter writer, string name, decimal value)
        {
            if (value > 0m)
                El(writer, name, EcfXmlFormat.Money(value));
        }
    }

    // ---- DetallesItems ----------------------------------------------------

    private static void WriteDetalles(XmlWriter w, EcfDocument doc)
    {
        w.WriteStartElement("DetallesItems");

        var amounts = doc.Calculation.Lines.ToDictionary(line => line.LineNumber);

        foreach (var line in doc.Lines.OrderBy(line => line.Number))
        {
            w.WriteStartElement("Item");
            El(w, "NumeroLinea", line.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            WriteCodigosItem(w, line.Codes);
            El(w, "IndicadorFacturacion", line.Rate.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            WriteRetencion(w, line.Retention, doc.Type);
            Text(w, "NombreItem", line.Name);
            El(w, "IndicadorBienoServicio", line.Kind.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Text(w, "DescripcionItem", line.Description);
            El(w, "CantidadItem", EcfXmlFormat.Money(line.Quantity));
            Text(w, "UnidadMedida", line.UnitOfMeasure);
            El(w, "PrecioUnitarioItem", EcfXmlFormat.UnitPrice(line.UnitPrice));
            if (line.Discount > 0m)
                El(w, "DescuentoMonto", EcfXmlFormat.Money(line.Discount));
            if (line.Surcharge > 0m)
                El(w, "RecargoMonto", EcfXmlFormat.Money(line.Surcharge));
            WriteOtraMonedaDetalle(w, line.ForeignCurrency);
            El(w, "MontoItem", EcfXmlFormat.Money(amounts[line.Number].LineAmount));
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    /// <summary><c>&lt;OtraMonedaDetalle&gt;</c> — precio y montos de la línea en divisa.</summary>
    private static void WriteOtraMonedaDetalle(XmlWriter w, EcfLineForeignCurrency? fx)
    {
        if (fx is null)
            return;

        w.WriteStartElement("OtraMonedaDetalle");
        if (fx.UnitPrice is { } up and > 0m)
            El(w, "PrecioOtraMoneda", EcfXmlFormat.UnitPrice(up));
        Amount(w, "DescuentoOtraMoneda", fx.Discount);
        Amount(w, "RecargoOtraMoneda", fx.Surcharge);
        Amount(w, "MontoItemOtraMoneda", fx.LineAmount);
        w.WriteEndElement();
    }

    /// <summary>
    /// <c>&lt;Retencion&gt;</c> del detalle (obligatorio en los tipos 41 y 47). El
    /// tipo 47 solo lleva ISR y el monto es obligatorio (aunque sea 0); su XSD no
    /// tiene <c>&lt;MontoITBISRetenido&gt;</c>.
    /// </summary>
    private static void WriteRetencion(XmlWriter w, EcfLineRetention? retention, EcfType type)
    {
        if (retention is null)
            return;

        w.WriteStartElement("Retencion");
        El(w, "IndicadorAgenteRetencionoPercepcion", Int(retention.Agent.Id));

        if (type == EcfType.PagosExterior)
        {
            El(w, "MontoISRRetenido", EcfXmlFormat.Money(retention.IsrWithheld));
        }
        else
        {
            if (retention.ItbisWithheld > 0m)
                El(w, "MontoITBISRetenido", EcfXmlFormat.Money(retention.ItbisWithheld));
            if (retention.IsrWithheld > 0m)
                El(w, "MontoISRRetenido", EcfXmlFormat.Money(retention.IsrWithheld));
        }

        w.WriteEndElement();
    }

    private static void WriteCodigosItem(XmlWriter w, IReadOnlyList<EcfItemCode>? codes)
    {
        if (codes is null || codes.Count == 0)
            return;

        w.WriteStartElement("TablaCodigosItem");
        foreach (var code in codes.Take(5))
        {
            w.WriteStartElement("CodigosItem");
            Text(w, "TipoCodigo", code.Type);
            Text(w, "CodigoItem", code.Value);
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    // ---- InformacionReferencia ------------------------------------------

    private static void WriteInformacionReferencia(XmlWriter w, EcfReference? reference)
    {
        if (reference is null)
            return;

        w.WriteStartElement("InformacionReferencia");
        Text(w, "NCFModificado", reference.ModifiedNcf);
        if (!string.IsNullOrWhiteSpace(reference.OtherIssuerRnc))
            El(w, "RNCOtroContribuyente", reference.OtherIssuerRnc.Trim());
        El(w, "FechaNCFModificado", EcfXmlFormat.Date(reference.ModifiedNcfDate));
        El(w, "CodigoModificacion", reference.Code.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        w.WriteEndElement();
    }

    // ---- helpers --------------------------------------------------------

    /// <summary>Elemento con valor fijo (ya seguro para XML).</summary>
    private static void El(XmlWriter w, string name, string value) =>
        w.WriteElementString(name, value);

    /// <summary>Entero en cultura invariante.</summary>
    private static string Int(int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Elemento opcional con valor ya seguro; se omite si es null/vacío.</summary>
    private static void Opt(XmlWriter w, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            w.WriteElementString(name, value.Trim());
    }

    /// <summary>
    /// Elemento monetario opcional (formato dinero). Se omite si es null o ≤ 0 — la
    /// mayoría de estos campos del XSD son "mayor que cero" y un 0 significa "no aplica".
    /// </summary>
    private static void Amount(XmlWriter w, string name, decimal? value)
    {
        if (value is { } v and > 0m)
            w.WriteElementString(name, EcfXmlFormat.Money(v));
    }

    /// <summary>Elemento de texto libre: <c>WriteElementString</c> escapa <c>&lt; &gt; &amp;</c>; el resto lo hace <see cref="EscapeDgii"/>.</summary>
    private static void Text(XmlWriter w, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            w.WriteElementString(name, value.Trim());
    }

    /// <summary>
    /// Completa el escape de la DGII (RF-02.3) sobre el cuerpo ya bien formado:
    /// <c>&lt; &gt; &amp;</c> los hizo <see cref="XmlWriter"/>; acá van
    /// <c>" ' © ® €</c>. El e-CF no tiene atributos, así que reemplazar en todo el
    /// cuerpo es seguro.
    /// </summary>
    private static string EscapeDgii(string body) =>
        body
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal)
            .Replace("©", "&#169;", StringComparison.Ordinal)
            .Replace("®", "&#174;", StringComparison.Ordinal)
            .Replace("€", "&#8364;", StringComparison.Ordinal);
}
