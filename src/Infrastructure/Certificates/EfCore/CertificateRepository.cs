using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Domain.Certificates;
using NovaFE.Domain.Common;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Certificates.EfCore;

internal sealed class CertificateRepository(AppDbContext context) : ICertificateRepository
{
    public Task<Certificate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => context.Certificates.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Certificate?> GetActiveAsync(DgiiEnvironment environment, CancellationToken ct = default)
        => context.Certificates.FirstOrDefaultAsync(
            c => c.Environment == environment && c.Status == CertificateStatus.Active, ct);

    public Task<bool> HasActiveCertificateAsync(DgiiEnvironment environment, CancellationToken ct = default)
        => context.Certificates.AnyAsync(
            c => c.Environment == environment && c.Status == CertificateStatus.Active, ct);

    public Task<bool> HasActiveForTenantAsync(Guid tenantId, DgiiEnvironment environment, CancellationToken ct = default)
        => context.Certificates.IgnoreQueryFilters().AnyAsync(
            c => c.TenantId == tenantId
                 && !c.IsDeleted
                 && c.Environment == environment
                 && c.Status == CertificateStatus.Active, ct);

    public async Task AddAsync(Certificate certificate, CancellationToken ct = default)
    {
        await context.Certificates.AddAsync(certificate, ct);
        await context.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(Certificate certificate, CancellationToken ct = default)
        => context.SaveChangesAsync(ct);
}
