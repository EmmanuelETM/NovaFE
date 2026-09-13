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

    [Fact]
    public void The_kill_switches_are_marked_sensitive()
    {
        SettingDefinitions.MaintenanceMode.Sensitive.ShouldBeTrue();
        SettingDefinitions.ContingencyMode.Sensitive.ShouldBeTrue();
    }

    [Fact]
    public void The_default_representation_layout_is_a_tenant_writable_option()
    {
        var def = SettingDefinitions.RepresentationDefaultLayout;

        def.Scope.ShouldBe(SettingScope.Tenant);
        def.TenantWritable.ShouldBeTrue();
        def.Options.ShouldBe(new[] { "letter", "pos" });
        def.Validate("pos").IsError.ShouldBeFalse();
        def.Validate("POS").IsError.ShouldBeFalse();
        def.Validate("a4").IsError.ShouldBeTrue();
    }

    [Fact]
    public void The_duplicate_detection_mode_defaults_to_off_with_three_options()
    {
        var def = SettingDefinitions.EcfDuplicateDetectionMode;

        def.Scope.ShouldBe(SettingScope.Platform);
        def.TenantWritable.ShouldBeFalse();
        def.SerializedDefault.ShouldBe("off");
        def.Options.ShouldBe(new[] { "off", "observar", "bloquear" });
        def.Validate("bloquear").IsError.ShouldBeFalse();
        def.Validate("BLOQUEAR").IsError.ShouldBeFalse();
        def.Validate("bloqueando").IsError.ShouldBeTrue();
    }

    [Fact]
    public void The_duplicate_detection_window_is_bounded_in_minutes_to_hours()
    {
        var def = SettingDefinitions.EcfDuplicateDetectionWindow;

        def.Scope.ShouldBe(SettingScope.Platform);
        def.Validate("5m").IsError.ShouldBeFalse();
        def.Validate("10s").IsError.ShouldBeTrue();
        def.Validate("2h").IsError.ShouldBeTrue();
    }
}
