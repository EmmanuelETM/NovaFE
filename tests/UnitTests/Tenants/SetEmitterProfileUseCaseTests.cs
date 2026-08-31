using ErrorOr;
using NSubstitute;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Tenants.SetEmitterProfile;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Tenants;

public class SetEmitterProfileUseCaseTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private readonly ITenantReadRepository _tenants = Substitute.For<ITenantReadRepository>();
    private readonly IEmitterProfileRepository _profiles = Substitute.For<IEmitterProfileRepository>();

    public SetEmitterProfileUseCaseTests()
    {
        _tenants.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantDto(TenantId, "132786262", "Acme SRL", null, "Business", "Active", Clock.GetUtcNow()));
    }

    private SetEmitterProfileUseCase Sut() =>
        new(LoggerFactory, new SetEmitterProfileCommandValidator(), _tenants, _profiles);

    private static SetEmitterProfileCommand Command() => new(
        TenantId, "Av. 27 de Febrero 100", "010100", "01",
        ["809-555-0100"], "facturacion@acme.do", "Comercio", "TestEcf");

    [Fact]
    public async Task Creates_the_profile_when_the_tenant_has_none()
    {
        _profiles.GetByTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns((EmitterProfile?)null);

        var result = await Sut().Execute(Command());

        result.IsError.ShouldBeFalse();
        result.Value.Address.ShouldBe("Av. 27 de Febrero 100");
        result.Value.DefaultEnvironment.ShouldBe("TestEcf");
        await _profiles.Received(1).AddAsync(
            Arg.Is<EmitterProfile>(p => p.TenantId == TenantId && p.Address == "Av. 27 de Febrero 100"),
            Arg.Any<CancellationToken>());
        await _profiles.DidNotReceive().UpdateAsync(Arg.Any<EmitterProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Updates_the_existing_profile()
    {
        var existing = EmitterProfile.Create(
            TenantId, "Old", null, null, null, null, null, DgiiEnvironment.TestEcf).Value;
        _profiles.GetByTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await Sut().Execute(Command() with { Address = "New", DefaultEnvironment = "Production" });

        result.IsError.ShouldBeFalse();
        existing.Address.ShouldBe("New");
        existing.DefaultEnvironment.ShouldBe(DgiiEnvironment.Production);
        await _profiles.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await _profiles.DidNotReceive().AddAsync(Arg.Any<EmitterProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_when_the_tenant_does_not_exist()
    {
        _tenants.GetByIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns((TenantDto?)null);

        var result = await Sut().Execute(Command());

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.NotFound");
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Theory]
    [InlineData("", "TestEcf")]         // blank address
    [InlineData("Calle 1", "eCF")]      // unknown environment name
    public async Task Rejects_invalid_input(string address, string environment)
    {
        var result = await Sut().Execute(Command() with { Address = address, DefaultEnvironment = environment });

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);
    }
}
