using NovaFE.Application.Settings.Contracts;
using NovaFE.Domain.Settings;

namespace NovaFE.Application.Settings;

/// <summary>
/// Proyecta una definición de scope <c>Tenant</c> (+ su override de tenant y/o de
/// plataforma) al DTO de la API. La resolución es <c>tenant → platform → default</c>.
/// </summary>
internal static class TenantSettingMapper
{
    public static TenantSettingDto ToDto(
        SettingDefinition definition,
        TenantSettingRecord? tenantRow,
        string? platformOverrideValue)
    {
        var hints = definition.Hints;

        var (effective, resolvedFrom, overridden) = tenantRow is not null
            ? (tenantRow.Value, "tenant", true)
            : platformOverrideValue is not null
                ? (platformOverrideValue, "platform", true)
                : (definition.SerializedDefault, "default", false);

        return new TenantSettingDto(
            Key: definition.Key,
            Group: definition.Group,
            Label: definition.Label,
            Description: definition.Description,
            ValueType: definition.ValueType,
            Unit: definition.Unit,
            Constraints: definition.Constraints,
            Options: hints?.Options,
            Min: hints?.Min,
            Max: hints?.Max,
            TenantWritable: definition.TenantWritable,
            Deprecated: definition.Deprecated,
            DefaultValue: definition.SerializedDefault,
            EffectiveValue: effective,
            IsOverridden: overridden,
            ResolvedFrom: resolvedFrom,
            UpdatedAt: tenantRow?.UpdatedAt,
            UpdatedBy: tenantRow?.UpdatedBy);
    }
}
