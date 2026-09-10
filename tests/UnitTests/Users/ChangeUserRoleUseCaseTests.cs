using ErrorOr;
using NSubstitute;
using NovaFE.Application.Users.ChangeUserRole;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Users;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Users;

public class ChangeUserRoleUseCaseTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private readonly IPlatformUserRepository _users = Substitute.For<IPlatformUserRepository>();

    private ChangeUserRoleUseCase Sut() =>
        new(LoggerFactory, new ChangeUserRoleCommandValidator(), _users);

    private static PlatformUser TenantUser() =>
        PlatformUser.CreateTenantUser("emisor@cliente.do", TenantId, PlatformRole.Emisor).Value;

    [Fact]
    public async Task Changes_the_role_and_persists()
    {
        var user = TenantUser();
        _users.GetAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Sut().Execute(new ChangeUserRoleCommand(user.Id, TenantId, "consultor"));

        result.IsError.ShouldBeFalse();
        result.Value.Role.ShouldBe("consultor");
        user.Role.ShouldBe(PlatformRole.Consultor);
        await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_an_unknown_or_operator_role_with_validation()
    {
        var result = await Sut().Execute(new ChangeUserRoleCommand(Guid.CreateVersion7(), TenantId, "admin_sistema"));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        await _users.DidNotReceive().UpdateAsync(Arg.Any<PlatformUser>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reports_not_found_for_a_user_of_another_tenant()
    {
        var user = TenantUser();
        _users.GetAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Sut().Execute(
            new ChangeUserRoleCommand(user.Id, Guid.CreateVersion7(), "consultor"));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }
}
