using Microsoft.EntityFrameworkCore;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Users;
using NovaFE.Infrastructure.Persistence.EfCore;

namespace NovaFE.Infrastructure.Users.EfCore;

internal sealed class PlatformUserRepository(AppDbContext context) : IPlatformUserRepository
{
    public Task<PlatformUser?> GetAsync(Guid id, CancellationToken ct = default)
        => context.PlatformUsers.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<PlatformUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        return context.PlatformUsers.FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    public async Task AddAsync(PlatformUser user, CancellationToken ct = default)
    {
        await context.PlatformUsers.AddAsync(user, ct);
        await context.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(PlatformUser user, CancellationToken ct = default)
        => context.SaveChangesAsync(ct);

    public Task LinkAuthUserAsync(Guid id, string authUserId, CancellationToken ct = default)
        => context.PlatformUsers
            .Where(u => u.Id == id && u.AuthUserId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.AuthUserId, authUserId), ct);
}
