using System.Globalization;
using NovaFE.Application.Ecf.Representation;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NovaFE.Infrastructure.Representation;

using T = RepresentationTheme;

/// <summary>
/// Representación Impresa en tamaño Carta. Un documento fiscal que se lee como una
/// pieza de producto: rótulo del comprobante arriba a la derecha, partes en dos
/// columnas, tabla de líneas con reglas finas, panel de totales alineado a la
/// derecha, y el timbre (QR + código de seguridad) en el pie. Todas las páginas
/// repiten cabecera y pie; el detalle pagina solo.
/// </summary>
internal sealed class LetterRepresentationDocument(RepresentationModel model) : IDocument
{
    private static readonly NumberFormatInfo Pesos = new()
    {
        NumberGroupSeparator = ",",
        NumberDecimalSeparator = ".",
        NumberDecimalDigits = 2,
    };

    private readonly bool _anyDiscount = model.Lines.Any(l => l.Discount is > 0m);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Representación Impresa {model.Document.Encf}",
        Author = "NovaFE",
        Subject = model.Document.TypeName,
    };

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.Letter);
        page.MarginVertical(T.PageMarginY);
        page.MarginHorizontal(T.PageMarginX);
        page.DefaultTextStyle(x => x
            .FontFamily(RepresentationFonts.Sans).FontSize(T.Body).FontColor(T.Ink).LineHeight(1.3f));

        page.Header().Element(Header);
        page.Content().Element(Content);
        page.Footer().Element(Footer);
    });

    // ---- cabecera ----------------------------------------------------------

    private void Header(IContainer container) => container.Column(col =>
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().PaddingTop(2).Column(c =>
            {
                c.Item().Text("REPRESENTACIÓN IMPRESA").Style(T.EyebrowStyle);
                c.Item().PaddingTop(T.Unit).Text(model.Issuer.Name)
                    .FontFamily(RepresentationFonts.Sans).FontSize(T.Title).SemiBold().FontColor(T.Ink);

                if (model.Issuer.TradeName is { } trade && !trade.Equals(model.Issuer.Name, StringComparison.OrdinalIgnoreCase))
                    c.Item().PaddingTop(1).Text(trade).FontSize(T.Small).FontColor(T.InkSoft);
            });

            row.ConstantItem(240).Column(c =>
            {
                c.Item().AlignRight().Text(model.Document.TypeName)
                    .FontFamily(RepresentationFonts.Sans).FontSize(T.BodyStrong).Medium().FontColor(T.Ink);
                c.Item().PaddingTop(T.Unit * 2).AlignRight().Text("e-NCF").Style(T.LabelStyle);
                c.Item().PaddingTop(1).AlignRight().Text(model.Document.Encf)
                    .FontFamily(RepresentationFonts.Mono).FontSize(T.Title * 0.72f).Medium().FontColor(T.Ink);
            });
        });

        col.Item().PaddingTop(T.Unit * 3).LineHorizontal(1f).LineColor(T.Ink);
    });

    // ---- contenido --------------------------------------------------------

    private void Content(IContainer container) => container.PaddingTop(T.Unit * 3).Column(col =>
    {
        col.Spacing(T.Unit * 4);

        col.Item().Element(MetaStrip);
        col.Item().Element(Parties);
        col.Item().Element(LineTable);
        col.Item().Element(TotalsAndReference);

        if (model.ContingencyNotice is { Length: > 0 } notice)
            col.Item().Element(c => Callout(c, "CONTINGENCIA", notice));

        col.Item().PaddingTop(T.Unit * 3).Element(Timbre);
    });

    // ---- timbre (QR + código de seguridad + estado DGII) ---------------

    private void Timbre(IContainer container) => container.Column(col =>
    {
        col.Item().LineHorizontal(0.5f).LineColor(T.Hairline);
        col.Item().PaddingTop(T.Unit * 2.5f).Row(row =>
        {
            row.ConstantItem(78).Height(78).Image(QrImage.Png(model.Verification.QrUrl));
            row.ConstantItem(T.Unit * 4);

            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Código de seguridad").Style(T.LabelStyle);
                c.Item().PaddingTop(1).Text(model.Verification.SecurityCode)
                    .FontFamily(RepresentationFonts.Mono).FontSize(12).Medium().FontColor(T.Ink);
                c.Item().PaddingTop(T.Unit * 1.5f).Text("Firmado").Style(T.LabelStyle);
                c.Item().PaddingTop(1).Text(model.Document.SignedAtText)
                    .FontFamily(RepresentationFonts.Mono).FontSize(T.Small).FontColor(T.InkSoft);
            });

            row.ConstantItem(168).Column(c =>
            {
                if (model.Dgii is not { } dgii)
                    return;

                var (ink, bg) = StatusColors(dgii);
                c.Item().AlignRight().Background(bg).PaddingVertical(3).PaddingHorizontal(T.Unit * 2)
                    .Text(StatusLabel(dgii)).FontSize(T.Label).FontColor(ink).SemiBold().LetterSpacing(0.03f);
                if (dgii.TrackId is { } track)
                    c.Item().PaddingTop(T.Unit).AlignRight().Text($"TrackId {track}")
                        .FontFamily(RepresentationFonts.Mono).FontSize(T.Label).FontColor(T.InkFaint);
            });
        });
    });

    private void MetaStrip(IContainer container)
    {
        var fields = new List<(string Label, string Value, bool Mono)>
        {
            ("Fecha de emisión", D(model.Document.IssueDate), true),
        };
        if (model.Document.SequenceExpiresOn is { } exp)
            fields.Add(("Vence la secuencia", D(exp), true));
        if (model.Document.IncomeType is { } income)
            fields.Add(("Tipo de ingreso", income, false));
        if (model.Payment.ConditionLabel is { } cond)
            fields.Add(("Condición de pago", cond, false));
        if (model.Payment.DueDate is { } due)
            fields.Add(("Fecha límite de pago", D(due), true));

        container
            .BorderHorizontal(0.5f).BorderColor(T.Hairline)
            .PaddingVertical(T.Unit * 2.5f)
            .Column(col =>
            {
                col.Spacing(T.Unit * 2);
                foreach (var chunk in fields.Chunk(3))
                    col.Item().Row(row =>
                    {
                        row.Spacing(T.Unit * 3);
                        foreach (var (label, value, mono) in chunk)
                            Field(row.RelativeItem(), label, value, mono);
                        for (var i = chunk.Length; i < 3; i++)
                            row.RelativeItem();
                    });
            });
    }

    private static void Field(IContainer container, string label, string value, bool mono = false) =>
        container.Column(c =>
        {
            c.Item().Text(label).Style(T.LabelStyle);
            var t = c.Item().Text(value).FontSize(T.Small).FontColor(T.Ink);
            if (mono)
                t.FontFamily(RepresentationFonts.Mono);
        });

    private void Parties(IContainer container) => container.Row(row =>
    {
        row.Spacing(T.Unit * 6);
        row.RelativeItem().Element(c => Party(c, "EMISOR", model.Issuer));
        row.RelativeItem().Element(c => Party(c, "COMPRADOR", model.Buyer));
    });

    private static void Party(IContainer container, string heading, RepresentationParty? party) => container.Column(col =>
    {
        col.Item().Text(heading).Style(T.EyebrowStyle).FontColor(T.InkSoft);
        col.Item().PaddingTop(T.Unit).LineHorizontal(0.5f).LineColor(T.Hairline);
        col.Item().PaddingTop(T.Unit * 1.5f);

        if (party is null)
        {
            col.Item().Text("Sin identificación del comprador").FontSize(T.Small).FontColor(T.InkFaint).Italic();
            return;
        }

        col.Item().Text(party.Name).FontSize(T.BodyStrong).Medium().FontColor(T.Ink);

        if (party.TaxId is { } taxId)
        {
            var kind = party.Rnc is not null ? "RNC" : "ID";
            col.Item().Text($"{kind} {taxId}").FontFamily(RepresentationFonts.Mono).FontSize(T.Small).FontColor(T.InkSoft);
        }

        col.Item().PaddingTop(T.Unit);
        Line(col, party.Address);
        Line(col, Join(party.Municipality, party.Province));
        Line(col, party.Phones.Count > 0 ? string.Join(" · ", party.Phones) : null);
        Line(col, party.Email);
        Line(col, party.EconomicActivity);
        Line(col, party.Contact is { } contact ? $"Contacto: {contact}" : null);

        static void Line(ColumnDescriptor col, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                col.Item().Text(value).FontSize(T.Small).FontColor(T.InkSoft);
        }
    });

    // ---- tabla de líneas ------------------------------------------------

    private void LineTable(IContainer container) => container.Table(table =>
    {
        table.ColumnsDefinition(cols =>
        {
            cols.ConstantColumn(20);   // #
            cols.RelativeColumn();     // descripción
            cols.ConstantColumn(46);   // cantidad
            cols.ConstantColumn(70);   // precio unitario
            if (_anyDiscount)
                cols.ConstantColumn(58); // descuento
            cols.ConstantColumn(42);   // itbis
            cols.ConstantColumn(76);   // importe
        });

        table.Header(header =>
        {
            HeaderCell(header, "#", left: true);
            HeaderCell(header, "Descripción", left: true);
            HeaderCell(header, "Cant.");
            HeaderCell(header, "Precio unit.");
            if (_anyDiscount)
                HeaderCell(header, "Desc.");
            HeaderCell(header, "ITBIS");
            HeaderCell(header, "Importe");
        });

        foreach (var line in model.Lines)
        {
            BodyCell(table).Text(line.Number.ToString(CultureInfo.InvariantCulture))
                .FontFamily(RepresentationFonts.Mono).FontSize(T.Small).FontColor(T.InkSoft);

            BodyCell(table).Column(c =>
            {
                c.Item().Text(line.Name).FontSize(T.Body).FontColor(T.Ink);

                if (line.Kind is { } kind)
                    c.Item().Text(kind).FontSize(T.Label).FontColor(T.InkFaint);

                if (line is { ItbisWithheld: > 0m } or { IsrWithheld: > 0m })
                {
                    var parts = new List<string>();
                    if (line.ItbisWithheld is > 0m and { } wi)
                        parts.Add($"ITBIS {Money(wi)}");
                    if (line.IsrWithheld is > 0m and { } wr)
                        parts.Add($"ISR {Money(wr)}");
                    c.Item().Text($"Retención: {string.Join(" · ", parts)}")
                        .FontSize(T.Label).FontColor(T.InkSoft).Italic();
                }
            });

            NumCell(table, Qty(line.Quantity));
            NumCell(table, Money(line.UnitPrice));
            if (_anyDiscount)
                NumCell(table, line.Discount is > 0m and { } d ? Money(d) : "—");
            BodyCell(table).AlignRight().Text(line.TaxLabel ?? "—").FontSize(T.Small).FontColor(T.InkSoft);
            NumCell(table, Money(line.Amount), strong: true);
        }
    });

    private static void HeaderCell(TableCellDescriptor header, string text, bool left = false)
    {
        var cell = header.Cell().PaddingBottom(T.Unit).BorderBottom(0.75f).BorderColor(T.Ink)
            .Text(text).Style(T.LabelStyle).FontColor(T.InkSoft);
        if (!left)
            cell.AlignRight();
    }

    private static IContainer BodyCell(TableDescriptor table) =>
        table.Cell().BorderBottom(0.5f).BorderColor(T.Hairline).PaddingVertical(T.Unit * 1.25f).PaddingRight(T.Unit);

    private static void NumCell(TableDescriptor table, string value, bool strong = false)
    {
        var text = BodyCell(table).AlignRight().Text(value)
            .FontFamily(RepresentationFonts.Mono).FontSize(T.Small).FontColor(T.Ink);
        if (strong)
            text.Medium();
    }

    // ---- totales + referencia -----------------------------------------

    private void TotalsAndReference(IContainer container) => container.Row(row =>
    {
        row.RelativeItem().Column(c =>
        {
            if (model.Reference is { } reference)
                c.Item().Element(x => ReferencePanel(x, reference));

            if (model.Payment.Methods.Count > 0)
                c.Item().PaddingTop(model.Reference is null ? 0 : T.Unit * 3).Element(PaymentMethods);
        });

        row.ConstantItem(T.Unit * 6);
        row.ConstantItem(232).Element(TotalsPanel);
    });

    private void PaymentMethods(IContainer container) => container.Column(col =>
    {
        col.Item().Text("FORMAS DE PAGO").Style(T.EyebrowStyle).FontColor(T.InkSoft);
        col.Item().PaddingTop(T.Unit);
        foreach (var method in model.Payment.Methods)
            col.Item().Row(r =>
            {
                r.RelativeItem().Text(method.Label).FontSize(T.Small).FontColor(T.InkSoft);
                r.AutoItem().Text(Money(method.Amount)).FontFamily(RepresentationFonts.Mono).FontSize(T.Small).FontColor(T.Ink);
            });
    });

    private static void ReferencePanel(IContainer container, RepresentationReference reference) => container
        .Background(T.Surface).Border(0.5f).BorderColor(T.Hairline)
        .PaddingVertical(T.Unit * 2).PaddingHorizontal(T.Unit * 3).Column(c =>
        {
            c.Item().Text("MODIFICA EL COMPROBANTE").Style(T.EyebrowStyle).FontColor(T.InkSoft);
            c.Item().PaddingTop(T.Unit).Text(reference.ModifiedNcf)
                .FontFamily(RepresentationFonts.Mono).FontSize(T.BodyStrong).FontColor(T.Ink);
            if (reference.ModifiedDate is { } date)
                c.Item().Text($"Emitido el {D(date)}").FontSize(T.Label).FontColor(T.InkSoft);
            if (reference.Reason is { } reason)
                c.Item().Text(reason).FontSize(T.Label).FontColor(T.InkSoft);
        });

    private void TotalsPanel(IContainer container) => container.Column(col =>
    {
        var t = model.Totals;

        TotalRow(col, "Gravado 18%", t.MontoGravadoI1);
        TotalRow(col, "Gravado 16%", t.MontoGravadoI2);
        TotalRow(col, "Gravado 0%", t.MontoGravadoI3);
        TotalRow(col, "Exento", t.MontoExento);
        TotalRow(col, "ITBIS 18%", t.Itbis1);
        TotalRow(col, "ITBIS 16%", t.Itbis2);
        TotalRow(col, "ITBIS 0%", t.Itbis3);
        if (t.Itbis2 is null && t.Itbis3 is null)
            TotalRow(col, "ITBIS", t.TotalItbis, onlyIf: t.Itbis1 is null);
        TotalRow(col, "ITBIS adicional", t.MontoImpuestoAdicional);

        col.Item().PaddingVertical(T.Unit).LineHorizontal(0.75f).LineColor(T.Ink);
        col.Item().Row(r =>
        {
            r.RelativeItem().Text("MONTO TOTAL")
                .FontFamily(RepresentationFonts.Sans).FontSize(T.Label).FontColor(T.Ink).LetterSpacing(0.1f).Bold();
            r.AutoItem().Text(Money(t.MontoTotal))
                .FontFamily(RepresentationFonts.Mono).FontSize(T.TotalValue).SemiBold().FontColor(T.Accent);
        });

        if (t.TotalItbisWithheld is > 0m || t.TotalIsrWithheld is > 0m)
        {
            col.Item().PaddingTop(T.Unit * 1.5f);
            TotalRow(col, "ITBIS retenido", t.TotalItbisWithheld is > 0m ? -t.TotalItbisWithheld : null, signed: true);
            TotalRow(col, "ISR retenido", t.TotalIsrWithheld is > 0m ? -t.TotalIsrWithheld : null, signed: true);
        }

        if (t.AmountDue is { } due)
        {
            col.Item().PaddingVertical(T.Unit).LineHorizontal(0.5f).LineColor(T.Hairline);
            col.Item().Row(r =>
            {
                r.RelativeItem().Text("Valor a pagar").FontSize(T.Small).Medium().FontColor(T.Ink);
                r.AutoItem().Text(Money(due)).FontFamily(RepresentationFonts.Mono).FontSize(T.BodyStrong).Medium().FontColor(T.Ink);
            });
        }
    });

    private static void TotalRow(ColumnDescriptor col, string label, decimal? value, bool onlyIf = true, bool signed = false)
    {
        if (value is null || !onlyIf)
            return;

        col.Item().Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(T.Small).FontColor(T.InkSoft);
            r.AutoItem().Text((signed && value < 0m ? "−" : string.Empty) + Money(Math.Abs(value.Value)))
                .FontFamily(RepresentationFonts.Mono).FontSize(T.Small).FontColor(T.Ink);
        });
    }

    private static void Callout(IContainer container, string heading, string body) => container
        .Border(0.75f).BorderColor(T.Ink).PaddingVertical(T.Unit * 2).PaddingHorizontal(T.Unit * 3).Column(c =>
        {
            c.Item().Text(heading).Style(T.EyebrowStyle).FontColor(T.Ink);
            c.Item().PaddingTop(T.Unit).Text(body).FontSize(T.Small).FontColor(T.Ink);
        });

    // ---- pie -----------------------------------------------------------

    private void Footer(IContainer container) => container.PaddingTop(T.Unit * 2).Column(col =>
    {
        col.Item().LineHorizontal(0.5f).LineColor(T.Hairline);
        col.Item().PaddingTop(T.Unit * 1.5f).Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span("Verifica este comprobante en ").FontSize(T.Label).FontColor(T.InkFaint);
                text.Span(VerificationEndpoint()).FontFamily(RepresentationFonts.Mono).FontSize(T.Label).FontColor(T.InkSoft);
            });
            row.AutoItem().Text(text =>
            {
                text.Span("Página ").FontSize(T.Label).FontColor(T.InkFaint);
                text.CurrentPageNumber().FontSize(T.Label).FontColor(T.InkSoft);
                text.Span(" de ").FontSize(T.Label).FontColor(T.InkFaint);
                text.TotalPages().FontSize(T.Label).FontColor(T.InkSoft);
            });
        });

        col.Item().PaddingTop(2).Text(
            "Representación impresa de un Comprobante Fiscal Electrónico (e-CF). El documento con validez fiscal es el archivo XML firmado.")
            .FontSize(T.Label).FontColor(T.InkFaint);
    });

    // ---- helpers ------------------------------------------------------

    private static string Money(decimal value) => "RD$ " + value.ToString("N2", Pesos);

    private static string Qty(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string D(DateOnly date) => date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    private static string? Join(string? a, string? b)
    {
        var parts = new[] { a, b }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    private string VerificationEndpoint()
    {
        if (!Uri.TryCreate(model.Verification.QrUrl, UriKind.Absolute, out var uri))
            return "dgii.gov.do";

        var page = uri.Segments.LastOrDefault()?.Trim('/');
        return string.IsNullOrEmpty(page) ? uri.Host : $"{uri.Host}/{page}";
    }

    private static string StatusLabel(RepresentationDgiiStatus dgii) => dgii.Status switch
    {
        "accepted" => "ACEPTADO POR LA DGII",
        "accepted_conditional" => "ACEPTADO CONDICIONAL",
        "rejected" => "RECHAZADO POR LA DGII",
        "review" => "EN REVISIÓN",
        "submitted" => "EN PROCESO EN LA DGII",
        "failed" => "ENVÍO PENDIENTE",
        _ => "PENDIENTE DE ENVÍO",
    };

    private static (string Ink, string Bg) StatusColors(RepresentationDgiiStatus dgii) => dgii.Status switch
    {
        "accepted" or "accepted_conditional" => (T.OkInk, T.OkBg),
        "rejected" => (T.BadInk, T.BadBg),
        _ => (T.WaitInk, T.WaitBg),
    };
}
