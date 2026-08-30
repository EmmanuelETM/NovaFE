using NovaFE.Application.Certificates.Contracts;

namespace NovaFE.Application.Certificates.Interfaces;

/// <summary>Read side (Dapper). Scoped to the current tenant.</summary>
public interface ICertificateReadRepository
{
    Task<CertificateDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>All of the tenant's certificates (there are only a handful — no paging).</summary>
    Task<IReadOnlyList<CertificateDto>> ListAsync(CancellationToken ct = default);
}
