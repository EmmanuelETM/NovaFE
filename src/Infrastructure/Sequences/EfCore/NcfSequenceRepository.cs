using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Sequences.EfCore;

internal sealed class NcfSequenceRepository(AppDbContext context) : INcfSequenceRepository
{
    public Task<NcfSequence?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => context.NcfSequences.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<bool> HasActiveRangeAsync(
        DgiiEnvironment environment,
        EcfType type,
        char series,
        CancellationToken ct = default)
        => context.NcfSequences.AnyAsync(
            s => s.Environment == environment && s.Type == type && s.Series == series && s.Active, ct);

    public async Task AddAsync(NcfSequence sequence, CancellationToken ct = default)
    {
        await context.NcfSequences.AddAsync(sequence, ct);
        await context.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(NcfSequence sequence, CancellationToken ct = default)
        => context.SaveChangesAsync(ct);
}
