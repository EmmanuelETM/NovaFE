namespace NovaFE.Application.Common.Interfaces;

/// <summary>Último latido de un worker y cuánto puede pasar sin uno antes de considerarlo atascado.</summary>
public readonly record struct WorkerHeartbeatEntry(DateTimeOffset LastBeatAt, TimeSpan MaxSilence);

/// <summary>
/// Última vez que cada worker en background completó un tick (exitoso o no,
/// mientras no se haya quedado colgado a mitad de un <c>await</c>). La
/// implementación vive en <c>Service</c> (es plomería de proceso, no una regla
/// de negocio), pero se declara acá para que el panel de operación
/// (<c>GetOpsStatusUseCase</c>) pueda leerla sin que <c>Application</c>
/// referencie <c>Service</c> — mismo patrón que <see cref="ICurrentUser"/>.
/// </summary>
public interface IWorkerHeartbeat
{
    /// <summary>
    /// Registra que <paramref name="worker"/> acaba de completar un tick.
    /// <paramref name="maxSilence"/> es cuánto puede tardar el próximo antes de
    /// considerarlo atascado — cada worker lo deriva de su propio intervalo
    /// configurado (con margen), no hay un umbral global único porque un
    /// worker de segundos y uno de horas no son comparables.
    /// </summary>
    void Beat(string worker, TimeSpan maxSilence);

    /// <summary>Estado de cada worker que ya latió al menos una vez.</summary>
    IReadOnlyDictionary<string, WorkerHeartbeatEntry> Entries { get; }
}
