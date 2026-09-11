using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Settings;
using NovaFE.Infrastructure.Settings;

namespace NovaFE.UnitTests.Settings;

public class ScopedTenantSettingsReaderTests
{
    private static readonly OptionSetting Layout = new(
        "representation.default_layout", "RI", "Formato", @default: "letter",
        options: ["letter", "pos"], scope: SettingScope.Tenant, tenantWritable: true);

    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ITenantSettingReadRepository _readRepo = Substitute.For<ITenantSettingReadRepository>();
    private readonly ISettingsReader _platform = Substitute.For<ISettingsReader>();

    public ScopedTenantSettingsReaderTests()
    {
        _tenant.TenantId.Returns(Guid.NewGuid());
        _platform.GetValue(Layout).Returns("letter");
        _readRepo.ListAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private ScopedTenantSettingsReader Sut() => new(
        _tenant, _readRepo, _platform, NullLogger<ScopedTenantSettingsReader>.Instance);

    [Fact]
    public async Task Returns_the_tenant_override_when_present()
    {
        _readRepo.ListAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([new TenantSettingRecord("representation.default_layout", "", "pos", null, null)]);

        (await Sut().GetValueAsync(Layout)).ShouldBe("pos");
    }

    [Fact]
    public async Task Falls_back_to_the_platform_layer_when_there_is_no_tenant_row()
    {
        (await Sut().GetValueAsync(Layout)).ShouldBe("letter");
        _platform.Received().GetValue(Layout);
    }

    [Fact]
    public async Task Falls_back_when_the_tenant_override_is_corrupt()
    {
        _readRepo.ListAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([new TenantSettingRecord("representation.default_layout", "", "a4", null, null)]);

        (await Sut().GetValueAsync(Layout)).ShouldBe("letter");
    }

    [Fact]
    public async Task Loads_the_tenant_rows_only_once()
    {
        var sut = Sut();
        await sut.GetValueAsync(Layout);
        await sut.GetValueAsync(Layout);

        await _readRepo.Received(1).ListAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Uses_the_platform_layer_when_there_is_no_current_tenant()
    {
        _tenant.TenantId.Returns((Guid?)null);

        (await Sut().GetValueAsync(Layout)).ShouldBe("letter");
        await _readRepo.DidNotReceive().ListAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
