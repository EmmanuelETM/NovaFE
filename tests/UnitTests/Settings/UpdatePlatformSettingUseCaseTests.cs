using ErrorOr;
using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Application.Settings.UpdatePlatformSetting;
using NovaFE.Domain.Settings;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Settings;

public class UpdatePlatformSettingUseCaseTests : UseCaseTestBase
{
    private readonly IPlatformSettingRepository _repository = Substitute.For<IPlatformSettingRepository>();
    private readonly IPlatformSettingChangeLog _changeLog = Substitute.For<IPlatformSettingChangeLog>();
    private readonly ISettingsGenerationStore _generation = Substitute.For<ISettingsGenerationStore>();
    private readonly ISettingsCacheInvalidator _invalidator = Substitute.For<ISettingsCacheInvalidator>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public UpdatePlatformSettingUseCaseTests()
    {
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((Func<CancellationToken, Task>)call[0]).Invoke(call.Arg<CancellationToken>()));
    }

    private UpdatePlatformSettingUseCase Sut() => new(
        LoggerFactory, new UpdatePlatformSettingCommandValidator(),
        _repository, _changeLog, _generation, _invalidator, _uow);

    // Todos los settings del catálogo hoy son `Sensitive`, así que las pruebas de
    // operación normal confirman; el gate de confirmación tiene sus propias pruebas.
    private static UpdatePlatformSettingCommand Confirmed(string key, string value) =>
        new(key, value, Confirmed: true);

    [Fact]
    public async Task Persists_the_canonical_value_logs_the_change_bumps_and_refreshes()
    {
        _repository.GetAsync("platform.maintenance_mode", Arg.Any<CancellationToken>())
            .Returns((PlatformSettingRecord?)null,
                     new PlatformSettingRecord("platform.maintenance_mode", "", "true", Clock.GetUtcNow(), "op"));

        var result = await Sut().Execute(Confirmed("platform.maintenance_mode", "1"));

        result.IsError.ShouldBeFalse();
        result.Value.EffectiveValue.ShouldBe("true");
        result.Value.ResolvedFrom.ShouldBe("platform");

        await _repository.Received(1).UpsertAsync("platform.maintenance_mode", "true", Arg.Any<CancellationToken>());
        await _changeLog.Received(1).RecordAsync("platform.maintenance_mode", null, "true", Arg.Any<CancellationToken>());
        await _generation.Received(1).BumpAsync(Arg.Any<CancellationToken>());
        await _invalidator.Received(1).RefreshNowAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_an_unknown_key_with_not_found()
    {
        var result = await Sut().Execute(new UpdatePlatformSettingCommand("nope.not.here", "true"));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        await _generation.DidNotReceive().BumpAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_an_invalid_value_with_validation()
    {
        var result = await Sut().Execute(Confirmed("platform.maintenance_mode", "quizás"));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Is_a_no_op_when_the_value_did_not_change()
    {
        _repository.GetAsync("platform.maintenance_mode", Arg.Any<CancellationToken>())
            .Returns(new PlatformSettingRecord("platform.maintenance_mode", "", "true", Clock.GetUtcNow(), "op"));

        var result = await Sut().Execute(Confirmed("platform.maintenance_mode", "true"));

        result.IsError.ShouldBeFalse();
        await _generation.DidNotReceive().BumpAsync(Arg.Any<CancellationToken>());
        await _invalidator.DidNotReceive().RefreshNowAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_sensitive_setting_needs_confirmation()
    {
        var unconfirmed = await Sut().Execute(new UpdatePlatformSettingCommand("platform.maintenance_mode", "true"));

        unconfirmed.IsError.ShouldBeTrue();
        unconfirmed.FirstError.Code.ShouldBe("Setting.ConfirmationRequired");
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        _repository.GetAsync("platform.maintenance_mode", Arg.Any<CancellationToken>())
            .Returns((PlatformSettingRecord?)null,
                     new PlatformSettingRecord("platform.maintenance_mode", "", "true", Clock.GetUtcNow(), "op"));

        var confirmed = await Sut().Execute(Confirmed("platform.maintenance_mode", "true"));
        confirmed.IsError.ShouldBeFalse();
    }
}
