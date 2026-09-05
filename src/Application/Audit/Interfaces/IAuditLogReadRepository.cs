using NovaFE.Application.Audit.Contracts;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Audit.Interfaces;

/// <summary>Read side (Dapper) del registro de auditoría.</summary>
public interface IAuditLogReadRepository
{
    /// <summary>Las filas de un tenant, la más reciente primero.</summary>
    Task<PagedResult<AuditLogEntryDto>> ListByTenantAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default);
}
