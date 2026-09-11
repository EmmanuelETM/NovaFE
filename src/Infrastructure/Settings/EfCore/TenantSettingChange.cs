using NovaFE.Domain.Common.Entities;

namespace NovaFE.Infrastructure.Settings.EfCore;

/// <summary>
/// Fila de <c>tenant_setting_changes</c>: la bitácora append-only de cambios de
/// settings de un contribuyente. Entidad <see cref="ITenantOwned"/> con RLS. Solo
/// se inserta — nunca se actualiza ni se borra (esa ausencia es la inmutabilidad).
/// </summary>
internal sealed class TenantSettingChange : ITenantOwned
{
    private TenantSettingChange()
    {
    }

    public TenantSettingChange(
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

    /// <summary>Lo estampa <c>TenantStampingInterceptor</c> al insertar.</summary>
    public Guid TenantId { get; private set; }

    public string Key { get; private set; } = null!;

    public string Environment { get; private set; } = "";

    /// <summary>null = primera sobrescritura.</summary>
    public string? PreviousValue { get; private set; }

    /// <summary>null = vuelta al default.</summary>
    public string? NewValue { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>Correo (<see cref="Application.Common.Interfaces.ICurrentUser.UserName"/>) del usuario que hizo el cambio.</summary>
    public string? ChangedBy { get; private set; }
}
