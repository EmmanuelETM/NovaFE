using ErrorOr;
using NSubstitute;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Tenants.RegisterTenant;
using NovaFE.Domain.Tenants;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Tenants;

public class RegisterTenantUseCaseTests : UseCaseTestBase
{
    private readonly ITenantRepository _tenants = Substitute.For<ITenantRepository>();

    private RegisterTenantUseCase Sut() =>
        new(LoggerFactory, new RegisterTenantCommandValidator(), _tenants);

    [Fact]
    public async Task Registers_a_new_tenant()
    {
        _tenants.RncExistsAsync("101672919", Arg.Any<CancellationToken>()).Returns(false);

        var result = await Sut().Execute(
            new RegisterTenantCommand("101672919", "Acme SRL", null, "Developer"));

        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBe(Guid.Empty);
        await _tenants.Received(1).AddAsync(
            Arg.Is<Tenant>(t => t.Rnc.Value == "101672919" && t.LegalName == "Acme SRL"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_duplicate_rnc_with_conflict()
    {
        _tenants.RncExistsAsync("101672919", Arg.Any<CancellationToken>()).Returns(true);

        var result = await Sut().Execute(
            new RegisterTenantCommand("101672919", "Acme SRL", null, "Developer"));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Tenant.RncAlreadyRegistered");
        await _tenants.DidNotReceive().AddAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("123", "Acme", "Developer")]     // bad RNC
    [InlineData("101672919", "", "Developer")]   // missing legal name
    [InlineData("101672919", "Acme", "Premium")] // unknown plan
    public async Task Rejects_invalid_input_with_validation_errors(string rnc, string legalName, string plan)
    {
        var result = await Sut().Execute(new RegisterTenantCommand(rnc, legalName, null, plan));

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);
        await _tenants.DidNotReceive().AddAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
    }
}
