using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace NovaFE.Infrastructure.Representation;

/// <summary>
/// Registra la fuente <b>Geist</b> (OFL, vendorizada y embebida) en QuestPDF y
/// fija la licencia Community. Idempotente y thread-safe — se llama al construir
/// el renderer.
/// </summary>
internal static class RepresentationFonts
{
    /// <summary>Nombre de familia de la fuente de texto.</summary>
    public const string Sans = "Geist";

    /// <summary>Nombre de familia de la fuente monoespaciada (e-NCF, códigos, montos).</summary>
    public const string Mono = "Geist Mono";

    private static readonly Lock Gate = new();
    private static bool _done;

    public static void EnsureRegistered()
    {
        if (_done)
            return;

        lock (Gate)
        {
            if (_done)
                return;

            QuestPDF.Settings.License = LicenseType.Community;

            var assembly = typeof(RepresentationFonts).Assembly;
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (!name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"No se pudo abrir el recurso de fuente '{name}'.");
                FontManager.RegisterFont(stream);
            }

            _done = true;
        }
    }
}
