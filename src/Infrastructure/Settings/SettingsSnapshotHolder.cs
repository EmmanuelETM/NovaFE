namespace NovaFE.Infrastructure.Settings;

/// <summary>Guarda el <see cref="SettingsSnapshot"/> vigente. Singleton.</summary>
internal interface ISettingsSnapshotHolder
{
    SettingsSnapshot Current { get; }

    void Replace(SettingsSnapshot snapshot);
}

internal sealed class SettingsSnapshotHolder : ISettingsSnapshotHolder
{
    private volatile SettingsSnapshot _current = SettingsSnapshot.Empty;

    public SettingsSnapshot Current => _current;

    public void Replace(SettingsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _current = snapshot;
    }
}
