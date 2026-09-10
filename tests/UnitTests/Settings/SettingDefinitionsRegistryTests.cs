using NovaFE.Domain.Settings;

namespace NovaFE.UnitTests.Settings;

public class SettingDefinitionsRegistryTests
{
    [Fact]
    public void All_is_not_empty_and_contains_maintenance_mode()
    {
        SettingDefinitions.All.ShouldNotBeEmpty();
        SettingDefinitions.All.ShouldContain(SettingDefinitions.MaintenanceMode);
    }

    [Fact]
    public void Keys_are_unique()
    {
        var keys = SettingDefinitions.All.Select(d => d.Key).ToArray();

        keys.Distinct(StringComparer.Ordinal).Count().ShouldBe(keys.Length);
    }

    [Fact]
    public void Every_default_is_valid_against_its_own_definition()
    {
        foreach (var def in SettingDefinitions.All)
            def.Validate(def.SerializedDefault).IsError
                .ShouldBeFalse($"el default de «{def.Key}» no pasa su propia validación");
    }

    [Fact]
    public void FindByKey_resolves_known_keys_and_returns_null_otherwise()
    {
        SettingDefinitions.FindByKey("platform.maintenance_mode").ShouldBe(SettingDefinitions.MaintenanceMode);
        SettingDefinitions.FindByKey("nope.not.here").ShouldBeNull();
    }
}
