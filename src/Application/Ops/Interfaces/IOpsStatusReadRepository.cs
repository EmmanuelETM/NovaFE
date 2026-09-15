using NovaFE.Application.Ops.Contracts;

namespace NovaFE.Application.Ops.Interfaces;

/// <summary>Lecturas cross-tenant para el panel de operación.</summary>
public interface IOpsStatusReadRepository
{
    Task<OutboxStatusDto> GetEcfSubmissionOutboxStatusAsync(CancellationToken ct = default);

    Task<OutboxStatusDto> GetWebhookOutboxStatusAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SequenceAtRiskDto>> GetSequencesAtRiskAsync(decimal lowStockFraction, CancellationToken ct = default);
}
