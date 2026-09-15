namespace NovaFE.Application.Ops.Contracts;

/// <summary>Un worker de fondo y si su último latido sigue dentro de lo esperado.</summary>
public sealed record WorkerStatusDto(
    string Name,
    DateTimeOffset LastBeatAt,
    TimeSpan MaxSilence,
    bool Healthy);
