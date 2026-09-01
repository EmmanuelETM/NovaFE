using System.Globalization;
using System.Xml.Linq;
using NovaFE.Application.Ecf.Representation;
using NovaFE.Domain.Common;

namespace NovaFE.Infrastructure.Ecf.Representation;

/// <summary>
/// <see cref="IEcfRepresentationReader"/> sobre <see cref="XDocument"/>. El
/// <c>&lt;ECF&gt;</c> no lleva namespace; el bloque <c>&lt;Signature&gt;</c> se
/// ignora (no se mira). Lee de forma tolerante: lo que no está queda en
/// <c>null</c> y la RI simplemente no lo pinta.
/// </summary>
internal sealed class EcfXmlRepresentationReader : IEcfRepresentationReader
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public RepresentationModel Read(
        string signedEcfXml,
        RepresentationVerification verification,
        RepresentationDgiiStatus? dgii)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedEcfXml);
        ArgumentNullException.ThrowIfNull(verification);

        var root = XDocument.Parse(signedEcfXml).Root
            ?? throw new FormatException("El XML del e-CF no tiene elemento raíz.");

        var header = root.Element("Encabezado")
            ?? throw new FormatException("El XML del e-CF no tiene <Encabezado>.");
        var idDoc = header.Element("IdDoc") ?? new XElement("IdDoc");
        var emisor = header.Element("Emisor") ?? new XElement("Emisor");
        var totales = header.Element("Totales") ?? new XElement("Totales");

        return new RepresentationModel(
            Document: ReadDocument(idDoc, emisor, root),
            Issuer: ReadIssuer(emisor),
            Buyer: ReadBuyer(header.Element("Comprador")),
            Payment: ReadPayment(idDoc),
            Lines: ReadLines(root.Element("DetallesItems")),
            Totals: ReadTotals(totales),
            Reference: ReadReference(root.Element("InformacionReferencia")),
            Verification: verification,
            Dgii: dgii);
    }

    private static RepresentationDocumentInfo ReadDocument(XElement idDoc, XElement emisor, XElement root)
    {
        var code = Str(idDoc, "TipoeCF") ?? string.Empty;
        var type = int.TryParse(code, out var id) ? EcfType.FromCodeOrDefault(id) : null;

        return new RepresentationDocumentInfo(
            TypeCode: code,
            TypeName: type?.DisplayName ?? $"Comprobante tipo {code}",
            Encf: Str(idDoc, "eNCF") ?? string.Empty,
            IssueDate: Date(emisor, "FechaEmision") ?? default,
            SequenceExpiresOn: Date(idDoc, "FechaVencimientoSecuencia"),
            IncomeType: IncomeTypeLabel(Str(idDoc, "TipoIngresos")),
            SignedAtText: Str(root, "FechaHoraFirma") ?? string.Empty);
    }

    private static RepresentationParty ReadIssuer(XElement emisor) => new(
        Name: Str(emisor, "RazonSocialEmisor") ?? string.Empty,
        Rnc: Str(emisor, "RNCEmisor"),
        ForeignId: null,
        TradeName: Str(emisor, "NombreComercial"),
        Address: Str(emisor, "DireccionEmisor"),
        Municipality: Str(emisor, "Municipio"),
        Province: Str(emisor, "Provincia"),
        Phones: Phones(emisor.Element("TablaTelefonoEmisor")),
        Email: Str(emisor, "CorreoEmisor"),
        EconomicActivity: Str(emisor, "ActividadEconomica"),
        Contact: null);

    private static RepresentationParty? ReadBuyer(XElement? comprador)
    {
        if (comprador is null)
            return null;

        return new RepresentationParty(
            Name: Str(comprador, "RazonSocialComprador") ?? string.Empty,
            Rnc: Str(comprador, "RNCComprador"),
            ForeignId: Str(comprador, "IdentificadorExtranjero"),
            TradeName: null,
            Address: Str(comprador, "DireccionComprador"),
            Municipality: Str(comprador, "MunicipioComprador"),
            Province: Str(comprador, "ProvinciaComprador"),
            Phones: [],
            Email: Str(comprador, "CorreoComprador"),
            EconomicActivity: null,
            Contact: Str(comprador, "ContactoComprador"));
    }

    private static RepresentationPayment ReadPayment(XElement idDoc)
    {
        var methods = idDoc.Element("TablaFormasPago")?.Elements("FormaDePago")
            .Select(fp => new RepresentationPaymentMethod(
                PaymentMethodLabel(Str(fp, "FormaPago")),
                Dec(fp, "MontoPago") ?? 0m))
            .ToList() ?? [];

        return new RepresentationPayment(
            ConditionLabel: PaymentConditionLabel(Str(idDoc, "TipoPago")),
            DueDate: Date(idDoc, "FechaLimitePago"),
            Methods: methods);
    }

    private static List<RepresentationLine> ReadLines(XElement? detalles)
    {
        if (detalles is null)
            return [];

        return detalles.Elements("Item").Select(item =>
        {
            var retencion = item.Element("Retencion");
            return new RepresentationLine(
                Number: int.TryParse(Str(item, "NumeroLinea"), out var n) ? n : 0,
                Name: Str(item, "NombreItem") ?? Str(item, "DescripcionItem") ?? string.Empty,
                Kind: GoodOrServiceLabel(Str(item, "IndicadorBienoServicio")),
                Quantity: Dec(item, "CantidadItem") ?? 0m,
                UnitOfMeasure: Str(item, "UnidadMedida"),
                UnitPrice: Dec(item, "PrecioUnitarioItem") ?? 0m,
                Discount: Dec(item, "DescuentoMonto"),
                Surcharge: Dec(item, "RecargoMonto"),
                TaxLabel: BillingIndicatorLabel(Str(item, "IndicadorFacturacion")),
                Amount: Dec(item, "MontoItem") ?? 0m,
                ItbisWithheld: Dec(retencion, "MontoITBISRetenido"),
                IsrWithheld: Dec(retencion, "MontoISRRetenido"));
        }).ToList();
    }

    private static RepresentationTotals ReadTotals(XElement t) => new(
        MontoGravadoTotal: Dec(t, "MontoGravadoTotal"),
        MontoGravadoI1: Dec(t, "MontoGravadoI1"),
        MontoGravadoI2: Dec(t, "MontoGravadoI2"),
        MontoGravadoI3: Dec(t, "MontoGravadoI3"),
        MontoExento: Dec(t, "MontoExento"),
        Itbis1: Dec(t, "TotalITBIS1"),
        Itbis2: Dec(t, "TotalITBIS2"),
        Itbis3: Dec(t, "TotalITBIS3"),
        TotalItbis: Dec(t, "TotalITBIS"),
        MontoImpuestoAdicional: Dec(t, "MontoImpuestoAdicional"),
        TotalItbisWithheld: Dec(t, "TotalITBISRetenido"),
        TotalIsrWithheld: Dec(t, "TotalISRRetencion"),
        MontoTotal: Dec(t, "MontoTotal") ?? 0m,
        AmountDue: Dec(t, "ValorPagar"));

    private static RepresentationReference? ReadReference(XElement? reference)
    {
        if (reference is null)
            return null;

        return new RepresentationReference(
            ModifiedNcf: Str(reference, "NCFModificado") ?? string.Empty,
            ModifiedDate: Date(reference, "FechaNCFModificado"),
            Reason: ModificationReasonLabel(Str(reference, "CodigoModificacion")));
    }

    private static List<string> Phones(XElement? table) =>
        table?.Elements("TelefonoEmisor").Select(e => e.Value.Trim())
            .Where(v => v.Length > 0).ToList() ?? [];

    // --- lectura tolerante ---------------------------------------------------

    private static string? Str(XElement? parent, string name)
    {
        var value = parent?.Element(name)?.Value.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static decimal? Dec(XElement? parent, string name) =>
        Str(parent, name) is { } s && decimal.TryParse(s, NumberStyles.Number, Invariant, out var d) ? d : null;

    private static DateOnly? Date(XElement? parent, string name) =>
        Str(parent, name) is { } s
        && DateOnly.TryParseExact(s, DominicanTimeZone.DateFormat, Invariant, DateTimeStyles.None, out var date)
            ? date
            : null;

    // --- códigos DGII → texto (español; es lo que emite la API) -------------

    private static string? PaymentConditionLabel(string? code) => code switch
    {
        "1" => "Contado",
        "2" => "Crédito",
        "3" => "Gratuito",
        _ => null,
    };

    private static string PaymentMethodLabel(string? code) => code switch
    {
        "1" => "Efectivo",
        "2" => "Cheque / Transferencia / Depósito",
        "3" => "Tarjeta de débito o crédito",
        "4" => "Venta a crédito",
        "5" => "Bonos o certificados de regalo",
        "6" => "Permuta",
        "7" => "Nota de crédito",
        "8" => "Otras formas de pago",
        _ => "Forma de pago " + (code ?? "?"),
    };

    private static string? GoodOrServiceLabel(string? code) => code switch
    {
        "1" => "Bien",
        "2" => "Servicio",
        _ => null,
    };

    private static string? BillingIndicatorLabel(string? code) => code switch
    {
        "1" => "18%",
        "2" => "16%",
        "3" => "0%",
        "4" => "Exento",
        _ => null,
    };

    private static string? IncomeTypeLabel(string? code) => code switch
    {
        "01" => "Operaciones (no financieras)",
        "02" => "Financieros",
        "03" => "Extraordinarios",
        "04" => "Arrendamiento",
        "05" => "Venta de activo depreciable",
        "06" => "Otros ingresos",
        _ => null,
    };

    private static string? ModificationReasonLabel(string? code) => code switch
    {
        "1" => "Anula el comprobante referenciado",
        "2" => "Corrige texto del comprobante",
        "3" => "Corrige montos del comprobante",
        "4" => "Reemplaza un comprobante de contingencia",
        "5" => "Referencia a una factura de consumo",
        _ => null,
    };
}
