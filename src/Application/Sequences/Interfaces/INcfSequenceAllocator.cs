using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;

namespace NovaFE.Application.Sequences.Interfaces;

/// <summary>
/// Entrega una secuencia e-NCF del inventario del <b>tenant actual</b> de forma
/// atómica: toma un lock pesimista sobre los rangos autorizados del tipo, valida
/// vencimiento antes del lock lógico, y avanza el puntero de un solo rango dentro
/// de una transacción. Bajo concurrencia, dos peticiones nunca reciben el mismo
/// número (RF-07.2).
/// </summary>
public interface INcfSequenceAllocator
{
    Task<ErrorOr<Encf>> AllocateAsync(
        DgiiEnvironment environment,
        EcfType type,
        CancellationToken ct = default);
}
