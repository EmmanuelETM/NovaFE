using ErrorOr;
using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Application.Settings.ResetPlatformSetting;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Settings;

public class ResetPlatformSettingUseCaseTests : UseCaseTestBase
{
    private readonly IPlatformSettingRepository _repository = Substitute.For<IPlatformSettingRepository>();
    private readonly IPlatformSettingChangeLog _changeLog = Substitute.For<IPlatformSettingChangeLog>();
    private readonly ISettingsGenerationStore _generation = Substitute.For<ISettingsGenerationStore>();
    private readonly ISettingsCacheInvalidator _invalidator = Substitute.For<ISettingsCacheInvalidator>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public ResetPlatformSettingUseCaseTests()
    {
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((Func<CancellationToken, Task>)call[0]).Invoke(call.Arg<CancellationToken>()));
    }

    private ResetPlatformSettingUseCase Sut() => new(
        LoggerFactory, _repository, _changeLog, _generation, _invalidator, _uow);

    [Fact]
    public async Task Removes_the_override_and_logs_new_value_null()
    {
        _repository.GetAsync("platform.maintenance_mode", Arg.Any<CancellationToken>())
            .Returns(new PlatformSettingRecord("platform.maintenance_mode", "", "true", Clock.GetUtcNow(), "op"));

        var result = await Sut().Execute(new ResetPlatformSettingCommand("platform.maintenance_mode"));

        result.IsError.ShouldBeFalse();
        result.Value.ResolvedFrom.ShouldBe("default");
        result.Value.IsOverridden.ShouldBeFalse();

        await _repository.Received(1).RemoveAsync("platform.maintenance_mode", Arg.Any<CancellationToken>());
        await _changeLog.Received(1).RecordAsync("platform.maintenance_mode", "true", null, Arg.Any<CancellationToken>());
        await _generation.Received(1).BumpAsync(Arg.Any<CancellationToken>());
        await _invalidator.Received(1).RefreshNowAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Is_idempotent_when_there_is_no_override()
    {
        _repository.GetAsync("platform.maintenance_mode", Arg.Any<CancellationToken>())
            .Returns((PlatformSettingRecord?)null);

        var result = await Sut().Execute(new ResetPlatformSettingCommand("platform.maintenance_mode"));

        result.IsError.ShouldBeFalse();
        await _generation.DidNotReceive().BumpAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_an_unknown_key()
    {
        var result = await Sut().Execute(new ResetPlatformSettingCommand("nope.not.here"));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }
}
