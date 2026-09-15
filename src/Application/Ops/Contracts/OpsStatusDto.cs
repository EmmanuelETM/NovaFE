namespace NovaFE.Application.Ops.Contracts;

/// <summary>
/// Estado operacional en vivo de la plataforma: recurso de operador, no de
/// negocio (no hay entidad de dominio detrás). Ver docs/observability.md.
/// </summary>
public sealed record OpsStatusDto(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<WorkerStatusDto> Workers,
    OutboxStatusDto EcfSubmissionOutbox,
    OutboxStatusDto WebhookOutbox,
    IReadOnlyList<SequenceAtRiskDto> SequencesAtRisk,
    bool ContingencyActive);
