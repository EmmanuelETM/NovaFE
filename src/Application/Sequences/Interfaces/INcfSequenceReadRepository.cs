using NovaFE.Application.Sequences.ReadModels;

namespace NovaFE.Application.Sequences.Interfaces;

/// <summary>Lado de lectura (Dapper). Filtrado al tenant actual.</summary>
public interface INcfSequenceReadRepository
{
    Task<NcfSequenceView?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Todos los rangos del tenant (son pocos — sin paginación).</summary>
    Task<IReadOnlyList<NcfSequenceView>> ListAsync(CancellationToken ct = default);
}
