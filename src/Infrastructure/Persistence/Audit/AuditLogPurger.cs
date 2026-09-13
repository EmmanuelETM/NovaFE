using Dapper;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Persistence.Audit;

/// <summary>
/// Purga filas viejas de <c>audit_log</c> por retención. Deliberadamente en su
/// propia clase, separada de <see cref="AuditLogWriter"/> — ver
/// <see cref="IAuditLogPurger"/>.
/// </summary>
internal sealed class AuditLogPurger(IDbSession session, TimeProvider timeProvider) : IAuditLogPurger
{
    public async Task<int> PurgeAsync(TimeSpan olderThan, CancellationToken ct = default)
    {
        var connection = await session.GetConnectionAsync(ct);
        var cutoff = timeProvider.GetUtcNow() - olderThan;

        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM audit_log WHERE occurred_at < @cutoff",
            new { cutoff },
            session.Transaction, cancellationToken: ct));
    }
}
