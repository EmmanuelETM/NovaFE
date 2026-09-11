using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Application.Settings.ResetTenantSetting;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Settings;

public class ResetTenantSettingUseCaseTests : UseCaseTestBase
{
    private const string LayoutKey = "representation.default_layout";

    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ITenantSettingRepository _repository = Substitute.For<ITenantSettingRepository>();
    private readonly ITenantSettingChangeLog _changeLog = Substitute.For<ITenantSettingChangeLog>();
    private readonly IPlatformSettingRepository _platform = Substitute.For<IPlatformSettingRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public ResetTenantSettingUseCaseTests()
    {
        _tenant.TenantId.Returns(Guid.NewGuid());
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((Func<CancellationToken, Task>)call[0]).Invoke(call.Arg<CancellationToken>()));
    }

    private ResetTenantSettingUseCase Sut() => new(
        LoggerFactory, _tenant, _repository, _changeLog, _platform, _uow);

    [Fact]
    public async Task Removes_the_override_and_logs_the_reset()
    {
        _repository.GetAsync(LayoutKey, Arg.Any<CancellationToken>())
            .Returns(new TenantSettingRecord(LayoutKey, "", "pos", Clock.GetUtcNow(), "user@x.com"));

        var result = await Sut().Execute(new ResetTenantSettingCommand(LayoutKey));

        result.IsError.ShouldBeFalse();
        result.Value.ResolvedFrom.ShouldBe("default");
        result.Value.EffectiveValue.ShouldBe("letter");

        await _repository.Received(1).RemoveAsync(LayoutKey, Arg.Any<CancellationToken>());
        await _changeLog.Received(1).RecordAsync(LayoutKey, "pos", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Falls_back_to_the_platform_layer_when_there_is_a_platform_override()
    {
        _repository.GetAsync(LayoutKey, Arg.Any<CancellationToken>())
            .Returns(new TenantSettingRecord(LayoutKey, "", "pos", Clock.GetUtcNow(), "user@x.com"));
        _platform.GetAsync(LayoutKey, Arg.Any<CancellationToken>())
            .Returns(new PlatformSettingRecord(LayoutKey, "", "letter", Clock.GetUtcNow(), "op"));

        var result = await Sut().Execute(new ResetTenantSettingCommand(LayoutKey));

        result.Value.ResolvedFrom.ShouldBe("platform");
    }

    [Fact]
    public async Task Is_a_no_op_when_there_was_no_override()
    {
        _repository.GetAsync(LayoutKey, Arg.Any<CancellationToken>()).Returns((TenantSettingRecord?)null);

        var result = await Sut().Execute(new ResetTenantSettingCommand(LayoutKey));

        result.IsError.ShouldBeFalse();
        await _repository.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _changeLog.DidNotReceive().RecordAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
