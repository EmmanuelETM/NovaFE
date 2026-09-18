using ErrorOr;
using NSubstitute;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Application.Users.ReinstateUser;
using NovaFE.Domain.Users;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Users;

public class ReinstateUserUseCaseTests : UseCaseTestBase
{
    private readonly IPlatformUserRepository _users = Substitute.For<IPlatformUserRepository>();
    private readonly ITenantMemberRepository _tenantMembers = Substitute.For<ITenantMemberRepository>();

    private ReinstateUserUseCase Sut() => new(LoggerFactory, _users, _tenantMembers);

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

    [Fact]
    public async Task Reinstates_a_tenant_user_scoped_by_their_membership_not_by_the_vestigial_tenant_id()
    {
        var tenantId = Guid.CreateVersion7();
        var user = NovaFE.Domain.Users.PlatformUser.CreateTenantUser(
            "multi@cliente.do", Guid.CreateVersion7(), PlatformRole.Consultor).Value;
        user.Revoke(Clock.GetUtcNow());
        var member = NovaFE.Domain.Tenants.TenantMember.Create(tenantId, user.Id, PlatformRole.Consultor).Value;
        _users.GetAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _tenantMembers.GetAsync(tenantId, user.Id, Arg.Any<CancellationToken>()).Returns(member);

        // El tenant que se pide es distinto de PlatformUser.TenantId (vestigial,
        // del primer tenant al que se dio de alta) — igual debe alcanzar por
        // tener una fila de TenantMember ahí.
        var result = await Sut().Execute(new ReinstateUserCommand(user.Id, tenantId));

        result.IsError.ShouldBeFalse();
        user.IsUsable.ShouldBeTrue();
    }
}
