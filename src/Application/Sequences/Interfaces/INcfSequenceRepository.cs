using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;

namespace NovaFE.Application.Sequences.Interfaces;

/// <summary>Lado de escritura (EF Core). Todas las consultas ya van filtradas al tenant actual.</summary>
public interface INcfSequenceRepository
{
    Task<NcfSequence?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>¿Hay ya un rango activo para esa serie, tipo y ambiente?</summary>
    Task<bool> HasActiveRangeAsync(
        DgiiEnvironment environment,
        EcfType type,
        char series,
        CancellationToken ct = default);

    /// <summary>
    /// ¿El contribuyente <paramref name="tenantId"/> tiene algún rango activo (de
    /// cualquier tipo) en ese ambiente? Consulta fuera del scope de tenant (la usa
    /// el operador al acuñar API keys): ignora los filtros globales.
    /// </summary>
    Task<bool> HasAnyActiveRangeForTenantAsync(
        Guid tenantId,
        DgiiEnvironment environment,
        CancellationToken ct = default);

    Task AddAsync(NcfSequence sequence, CancellationToken ct = default);

    Task UpdateAsync(NcfSequence sequence, CancellationToken ct = default);
}
