using NovaFE.Application.Sequences.Contracts;

namespace NovaFE.Application.Sequences.Interfaces;

/// <summary>Lado de lectura (Dapper). Filtrado al tenant actual.</summary>
public interface INcfSequenceReadRepository
{
    Task<NcfSequenceDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Todos los rangos del tenant (son pocos — sin paginación).</summary>
    Task<IReadOnlyList<NcfSequenceDto>> ListAsync(CancellationToken ct = default);
}
