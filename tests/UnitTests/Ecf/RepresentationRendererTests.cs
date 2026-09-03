using System.Globalization;
using System.Text;
using NovaFE.Application.Ecf.Representation;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;
using NovaFE.Infrastructure.Ecf;
using NovaFE.Infrastructure.Ecf.Representation;
using NovaFE.Infrastructure.Representation;
using QuestPDF.Fluent;

namespace NovaFE.UnitTests.Ecf;

/// <summary>
/// Renderiza la Representación Impresa de varios comprobantes y <b>vuelca los PDF</b>
/// a <c>samples/representation/</c> en la raíz del repo (está en <c>.gitignore</c>).
/// Es la forma de <b>ver</b> el diseño:
/// <code>dotnet test tests/UnitTests/NovaFE.UnitTests.csproj --filter "FullyQualifiedName~RepresentationRendererTests"</code>
/// </summary>
public class RepresentationRendererTests
{
    private static readonly EcfXmlSerializer Serializer = new();
    private static readonly EcfXmlRepresentationReader Reader = new();
    private static readonly QuestPdfRepresentationRenderer Renderer = new();

    private static RepresentationModel Model(EcfDocument document, RepresentationDgiiStatus? dgii = null)
    {
        var xml = Serializer.Serialize(document, EcfTestData.SignedAt);
        var url = EcfVerificationUrl.For(document, Domain.Common.DgiiEnvironment.TestEcf, "aB3xZ9", EcfTestData.SignedAt);
        return Reader.Read(xml, new RepresentationVerification("aB3xZ9", url), dgii);
    }

    [Fact]
    public void Renders_a_valid_pdf_for_each_type()
    {
        var accepted = new RepresentationDgiiStatus("accepted", 1, "Aceptado", "TRACK-2026-0042");
        var cases = new (string Name, EcfDocument Doc, RepresentationDgiiStatus? Dgii)[]
        {
            ("01-credito-fiscal", EcfTestData.CreditoFiscal(), accepted),
            ("02-credito-fiscal-multi-tasa", EcfTestData.CreditoFiscal(
                EcfTestData.Line(1, ItbisRate.Eighteen, unitPrice: 1000m, name: "Licencia de software (anual)"),
                EcfTestData.Line(2, ItbisRate.Sixteen, unitPrice: 500m, name: "Servicio de instalación"),
                EcfTestData.Line(3, ItbisRate.Exempt, unitPrice: 300m, name: "Material impreso")), accepted),
            ("03-consumo", EcfTestData.Consumo(), new RepresentationDgiiStatus("submitted", null, null, "TRACK-2026-0101")),
            ("05-nota-credito", EcfTestData.NotaCredito(), accepted),
            ("06-compras-retencion", EcfTestData.Compras(), accepted),
            ("07-gastos-menores", EcfTestData.GastosMenores(), null),
            ("10-exportacion", EcfTestData.Exportaciones(), accepted),
        };

        var folder = Path.Combine(RepoRoot(), "samples", "representation");
        Directory.CreateDirectory(folder);
        var index = new StringBuilder("# Galería de Representaciones Impresas\n\n");

        foreach (var (name, doc, dgii) in cases)
        {
            var pdf = Renderer.Render(Model(doc, dgii), RepresentationLayout.Letter);

            pdf.Length.ShouldBeGreaterThan(3000, name);
            Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-", name);

            File.WriteAllBytes(Path.Combine(folder, name + ".pdf"), pdf);
            index.Append("- `").Append(name).Append(".pdf`\n");

            // PNG de la primera página, para hojear el diseño sin abrir un lector de PDF.
            var png = new LetterRepresentationDocument(Model(doc, dgii))
                .GenerateImages(new QuestPDF.Infrastructure.ImageGenerationSettings { RasterDpi = 144 })
                .First();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), png);
        }

        File.WriteAllText(Path.Combine(folder, "_README.md"), index.ToString());
    }

    [Fact]
    public void A_long_comprobante_paginates()
    {
        var lines = Enumerable.Range(1, 60)
            .Select(i => EcfTestData.Line(i, ItbisRate.Eighteen, unitPrice: 100m + i, name: $"Artículo de catálogo #{i:000}"))
            .ToArray();

        var pdf = Renderer.Render(Model(EcfTestData.CreditoFiscal(lines)), RepresentationLayout.Letter);
        var folder = Path.Combine(RepoRoot(), "samples", "representation");
        File.WriteAllBytes(Path.Combine(folder, "99-paginado.pdf"), pdf);

        var images = new LetterRepresentationDocument(Model(EcfTestData.CreditoFiscal(lines)))
            .GenerateImages(new QuestPDF.Infrastructure.ImageGenerationSettings { RasterDpi = 144 })
            .ToList();
        for (var i = 0; i < images.Count; i++)
            File.WriteAllBytes(Path.Combine(folder, $"99-paginado-p{i + 1}.png"), images[i]);

        images.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Renders_a_valid_pos_pdf_for_each_type()
    {
        var accepted = new RepresentationDgiiStatus("accepted", 1, "Aceptado", "TRACK-2026-0042");
        var cases = new (string Name, EcfDocument Doc, RepresentationDgiiStatus? Dgii)[]
        {
            ("01-credito-fiscal", EcfTestData.CreditoFiscal(), accepted),
            ("02-credito-fiscal-multi-tasa", EcfTestData.CreditoFiscal(
                EcfTestData.Line(1, ItbisRate.Eighteen, unitPrice: 1000m, name: "Licencia de software (anual)"),
                EcfTestData.Line(2, ItbisRate.Sixteen, unitPrice: 500m, name: "Servicio de instalación"),
                EcfTestData.Line(3, ItbisRate.Exempt, unitPrice: 300m, name: "Material impreso")), accepted),
            ("03-consumo", EcfTestData.Consumo(), new RepresentationDgiiStatus("submitted", null, null, "TRACK-2026-0101")),
            ("05-nota-credito", EcfTestData.NotaCredito(), accepted),
            ("06-compras-retencion", EcfTestData.Compras(), accepted),
            ("07-gastos-menores", EcfTestData.GastosMenores(), null),
        };

        var folder = Path.Combine(RepoRoot(), "samples", "representation", "pos");
        Directory.CreateDirectory(folder);

        foreach (var (name, doc, dgii) in cases)
        {
            var model = Model(doc, dgii);
            var pdf = Renderer.Render(model, RepresentationLayout.Pos);

            pdf.Length.ShouldBeGreaterThan(3000, name);
            Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-", name);

            File.WriteAllBytes(Path.Combine(folder, name + ".pdf"), pdf);

            var png = new PosRepresentationDocument(model)
                .GenerateImages(new QuestPDF.Infrastructure.ImageGenerationSettings { RasterDpi = 192 })
                .First();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), png);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NovaFE.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repo.");
    }

    static RepresentationRendererTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
}
