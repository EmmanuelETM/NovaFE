using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Infrastructure.Persistence.EfCore;

namespace NovaFE.Infrastructure.Settings.EfCore;

/// <summary>
/// Bitácora append-only de cambios de settings de plataforma. Solo inserta — sin
/// update/delete (mismo criterio de inmutabilidad que <c>AuditLogWriter</c>). La
/// entidad no es <c>IAuditableEntity</c>, así que la fecha y el autor se asignan
/// acá explícitamente.
/// </summary>
internal sealed class PlatformSettingChangeLog(
    AppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IPlatformSettingChangeLog
{
    public async Task RecordAsync(
        string key, string? previousValue, string? newValue, CancellationToken ct = default)
    {
        await context.PlatformSettingChanges.AddAsync(
            new PlatformSettingChange(
                key, previousValue, newValue, timeProvider.GetUtcNow(), currentUser.Id),
            ct);

        await context.SaveChangesAsync(ct);
    }
}
