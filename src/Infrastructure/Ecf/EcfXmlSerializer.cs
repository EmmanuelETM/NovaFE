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
    public string Serialize(EcfDocument document, DateTimeOffset signedAt)
    {
        ArgumentNullException.ThrowIfNull(document);

        var profile = EcfXmlProfiles.For(document.Type);
        var buffer = new StringBuilder();

        using (var xmlWriter = XmlWriter.Create(buffer, EcfXml.WriterSettings))
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

        return EcfXml.Finish(buffer.ToString());
    }
}
