using NovaFE.Domain.Common;

namespace NovaFE.Application.Audit.ListAuditLog;

/// <summary>Listado paginado del audit log de un contribuyente (recurso de operador).</summary>
public sealed record ListAuditLogQuery(Guid TenantId) : PagedRequest;
