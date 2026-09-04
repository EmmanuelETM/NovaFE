using NovaFE.Domain.Certificates;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Certificates.Interfaces;

/// <summary>Write side (EF Core). All queries are already scoped to the current tenant.</summary>
public interface ICertificateRepository
{
    Task<Certificate?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>The tenant's active certificate for an environment, or null.</summary>
    Task<Certificate?> GetActiveAsync(DgiiEnvironment environment, CancellationToken ct = default);

    Task<bool> HasActiveCertificateAsync(DgiiEnvironment environment, CancellationToken ct = default);

    /// <summary>
    /// ¿El contribuyente <paramref name="tenantId"/> tiene un certificado activo
    /// para ese ambiente? Consulta fuera del scope de tenant (la usa el operador
    /// al acuñar API keys), así que ignora los filtros globales y filtra a mano.
    /// </summary>
    Task<bool> HasActiveForTenantAsync(Guid tenantId, DgiiEnvironment environment, CancellationToken ct = default);

    Task AddAsync(Certificate certificate, CancellationToken ct = default);

    Task UpdateAsync(Certificate certificate, CancellationToken ct = default);
}
