using NovaFE.Application.Settings.Contracts;
using NovaFE.Domain.Settings;

namespace NovaFE.Application.Settings;

/// <summary>Proyecta una definición (+ su override si lo hay) al DTO de la API.</summary>
internal static class PlatformSettingMapper
{
    public static PlatformSettingDto ToDto(SettingDefinition definition, PlatformSettingRecord? overrideRow)
    {
        var overridden = overrideRow is not null;

        return new PlatformSettingDto(
            Key: definition.Key,
            Group: definition.Group,
            Label: definition.Label,
            Description: definition.Description,
            ValueType: definition.ValueType,
            Unit: definition.Unit,
            Constraints: definition.Constraints,
            KillSwitch: definition.KillSwitch,
            Sensitive: definition.Sensitive,
            Deprecated: definition.Deprecated,
            DefaultValue: definition.SerializedDefault,
            EffectiveValue: overrideRow?.Value ?? definition.SerializedDefault,
            IsOverridden: overridden,
            ResolvedFrom: overridden ? "platform" : "default",
            UpdatedAt: overrideRow?.UpdatedAt,
            UpdatedBy: overrideRow?.UpdatedBy);
    }
}
