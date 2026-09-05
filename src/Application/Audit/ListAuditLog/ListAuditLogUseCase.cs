using ErrorOr;
using NovaFE.Application.Audit.Contracts;
using NovaFE.Application.Audit.Interfaces;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Audit.ListAuditLog;

/// <summary>Lista el audit log de un contribuyente. Recurso de operador.</summary>
public sealed class ListAuditLogUseCase(
    ILoggerFactory loggerFactory,
    ITenantReadRepository tenants,
    IAuditLogReadRepository auditLog)
    : QueryUseCase<ListAuditLogQuery, PagedResult<AuditLogEntryDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<PagedResult<AuditLogEntryDto>>> ExecuteCore(
        ListAuditLogQuery request,
        CancellationToken ct)
    {
        if (await tenants.GetByIdAsync(request.TenantId, ct) is null)
            return TenantErrors.NotFound(request.TenantId);

        return await auditLog.ListByTenantAsync(request.TenantId, request.Page, request.PageSize, ct);
    }
}
