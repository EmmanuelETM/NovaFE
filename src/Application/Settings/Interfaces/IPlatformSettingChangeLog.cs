namespace NovaFE.Application.Settings.Interfaces;

/// <summary>
/// Bitácora append-only de cambios de settings de plataforma. Solo tiene
/// <see cref="RecordAsync"/> — la ausencia de update/delete es la inmutabilidad a
/// nivel de aplicación (mismo criterio que <c>audit_log</c>).
/// <para>
/// <paramref name="previousValue"/> null distingue la <b>primera</b> sobrescritura;
/// <paramref name="newValue"/> null es la <b>vuelta al default</b>.
/// </para>
/// </summary>
public interface IPlatformSettingChangeLog
{
    Task RecordAsync(string key, string? previousValue, string? newValue, CancellationToken ct = default);
}
