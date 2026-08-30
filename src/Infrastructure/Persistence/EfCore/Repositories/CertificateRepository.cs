using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Domain.Certificates;
using NovaFE.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Persistence.EfCore.Repositories;

internal sealed class CertificateRepository(AppDbContext context) : ICertificateRepository
{
    public Task<Certificate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => context.Certificates.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> HasActiveCertificateAsync(DgiiEnvironment environment, CancellationToken ct = default)
        => context.Certificates.AnyAsync(
            c => c.Environment == environment && c.Status == CertificateStatus.Active, ct);

    public async Task AddAsync(Certificate certificate, CancellationToken ct = default)
    {
        await context.Certificates.AddAsync(certificate, ct);
        await context.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(Certificate certificate, CancellationToken ct = default)
        => context.SaveChangesAsync(ct);
}
