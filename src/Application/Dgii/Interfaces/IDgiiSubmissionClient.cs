using ErrorOr;
using NovaFE.Application.Dgii.Contracts;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Dgii.Interfaces;

/// <summary>
/// Cliente de bajo nivel de los servicios de <b>recepción</b> y <b>consulta de
/// resultado</b> de la DGII. HTTP puro: no cachea ni firma; el token Bearer entra
/// por parámetro (lo resuelve <see cref="IDgiiTokenProvider"/>). Los fallos de red
/// se devuelven como <c>Errors.Http.*</c>.
/// </summary>
public interface IDgiiSubmissionClient
{
    /// <summary><c>POST /{amb}/recepcion/api/facturaselectronicas</c> (multipart, campo <c>xml</c>).</summary>
    Task<ErrorOr<DgiiSubmissionReceipt>> SubmitEcfAsync(
        DgiiEnvironment environment, string bearerToken, string signedXml, string encf, CancellationToken ct = default);

    /// <summary><c>POST /{amb}/recepcionfc/api/recepcion/ecf</c> (multipart, campo <c>xml</c>) — dominio <c>fc.dgii.gov.do</c>.</summary>
    Task<ErrorOr<DgiiRfceReceipt>> SubmitRfceAsync(
        DgiiEnvironment environment, string bearerToken, string signedRfceXml, string encf, CancellationToken ct = default);

    /// <summary><c>GET /{amb}/consultaresultado/api/consultas/estado?trackid=X</c>.</summary>
    Task<ErrorOr<DgiiEcfResult>> GetResultAsync(
        DgiiEnvironment environment, string bearerToken, string trackId, CancellationToken ct = default);
}
