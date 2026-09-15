using ErrorOr;
using NovaFE.Application.Dgii.Contracts;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Dgii.Interfaces;

/// <summary>
/// Cliente de bajo nivel de las consultas de trackIds y directorio de la DGII
/// (Módulo 10) — dominio <c>ecf.dgii.gov.do</c>, mismo Bearer token que
/// <see cref="IDgiiSubmissionClient"/>. HTTP puro: no cachea ni resuelve el
/// token. No disponibles en CerteCF — devuelve
/// <c>DgiiQueryErrors.NotAvailableInEnvironment</c> sin llamar.
/// </summary>
public interface IDgiiQueryClient
{
    /// <summary><c>GET /{amb}/consultatrackids/api/trackids/consulta?rncemisor=X&amp;encf=Y</c>.</summary>
    Task<ErrorOr<IReadOnlyList<DgiiTrackIdEntry>>> GetTrackIdsAsync(
        DgiiEnvironment environment, string bearerToken, string rncEmisor, string encf, CancellationToken ct = default);

    /// <summary><c>GET /{amb}/consultadirectorio/api/consultas/listado</c>.</summary>
    Task<ErrorOr<IReadOnlyList<DgiiDirectoryEntry>>> ListDirectoryAsync(
        DgiiEnvironment environment, string bearerToken, CancellationToken ct = default);

    /// <summary><c>GET /{amb}/consultadirectorio/api/consultas/obtenerDirectorioporrnc?RNC=X</c>.</summary>
    Task<ErrorOr<DgiiDirectoryEntry?>> GetDirectoryEntryAsync(
        DgiiEnvironment environment, string bearerToken, string rnc, CancellationToken ct = default);
}
