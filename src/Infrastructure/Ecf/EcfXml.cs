using System.Text;
using System.Xml;

namespace NovaFE.Infrastructure.Ecf;

/// <summary>
/// Plomería compartida por <see cref="EcfXmlSerializer"/> (el <c>&lt;ECF&gt;</c>) y
/// <see cref="RfceSerializer"/> (el <c>&lt;RFCE&gt;</c>): la configuración del
/// <see cref="XmlWriter"/> y el cierre del documento (declaración XML + escape
/// completo de la DGII).
/// </summary>
internal static class EcfXml
{
    /// <summary>Salida sin declaración (la ponemos a mano), sin indentar, UTF-8 sin BOM.</summary>
    public static readonly XmlWriterSettings WriterSettings = new()
    {
        OmitXmlDeclaration = true,
        Indent = false,
        NewLineHandling = NewLineHandling.None,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    /// <summary>
    /// Cierra el documento: antepone la declaración XML y completa el escape de la
    /// DGII (RF-02.3) sobre el cuerpo ya bien formado. <c>&lt; &gt; &amp;</c> los hizo
    /// <see cref="XmlWriter"/>; acá van <c>" ' © ® €</c>. Ni el e-CF ni el RFCE tienen
    /// atributos, así que reemplazar en todo el cuerpo es seguro. <c>© ® €</c> van
    /// como referencias numéricas.
    /// </summary>
    public static string Finish(string body) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        body
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal)
            .Replace("©", "&#169;", StringComparison.Ordinal)
            .Replace("®", "&#174;", StringComparison.Ordinal)
            .Replace("€", "&#8364;", StringComparison.Ordinal);
}
