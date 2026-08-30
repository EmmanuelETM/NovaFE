using NovaFE.Domain.Common;

namespace NovaFE.Application.Tenants.ListTenants;

/// <summary>
/// Paged list of tenants for the operator console. <see cref="Search"/> matches
/// RNC or legal name. Inherits <c>Page</c>/<c>PageSize</c>/<c>Skip</c> already
/// clamped from <see cref="PagedRequest"/>.
/// </summary>
public sealed record ListTenantsQuery : PagedRequest
{
    public string? Search { get; init; }
}
