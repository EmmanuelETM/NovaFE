using NovaFE.Domain.Common;

namespace NovaFE.Application.Organizations.ListOrganizationTenants;

/// <summary>Paged list of the tenants ("proyectos") that belong to an organization.</summary>
public sealed record ListOrganizationTenantsQuery(Guid OrganizationId) : PagedRequest;
