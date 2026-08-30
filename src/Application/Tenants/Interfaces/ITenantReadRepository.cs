using NovaFE.Application.Tenants.Contracts;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Tenants.Interfaces;

/// <summary>
/// Read side (Dapper). Returns read models, never the domain aggregate.
/// </summary>
public interface ITenantReadRepository
{
    Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<TenantSummaryDto>> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default);
}
