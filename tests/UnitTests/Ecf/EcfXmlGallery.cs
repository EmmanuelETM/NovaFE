using System.Globalization;
using System.Text;
using System.Xml.Linq;
using ErrorOr;
using Microsoft.Extensions.Time.Testing;
using NovaFE.Application.Ecf;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;
using NovaFE.Infrastructure.Ecf;
using NovaFE.UnitTests.Signing;

namespace NovaFE.UnitTests.Ecf;

/// <summary>
/// Genera una <b>galería</b> de XML de ejemplo (un archivo por tipo/combinación) en
/// <c>samples/ecf/</c> en la raíz del repo, y valida cada uno contra su XSD oficial.
/// <para>
/// Sirve para dos cosas: (1) es una prueba de humo — si algún serializador tira o
/// genera XML inválido, falla; (2) es la forma de <b>ver</b> el XML. Corré:
/// </para>
/// <code>dotnet test tests/UnitTests/NovaFE.UnitTests.csproj --filter "FullyQualifiedName~EcfXmlGallery"</code>
/// <para>
/// y abrí <c>samples/ecf/</c> (está en <c>.gitignore</c>). Para agregar una
/// combinación propia: sumá una entrada a <see cref="Cases"/>.
/// </para>
/// </summary>
public class EcfXmlGallery(ITestOutputHelper output)
{
    private static readonly EcfXmlSerializer Serializer = new();
    private static readonly RfceSerializer Rfce = new();
    private static readonly EcfXsdValidator Validator = new();

    /// <summary>
    /// <see cref="EcfSigner"/> real, con una firma autofirmada efímera en lugar del
    /// certificado del vault. Reloj fijo para que <c>&lt;FechaHoraFirma&gt;</c> no
    /// cambie entre corridas.
    /// </summary>
    private static readonly EcfSigner Signer = new(
        Serializer, Rfce, Validator,
        new SelfSignedCertificateSigner(),
        new FakeTimeProvider(EcfTestData.SignedAt));

    [Fact]
    public void Generate()
    {
        var folder = Path.Combine(RepoRoot(), "samples", "ecf");
        Directory.CreateDirectory(folder);

        var index = new StringBuilder()
            .AppendLine("# Galería de XML de ejemplo del e-CF")
            .AppendLine()
            .Append("Generado por `EcfXmlGallery` — ")
            .AppendLine(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
            .AppendLine("XML *pretty-printed* para leer; en el cable va en una sola línea.")
            .AppendLine()
            .AppendLine("| Archivo | Descripción | XSD |")
            .AppendLine("|---|---|---|");

        var failures = new List<string>();

        foreach (var (name, description, xml, validation) in Cases())
        {
            File.WriteAllText(Path.Combine(folder, name + ".xml"), Prettify(xml));

            var status = validation.IsError ? "❌ " + validation.FirstError.Description : "✅";
            index.Append("| `").Append(name).Append(".xml` | ").Append(description).Append(" | ").Append(status).AppendLine(" |");
            if (validation.IsError)
                failures.Add($"{name}: {validation.FirstError.Description}");
        }

        File.WriteAllText(Path.Combine(folder, "_README.md"), index.ToString());

        output.WriteLine($"Galería en: {folder}");
        failures.ShouldBeEmpty();
    }

    private static IEnumerable<(string Name, string Description, string Xml, ErrorOr<Success> Validation)> Cases()
    {
        foreach (var (name, description, doc) in Documents())
        {
            var xml = Serializer.Serialize(doc, EcfTestData.SignedAt);
            yield return (name, description, xml, Validator.Validate(SignedShape(xml), doc.Type));
        }

        var rfceDoc = EcfTestData.Consumo();
        var rfceXml = Rfce.Serialize(rfceDoc, "aB3xZ9");
        yield return ("19-rfce", "Resumen (RFCE) de un tipo 32 < DOP 250 000",
            rfceXml, Validator.ValidateRfce(SignedShapeRfce(rfceXml)));

        // Variantes firmadas: el e-CF con <Signature> real (XMLDSig, C14N estándar)
        // pasado por EcfSigner. La validación XSD ya corre dentro del signer.
        foreach (var (name, description, doc) in SignedShowcase())
        {
            var signed = Signer.SignAsync(doc, DgiiEnvironment.Test).GetAwaiter().GetResult();

            ErrorOr<Success> validation = Result.Success;
            if (signed.IsError)
                validation = signed.FirstError;

            yield return ($"{name}-firmado", $"{description} — firmado (XMLDSig)",
                signed.IsError ? "<error/>" : signed.Value.EcfXml, validation);

            if (!signed.IsError && signed.Value.RfceXml is { } signedRfce)
                yield return ($"{name}-rfce-firmado", $"{description} — RFCE firmado",
                    signedRfce, Result.Success);
        }
    }

    private static IEnumerable<(string Name, string Description, EcfDocument Doc)> SignedShowcase()
    {
        yield return ("20-credito-fiscal", "Crédito fiscal (31)", EcfTestData.CreditoFiscal());
        yield return ("21-consumo", "Factura de consumo (32) < DOP 250 000", EcfTestData.Consumo());
        yield return ("22-exportacion", "Exportaciones (46)", EcfTestData.Exportaciones());
    }

    private static IEnumerable<(string Name, string Description, EcfDocument Doc)> Documents()
    {
        yield return ("01-credito-fiscal", "Crédito fiscal (31), una línea gravada a 18 %",
            EcfTestData.CreditoFiscal());

        yield return ("02-credito-fiscal-multi-tasa", "Crédito fiscal con las cuatro tasas (18 / 16 / 0 / exento)",
            EcfTestData.CreditoFiscal(
                EcfTestData.Line(1, ItbisRate.Eighteen, unitPrice: 1000m, name: "Consultoría"),
                EcfTestData.Line(2, ItbisRate.Sixteen, unitPrice: 500m, name: "Seguro"),
                EcfTestData.Line(3, ItbisRate.Zero, unitPrice: 300m, name: "Exportación de servicio"),
                EcfTestData.Line(4, ItbisRate.Exempt, unitPrice: 200m, name: "Medicamento")));

        yield return ("03-consumo", "Factura de consumo (32), bajo monto (comprador sin RNC)",
            EcfTestData.Consumo());

        yield return ("04-nota-debito", "Nota de débito (33) que modifica un crédito fiscal",
            EcfTestData.NotaDebito());

        yield return ("05-nota-credito", "Nota de crédito (34), dentro de los 30 días (IndicadorNotaCredito 0)",
            EcfTestData.NotaCredito());

        yield return ("06-compras-retencion", "Compras (41) a informal, con retención de ITBIS + ISR por línea",
            EcfTestData.Compras());

        yield return ("07-gastos-menores", "Gastos menores (43) — caja chica, sin comprador",
            EcfTestData.GastosMenores());

        yield return ("08-regimenes-especiales", "Regímenes especiales (44) — todo exento",
            EcfTestData.RegimenesEspeciales());

        yield return ("09-gubernamental", "Gubernamental (45) — estructuralmente igual al 31",
            EcfTestData.Gubernamental());

        yield return ("10-exportacion", "Exportaciones (46) — toda línea a ITBIS 0 %",
            EcfTestData.Exportaciones());

        yield return ("11-pagos-al-exterior", "Pagos al exterior (47) — retención de solo ISR, comprador reducido",
            EcfTestData.PagosExterior());

        yield return ("12-consumo-precios-con-itbis", "Consumo (32) con IndicadorMontoGravado = 1 (precios con ITBIS incluido)",
            EcfDocument.Create(
                EcfType.Consumo,
                EcfTestData.Header(32, pricesIncludeTax: true) with
                {
                    SequenceExpiresOn = null,
                    Buyer = new EcfBuyer("Consumidor Final"),
                },
                [EcfTestData.Line(unitPrice: 1180m, name: "Producto con ITBIS incluido")]).Value);

        yield return ("13-credito-fiscal-otra-moneda", "Crédito fiscal facturado en USD (bloque OtraMoneda)",
            EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(31) with
                {
                    ForeignCurrency = new EcfForeignCurrency(
                        CurrencyCode.USD, 58.50m,
                        new EcfForeignCurrencyTotals(
                            MontoGravadoTotal: 34.19m, MontoGravadoI1: 34.19m,
                            TotalItbis: 6.15m, TotalItbis1: 6.15m, MontoTotal: 40.34m)),
                },
                [EcfTestData.Line(unitPrice: 2000m) with
                {
                    ForeignCurrency = new EcfLineForeignCurrency(UnitPrice: 34.19m, LineAmount: 34.19m),
                }]).Value);

        yield return ("14-credito-fiscal-seccion-d", "Crédito fiscal con descuento global (Sección D) sobre la tasa 18 %",
            EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(31) with
                {
                    GlobalAdjustments =
                    [
                        new EcfGlobalAdjustment(1, AdjustmentKind.Discount, ItbisRate.Eighteen, 1000m,
                            Description: "Descuento por volumen", Percentage: 10m),
                    ],
                },
                [EcfTestData.Line(unitPrice: 10000m, name: "Mercancía")]).Value);

        yield return ("15-credito-fiscal-isc-desglose", "Crédito fiscal de ron: desglose ImpuestosAdicionales (ISC 014 + propina 001)",
            EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(31),
                [EcfTestData.Line(unitPrice: 1000m, name: "Ron añejo") with
                {
                    AdditionalTaxes = 236.30m,
                    AdditionalTaxDetail =
                    [
                        new EcfAdditionalTax("014", Rate: 10m, IscEspecifico: 191.30m),
                        new EcfAdditionalTax("001", Rate: 10m, Otros: 45.00m),
                    ],
                    Details = new EcfLineDetails(AlcoholDegrees: 40m, ReferenceQuantity: 0.75m, ReferenceUnit: "43"),
                }]).Value);

        yield return ("16-credito-fiscal-embarque-transporte", "Crédito fiscal con InformacionesAdicionales + Transporte",
            EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(31) with
                {
                    Shipping = new EcfShippingInfo(
                        ShipmentDate: new DateOnly(2026, 3, 1), ContainerNumber: "MSKU7654321",
                        GrossWeight: 1250.50m, GrossWeightUnit: "43", PackageCount: 12m, PackageUnit: "43"),
                    Transport = new EcfTransport(Driver: "Juan Pérez", Plate: "A123456", Route: "Ruta 4"),
                },
                [EcfTestData.Line()]).Value);

        yield return ("17-credito-fiscal-subtotales-paginacion", "Crédito fiscal con Subtotales + Paginacion",
            EcfDocument.Create(
                EcfType.CreditoFiscal,
                EcfTestData.Header(31) with
                {
                    Subtotals = [new EcfSubtotal(Number: 1, Description: "Bienes", MontoGravadoTotal: 2000m, TotalItbis: 360m, Amount: 2360m, Lines: 1)],
                    Pagination = [new EcfPage(Number: 1, LineFrom: 1, LineTo: 1, MontoGravadoTotal: 2000m, Amount: 2360m)],
                },
                [EcfTestData.Line()]).Value);

        yield return ("18-exportacion-completa", "Exportaciones (46) con datos de embarque, transporte y OtraMoneda en EUR",
            EcfDocument.Create(
                EcfType.Exportaciones,
                EcfTestData.Header(46) with
                {
                    Buyer = new EcfBuyer("Global Imports LLC", ForeignId: "US-4471203"),
                    Shipping = new EcfShippingInfo(
                        ReferenceNumber: "7788",
                        Export: new EcfExportDetails(
                            LoadingPortName: "Puerto Haina", DeliveryTerms: "FOB",
                            TotalFob: 15000m, Insurance: 300m, Freight: 1200m, TotalCif: 16500m),
                        GrossWeight: 900m),
                    Transport = new EcfTransport(Via: TransportVia.Sea, OriginCountry: "República Dominicana",
                        DestinationCountry: "Estados Unidos", CarrierName: "Maersk Line"),
                    ForeignCurrency = new EcfForeignCurrency(
                        CurrencyCode.EUR, 63.10m,
                        new EcfForeignCurrencyTotals(
                            MontoGravadoTotal: 237.72m, MontoGravadoI3: 237.72m,
                            TotalItbis: 0m, TotalItbis3: 0m, MontoTotal: 237.72m)),
                },
                [EcfTestData.Line(rate: ItbisRate.Zero, unitPrice: 15000m, name: "Cacao orgánico en grano", kind: ItemKind.Good)]).Value);
    }

    // --- helpers --------------------------------------------------------

    private static string SignedShape(string xml) =>
        xml.Replace("</ECF>", "<Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"/></ECF>", StringComparison.Ordinal);

    private static string SignedShapeRfce(string xml) =>
        xml.Replace("</RFCE>", "<Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"/></RFCE>", StringComparison.Ordinal);

    private static string Prettify(string xml) =>
        XDocument.Parse(xml).ToString();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NovaFE.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repo (NovaFE.slnx).");
    }
}
