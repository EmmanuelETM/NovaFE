using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common.Json;
using NovaFE.Domain.Webhooks;
using NovaFE.Infrastructure.Persistence.EfCore;

namespace NovaFE.Infrastructure.Webhooks.Outbox;

/// <summary>
/// Outbox de entrega sobre <c>webhook_deliveries</c>. Reclamo con
/// <c>FOR UPDATE SKIP LOCKED</c> + <c>locked_by</c> único por llamada, igual que
/// <c>PostgresEcfSubmissionQueue</c>. El <c>Enqueue</c> lee los endpoints del
/// tenant y crea una fila por cada uno habilitado y suscrito al tipo.
/// </summary>
internal sealed class PostgresWebhookOutbox(AppDbContext context, TimeProvider timeProvider) : IWebhookOutbox
{
    public async Task<int> EnqueueAsync(WebhookEventEnvelope envelope, Guid tenantId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // El filtro global "Tenant" acota esto al tenant en curso (que el worker /
        // la petición ya fijaron). El comodín no es SQL-friendly → se filtra acá.
        var endpoints = await context.WebhookEndpoints
            .Where(e => e.Enabled)
            .Select(e => new { e.Id, e.Events })
            .ToListAsync(ct);

        var targets = endpoints
            .Where(e => e.Events.Any(s => WebhookEventType.Covers(s, envelope.Type)))
            .Select(e => e.Id)
            .ToList();

        if (targets.Count == 0)
            return 0;

        var payload = JsonSerializer.Serialize(envelope, JsonSettings.Bulletproof);
        var now = timeProvider.GetUtcNow();

        foreach (var endpointId in targets)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO webhook_deliveries
                    (id, tenant_id, endpoint_id, event_id, event_type, payload, status, attempts, next_attempt_at, created_at, updated_at)
                VALUES
                    ({Guid.CreateVersion7()}, {tenantId}, {endpointId}, {envelope.Id}, {envelope.Type},
                     {payload}::jsonb, 'pending', 0, {now}, {now}, {now})
                """, ct);
        }

        return targets.Count;
    }

    public async Task<IReadOnlyList<WebhookDeliveryItem>> ClaimBatchAsync(int max, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var token = Guid.NewGuid();

        var claimed = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE webhook_deliveries
            SET status = 'processing', locked_at = {now}, locked_by = {token}, updated_at = {now}
            WHERE id IN (
                SELECT id FROM webhook_deliveries
                WHERE status = 'pending' AND next_attempt_at <= {now}
                ORDER BY next_attempt_at
                FOR UPDATE SKIP LOCKED
                LIMIT {max}
            )
            """, ct);

        if (claimed == 0)
            return [];

        var rows = await context.WebhookDeliveries
            .FromSql($"SELECT * FROM webhook_deliveries WHERE locked_by = {token}")
            .AsNoTracking()
            .ToListAsync(ct);

        return [.. rows.Select(r => new WebhookDeliveryItem(
            r.Id, r.TenantId, r.EndpointId, r.EventId, r.EventType, r.Payload, r.Attempts))];
    }

    public Task CompleteAsync(Guid rowId, int statusCode, CancellationToken ct = default)
        => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE webhook_deliveries
            SET status = 'delivered', last_status_code = {statusCode}, last_error = NULL,
                locked_at = NULL, locked_by = NULL, updated_at = {timeProvider.GetUtcNow()}
            WHERE id = {rowId}
            """, ct);

    public Task RescheduleAsync(
        Guid rowId, DateTimeOffset nextAttemptAt, int attempts, int? statusCode, string? lastError, CancellationToken ct = default)
        => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE webhook_deliveries
            SET status = 'pending', next_attempt_at = {nextAttemptAt}, attempts = {attempts},
                last_status_code = {statusCode}, last_error = {Truncate(lastError)},
                locked_at = NULL, locked_by = NULL, updated_at = {timeProvider.GetUtcNow()}
            WHERE id = {rowId}
            """, ct);

    public Task MarkDeadAsync(Guid rowId, int? statusCode, string? lastError, CancellationToken ct = default)
        => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE webhook_deliveries
            SET status = 'dead', last_status_code = {statusCode}, last_error = {Truncate(lastError)},
                locked_at = NULL, locked_by = NULL, updated_at = {timeProvider.GetUtcNow()}
            WHERE id = {rowId}
            """, ct);

    public Task<int> ReapStuckAsync(TimeSpan olderThan, CancellationToken ct = default)
    {
        var cutoff = timeProvider.GetUtcNow() - olderThan;

        return context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE webhook_deliveries
            SET status = 'pending', locked_at = NULL, locked_by = NULL, updated_at = {timeProvider.GetUtcNow()}
            WHERE status = 'processing' AND locked_at < {cutoff}
            """, ct);
    }

    private static string? Truncate(string? value) =>
        value is { Length: > 1000 } ? value[..1000] : value;
}
