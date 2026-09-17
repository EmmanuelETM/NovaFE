using Microsoft.EntityFrameworkCore;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Organizations;
using NovaFE.Infrastructure.Persistence.EfCore;

namespace NovaFE.Infrastructure.Organizations.EfCore;

internal sealed class OrganizationRepository(AppDbContext context) : IOrganizationRepository
{
    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => context.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => context.Organizations.AnyAsync(o => o.Slug == slug, ct);

    public async Task AddAsync(Organization organization, CancellationToken ct = default)
    {
        await context.Organizations.AddAsync(organization, ct);
        await context.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(Organization organization, CancellationToken ct = default)
        => context.SaveChangesAsync(ct);
}
