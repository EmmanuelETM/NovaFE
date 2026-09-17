using NovaFE.Application.Organizations.Contracts;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Organizations.Interfaces;

/// <summary>Read side (Dapper). Returns read models, never the domain aggregate.</summary>
public interface IOrganizationReadRepository
{
    Task<OrganizationDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<OrganizationSummaryDto>> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default);

    /// <summary>Los miembros de la organización, el más reciente primero.</summary>
    Task<IReadOnlyList<OrganizationMemberDto>> ListMembersAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Los contribuyentes (tenants/"proyectos") asociados a la organización.</summary>
    Task<PagedResult<TenantSummaryDto>> ListTenantsAsync(
        Guid organizationId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
