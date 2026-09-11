using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Infrastructure.Persistence.EfCore;

namespace NovaFE.Infrastructure.Settings.EfCore;

/// <summary>
/// Bitácora append-only de cambios de settings de tenant. Solo inserta — sin
/// update/delete (mismo criterio de inmutabilidad que <c>AuditLogWriter</c>). La
/// entidad no es <c>IAuditableEntity</c>, así que la fecha y el autor se asignan
/// acá; el <c>TenantId</c> lo estampa <c>TenantStampingInterceptor</c>.
/// <para>
/// El autor se guarda como el <b>correo</b> del usuario (<c>ICurrentUser.UserName</c>),
/// para leer el historial sin resolver ids contra <c>platform_users</c>.
/// </para>
/// </summary>
internal sealed class TenantSettingChangeLog(
    AppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ITenantSettingChangeLog
{
    public async Task RecordAsync(
        string key, string? previousValue, string? newValue, CancellationToken ct = default)
    {
        await context.TenantSettingChanges.AddAsync(
            new TenantSettingChange(
                key, previousValue, newValue, timeProvider.GetUtcNow(),
                currentUser.UserName ?? currentUser.Id ?? "usuario"),
            ct);

        await context.SaveChangesAsync(ct);
    }
}
