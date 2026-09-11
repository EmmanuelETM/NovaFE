using NovaFE.Domain.Common.Entities;

namespace NovaFE.Infrastructure.Settings.EfCore;

/// <summary>
/// Una fila de <c>tenant_settings</c>: el override de un <c>SettingDefinition</c>
/// de scope <c>Tenant</c> para un contribuyente. Entidad <see cref="ITenantOwned"/>
/// con RLS. Volver al default borra la fila físicamente, así que <b>no</b> es
/// <see cref="ISoftDeletable"/>.
/// <para>
/// <see cref="TenantId"/> lo estampa <c>TenantStampingInterceptor</c> al insertar;
/// la unicidad real es <c>(tenant_id, key, environment)</c> (índice único), con un
/// <see cref="Id"/> sustituto como clave primaria.
/// </para>
/// </summary>
internal sealed class TenantSetting : ITenantOwned, IAuditableEntity
{
    private TenantSetting()
    {
    }

    public TenantSetting(string key, string value, string environment = "")
    {
        Id = Guid.CreateVersion7();
        Key = key;
        Value = value;
        Environment = environment;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Clave del <c>SettingDefinition</c>.</summary>
    public string Key { get; private set; } = null!;

    /// <summary>Ambiente DGII (<c>test</c>/<c>cert</c>/<c>prod</c>) o <c>""</c> (agnóstico).</summary>
    public string Environment { get; private set; } = "";

    /// <summary>Valor crudo; lo parsea el <c>SettingDefinition</c>.</summary>
    public string Value { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public void SetValue(string value) => Value = value;
}
