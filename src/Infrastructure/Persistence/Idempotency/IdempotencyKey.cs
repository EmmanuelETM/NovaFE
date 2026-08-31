namespace NovaFE.Infrastructure.Persistence.Idempotency;

/// <summary>
/// Fila de <c>idempotency_keys</c>. Solo define el esquema (las migraciones salen
/// de entidades EF); las lecturas y el upsert los hace
/// <see cref="PostgresIdempotencyStore"/> con Dapper.
/// </summary>
internal sealed class IdempotencyKey
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Key { get; private set; } = null!;

    public string RequestHash { get; private set; } = null!;

    public Guid? ResourceId { get; private set; }

    public string Status { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }
}
