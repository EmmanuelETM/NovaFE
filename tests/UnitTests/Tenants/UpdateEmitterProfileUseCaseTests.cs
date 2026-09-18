using ErrorOr;
using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Tenants.UpdateEmitterProfile;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Tenants;

public class UpdateEmitterProfileUseCaseTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IEmitterProfileRepository _profiles = Substitute.For<IEmitterProfileRepository>();

    public UpdateEmitterProfileUseCaseTests()
    {
        _currentTenant.TenantId.Returns(TenantId);
        _currentTenant.HasValue.Returns(true);
    }

    private UpdateEmitterProfileUseCase Sut() =>
        new(LoggerFactory, new UpdateEmitterProfileCommandValidator(), _currentTenant, _profiles);

    private static UpdateEmitterProfileCommand Command() => new(
        "Av. 27 de Febrero 100", "010100", "010000",
        ["809-555-0100"], "facturacion@acme.do", "Comercio");

    [Fact]
    public async Task Fails_when_the_tenant_has_no_profile_yet()
    {
        _profiles.GetByTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns((EmitterProfile?)null);

        var result = await Sut().Execute(Command());

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("EmitterProfile.NotConfigured");
    }

    [Fact]
    public async Task Updates_the_fields_and_keeps_the_current_environment()
    {
        var existing = EmitterProfile.Create(
            TenantId, "Old", null, null, null, null, null, DgiiEnvironment.Production).Value;
        _profiles.GetByTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await Sut().Execute(Command());

        result.IsError.ShouldBeFalse();
        result.Value.Address.ShouldBe("Av. 27 de Febrero 100");
        result.Value.DefaultEnvironment.ShouldBe("Production");
        existing.DefaultEnvironment.ShouldBe(DgiiEnvironment.Production);
        await _profiles.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_blank_address()
    {
        var existing = EmitterProfile.Create(
            TenantId, "Old", null, null, null, null, null, DgiiEnvironment.Test).Value;
        _profiles.GetByTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await Sut().Execute(Command() with { Address = "" });

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);
    }

    [Fact]
    public async Task Rejects_a_province_code_that_is_not_in_the_dgii_catalog()
    {
        var existing = EmitterProfile.Create(
            TenantId, "Old", null, null, null, null, null, DgiiEnvironment.Test).Value;
        _profiles.GetByTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await Sut().Execute(Command() with { Province = "999999" });

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);
        await _profiles.DidNotReceive().UpdateAsync(Arg.Any<EmitterProfile>(), Arg.Any<CancellationToken>());
    }
}
