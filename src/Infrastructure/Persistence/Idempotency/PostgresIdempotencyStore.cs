using Dapper;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Persistence.Idempotency;

/// <summary>
/// Almacén de idempotencia sobre <c>idempotency_keys</c>. <see cref="BeginAsync"/>
/// reserva la clave con <c>INSERT … ON CONFLICT DO NOTHING</c>; si ya existía,
/// decide entre replay, conflicto o "en curso" según su estado y su hash. Una fila
/// <c>pending</c> más vieja que <see cref="StalePendingMinutes"/> se considera
/// abandonada y se puede reclamar.
/// </summary>
internal sealed class PostgresIdempotencyStore(IDbSession session, TimeProvider timeProvider) : IIdempotencyStore
{
    private const int StalePendingMinutes = 10;

    public async Task<IdempotencyOutcome> BeginAsync(
        Guid tenantId, string key, string requestHash, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var connection = await session.GetConnectionAsync(ct);

        var reserved = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            """
            INSERT INTO idempotency_keys (id, tenant_id, key, request_hash, status, created_at)
            VALUES (@id, @tenantId, @key, @requestHash, 'pending', @now)
            ON CONFLICT (tenant_id, key) DO NOTHING
            RETURNING id
            """,
            new { id = Guid.CreateVersion7(), tenantId, key, requestHash, now },
            session.Transaction, cancellationToken: ct));

        if (reserved is not null)
            return new IdempotencyOutcome(IdempotencyDecision.Proceed);

        var existing = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            """
            SELECT request_hash AS "RequestHash", status AS "Status", resource_id AS "ResourceId",
                   (created_at > @staleBefore) AS "Fresh"
            FROM idempotency_keys
            WHERE tenant_id = @tenantId AND key = @key
            """,
            new { tenantId, key, staleBefore = now.AddMinutes(-StalePendingMinutes) },
            session.Transaction, cancellationToken: ct));

        if (existing is null)
            return new IdempotencyOutcome(IdempotencyDecision.Proceed); // carrera improbable: la fila se fue

        if (existing.Status == "completed")
        {
            return existing.RequestHash == requestHash
                ? new IdempotencyOutcome(IdempotencyDecision.Replay, existing.ResourceId)
                : new IdempotencyOutcome(IdempotencyDecision.Conflict);
        }

        if (existing.Fresh)
            return new IdempotencyOutcome(IdempotencyDecision.InProgress);

        // pending y vieja: reclamar.
        var reclaimed = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            """
            UPDATE idempotency_keys
            SET request_hash = @requestHash, created_at = @now, status = 'pending', resource_id = NULL
            WHERE tenant_id = @tenantId AND key = @key
              AND status = 'pending' AND created_at <= @staleBefore
            RETURNING id
            """,
            new { tenantId, key, requestHash, now, staleBefore = now.AddMinutes(-StalePendingMinutes) },
            session.Transaction, cancellationToken: ct));

        return reclaimed is not null
            ? new IdempotencyOutcome(IdempotencyDecision.Proceed)
            : new IdempotencyOutcome(IdempotencyDecision.InProgress);
    }

    public async Task CompleteAsync(Guid tenantId, string key, Guid resourceId, CancellationToken ct = default)
    {
        var connection = await session.GetConnectionAsync(ct);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE idempotency_keys
            SET status = 'completed', resource_id = @resourceId, completed_at = @now
            WHERE tenant_id = @tenantId AND key = @key
            """,
            new { tenantId, key, resourceId, now = timeProvider.GetUtcNow() },
            session.Transaction, cancellationToken: ct));
    }

    private sealed record Row(string RequestHash, string Status, Guid? ResourceId, bool Fresh);
}
