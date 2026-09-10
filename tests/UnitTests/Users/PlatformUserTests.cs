using ErrorOr;
using NovaFE.Domain.Users;

namespace NovaFE.UnitTests.Users;

public class PlatformUserTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateOperator_has_admin_sistema_and_no_tenant()
    {
        var user = PlatformUser.CreateOperator("ops@nemus.do").Value;

        user.Role.ShouldBe(PlatformRole.AdminSistema);
        user.TenantId.ShouldBeNull();
        user.AuthUserId.ShouldBeNull();
        user.IsUsable.ShouldBeTrue();
    }

    [Fact]
    public void CreateTenantUser_binds_tenant_and_role()
    {
        var user = PlatformUser.CreateTenantUser("emisor@cliente.do", TenantId, PlatformRole.Emisor).Value;

        user.TenantId.ShouldBe(TenantId);
        user.Role.ShouldBe(PlatformRole.Emisor);
    }

    [Fact]
    public void CreateTenantUser_rejects_admin_sistema()
    {
        var result = PlatformUser.CreateTenantUser("x@cliente.do", TenantId, PlatformRole.AdminSistema);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("PlatformUser.InvalidRoleForTenant");
    }

    [Fact]
    public void CreateTenantUser_rejects_a_blank_tenant()
    {
        var result = PlatformUser.CreateTenantUser("x@cliente.do", Guid.Empty, PlatformRole.Consultor);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("PlatformUser.TenantRequired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notanemail")]
    [InlineData("no@domain")]
    [InlineData("two@@at.com")]
    [InlineData("has space@x.com")]
    [InlineData("@x.com")]
    [InlineData("ends@with.at@")]
    public void CreateOperator_rejects_a_malformed_email(string email)
    {
        var result = PlatformUser.CreateOperator(email);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void The_email_is_normalized_to_lowercase_and_trimmed()
    {
        var user = PlatformUser.CreateOperator("  Ops@Nemus.DO  ").Value;

        user.Email.ShouldBe("ops@nemus.do");
    }

    [Fact]
    public void LinkAuthUser_sets_the_id_once_and_is_idempotent_for_the_same_id()
    {
        var user = PlatformUser.CreateOperator("ops@nemus.do").Value;

        user.LinkAuthUser("  auth-abc  ").IsError.ShouldBeFalse();
        user.AuthUserId.ShouldBe("auth-abc");

        user.LinkAuthUser("auth-abc").IsError.ShouldBeFalse();
    }

    [Fact]
    public void LinkAuthUser_rejects_relinking_to_a_different_id()
    {
        var user = PlatformUser.CreateOperator("ops@nemus.do").Value;
        user.LinkAuthUser("auth-abc");

        var result = user.LinkAuthUser("auth-xyz");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("PlatformUser.AuthUserAlreadyLinked");
    }

    [Fact]
    public void Revoking_makes_it_unusable_and_is_not_idempotent()
    {
        var user = PlatformUser.CreateOperator("ops@nemus.do").Value;

        user.Revoke(Now).IsError.ShouldBeFalse();
        user.RevokedAt.ShouldBe(Now);
        user.IsUsable.ShouldBeFalse();

        var second = user.Revoke(Now.AddMinutes(1));
        second.IsError.ShouldBeTrue();
        second.FirstError.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public void Reinstate_clears_the_revocation_and_is_not_idempotent()
    {
        var user = PlatformUser.CreateTenantUser("e@cliente.do", TenantId, PlatformRole.Emisor).Value;

        user.Reinstate().IsError.ShouldBeTrue();   // no estaba revocado

        user.Revoke(Now);
        user.Reinstate().IsError.ShouldBeFalse();
        user.RevokedAt.ShouldBeNull();
        user.IsUsable.ShouldBeTrue();

        var again = user.Reinstate();
        again.IsError.ShouldBeTrue();
        again.FirstError.Code.ShouldBe("PlatformUser.NotRevoked");
        again.FirstError.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public void ChangeRole_sets_a_tenant_role_and_rejects_admin_sistema()
    {
        var user = PlatformUser.CreateTenantUser("e@cliente.do", TenantId, PlatformRole.Emisor).Value;

        user.ChangeRole(PlatformRole.Consultor).IsError.ShouldBeFalse();
        user.Role.ShouldBe(PlatformRole.Consultor);

        // no-op con el mismo rol
        user.ChangeRole(PlatformRole.Consultor).IsError.ShouldBeFalse();

        var toOperator = user.ChangeRole(PlatformRole.AdminSistema);
        toOperator.IsError.ShouldBeTrue();
        toOperator.FirstError.Code.ShouldBe("PlatformUser.InvalidRoleForTenant");
        user.Role.ShouldBe(PlatformRole.Consultor);
    }

    [Fact]
    public void ChangeRole_is_allowed_on_a_revoked_user()
    {
        var user = PlatformUser.CreateTenantUser("e@cliente.do", TenantId, PlatformRole.Emisor).Value;
        user.Revoke(Now);

        user.ChangeRole(PlatformRole.AdminTenant).IsError.ShouldBeFalse();
        user.Role.ShouldBe(PlatformRole.AdminTenant);
    }

    [Fact]
    public void PlatformRole_IsTenantRole_excludes_only_admin_sistema()
    {
        PlatformRole.AdminSistema.IsTenantRole.ShouldBeFalse();
        PlatformRole.AdminTenant.IsTenantRole.ShouldBeTrue();
        PlatformRole.Emisor.IsTenantRole.ShouldBeTrue();
        PlatformRole.Consultor.IsTenantRole.ShouldBeTrue();
    }
}
