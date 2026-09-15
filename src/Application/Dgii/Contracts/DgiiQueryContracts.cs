namespace NovaFE.Application.Dgii.Contracts;

/// <summary>
/// Una entrada de <c>/{amb}/consultatrackids/api/trackids/consulta</c>. Puede haber
/// más de una para el mismo e-NCF si se remitió varias veces.
/// </summary>
public sealed record DgiiTrackIdEntry(string TrackId, string Estado, DateTimeOffset? FechaRecepcion);

/// <summary>
/// Una entrada del directorio de facturadores electrónicos
/// (<c>/{amb}/consultadirectorio</c>) — la URL B2B de un contribuyente para
/// enviarle un e-CF directamente (Módulo 5, sin construir).
/// </summary>
public sealed record DgiiDirectoryEntry(
    string Rnc, string Nombre, string? UrlRecepcion, string? UrlAceptacion, string? UrlOpcional);
