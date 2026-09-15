namespace NovaFE.Application.Dgii.Contracts;

/// <summary>
/// Una entrada de <c>statusecf.dgii.gov.do/api/EstatusServicios/ObtenerEstatus</c>.
/// <b>Sin verificar</b>: el OpenAPI de la DGII no documenta el schema de la
/// respuesta (solo <c>"200": {"description": "Success"}</c>), así que los campos
/// son la mejor suposición a partir del nombre del endpoint — nunca requeridos, y
/// <see cref="RawJson"/> siempre trae el objeto tal cual llegó por si ninguno
/// matchea. Ver <c>docs/dgii-queries.md</c>.
/// </summary>
public sealed record DgiiServiceStatus(string? Nombre, bool? Disponible, string RawJson);

/// <summary>
/// Una entrada de
/// <c>statusecf.dgii.gov.do/api/EstatusServicios/ObtenerVentanasMantenimiento</c>.
/// <b>Sin verificar</b> — ver <see cref="DgiiServiceStatus"/>.
/// </summary>
public sealed record DgiiMaintenanceWindow(DateTimeOffset? Inicio, DateTimeOffset? Fin, string RawJson);

/// <summary>
/// Respuesta de
/// <c>statusecf.dgii.gov.do/api/EstatusServicios/VerificarEstado?Ambiente=N</c>.
/// <b>Sin verificar</b> — ver <see cref="DgiiServiceStatus"/>.
/// </summary>
public sealed record DgiiEnvironmentStatus(bool? EnMantenimiento, string RawJson);
