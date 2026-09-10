namespace NovaFE.Infrastructure.Settings.EfCore;

/// <summary>
/// Fila de <c>platform_setting_changes</c>: la bitácora append-only de cambios de
/// settings de plataforma. Tabla de sistema, sin RLS. Solo se inserta — nunca se
/// actualiza ni se borra (esa ausencia es la inmutabilidad).
/// </summary>
internal sealed class PlatformSettingChange
{
    private PlatformSettingChange()
    {
    }

    public PlatformSettingChange(
        string key,
        string? previousValue,
        string? newValue,
        DateTimeOffset changedAt,
        string? changedBy,
        string environment = "")
    {
        Id = Guid.CreateVersion7();
        Key = key;
        Environment = environment;
        PreviousValue = previousValue;
        NewValue = newValue;
        ChangedAt = changedAt;
        ChangedBy = changedBy;
    }

    public Guid Id { get; private set; }

    public string Key { get; private set; } = null!;

    public string Environment { get; private set; } = "";

    /// <summary>null = primera sobrescritura.</summary>
    public string? PreviousValue { get; private set; }

    /// <summary>null = vuelta al default.</summary>
    public string? NewValue { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary><see cref="Application.Common.Interfaces.ICurrentUser"/> del operador que hizo el cambio.</summary>
    public string? ChangedBy { get; private set; }
}
