using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NovaFE.Application.Common;
using NovaFE.Application.Contingency;
using NovaFE.Application.Ops.Contracts;
using NovaFE.Application.Ops.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Application.Settings.UpdatePlatformSetting;
using NovaFE.Domain.Settings;
using NovaFE.Domain.Webhooks;

namespace NovaFE.UnitTests.Contingency;

public class ContingencyMonitorTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 21, 14, 30, 0, TimeSpan.Zero);
    private static readonly TimeSpan Threshold = TimeSpan.FromMinutes(5);

    private readonly IOpsStatusReadRepository _outboxStatus = Substitute.For<IOpsStatusReadRepository>();
    private readonly ISettingsReader _settingsReader = Substitute.For<ISettingsReader>();
    private readonly IUseCase<UpdatePlatformSettingCommand, PlatformSettingDto> _updateSetting =
        Substitute.For<IUseCase<UpdatePlatformSettingCommand, PlatformSettingDto>>();
    private readonly FakeTimeProvider _clock = new(Now);

    private ContingencyMonitor Sut() => new(
        _outboxStatus, _settingsReader, _updateSetting, _clock, NullLogger<ContingencyMonitor>.Instance);

    private void StubOldestPendingAge(TimeSpan? age) =>
        _outboxStatus.GetEcfSubmissionOutboxStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new OutboxStatusDto(0, 0, 0, age is null ? null : Now - age));

    private static PlatformSettingDto Dto(string value) => new(
        SettingDefinitions.ContingencyMode.Key, "Plataforma", "Modo contingencia", null, "boolean", null, null,
        null, null, null, true, true, false, "false", value, true, "platform", Now, null);

    [Fact]
    public async Task Does_nothing_when_the_outbox_is_healthy_and_contingency_is_already_off()
    {
        StubOldestPendingAge(null);
        _settingsReader.GetValue(SettingDefinitions.ContingencyMode).Returns(false);

        var transition = await Sut().CheckAndTransitionAsync(Threshold);

        transition.ShouldBeNull();
        await _updateSetting.DidNotReceive().Execute(Arg.Any<UpdatePlatformSettingCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activates_when_the_oldest_pending_row_crosses_the_threshold()
    {
        StubOldestPendingAge(TimeSpan.FromMinutes(6));
        _settingsReader.GetValue(SettingDefinitions.ContingencyMode).Returns(false);
        _updateSetting.Execute(Arg.Any<UpdatePlatformSettingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Dto("true"));

        var transition = await Sut().CheckAndTransitionAsync(Threshold);

        transition.ShouldNotBeNull();
        transition.EventType.ShouldBe(WebhookEventType.ContingencyActivated);
        transition.Payload.Status.ShouldBe("active");
        await _updateSetting.Received(1).Execute(
            Arg.Is<UpdatePlatformSettingCommand>(c =>
                c.Key == SettingDefinitions.ContingencyMode.Key && c.Value == "true" && c.Confirmed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_activate_on_a_blip_below_the_threshold()
    {
        StubOldestPendingAge(TimeSpan.FromMinutes(2));
        _settingsReader.GetValue(SettingDefinitions.ContingencyMode).Returns(false);

        var transition = await Sut().CheckAndTransitionAsync(Threshold);

        transition.ShouldBeNull();
        await _updateSetting.DidNotReceive().Execute(Arg.Any<UpdatePlatformSettingCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deactivates_once_the_outbox_catches_up()
    {
        StubOldestPendingAge(null);
        _settingsReader.GetValue(SettingDefinitions.ContingencyMode).Returns(true);
        _updateSetting.Execute(Arg.Any<UpdatePlatformSettingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Dto("false"));

        var transition = await Sut().CheckAndTransitionAsync(Threshold);

        transition.ShouldNotBeNull();
        transition.EventType.ShouldBe(WebhookEventType.ContingencyDeactivated);
        transition.Payload.Status.ShouldBe("inactive");
        await _updateSetting.Received(1).Execute(
            Arg.Is<UpdatePlatformSettingCommand>(c => c.Value == "false" && c.Confirmed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stays_active_while_the_outbox_is_still_stuck()
    {
        StubOldestPendingAge(TimeSpan.FromMinutes(10));
        _settingsReader.GetValue(SettingDefinitions.ContingencyMode).Returns(true);

        var transition = await Sut().CheckAndTransitionAsync(Threshold);

        transition.ShouldBeNull();
        await _updateSetting.DidNotReceive().Execute(Arg.Any<UpdatePlatformSettingCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_null_without_throwing_when_the_write_fails()
    {
        StubOldestPendingAge(TimeSpan.FromMinutes(6));
        _settingsReader.GetValue(SettingDefinitions.ContingencyMode).Returns(false);
        _updateSetting.Execute(Arg.Any<UpdatePlatformSettingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.Failure("boom"));

        var transition = await Sut().CheckAndTransitionAsync(Threshold);

        transition.ShouldBeNull();
    }
}
