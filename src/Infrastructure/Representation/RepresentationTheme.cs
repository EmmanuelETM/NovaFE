using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace NovaFE.Infrastructure.Representation;

/// <summary>
/// Tokens de diseño de la Representación Impresa: una paleta corta y sobria, una
/// escala tipográfica y una unidad de espaciado. La idea es que la RI se lea
/// limpia y ordenada — jerarquía por peso y tamaño, no por cajas ni colores.
/// </summary>
internal static class RepresentationTheme
{
    // Tinta
    public const string Ink = "#18181B";       // texto principal
    public const string InkSoft = "#52525B";   // etiquetas, texto secundario
    public const string InkFaint = "#A1A1AA";  // terciario, marcas de agua
    public const string Hairline = "#E4E4E7";  // líneas y bordes
    public const string Surface = "#FAFAFA";   // fondo de panel sutil
    public const string Accent = "#4F46E5";    // solo el rótulo y el total

    // Estado DGII (texto / fondo)
    public const string OkInk = "#15803D";
    public const string OkBg = "#F0FDF4";
    public const string BadInk = "#B91C1C";
    public const string BadBg = "#FEF2F2";
    public const string WaitInk = "#B45309";
    public const string WaitBg = "#FFFBEB";

    // Escala tipográfica (pt)
    public const float Eyebrow = 7.5f;   // rótulos tracked en mayúsculas
    public const float Label = 7.5f;
    public const float Small = 8f;
    public const float Body = 8.75f;
    public const float BodyStrong = 9.5f;
    public const float Title = 15f;
    public const float TotalValue = 13f;

    // Espaciado
    public const float Unit = 4f;
    public const float PageMarginX = 46f;
    public const float PageMarginY = 40f;

    public static TextStyle EyebrowStyle => TextStyle.Default
        .FontFamily(RepresentationFonts.Sans).FontSize(Eyebrow).FontColor(Accent)
        .LetterSpacing(0.04f).SemiBold();

    public static TextStyle LabelStyle => TextStyle.Default
        .FontFamily(RepresentationFonts.Sans).FontSize(Label).FontColor(InkSoft);

    public static TextStyle MonoStyle => TextStyle.Default
        .FontFamily(RepresentationFonts.Mono).FontSize(Body).FontColor(Ink);
}
