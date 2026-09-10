using ErrorOr;
using NSubstitute;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Application.Users.ReinstateUser;
using NovaFE.Domain.Users;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Users;

public class ReinstateUserUseCaseTests : UseCaseTestBase
{
    private readonly IPlatformUserRepository _users = Substitute.For<IPlatformUserRepository>();

    private ReinstateUserUseCase Sut() => new(LoggerFactory, _users);

    [Fact]
    public async Task Reinstates_a_revoked_operator()
    {
        var user = PlatformUser.CreateOperator("ops@nemus.do").Value;
        user.Revoke(Clock.GetUtcNow());
        _users.GetAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Sut().Execute(new ReinstateUserCommand(user.Id, TenantScope: null));

        result.IsError.ShouldBeFalse();
        user.IsUsable.ShouldBeTrue();
        await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Conflicts_when_the_user_is_not_revoked()
    {
        var user = PlatformUser.CreateOperator("ops@nemus.do").Value;
        _users.GetAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Sut().Execute(new ReinstateUserCommand(user.Id, TenantScope: null));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task Reports_not_found_when_the_scope_does_not_match()
    {
        var user = PlatformUser.CreateOperator("ops@nemus.do").Value;
        user.Revoke(Clock.GetUtcNow());
        _users.GetAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        // el usuario es operador (TenantId null) pero se pide con un tenant scope
        var result = await Sut().Execute(new ReinstateUserCommand(user.Id, Guid.CreateVersion7()));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }
}
