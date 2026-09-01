namespace NovaFE.Application.Ecf.Representation;

/// <summary>Formato de página de la Representación Impresa.</summary>
public enum RepresentationLayout
{
    /// <summary>Carta (8.5 × 11 in). El formato por defecto.</summary>
    Letter = 0,

    /// <summary>Rollo térmico de 80 mm (punto de venta). Pendiente — ver <c>docs/representation.md</c>.</summary>
    Pos = 1,
}

/// <summary>
/// Pinta un <see cref="RepresentationModel"/> como PDF. Sin estado; la
/// implementación vive en Infrastructure (QuestPDF).
/// </summary>
public interface IRepresentationRenderer
{
    /// <summary>El PDF de la RI para <paramref name="model"/> en el formato <paramref name="layout"/>.</summary>
    byte[] Render(RepresentationModel model, RepresentationLayout layout);
}
