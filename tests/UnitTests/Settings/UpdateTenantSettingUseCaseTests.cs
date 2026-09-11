using ErrorOr;
using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Application.Settings.UpdateTenantSetting;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Settings;

public class UpdateTenantSettingUseCaseTests : UseCaseTestBase
{
    private const string LayoutKey = "representation.default_layout";

    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ITenantSettingRepository _repository = Substitute.For<ITenantSettingRepository>();
    private readonly ITenantSettingChangeLog _changeLog = Substitute.For<ITenantSettingChangeLog>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public UpdateTenantSettingUseCaseTests()
    {
        _tenant.TenantId.Returns(Guid.NewGuid());
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((Func<CancellationToken, Task>)call[0]).Invoke(call.Arg<CancellationToken>()));
    }

    private UpdateTenantSettingUseCase Sut() => new(
        LoggerFactory, new UpdateTenantSettingCommandValidator(),
        _tenant, _repository, _changeLog, _uow);

    [Fact]
    public async Task Persists_the_canonical_value_and_logs_the_change()
    {
        _repository.GetAsync(LayoutKey, Arg.Any<CancellationToken>())
            .Returns((TenantSettingRecord?)null,
                     new TenantSettingRecord(LayoutKey, "", "pos", Clock.GetUtcNow(), "user@x.com"));

        var result = await Sut().Execute(new UpdateTenantSettingCommand(LayoutKey, "POS"));

        result.IsError.ShouldBeFalse();
        result.Value.EffectiveValue.ShouldBe("pos");
        result.Value.ResolvedFrom.ShouldBe("tenant");

        await _repository.Received(1).UpsertAsync(LayoutKey, "pos", Arg.Any<CancellationToken>());
        await _changeLog.Received(1).RecordAsync(LayoutKey, null, "pos", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_an_unknown_key_with_not_found()
    {
        var result = await Sut().Execute(new UpdateTenantSettingCommand("nope.not.here", "pos"));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_platform_scoped_key_as_unknown()
    {
        var result = await Sut().Execute(new UpdateTenantSettingCommand("platform.maintenance_mode", "true"));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Rejects_an_invalid_value_with_validation()
    {
        var result = await Sut().Execute(new UpdateTenantSettingCommand(LayoutKey, "a4"));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Requires_a_resolved_tenant()
    {
        _tenant.TenantId.Returns((Guid?)null);

        var result = await Sut().Execute(new UpdateTenantSettingCommand(LayoutKey, "pos"));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Auth.TenantNotResolved");
    }
}
