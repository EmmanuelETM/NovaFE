using NovaFE.Application.Dgii.Contracts;

namespace NovaFE.Application.Ops.Contracts;

/// <summary>
/// Diagnóstico crudo del estatus de servicio de la DGII (Módulo 10,
/// <c>statusecf.dgii.gov.do</c>) — recurso de operador, no de negocio.
/// <see cref="Verified"/> siempre <c>false</c>: el schema de la respuesta no está
/// documentado por la DGII, así que esto es lectura tolerante para que un humano
/// la mire, no un input de ninguna decisión automática. Ver
/// <c>docs/dgii-queries.md</c>.
/// </summary>
public sealed record DgiiStatusDiagnosticDto(
    bool Verified,
    DgiiStatusSectionDto<IReadOnlyList<DgiiServiceStatus>> Services,
    DgiiStatusSectionDto<IReadOnlyList<DgiiMaintenanceWindow>> MaintenanceWindows,
    IReadOnlyList<DgiiEnvironmentStatusEntryDto> Environments);

/// <summary>Una de las tres consultas de estatus: el dato si salió bien, o el motivo si no.</summary>
public sealed record DgiiStatusSectionDto<T>(T? Data, string? Error);

/// <summary><c>VerificarEstado</c> para un ambiente puntual — se consultan los tres.</summary>
public sealed record DgiiEnvironmentStatusEntryDto(string Environment, DgiiEnvironmentStatus? Status, string? Error);
