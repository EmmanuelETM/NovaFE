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
    public void The_low_stock_fraction_is_a_bounded_platform_decimal()
    {
        var def = SettingDefinitions.SequenceLowStockFraction;

        def.Scope.ShouldBe(SettingScope.Platform);
        def.TenantWritable.ShouldBeFalse();
        def.Validate("0.20").IsError.ShouldBeFalse();
        def.Validate("1").IsError.ShouldBeFalse();
        def.Validate("0").IsError.ShouldBeTrue();
        def.Validate("1.01").IsError.ShouldBeTrue();
    }

    [Theory]
    [InlineData("notifications.certificate_expiry_thresholds_days", "90,30,15,7")]
    [InlineData("notifications.sequence_expiry_thresholds_days", "30,7")]
    public void The_expiry_threshold_settings_are_platform_scoped_and_reject_malformed_lists(string key, string validList)
    {
        var def = SettingDefinitions.FindByKey(key).ShouldBeOfType<StringSetting>();

        def.Scope.ShouldBe(SettingScope.Platform);
        def.TenantWritable.ShouldBeFalse();
        def.Validate(validList).IsError.ShouldBeFalse();
        def.Validate("abc").IsError.ShouldBeTrue();
        def.Validate("30,-7").IsError.ShouldBeTrue();
        def.Validate("").IsError.ShouldBeTrue();
    }
}
