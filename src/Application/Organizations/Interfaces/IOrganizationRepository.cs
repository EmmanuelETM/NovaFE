using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.Interfaces;

/// <summary>Write side (EF Core). Loads and persists the <see cref="Organization"/> aggregate.</summary>
public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);

    Task AddAsync(Organization organization, CancellationToken ct = default);

    Task UpdateAsync(Organization organization, CancellationToken ct = default);
}
