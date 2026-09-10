using NovaFE.Domain.Common.Entities;

namespace NovaFE.Infrastructure.Settings.EfCore;

/// <summary>
/// Una fila de <c>platform_settings</c>: un override de un
/// <c>SettingDefinition</c>. Tabla de sistema — <b>no</b> es <c>ITenantOwned</c> ni
/// lleva RLS; la administra el operador. Volver al default borra la fila
/// físicamente, así que <b>no</b> es <c>ISoftDeletable</c>.
/// </summary>
internal sealed class PlatformSetting : IAuditableEntity
{
    private PlatformSetting()
    {
    }

    public PlatformSetting(string key, string value, string environment = "")
    {
        Key = key;
        Value = value;
        Environment = environment;
    }

    /// <summary>Clave del <c>SettingDefinition</c>. Parte de la clave primaria.</summary>
    public string Key { get; private set; } = null!;

    /// <summary>Ambiente DGII (<c>test</c>/<c>cert</c>/<c>prod</c>) o <c>""</c> (agnóstico). Parte de la PK.</summary>
    public string Environment { get; private set; } = "";

    /// <summary>Valor crudo; lo parsea el <c>SettingDefinition</c>.</summary>
    public string Value { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public void SetValue(string value) => Value = value;
}
