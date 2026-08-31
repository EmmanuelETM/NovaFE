using NovaFE.Domain.Dgii;

namespace NovaFE.Application.Dgii.Contracts;

/// <summary>
/// Acuse de la DGII al recibir un <c>&lt;ECF&gt;</c> por
/// <c>/{amb}/recepcion/api/facturaselectronicas</c>. Solo confirma la recepción y
/// entrega el <see cref="TrackId"/>; el resultado fiscal se consulta luego.
/// </summary>
public sealed record DgiiSubmissionReceipt(string TrackId, string? Message);

/// <summary>
/// Resultado de la DGII al recibir un <c>&lt;RFCE&gt;</c> por
/// <c>/{amb}/recepcionfc/api/recepcion/ecf</c>. A diferencia del e-CF, el RFCE
/// devuelve el estado <b>síncrono</b> en <see cref="Codigo"/>
/// (1 aceptado / 2 rechazado / 4 aceptado condicional).
/// </summary>
public sealed record DgiiRfceReceipt(
    int Codigo,
    string Estado,
    IReadOnlyList<DgiiMessage> Mensajes,
    string? Encf,
    bool? SecuenciaUtilizada);

/// <summary>
/// Resultado de <c>/{amb}/consultaresultado/api/consultas/estado?trackid=X</c>.
/// <see cref="Codigo"/>: 0 no encontrado, 1 aceptado, 2 rechazado, 3 en proceso,
/// 4 aceptado condicional.
/// </summary>
public sealed record DgiiEcfResult(
    int Codigo,
    string Estado,
    IReadOnlyList<DgiiMessage> Mensajes,
    bool? SecuenciaUtilizada,
    DateTimeOffset? FechaRecepcion);
