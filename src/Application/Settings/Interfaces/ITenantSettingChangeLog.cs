namespace NovaFE.Application.Settings.Interfaces;

/// <summary>
/// Bitácora append-only de cambios de settings de tenant. Solo
/// <see cref="RecordAsync"/> — la ausencia de update/delete es la inmutabilidad
/// (mismo criterio que <c>audit_log</c>). El <c>tenant_id</c> lo estampa el
/// interceptor de EF a partir de <c>ICurrentTenant</c>.
/// <para>
/// <paramref name="previousValue"/> null distingue la <b>primera</b> sobrescritura;
/// <paramref name="newValue"/> null es la <b>vuelta al default</b>.
/// </para>
/// </summary>
public interface ITenantSettingChangeLog
{
    Task RecordAsync(string key, string? previousValue, string? newValue, CancellationToken ct = default);
}
