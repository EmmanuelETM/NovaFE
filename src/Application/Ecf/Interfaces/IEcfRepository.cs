using NovaFE.Domain.Ecf;

namespace NovaFE.Application.Ecf.Interfaces;

/// <summary>
/// Write side (EF Core) del agregado <see cref="Ecf"/> — el comprobante emitido.
/// Las consultas de solo lectura van por <see cref="IEcfReadRepository"/>.
/// </summary>
public interface IEcfRepository
{
    Task<IssuedEcf?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(IssuedEcf ecf, CancellationToken ct = default);
}
