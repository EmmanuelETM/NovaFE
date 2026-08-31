using Microsoft.EntityFrameworkCore;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Infrastructure.Persistence.EfCore;
using NovaFE.Infrastructure.Persistence.Outbox;

namespace NovaFE.Infrastructure.Ecf.Outbox;

/// <summary>
/// Cola de envío sobre <c>ecf_submission_outbox</c>. El reclamo usa
/// <c>FOR UPDATE SKIP LOCKED</c> + un <c>locked_by</c> único por llamada para leer
/// de vuelta exactamente las filas que se acaban de marcar <c>processing</c>, sin
/// mantener una transacción abierta mientras corre la llamada HTTP a la DGII.
/// </summary>
internal sealed class PostgresEcfSubmissionQueue(AppDbContext context, TimeProvider timeProvider) : IEcfSubmissionQueue
{
    public Task EnqueueSubmitAsync(Guid ecfId, Guid tenantId, DgiiEnvironment environment, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        return context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO ecf_submission_outbox
                (id, ecf_id, tenant_id, environment, kind, status, attempts, next_attempt_at, created_at, updated_at)
            VALUES
                ({Guid.CreateVersion7()}, {ecfId}, {tenantId}, {environment.Name}, 'submit', 'pending', 0, {now}, {now}, {now})
            """, ct);
    }

    public async Task<IReadOnlyList<EcfSubmissionWorkItem>> ClaimBatchAsync(int max, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var token = Guid.NewGuid();

        var claimed = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE ecf_submission_outbox
            SET status = 'processing', locked_at = {now}, locked_by = {token}, updated_at = {now}
            WHERE id IN (
                SELECT id FROM ecf_submission_outbox
                WHERE status = 'pending' AND next_attempt_at <= {now}
                ORDER BY next_attempt_at
                FOR UPDATE SKIP LOCKED
                LIMIT {max}
            )
            """, ct);

        return claimed == 0 ? [] : await ReadClaimedAsync(token, ct);
    }

    public async Task<EcfSubmissionWorkItem?> ClaimByEcfAsync(Guid ecfId, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var token = Guid.NewGuid();

        // El fast-path reclama la fila del comprobante ya, sin esperar a su
        // next_attempt_at (esa planificación es solo para el worker de fondo).
        var claimed = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE ecf_submission_outbox
            SET status = 'processing', locked_at = {now}, locked_by = {token}, updated_at = {now}
            WHERE id IN (
                SELECT id FROM ecf_submission_outbox
                WHERE ecf_id = {ecfId} AND status = 'pending'
                ORDER BY next_attempt_at
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            """, ct);

        if (claimed == 0)
            return null;

        var items = await ReadClaimedAsync(token, ct);
        return items.Count == 0 ? null : items[0];
    }

    public Task CompleteAsync(Guid rowId, CancellationToken ct = default)
        => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE ecf_submission_outbox
            SET status = 'done', locked_at = NULL, locked_by = NULL, updated_at = {timeProvider.GetUtcNow()}
            WHERE id = {rowId}
            """, ct);

    public Task RescheduleAsync(
        Guid rowId,
        EcfSubmissionKind kind,
        DateTimeOffset nextAttemptAt,
        int attempts,
        string? trackId = null,
        string? lastError = null,
        CancellationToken ct = default)
        => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE ecf_submission_outbox
            SET status = 'pending', kind = {KindToString(kind)}, next_attempt_at = {nextAttemptAt},
                attempts = {attempts}, track_id = COALESCE({trackId}, track_id), last_error = {lastError},
                locked_at = NULL, locked_by = NULL, updated_at = {timeProvider.GetUtcNow()}
            WHERE id = {rowId}
            """, ct);

    public Task MarkDeadAsync(Guid rowId, string lastError, CancellationToken ct = default)
        => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE ecf_submission_outbox
            SET status = 'dead', last_error = {lastError}, locked_at = NULL, locked_by = NULL,
                updated_at = {timeProvider.GetUtcNow()}
            WHERE id = {rowId}
            """, ct);

    public Task<int> ReapStuckAsync(TimeSpan olderThan, CancellationToken ct = default)
    {
        var cutoff = timeProvider.GetUtcNow() - olderThan;

        return context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE ecf_submission_outbox
            SET status = 'pending', locked_at = NULL, locked_by = NULL, updated_at = {timeProvider.GetUtcNow()}
            WHERE status = 'processing' AND locked_at < {cutoff}
            """, ct);
    }

    private async Task<IReadOnlyList<EcfSubmissionWorkItem>> ReadClaimedAsync(Guid token, CancellationToken ct)
    {
        var rows = await context.EcfSubmissionOutbox
            .FromSql($"SELECT * FROM ecf_submission_outbox WHERE locked_by = {token}")
            .AsNoTracking()
            .ToListAsync(ct);

        return [.. rows.Select(row => new EcfSubmissionWorkItem(
            row.Id,
            row.EcfId,
            row.TenantId,
            DgiiEnvironment.FromName(row.Environment),
            row.Kind == "poll" ? EcfSubmissionKind.Poll : EcfSubmissionKind.Submit,
            row.Attempts,
            row.TrackId))];
    }

    private static string KindToString(EcfSubmissionKind kind) => kind == EcfSubmissionKind.Poll ? "poll" : "submit";
}
