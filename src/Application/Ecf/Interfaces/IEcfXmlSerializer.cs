using NovaFE.Domain.Ecf;

namespace NovaFE.Application.Ecf.Interfaces;

/// <summary>
/// Serializa un <see cref="EcfDocument"/> al XML <c>&lt;ECF&gt;</c> de la DGII:
/// orden de secciones exacto del XSD, sin tags vacíos, escape de los 8 caracteres
/// especiales, formato numérico y de fechas de la DGII.
/// <para>
/// Incluye <c>&lt;FechaHoraFirma&gt;</c> (con <paramref name="signedAt"/>) pero
/// <b>no</b> la <c>&lt;Signature&gt;</c>: eso lo agrega Módulo 3 sobre este XML.
/// </para>
/// </summary>
public interface IEcfXmlSerializer
{
    string Serialize(EcfDocument document, DateTimeOffset signedAt);
}
