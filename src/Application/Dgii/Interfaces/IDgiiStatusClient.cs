using ErrorOr;
using NovaFE.Application.Dgii.Contracts;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Dgii.Interfaces;

/// <summary>
/// Cliente del servicio de estatus de la DGII (Módulo 10) —
/// <c>statusecf.dgii.gov.do</c>, dominio y auth propios (API key estática,
/// <c>Authorization: ApiKey {key}</c>, no el Bearer del resto de servicios). El
/// schema de la respuesta de los tres endpoints no está documentado por la DGII
/// (solo el path, el método y el esquema de auth lo están, vía el OpenAPI público
/// del servicio) — ver <c>docs/dgii-queries.md</c>. Sin <c>Dgii:StatusApiKey</c>
/// configurada, cada método devuelve <c>DgiiQueryErrors.StatusApiKeyNotConfigured</c>
/// sin intentar la llamada.
/// </summary>
public interface IDgiiStatusClient
{
    /// <summary><c>GET /api/EstatusServicios/ObtenerEstatus</c>.</summary>
    Task<ErrorOr<IReadOnlyList<DgiiServiceStatus>>> GetServiceStatusAsync(CancellationToken ct = default);

    /// <summary><c>GET /api/EstatusServicios/ObtenerVentanasMantenimiento</c>.</summary>
    Task<ErrorOr<IReadOnlyList<DgiiMaintenanceWindow>>> GetMaintenanceWindowsAsync(CancellationToken ct = default);

    /// <summary><c>GET /api/EstatusServicios/VerificarEstado?Ambiente={1|2|3}</c>.</summary>
    Task<ErrorOr<DgiiEnvironmentStatus>> VerifyEnvironmentStatusAsync(
        DgiiEnvironment environment, CancellationToken ct = default);
}
