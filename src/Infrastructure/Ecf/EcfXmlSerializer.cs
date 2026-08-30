using System.Text;
using System.Xml;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;

namespace NovaFE.Infrastructure.Ecf;

/// <summary>
/// Serializador del <c>&lt;ECF&gt;</c> (Módulo 2). El orden de los elementos sale del
/// XSD oficial de cada tipo; los opcionales sin valor se omiten (RF-02.5). Las
/// variaciones por tipo se leen de <see cref="EcfXmlProfile"/> (una tabla), no de
/// condicionales <c>doc.Type == …</c> repartidos.
/// <para>
/// La clase está partida por región: <c>.Encabezado</c>, <c>.Totales</c>,
/// <c>.Detalles</c>, <c>.Secciones</c>. Emite <c>&lt;FechaHoraFirma&gt;</c>, <b>no</b>
/// <c>&lt;Signature&gt;</c> (eso es Módulo 3). v1: los diez tipos con todos los bloques
/// del formato; falta solo el RFCE (tipo 32 &lt; DOP 250 k). Ver <c>docs/ecf-xml.md</c>.
/// </para>
/// </summary>
internal sealed partial class EcfXmlSerializer : IEcfXmlSerializer
{
    private static readonly XmlWriterSettings _settings = new()
    {
        OmitXmlDeclaration = true,
        Indent = false,
        NewLineHandling = NewLineHandling.None,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    public string Serialize(EcfDocument document, DateTimeOffset signedAt)
    {
        ArgumentNullException.ThrowIfNull(document);

        var profile = EcfXmlProfiles.For(document.Type);
        var buffer = new StringBuilder();

        using (var xmlWriter = XmlWriter.Create(buffer, _settings))
        {
            var w = new EcfElementWriter(xmlWriter);
            using (w.Element("ECF"))
            {
                WriteEncabezado(w, document, profile);
                WriteDetalles(w, document, profile);
                WriteSubtotales(w, document, profile.Totals);
                WriteDescuentosORecargos(w, document);
                WritePaginacion(w, document, profile.Totals);
                WriteInformacionReferencia(w, document.Reference);
                w.El("FechaHoraFirma", DominicanTimeZone.ToDateTimeString(signedAt));
            }
        }

        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + EscapeDgii(buffer.ToString());
    }

    /// <summary>
    /// Completa el escape de la DGII (RF-02.3) sobre el cuerpo ya bien formado:
    /// <c>&lt; &gt; &amp;</c> los hizo <see cref="XmlWriter"/>; acá van <c>" ' © ® €</c>.
    /// El e-CF no tiene atributos, así que reemplazar en todo el cuerpo es seguro.
    /// </summary>
    private static string EscapeDgii(string body) =>
        body
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal)
            .Replace("©", "&#169;", StringComparison.Ordinal)
            .Replace("®", "&#174;", StringComparison.Ordinal)
            .Replace("€", "&#8364;", StringComparison.Ordinal);
}
