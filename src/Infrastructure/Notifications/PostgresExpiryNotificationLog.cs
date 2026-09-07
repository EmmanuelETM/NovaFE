using NovaFE.Application.Notifications;
using NovaFE.Domain.Common;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Notifications;

/// <summary>
/// <see cref="IExpiryNotificationLog"/> sobre <c>expiry_notifications</c>. El
/// <c>INSERT … ON CONFLICT DO NOTHING</c> hace que solo el primero en registrar
/// un aviso lo envíe, aun con varios workers a la vez.
/// </summary>
internal sealed class PostgresExpiryNotificationLog(AppDbContext context, TimeProvider timeProvider)
    : IExpiryNotificationLog
{
    public async Task<bool> TryRecordAsync(
        string subjectType, Guid subjectId, string kind, CancellationToken ct = default)
    {
        var inserted = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO expiry_notifications (id, subject_type, subject_id, kind, notified_at)
            VALUES ({Guid.CreateVersion7()}, {subjectType}, {subjectId}, {kind}, {timeProvider.GetUtcNow()})
            ON CONFLICT (subject_type, subject_id, kind) DO NOTHING
            """, ct);

        return inserted == 1;
    }
}
