using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Ecf;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Ecf.EfCore;

internal sealed class EcfRepository(AppDbContext context) : IEcfRepository
{
    public Task<IssuedEcf?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => context.IssuedEcf.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task AddAsync(IssuedEcf ecf, CancellationToken ct = default)
    {
        await context.IssuedEcf.AddAsync(ecf, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(IssuedEcf ecf, CancellationToken ct = default)
    {
        context.IssuedEcf.Update(ecf);
        await context.SaveChangesAsync(ct);
    }
}
