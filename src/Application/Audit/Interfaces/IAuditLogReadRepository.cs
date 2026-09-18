using NovaFE.Application.Audit.Contracts;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Audit.Interfaces;

/// <summary>Read side (Dapper) del registro de auditoría.</summary>
public interface IAuditLogReadRepository
{
    /// <summary>Las filas de un tenant, la más reciente primero.</summary>
    Task<PagedResult<AuditLogEntryDto>> ListByTenantAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Las filas que tocan a una organización — no hay <c>organization_id</c> en
    /// la tabla (las acciones de operador no llevan tenant), así que se
    /// correlaciona por <c>path</c>: cualquier petición bajo
    /// <c>/organizations/{id}...</c> (plan, suspender, miembros, tenants).
    /// </summary>
    Task<PagedResult<AuditLogEntryDto>> ListByOrganizationAsync(
        Guid organizationId, int page, int pageSize, CancellationToken ct = default);
}
