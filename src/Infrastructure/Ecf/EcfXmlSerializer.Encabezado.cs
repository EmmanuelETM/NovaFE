using NovaFE.Domain.Ecf;

namespace NovaFE.Infrastructure.Ecf;

// <Encabezado> — IdDoc, Emisor, Comprador, InformacionesAdicionales, Transporte.
// (Totales y OtraMoneda están en EcfXmlSerializer.Totales.cs.)
internal sealed partial class EcfXmlSerializer
{
    private static void WriteEncabezado(EcfElementWriter w, EcfDocument doc, EcfXmlProfile p)
    {
        var h = doc.Header;

        using (w.Element("Encabezado"))
        {
            w.El("Version", "1.0");
            WriteIdDoc(w, doc, p);
            WriteEmisor(w, h.Issuer, h.IssueDate);
            WriteComprador(w, h.Buyer, p.Comprador);
            WriteInformacionesAdicionales(w, h.Shipping, p.ExportShipping);
            WriteTransporte(w, h.Transport, p.Transport);
            WriteTotales(w, doc, p);
            WriteOtraMoneda(w, h.ForeignCurrency, p.Totals);
        }
    }

    /// <summary><c>&lt;IdDoc&gt;</c> — la secuencia inicial la fija <see cref="EcfXmlProfile"/>.</summary>
    private static void WriteIdDoc(EcfElementWriter w, EcfDocument doc, EcfXmlProfile p)
    {
        var h = doc.Header;

        using (w.Element("IdDoc"))
        {
            w.El("TipoeCF", doc.Type.Id);
            w.El("eNCF", h.Encf.Value);

            if (p.MinimalIdDoc)
            {
                w.El("FechaVencimientoSecuencia", EcfXmlFormat.Date(h.SequenceExpiresOn!.Value));
                w.El("TipoPago", h.Payment.Condition.Id);
                return;
            }

            if (p.CreditNoteIndicator)
                w.El("IndicadorNotaCredito", doc.CreditNoteIndicator ?? 0);
            else
                w.Opt("FechaVencimientoSecuencia", h.SequenceExpiresOn);

            if (h.DeferredDelivery && p.DeferredDeliveryIndicator)
                w.El("IndicadorEnvioDiferido", "1");
            if (p.TaxedIndicator)
                w.El("IndicadorMontoGravado", h.PricesIncludeTax ? "1" : "0");
            if (p.IncomeType)
                w.El("TipoIngresos", h.IncomeType);
            w.El("TipoPago", h.Payment.Condition.Id);
            w.Opt("FechaLimitePago", h.Payment.DueDate);
            if (p.PaymentMethods)
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
            w.Opt("NombreComercial", issuer.TradeName);
            w.Opt("Sucursal", issuer.Branch);
            w.Opt("DireccionEmisor", issuer.Address);
            w.Opt("Municipio", issuer.Municipality);
            w.Opt("Provincia", issuer.Province);
            WritePhones(w, issuer.Phones);
            w.Opt("CorreoEmisor", issuer.Email);
            w.Opt("ActividadEconomica", issuer.EconomicActivity);
            w.Opt("CodigoVendedor", issuer.SellerCode);
            w.Opt("NumeroFacturaInterna", issuer.InternalInvoiceNumber);
            w.Opt("InformacionAdicionalEmisor", issuer.AdditionalInfo);
            w.El("FechaEmision", EcfXmlFormat.Date(issueDate));
        }
    }

    private static void WritePhones(EcfElementWriter w, IReadOnlyList<string>? phones)
    {
        if (phones is not { Count: > 0 })
            return;

        using (w.Element("TablaTelefonoEmisor"))
        {
            foreach (var phone in phones.Take(3))
                w.Opt("TelefonoEmisor", phone);
        }
    }

    /// <summary>
    /// <c>&lt;Comprador&gt;</c>. <see cref="CompradorShape.None"/> (tipo 43) no emite el
    /// bloque; <see cref="CompradorShape.Reduced"/> (tipo 47) solo identificador
    /// extranjero y razón social.
    /// </summary>
    private static void WriteComprador(EcfElementWriter w, EcfBuyer buyer, CompradorShape shape)
    {
        if (shape == CompradorShape.None)
            return;

        using (w.Element("Comprador"))
        {
            if (shape == CompradorShape.Reduced)
            {
                w.Opt("IdentificadorExtranjero", buyer.ForeignId);
                w.Opt("RazonSocialComprador", buyer.Name);
                return;
            }

            if (buyer.Rnc is { } rnc)
                w.El("RNCComprador", rnc.Value);
            w.Opt("IdentificadorExtranjero", buyer.ForeignId);
            w.Opt("RazonSocialComprador", buyer.Name);
            w.Opt("ContactoComprador", buyer.Contact);
            w.Opt("CorreoComprador", buyer.Email);
            w.Opt("DireccionComprador", buyer.Address);
            w.Opt("MunicipioComprador", buyer.Municipality);
            w.Opt("ProvinciaComprador", buyer.Province);
            w.Opt("InformacionAdicionalComprador", buyer.AdditionalInfo);
        }
    }

    /// <summary>
    /// <c>&lt;InformacionesAdicionales&gt;</c>. Los campos de exportación (FOB/CIF,
    /// puertos) se intercalan entre <c>NumeroReferencia</c> y <c>PesoBruto</c>, solo
    /// cuando <paramref name="exportShipping"/> (tipo 46).
    /// </summary>
    private static void WriteInformacionesAdicionales(EcfElementWriter w, EcfShippingInfo? s, bool exportShipping)
    {
        if (s is null)
            return;

        using (w.Element("InformacionesAdicionales"))
        {
            w.Opt("FechaEmbarque", s.ShipmentDate);
            w.Opt("NumeroEmbarque", s.ShipmentNumber);
            w.Opt("NumeroContenedor", s.ContainerNumber);
            w.Opt("NumeroReferencia", s.ReferenceNumber);

            if (exportShipping && s.Export is { } e)
            {
                w.Opt("NombrePuertoEmbarque", e.LoadingPortName);
                w.Opt("CondicionesEntrega", e.DeliveryTerms);
                w.MoneyOpt("TotalFob", e.TotalFob);
                w.MoneyOpt("Seguro", e.Insurance);
                w.MoneyOpt("Flete", e.Freight);
                w.MoneyOpt("OtrosGastos", e.OtherCharges);
                w.MoneyOpt("TotalCif", e.TotalCif);
                w.Opt("RegimenAduanero", e.CustomsRegime);
                w.Opt("NombrePuertoSalida", e.DeparturePortName);
                w.Opt("NombrePuertoDesembarque", e.UnloadingPortName);
            }

            w.MoneyOpt("PesoBruto", s.GrossWeight);
            w.MoneyOpt("PesoNeto", s.NetWeight);
            w.Opt("UnidadPesoBruto", s.GrossWeightUnit);
            w.Opt("UnidadPesoNeto", s.NetWeightUnit);
            w.MoneyOpt("CantidadBulto", s.PackageCount);
            w.Opt("UnidadBulto", s.PackageUnit);
            w.MoneyOpt("VolumenBulto", s.Volume);
            w.Opt("UnidadVolumen", s.VolumeUnit);
        }
    }

    /// <summary>
    /// <c>&lt;Transporte&gt;</c>. <see cref="TransportShape.Export"/> antepone
    /// vía/país/compañía transportista; <see cref="TransportShape.DestinationOnly"/>
    /// (tipo 47) solo emite <c>&lt;PaisDestino&gt;</c>.
    /// </summary>
    private static void WriteTransporte(EcfElementWriter w, EcfTransport? t, TransportShape shape)
    {
        if (t is null)
            return;

        using (w.Element("Transporte"))
        {
            if (shape == TransportShape.DestinationOnly)
            {
                w.Opt("PaisDestino", t.DestinationCountry);
                return;
            }

            if (shape == TransportShape.Export)
            {
                w.Opt("ViaTransporte", t.Via?.Code);
                w.Opt("PaisOrigen", t.OriginCountry);
                w.Opt("DireccionDestino", t.DestinationAddress);
                w.Opt("PaisDestino", t.DestinationCountry);
                w.Opt("RNCIdentificacionCompaniaTransportista", t.CarrierRnc);
                w.Opt("NombreCompaniaTransportista", t.CarrierName);
                w.Opt("NumeroViaje", t.VoyageNumber);
            }

            w.Opt("Conductor", t.Driver);
            w.Opt("DocumentoTransporte", t.TransportDocument);
            w.Opt("Ficha", t.VehicleId);
            w.Opt("Placa", t.Plate);
            w.Opt("RutaTransporte", t.Route);
            w.Opt("ZonaTransporte", t.Zone);
            w.Opt("NumeroAlbaran", t.DeliveryNote);
        }
    }
}
