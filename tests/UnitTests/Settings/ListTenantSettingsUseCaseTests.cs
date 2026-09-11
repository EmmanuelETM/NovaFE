using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Application.Settings.ListTenantSettings;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Settings;

public class ListTenantSettingsUseCaseTests : UseCaseTestBase
{
    private const string LayoutKey = "representation.default_layout";

    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ITenantSettingReadRepository _tenantSettings = Substitute.For<ITenantSettingReadRepository>();
    private readonly IPlatformSettingReadRepository _platformSettings = Substitute.For<IPlatformSettingReadRepository>();

    public ListTenantSettingsUseCaseTests()
    {
        _tenant.TenantId.Returns(Guid.NewGuid());
        _tenantSettings.ListAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _platformSettings.ListAsync(Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private ListTenantSettingsUseCase Sut() => new(
        LoggerFactory, _tenant, _tenantSettings, _platformSettings);

    [Fact]
    public async Task Resolves_from_default_when_nothing_is_overridden()
    {
        var result = await Sut().Execute();

        var layout = result.Value.ShouldHaveSingleItem();
        layout.Key.ShouldBe(LayoutKey);
        layout.ResolvedFrom.ShouldBe("default");
        layout.EffectiveValue.ShouldBe("letter");
        layout.IsOverridden.ShouldBeFalse();
    }

    [Fact]
    public async Task Resolves_from_platform_when_only_a_platform_override_exists()
    {
        _platformSettings.ListAsync(Arg.Any<CancellationToken>())
            .Returns([new PlatformSettingRecord(LayoutKey, "", "pos", Clock.GetUtcNow(), "op")]);

        var result = await Sut().Execute();

        var layout = result.Value.ShouldHaveSingleItem();
        layout.ResolvedFrom.ShouldBe("platform");
        layout.EffectiveValue.ShouldBe("pos");
    }

    [Fact]
    public async Task Resolves_from_tenant_when_the_tenant_has_an_override()
    {
        _tenantSettings.ListAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([new TenantSettingRecord(LayoutKey, "", "pos", Clock.GetUtcNow(), "user@x.com")]);
        _platformSettings.ListAsync(Arg.Any<CancellationToken>())
            .Returns([new PlatformSettingRecord(LayoutKey, "", "letter", Clock.GetUtcNow(), "op")]);

        var result = await Sut().Execute();

        var layout = result.Value.ShouldHaveSingleItem();
        layout.ResolvedFrom.ShouldBe("tenant");
        layout.EffectiveValue.ShouldBe("pos");
        layout.UpdatedBy.ShouldBe("user@x.com");
    }
}
