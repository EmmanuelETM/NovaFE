using NovaFE.Application.Ecf.Representation;
using QuestPDF.Fluent;

namespace NovaFE.Infrastructure.Representation;

/// <summary>
/// <see cref="IRepresentationRenderer"/> con QuestPDF. Registra la fuente Geist la
/// primera vez y despacha por formato de página.
/// </summary>
internal sealed class QuestPdfRepresentationRenderer : IRepresentationRenderer
{
    public QuestPdfRepresentationRenderer() => RepresentationFonts.EnsureRegistered();

    public byte[] Render(RepresentationModel model, RepresentationLayout layout)
    {
        ArgumentNullException.ThrowIfNull(model);

        return layout switch
        {
            RepresentationLayout.Letter => new LetterRepresentationDocument(model).GeneratePdf(),
            _ => throw new NotSupportedException(
                $"El formato de Representación Impresa '{layout}' todavía no está implementado."),
        };
    }
}
