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

    [Theory]
    [InlineData("submission.poll_ladder", "1m,2m")]
    [InlineData("submission.backoff", "1m,2h")]
    [InlineData("webhooks.backoff_ladder", "10s,1d")]
    public void The_ladder_settings_are_platform_scoped_and_reject_malformed_lists(string key, string validList)
    {
        var def = SettingDefinitions.FindByKey(key).ShouldBeOfType<StringSetting>();

        def.Scope.ShouldBe(SettingScope.Platform);
        def.TenantWritable.ShouldBeFalse();
        def.Validate(validList).IsError.ShouldBeFalse();
        def.Validate("abc").IsError.ShouldBeTrue();
        def.Validate("5x,1m").IsError.ShouldBeTrue();
        def.Validate("").IsError.ShouldBeTrue();
        def.Validate(",").IsError.ShouldBeTrue();
    }

    [Theory]
    [InlineData("submission.batch_size")]
    [InlineData("webhooks.max_attempts")]
    [InlineData("webhooks.auto_disable_after_failures")]
    [InlineData("webhooks.batch_size")]
    public void The_new_integer_settings_are_platform_scoped(string key)
    {
        var def = SettingDefinitions.FindByKey(key).ShouldBeOfType<IntegerSetting>();

        def.Scope.ShouldBe(SettingScope.Platform);
        def.TenantWritable.ShouldBeFalse();
    }

    [Fact]
    public void The_webhooks_delivery_timeout_is_a_bounded_duration()
    {
        var def = SettingDefinitions.WebhooksDeliveryTimeout;

        def.Scope.ShouldBe(SettingScope.Platform);
        def.Validate("10s").IsError.ShouldBeFalse();
        def.Validate("500ms").IsError.ShouldBeTrue();
        def.Validate("2m").IsError.ShouldBeTrue();
    }
}
