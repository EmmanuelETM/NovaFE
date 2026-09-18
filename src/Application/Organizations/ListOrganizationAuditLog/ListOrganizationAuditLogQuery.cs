using NovaFE.Domain.Common;

namespace NovaFE.Application.Organizations.ListOrganizationAuditLog;

/// <summary>Listado paginado de lo que tocó a una organización (plan, estado, miembros, tenants). Operador.</summary>
public sealed record ListOrganizationAuditLogQuery(Guid OrganizationId) : PagedRequest;
