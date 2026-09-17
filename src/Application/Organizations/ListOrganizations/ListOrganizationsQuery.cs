using NovaFE.Domain.Common;

namespace NovaFE.Application.Organizations.ListOrganizations;

/// <summary>Paged list of organizations for the operator console. <see cref="Search"/> matches name or slug.</summary>
public sealed record ListOrganizationsQuery : PagedRequest
{
    public string? Search { get; init; }
}
