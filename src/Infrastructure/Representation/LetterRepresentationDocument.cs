using System.Globalization;
using NovaFE.Application.Ecf.Representation;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NovaFE.Infrastructure.Representation;

using T = RepresentationTheme;

/// <summary>
/// Representación Impresa en tamaño Carta. Se lee como una pieza de producto:
/// cabecera de dos columnas (emisor a la izquierda, identidad fiscal del
/// comprobante a la derecha), fila de comprador + condiciones, tabla de líneas
/// con reglas finas, panel de totales a la derecha, y el timbre (QR + código de
/// seguridad + sello DGII) tras los totales. Cabecera y pie se repiten al paginar.
/// </summary>
internal sealed class LetterRepresentationDocument(RepresentationModel model) : IDocument
{
    private const float RightColumn = 236f;

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
            .FontFamily(RepresentationFonts.Sans).FontSize(T.Body).FontColor(T.Ink).LineHeight(1.28f));

        page.Header().Element(Header);
        page.Content().Element(Content);
        page.Footer().Element(Footer);
    });

    // ---- cabecera: emisor | identidad fiscal --------------------------------

    private void Header(IContainer container) => container.Column(col =>
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Element(Issuer);
            row.ConstantItem(T.Unit * 5);
            row.ConstantItem(RightColumn).Element(FiscalIdentity);
        });

        col.Item().PaddingTop(T.Unit * 2.5f).LineHorizontal(0.75f).LineColor(T.Ink);
    });

    private void Issuer(IContainer container) => container.Column(col =>
    {
        col.Item().Text("EMISOR").Style(T.EyebrowStyle).FontColor(T.InkSoft);
        col.Item().PaddingTop(T.Unit).Text(model.Issuer.Name)
            .FontFamily(RepresentationFonts.Sans).FontSize(T.Title).SemiBold().FontColor(T.Ink);

        var p = model.Issuer;
        if (p.TradeName is { } trade && !trade.Equals(p.Name, StringComparison.OrdinalIgnoreCase))
            col.Item().Text(trade).FontSize(T.Small).FontColor(T.InkSoft);

        if (p.Rnc is { } rnc)
            col.Item().PaddingTop(2).Text($"RNC {rnc}")
                .FontFamily(RepresentationFonts.Mono).FontSize(T.Small).FontColor(T.InkSoft);

        col.Item().PaddingTop(T.Unit * 1.5f).Column(lines =>
        {
            lines.Spacing(1);
            SoftLine(lines, p.Address);
            SoftLine(lines, p.Phones.Count > 0 ? string.Join(" · ", p.Phones) : null);
            SoftLine(lines, p.Email);
            SoftLine(lines, p.EconomicActivity);
        });
    });

    private void FiscalIdentity(IContainer container) => container.Column(col =>
    {
        col.Item().Text("REPRESENTACIÓN IMPRESA").Style(T.EyebrowStyle);
        col.Item().PaddingTop(T.Unit).Text(model.Document.TypeName)
            .FontFamily(RepresentationFonts.Sans).FontSize(T.BodyStrong).Medium().FontColor(T.Ink);

        var d = model.Document;
        var rows = new List<(string Label, string Value)> { ("e-NCF", d.Encf) };

        if (model.Reference is { } reference)
            rows.Add(("e-NCF modificado", reference.ModifiedNcf));
        if (d.SequenceExpiresOn is { } exp)
            rows.Add(("Válida hasta", D(exp)));
        if (d.InternalNumber is { } internalNumber)
            rows.Add(("N° interno", internalNumber));
        rows.Add(("Fecha de emisión", D(d.IssueDate)));
        if (model.Reference?.Reason is { } reason)
            rows.Add(("Modificación", reason));

        col.Item().PaddingTop(T.Unit * 2).Element(c => DefList(c, rows));
    });

    // ---- contenido --------------------------------------------------------

    private void Content(IContainer container) => container.PaddingTop(T.Unit * 3).Column(col =>
    {
        col.Spacing(T.Unit * 3.5f);

        col.Item().Element(BuyerAndTerms);
        col.Item().Element(LineTable);
        col.Item().Element(TotalsAndPayments);

        if (model.ContingencyNotice is { Length: > 0 } notice)
            col.Item().Element(c => Callout(c, "CONTINGENCIA", notice));

        col.Item().PaddingTop(T.Unit * 3).Element(Timbre);
    });

    private void BuyerAndTerms(IContainer container) => container.Column(outer =>
    {
        outer.Item().Row(row =>
        {
            row.RelativeItem().Element(Buyer);
            row.ConstantItem(T.Unit * 5);
            row.ConstantItem(RightColumn).Element(Terms);
        });

        outer.Item().PaddingTop(T.Unit * 3).LineHorizontal(0.5f).LineColor(T.Hairline);
    });

    private void Buyer(IContainer container)
    {
        if (model.Buyer is not { } buyer)
        {
            container.Column(c =>
            {
                c.Item().Text("COMPRADOR").Style(T.EyebrowStyle).FontColor(T.InkSoft);
                c.Item().PaddingTop(T.Unit).Text("Sin identificación del comprador")
                    .FontSize(T.Small).FontColor(T.InkFaint);
            });
            return;
        }

        container.Column(col =>
        {
            col.Item().Text("COMPRADOR").Style(T.EyebrowStyle).FontColor(T.InkSoft);
            col.Item().PaddingTop(T.Unit).Text(buyer.Name)
                .FontFamily(RepresentationFonts.Sans).FontSize(T.BodyStrong).Medium().FontColor(T.Ink);

            if (buyer.TaxId is { } taxId)
                col.Item().PaddingTop(1).Text($"{(buyer.Rnc is not null ? "RNC" : "ID")} {taxId}")
                    .FontFamily(RepresentationFonts.Mono).FontSize(T.Small).FontColor(T.InkSoft);

            col.Item().PaddingTop(T.Unit).Column(lines =>
            {
                lines.Spacing(1);
                SoftLine(lines, buyer.Address);
                SoftLine(lines, buyer.Email);
                SoftLine(lines, buyer.Contact is { } contact ? $"Contacto: {contact}" : null);
            });
        });
    }

    private void Terms(IContainer container)
    {
        var d = model.Document;
        var rows = new List<(string Label, string Value)>();

        if (model.Payment.ConditionLabel is { } cond)
            rows.Add(("Condición de pago", cond));
        if (model.Payment.DueDate is { } due)
            rows.Add(("Fecha límite de pago", D(due)));
        if (d.IncomeType is { } income)
            rows.Add(("Tipo de ingreso", income));
        if (d.Currency is { } currency)
            rows.Add(("Moneda", d.ExchangeRate is { } rate
                ? $"{currency} · 1 {currency} = RD$ {RepresentationText.Rate(rate)}"
                : currency));

        if (rows.Count > 0)
            container.Element(c => DefList(c, rows));
    }

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
            cols.ConstantColumn(78);   // importe
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
                NumCell(table, line.Discount is > 0m and { } disc ? Money(disc) : "—");
            BodyCell(table).AlignRight().Text(line.TaxLabel ?? "—").FontSize(T.Small).FontColor(T.InkSoft);
            NumCell(table, Money(line.GrossAmount), strong: true);
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

    // ---- totales + formas de pago -------------------------------------

    private void TotalsAndPayments(IContainer container) => container.Row(row =>
    {
        row.RelativeItem().Column(c =>
        {
            if (model.Payment.Methods.Count > 0)
                c.Item().Element(PaymentMethods);
        });

        row.ConstantItem(T.Unit * 5);
        row.ConstantItem(RightColumn).Element(TotalsPanel);
    });

    private void PaymentMethods(IContainer container) => container.Column(col =>
    {
        col.Item().Text("FORMAS DE PAGO").Style(T.EyebrowStyle).FontColor(T.InkSoft);
        col.Item().PaddingTop(T.Unit * 1.5f).Column(rows =>
        {
            rows.Spacing(1);
            foreach (var method in model.Payment.Methods)
                rows.Item().Row(r =>
                {
                    r.RelativeItem().Text(method.Label).FontSize(T.Small).FontColor(T.InkSoft);
                    r.AutoItem().Text(Money(method.Amount)).FontFamily(RepresentationFonts.Mono).FontSize(T.Small).FontColor(T.Ink);
                });
        });
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
                .FontFamily(RepresentationFonts.Sans).FontSize(T.Label).FontColor(T.Ink).LetterSpacing(0.06f).SemiBold();
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
        if (value is null or 0m || !onlyIf)
            return;

        col.Item().Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(T.Small).FontColor(T.InkSoft);
            r.AutoItem().Text((signed && value < 0m ? "-" : string.Empty) + Money(Math.Abs(value.Value)))
                .FontFamily(RepresentationFonts.Mono).FontSize(T.Small).FontColor(T.Ink);
        });
    }

    // ---- timbre -------------------------------------------------------

    private void Timbre(IContainer container) => container.Column(col =>
    {
        col.Item().LineHorizontal(0.5f).LineColor(T.Hairline);
        col.Item().PaddingTop(T.Unit * 2.5f).Row(row =>
        {
            row.ConstantItem(76).Height(76).Image(QrImage.Png(model.Verification.QrUrl));
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

    private static void Callout(IContainer container, string heading, string body) => container
        .Border(0.75f).BorderColor(T.Ink).PaddingVertical(T.Unit * 2).PaddingHorizontal(T.Unit * 3).Column(c =>
        {
            c.Item().Text(heading).Style(T.EyebrowStyle).FontColor(T.Ink);
            c.Item().PaddingTop(T.Unit).Text(body).FontSize(T.Small).FontColor(T.Ink);
        });

    // ---- pie ---------------------------------------------------------

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

    // ---- helpers ----------------------------------------------------

    private static void DefList(IContainer container, IEnumerable<(string Label, string Value)> rows) =>
        container.Column(col =>
        {
            col.Spacing(2.5f);
            foreach (var (label, value) in rows)
                col.Item().Row(r =>
                {
                    r.ConstantItem(96).Text(label).Style(T.LabelStyle);
                    r.RelativeItem().Text(value)
                        .FontFamily(RepresentationFonts.Mono).FontSize(T.Small).FontColor(T.Ink);
                });
        });

    private static void SoftLine(ColumnDescriptor col, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            col.Item().Text(value).FontSize(T.Small).FontColor(T.InkSoft);
    }

    private static string Money(decimal value) => RepresentationText.Money(value);

    private static string Qty(decimal value) => RepresentationText.Qty(value);

    private static string D(DateOnly date) => RepresentationText.Date(date);

    private string VerificationEndpoint() => RepresentationText.VerificationEndpoint(model.Verification.QrUrl);

    private static string StatusLabel(RepresentationDgiiStatus dgii) => RepresentationText.StatusLabel(dgii);

    private static (string Ink, string Bg) StatusColors(RepresentationDgiiStatus dgii) => RepresentationText.StatusColors(dgii);
}
