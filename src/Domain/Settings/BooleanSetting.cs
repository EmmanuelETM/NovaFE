namespace NovaFE.Domain.Settings;

/// <summary>Setting booleano. Acepta <c>true/false</c> (sin distinguir mayúsculas) y <c>1/0</c>.</summary>
public sealed class BooleanSetting(
    string key,
    string group,
    string label,
    bool @default,
    string? description = null,
    SettingScope scope = SettingScope.Platform,
    bool killSwitch = false,
    bool sensitive = false,
    bool deprecated = false,
    bool tenantWritable = false)
    : SettingDefinition<bool>(
        key, "boolean", group, label, @default, description, unit: null,
        scope, killSwitch, sensitive, deprecated, tenantWritable)
{
    public override string Format(bool value) => value ? "true" : "false";

    public override bool TryParse(string raw, out bool value)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "true" or "1":
                value = true;
                return true;
            case "false" or "0":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }
}
