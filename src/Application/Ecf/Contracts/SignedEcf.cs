namespace NovaFE.Application.Ecf.Contracts;

/// <summary>
/// El e-CF ya firmado y listo para la DGII — la salida de Módulo 3. Lo consume
/// Módulo 4 (envío + persistencia); este contrato no sabe nada de HTTP ni de base
/// de datos.
/// </summary>
/// <param name="SignedAt">
/// Instante de firma (UTC). Es el mismo que quedó en <c>&lt;FechaHoraFirma&gt;</c>
/// del XML.
/// </param>
/// <param name="EcfXml">
/// El <c>&lt;ECF&gt;</c> firmado, con <c>&lt;Signature&gt;</c> como último hijo de la
/// raíz. Se guarda localmente <b>siempre</b>, incluso cuando a la DGII va el RFCE.
/// </param>
/// <param name="RfceXml">
/// El <c>&lt;RFCE&gt;</c> (resumen) <b>ya firmado</b>, cuando el tipo 32 tiene
/// <c>MontoTotal &lt; DOP 250 000</c> (RF-02.6) — es lo que se envía a la DGII en
/// ese caso, por <c>POST /api/rfce</c>. Lleva su propia <c>&lt;Signature&gt;</c>
/// (Formato RFCE §B, firma sobre todo el documento) y queda atado al e-CF por su
/// <c>&lt;CodigoSeguridadeCF&gt;</c> = <see cref="SecurityCode"/>. <c>null</c> para
/// todos los demás documentos.
/// </param>
/// <param name="SignatureValue">
/// El Base64 de <c>&lt;SignatureValue&gt;</c> tal cual aparece en el XML firmado.
/// </param>
/// <param name="SecurityCode">
/// Los primeros 6 caracteres de <see cref="SignatureValue"/> — el
/// <c>CodigoSeguridad</c> del QR y de la Representación Impresa (RF-03.5).
/// </param>
/// <param name="DocumentHash">
/// SHA-256 en hex del <see cref="EcfXml"/> firmado — la huella de integridad
/// post-firma para auditoría (RF-03.4). Una vez firmado, el documento es inmutable.
/// </param>
/// <param name="QrUrl">
/// La URL del timbre QR de la Representación Impresa (Módulo 9, RF-09.1): una URL
/// de <c>consultatimbre</c> en los servidores de la DGII. Variante
/// <c>consultatimbrefc</c> (4 parámetros) cuando el documento va como RFCE.
/// </param>
public sealed record SignedEcf(
    DateTimeOffset SignedAt,
    string EcfXml,
    string? RfceXml,
    string SignatureValue,
    string SecurityCode,
    string DocumentHash,
    string QrUrl)
{
    /// <summary>
    /// El envío a la DGII usa el RFCE (<c>POST /api/rfce</c>) en lugar del e-CF
    /// completo (<c>POST /api/facturaselectronicas</c>).
    /// </summary>
    public bool SubmitsRfce => RfceXml is not null;
}
