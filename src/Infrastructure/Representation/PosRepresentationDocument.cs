using NovaFE.Application.Ecf.Representation;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace NovaFE.Infrastructure.Representation;

using T = RepresentationTheme;

/// <summary>
/// Representación Impresa para rollo térmico de <b>80 mm</b> (punto de venta).
/// Una sola columna, página continua (la altura crece con el contenido, sin
/// paginación), tipografía compacta y montos en Geist Mono alineados a la
/// derecha. Mismo modelo y mismos datos que el formato Carta.
/// </summary>
internal sealed class PosRepresentationDocument(RepresentationModel model) : IDocument
{
    // 80 mm de ancho, ~3 mm de margen a cada lado → ~74 mm de área útil.
    private const float RollWidthMm = 80f;
    private const float MarginXMm = 3f;
    private const float MarginYMm = 4f;

    // Escala tipográfica propia: el térmico imprime a ~203 dpi, conviene un
    // punto más grande que en Carta.
    private const float Micro = 6.5f;
    private const float Label = 7f;
    private const float Body = 8.5f;
    private const float Strong = 9.5f;
    private const float TotalValue = 11f;

    private readonly bool _anyDiscount = model.Lines.Any(l => l.Discount is > 0m);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Representación Impresa {model.Document.Encf}",
        Author = "NovaFE",
        Subject = model.Document.TypeName,
    };

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.ContinuousSize(RollWidthMm, Unit.Millimetre);
        page.MarginHorizontal(MarginXMm, Unit.Millimetre);
        page.MarginVertical(MarginYMm, Unit.Millimetre);
        page.DefaultTextStyle(x => x
            .FontFamily(RepresentationFonts.Sans).FontSize(Body).FontColor(T.Ink).LineHeight(1.25f));

        page.Content().Column(col =>
        {
            col.Spacing(T.Unit * 1.5f);

            col.Item().Element(Issuer);
            col.Item().Element(Rule);
            col.Item().Element(FiscalIdentity);
            col.Item().Element(Rule);

            if (model.Buyer is not null)
            {
                col.Item().Element(Buyer);
                col.Item().Element(Rule);
            }

            col.Item().Element(Lines);
            col.Item().Element(Rule);
            col.Item().Element(Totals);

            if (model.Payment.Methods.Count > 0)
            {
                col.Item().Element(Rule);
                col.Item().Element(PaymentMethods);
            }

            if (model.ContingencyNotice is { Length: > 0 } notice)
            {
                col.Item().Element(Rule);
                col.Item().Element(c => Note(c, "CONTINGENCIA", notice));
            }

            col.Item().Element(Rule);
            col.Item().Element(Timbre);
            col.Item().Element(Rule);
            col.Item().Element(Legal);
        });
    });

    // ---- emisor -----------------------------------------------------------

    private void Issuer(IContainer container) => container.Column(col =>
    {
        var p = model.Issuer;

        col.Item().AlignCenter().Text(p.Name)
            .FontSize(Strong).SemiBold().FontColor(T.Ink);

        if (p.TradeName is { } trade && !trade.Equals(p.Name, StringComparison.OrdinalIgnoreCase))
            col.Item().AlignCenter().Text(trade).FontSize(Label).FontColor(T.InkSoft);

        col.Item().PaddingTop(1).Column(lines =>
        {
            lines.Spacing(0.5f);
            if (p.Rnc is { } rnc)
                Centered(lines, $"RNC {rnc}", mono: true);
            Centered(lines, p.Address);
            Centered(lines, p.Phones.Count > 0 ? string.Join(" · ", p.Phones) : null);
            Centered(lines, p.Email);
        });
    });

    // ---- identidad fiscal del comprobante -------------------------------

    private void FiscalIdentity(IContainer container) => container.Column(col =>
    {
        col.Item().AlignCenter().Text(model.Document.TypeName.ToUpperInvariant())
            .FontSize(Label).SemiBold().FontColor(T.Ink).LetterSpacing(0.03f);

        col.Item().PaddingTop(T.Unit).Column(rows =>
        {
            rows.Spacing(1);
            KeyVal(rows, "e-NCF", model.Document.Encf, mono: true);

            if (model.Reference is { } reference)
                KeyVal(rows, "e-NCF mod.", reference.ModifiedNcf, mono: true);

            KeyVal(rows, "Emisión", D(model.Document.IssueDate), mono: true);

            if (model.Document.SequenceExpiresOn is { } exp)
                KeyVal(rows, "Válida hasta", D(exp), mono: true);
            if (model.Document.InternalNumber is { } internalNumber)
                KeyVal(rows, "N° interno", internalNumber, mono: true);
            if (model.Reference?.ModifiedDate is { } modDate)
                KeyVal(rows, "Fecha mod.", D(modDate), mono: true);
            if (model.Document.IncomeType is { } income)
                KeyVal(rows, "Tipo ingreso", income);
            if (model.Payment.ConditionLabel is { } cond)
                KeyVal(rows, "Condición", cond);
            if (model.Payment.DueDate is { } due)
                KeyVal(rows, "Límite pago", D(due), mono: true);
            if (model.Document.Currency is { } currency)
                KeyVal(rows, "Moneda", model.Document.ExchangeRate is { } rate
                    ? $"{currency} @ {RepresentationText.Rate(rate)}"
                    : currency);
        });

        if (model.Reference?.Reason is { } reason)
            col.Item().PaddingTop(T.Unit).Text(reason).FontSize(Label).FontColor(T.InkSoft).Italic();
    });

    // ---- comprador ------------------------------------------------------

    private void Buyer(IContainer container)
    {
        if (model.Buyer is not { } buyer)
            return;

        container.Column(col =>
        {
            col.Item().Text("COMPRADOR").FontSize(Micro).SemiBold().FontColor(T.InkSoft).LetterSpacing(0.04f);
            col.Item().PaddingTop(1).Text(buyer.Name).FontSize(Body).Medium().FontColor(T.Ink);

            if (buyer.TaxId is { } taxId)
                col.Item().Text($"{(buyer.Rnc is not null ? "RNC" : "ID")} {taxId}")
                    .FontFamily(RepresentationFonts.Mono).FontSize(Label).FontColor(T.InkSoft);

            col.Item().Column(lines =>
            {
                lines.Spacing(0.5f);
                Soft(lines, buyer.Address);
                Soft(lines, buyer.Email);
                Soft(lines, buyer.Contact is { } contact ? $"Contacto: {contact}" : null);
            });
        });
    }

    // ---- líneas -------------------------------------------------------

    private void Lines(IContainer container) => container.Column(col =>
    {
        col.Spacing(T.Unit);

        foreach (var line in model.Lines)
        {
            col.Item().Column(item =>
            {
                item.Item().Text(text =>
                {
                    text.Span($"{line.Number}  ").FontFamily(RepresentationFonts.Mono).FontSize(Label).FontColor(T.InkSoft);
                    text.Span(line.Name).FontSize(Body).FontColor(T.Ink);
                    if (line.Kind is { } kind)
                        text.Span($"  ({kind})").FontSize(Micro).FontColor(T.InkFaint);
                });

                item.Item().Row(row =>
                {
                    var detail = $"{Qty(line.Quantity)} × {Amount(line.UnitPrice)}";
                    if (_anyDiscount && line.Discount is > 0m and { } disc)
                        detail += $"  desc. {Amount(disc)}";
                    if (line.TaxLabel is { } tax)
                        detail += $"  ITBIS {tax}";

                    row.RelativeItem().Text(detail)
                        .FontFamily(RepresentationFonts.Mono).FontSize(Label).FontColor(T.InkSoft);
                    row.AutoItem().Text(Amount(line.GrossAmount))
                        .FontFamily(RepresentationFonts.Mono).FontSize(Body).Medium().FontColor(T.Ink);
                });

                if (line is { ItbisWithheld: > 0m } or { IsrWithheld: > 0m })
                {
                    var parts = new List<string>();
                    if (line.ItbisWithheld is > 0m and { } wi)
                        parts.Add($"ITBIS {Amount(wi)}");
                    if (line.IsrWithheld is > 0m and { } wr)
                        parts.Add($"ISR {Amount(wr)}");
                    item.Item().Text($"Retención: {string.Join(" · ", parts)}")
                        .FontFamily(RepresentationFonts.Mono).FontSize(Micro).FontColor(T.InkSoft);
                }
            });
        }
    });

    // ---- totales -----------------------------------------------------

    private void Totals(IContainer container) => container.Column(col =>
    {
        col.Spacing(1);
        var t = model.Totals;

        TotalRow(col, "Gravado 18%", t.MontoGravadoI1);
        TotalRow(col, "Gravado 16%", t.MontoGravadoI2);
        TotalRow(col, "Gravado 0%", t.MontoGravadoI3);
        TotalRow(col, "Exento", t.MontoExento);
        TotalRow(col, "ITBIS 18%", t.Itbis1);
        TotalRow(col, "ITBIS 16%", t.Itbis2);
        TotalRow(col, "ITBIS 0%", t.Itbis3);
        if (t.Itbis2 is null && t.Itbis3 is null && t.Itbis1 is null)
            TotalRow(col, "ITBIS", t.TotalItbis);
        TotalRow(col, "ITBIS adicional", t.MontoImpuestoAdicional);

        col.Item().PaddingVertical(1).LineHorizontal(0.75f).LineColor(T.Ink);
        col.Item().Row(r =>
        {
            r.RelativeItem().Text("TOTAL").FontSize(Label).SemiBold().FontColor(T.Ink).LetterSpacing(0.05f);
            r.AutoItem().Text(Money(t.MontoTotal))
                .FontFamily(RepresentationFonts.Mono).FontSize(TotalValue).SemiBold().FontColor(T.Ink);
        });

        if (t.TotalItbisWithheld is > 0m || t.TotalIsrWithheld is > 0m)
        {
            col.Item().PaddingTop(T.Unit);
            TotalRow(col, "ITBIS retenido", t.TotalItbisWithheld is > 0m ? -t.TotalItbisWithheld : null, signed: true);
            TotalRow(col, "ISR retenido", t.TotalIsrWithheld is > 0m ? -t.TotalIsrWithheld : null, signed: true);
        }

        if (t.AmountDue is { } due)
        {
            col.Item().PaddingVertical(1).LineHorizontal(0.5f).LineColor(T.Hairline);
            col.Item().Row(r =>
            {
                r.RelativeItem().Text("Valor a pagar").FontSize(Label).Medium().FontColor(T.Ink);
                r.AutoItem().Text(Money(due))
                    .FontFamily(RepresentationFonts.Mono).FontSize(Strong).Medium().FontColor(T.Ink);
            });
        }
    });

    private static void TotalRow(ColumnDescriptor col, string label, decimal? value, bool signed = false)
    {
        if (value is null or 0m)
            return;

        col.Item().Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(Label).FontColor(T.InkSoft);
            r.AutoItem().Text((signed && value < 0m ? "-" : string.Empty) + Amount(Math.Abs(value.Value)))
                .FontFamily(RepresentationFonts.Mono).FontSize(Label).FontColor(T.Ink);
        });
    }

    private void PaymentMethods(IContainer container) => container.Column(col =>
    {
        col.Item().Text("FORMAS DE PAGO").FontSize(Micro).SemiBold().FontColor(T.InkSoft).LetterSpacing(0.04f);
        col.Item().PaddingTop(1).Column(rows =>
        {
            rows.Spacing(0.5f);
            foreach (var method in model.Payment.Methods)
                rows.Item().Row(r =>
                {
                    r.RelativeItem().Text(method.Label).FontSize(Label).FontColor(T.InkSoft);
                    r.AutoItem().Text(Amount(method.Amount))
                        .FontFamily(RepresentationFonts.Mono).FontSize(Label).FontColor(T.Ink);
                });
        });
    });

    // ---- timbre ----------------------------------------------------

    private void Timbre(IContainer container) => container.Column(col =>
    {
        col.Item().AlignCenter().Width(46, Unit.Millimetre).Image(QrImage.Png(model.Verification.QrUrl));

        col.Item().PaddingTop(T.Unit).AlignCenter().Text(text =>
        {
            text.Span("Código de seguridad  ").FontSize(Label).FontColor(T.InkSoft);
            text.Span(model.Verification.SecurityCode)
                .FontFamily(RepresentationFonts.Mono).FontSize(Body).Medium().FontColor(T.Ink);
        });

        col.Item().AlignCenter().Text(text =>
        {
            text.Span("Firmado  ").FontSize(Label).FontColor(T.InkSoft);
            text.Span(model.Document.SignedAtText)
                .FontFamily(RepresentationFonts.Mono).FontSize(Label).FontColor(T.InkSoft);
        });

        if (model.Dgii is { } dgii)
        {
            var (ink, bg) = RepresentationText.StatusColors(dgii);
            col.Item().PaddingTop(T.Unit).AlignCenter().Background(bg).PaddingVertical(2).PaddingHorizontal(T.Unit * 2)
                .Text(RepresentationText.StatusLabel(dgii))
                .FontSize(Label).SemiBold().FontColor(ink).LetterSpacing(0.03f);

            if (dgii.TrackId is { } track)
                col.Item().PaddingTop(1).AlignCenter().Text($"TrackId {track}")
                    .FontFamily(RepresentationFonts.Mono).FontSize(Micro).FontColor(T.InkFaint);
        }
    });

    private static void Legal(IContainer container) => container.AlignCenter().Text(
        "Representación impresa de un Comprobante Fiscal Electrónico (e-CF). "
        + "El documento con validez fiscal es el archivo XML firmado.")
        .FontSize(Micro).FontColor(T.InkFaint);

    private static void Note(IContainer container, string heading, string body) => container.Column(c =>
    {
        c.Item().Text(heading).FontSize(Micro).SemiBold().FontColor(T.Ink).LetterSpacing(0.04f);
        c.Item().PaddingTop(1).Text(body).FontSize(Label).FontColor(T.Ink);
    });

    // ---- helpers -------------------------------------------------

    private static void Rule(IContainer container) =>
        container.PaddingVertical(1).LineHorizontal(0.5f).LineColor(T.Hairline);

    private static void KeyVal(ColumnDescriptor col, string label, string value, bool mono = false) =>
        col.Item().Row(r =>
        {
            r.ConstantItem(72).Text(label).FontSize(Label).FontColor(T.InkSoft);
            var text = r.RelativeItem().Text(value).FontSize(Label).FontColor(T.Ink);
            if (mono)
                text.FontFamily(RepresentationFonts.Mono);
        });

    private static void Centered(ColumnDescriptor col, string? value, bool mono = false)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var text = col.Item().AlignCenter().Text(value).FontSize(Label).FontColor(T.InkSoft);
        if (mono)
            text.FontFamily(RepresentationFonts.Mono);
    }

    private static void Soft(ColumnDescriptor col, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            col.Item().Text(value).FontSize(Label).FontColor(T.InkSoft);
    }

    private static string Money(decimal value) => RepresentationText.Money(value);

    private static string Amount(decimal value) => RepresentationText.Amount(value);

    private static string Qty(decimal value) => RepresentationText.Qty(value);

    private static string D(DateOnly date) => RepresentationText.Date(date);
}
