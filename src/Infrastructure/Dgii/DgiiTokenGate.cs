using System.Collections.Concurrent;
using NovaFE.Domain.Common;

namespace NovaFE.Infrastructure.Dgii;

/// <summary>
/// Serializa la renovación del token por (tenant, ambiente): si llegan varias
/// peticiones a la vez con la caché vacía, solo una corre el flujo semilla →
/// token; las demás esperan y toman el resultado de la caché. Singleton.
/// </summary>
internal sealed class DgiiTokenGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public async Task<IDisposable> EnterAsync(Guid tenantId, DgiiEnvironment environment, CancellationToken ct)
    {
        var gate = _gates.GetOrAdd($"{tenantId}:{environment.Name}", static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new Exit(gate);
    }

    private sealed class Exit(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                gate.Release();
        }
    }
}
