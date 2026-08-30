using NovaFE.Domain.Certificates;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Certificates.Interfaces;

/// <summary>Write side (EF Core). All queries are already scoped to the current tenant.</summary>
public interface ICertificateRepository
{
    Task<Certificate?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> HasActiveCertificateAsync(DgiiEnvironment environment, CancellationToken ct = default);

    Task AddAsync(Certificate certificate, CancellationToken ct = default);

    Task UpdateAsync(Certificate certificate, CancellationToken ct = default);
}
