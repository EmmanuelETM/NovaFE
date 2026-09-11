using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Infrastructure.Persistence.EfCore;

namespace NovaFE.Infrastructure.Settings.EfCore;

/// <summary>
/// Bitácora append-only de cambios de settings de plataforma. Solo inserta — sin
/// update/delete (mismo criterio de inmutabilidad que <c>AuditLogWriter</c>). La
/// entidad no es <c>IAuditableEntity</c>, así que la fecha y el autor se asignan
/// acá explícitamente.
/// <para>
/// El autor se guarda como el <b>correo</b> del operador (<c>ICurrentUser.UserName</c>,
/// que en el esquema del dashboard es el email; <c>operator</c> en el rompe-cristal)
/// — así el historial se lee sin resolver ids contra <c>platform_users</c>.
/// </para>
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
                key, previousValue, newValue, timeProvider.GetUtcNow(),
                currentUser.UserName ?? currentUser.Id ?? "operador"),
            ct);

        await context.SaveChangesAsync(ct);
    }
}
