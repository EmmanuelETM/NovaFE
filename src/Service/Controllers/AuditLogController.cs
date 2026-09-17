using Asp.Versioning;
using NovaFE.Application.Audit.Contracts;
using NovaFE.Application.Audit.ListAuditLog;
using NovaFE.Domain.Common;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Registro de auditoría (RF-14.4) del contribuyente. <b>Self-service</b>
/// (política <c>TenantConfig</c>, igual que certificados/secuencias/API
/// keys) — quién hizo qué sobre su propio contribuyente. El operador
/// conserva <see cref="TenantsController.GetAuditLog"/> para cualquier
/// contribuyente, mismo <see cref="ListAuditLogUseCase"/> reusado por los
/// dos caminos: la autorización vive en el controller, no en el caso de uso.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class AuditLogController(
    ListAuditLogUseCase listAuditLog,
    CurrentTenant currentTenant) : ApiController
{
    /// <summary>El registro de auditoría del tenant activo, paginado.</summary>
    [HttpGet]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [ProducesResponseType(typeof(PagedResult<AuditLogEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page, [FromQuery] int pageSize, CancellationToken ct)
        => (await listAuditLog.Execute(
                new ListAuditLogQuery(currentTenant.Require()) { Page = page, PageSize = pageSize }, ct))
            .Match(Ok, Problem);
}
