using System.Collections.Concurrent;

namespace NovaFE.Service.Workers;

/// <summary>Implementación en memoria de <see cref="IWorkerHeartbeat"/>. Un singleton por proceso.</summary>
public sealed class WorkerHeartbeat(TimeProvider timeProvider) : IWorkerHeartbeat
{
    private readonly ConcurrentDictionary<string, WorkerHeartbeatEntry> _entries = new(StringComparer.Ordinal);

    public void Beat(string worker, TimeSpan maxSilence) =>
        _entries[worker] = new WorkerHeartbeatEntry(timeProvider.GetUtcNow(), maxSilence);

    public IReadOnlyDictionary<string, WorkerHeartbeatEntry> Entries => _entries;
}
