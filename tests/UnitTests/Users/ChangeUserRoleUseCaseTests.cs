using ErrorOr;
using NSubstitute;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Users.ChangeUserRole;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Tenants;
using NovaFE.Domain.Users;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Users;

public class ChangeUserRoleUseCaseTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private readonly IPlatformUserRepository _users = Substitute.For<IPlatformUserRepository>();
    private readonly ITenantMemberRepository _tenantMembers = Substitute.For<ITenantMemberRepository>();

    private ChangeUserRoleUseCase Sut() =>
        new(LoggerFactory, new ChangeUserRoleCommandValidator(), _users, _tenantMembers);

    private static PlatformUser TenantUser() =>
        PlatformUser.CreateTenantUser("emisor@cliente.do", TenantId, PlatformRole.Emisor).Value;

    [Fact]
    public async Task Changes_the_role_and_persists()
    {
        var user = TenantUser();
        var member = TenantMember.Create(TenantId, user.Id, PlatformRole.Emisor).Value;
        _users.GetAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _tenantMembers.GetAsync(TenantId, user.Id, Arg.Any<CancellationToken>()).Returns(member);

        var result = await Sut().Execute(new ChangeUserRoleCommand(user.Id, TenantId, "consultor"));

        result.IsError.ShouldBeFalse();
        result.Value.Role.ShouldBe("consultor");
        member.Role.ShouldBe(PlatformRole.Consultor);
        // El rol vestigial de PlatformUser no se toca: un usuario multi-tenant
        // tiene un rol por tenant, no uno global.
        user.Role.ShouldBe(PlatformRole.Emisor);
        await _tenantMembers.Received(1).UpdateAsync(member, Arg.Any<CancellationToken>());
        await _users.DidNotReceive().UpdateAsync(Arg.Any<PlatformUser>(), Arg.Any<CancellationToken>());
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
