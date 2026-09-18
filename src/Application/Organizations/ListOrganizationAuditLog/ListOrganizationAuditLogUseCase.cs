using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Audit.Contracts;
using NovaFE.Application.Audit.Interfaces;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.ListOrganizationAuditLog;

public sealed class ListOrganizationAuditLogUseCase(
    ILoggerFactory loggerFactory,
    IOrganizationRepository organizations,
    IAuditLogReadRepository auditLog)
    : QueryUseCase<ListOrganizationAuditLogQuery, PagedResult<AuditLogEntryDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<PagedResult<AuditLogEntryDto>>> ExecuteCore(
        ListOrganizationAuditLogQuery request,
        CancellationToken ct)
    {
        if (await organizations.GetByIdAsync(request.OrganizationId, ct) is null)
            return OrganizationErrors.NotFound(request.OrganizationId);

        return await auditLog.ListByOrganizationAsync(request.OrganizationId, request.Page, request.PageSize, ct);
    }
}
