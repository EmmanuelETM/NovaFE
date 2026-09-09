using NovaFE.Application.Users.ProvisionTenantUser;

namespace NovaFE.UnitTests.Users;

public class ProvisionTenantUserCommandValidatorTests
{
    private readonly ProvisionTenantUserCommandValidator _validator = new();

    private static ProvisionTenantUserCommand Valid() =>
        new(Guid.CreateVersion7(), "empleado@cliente.do", "emisor");

    [Fact]
    public void Accepts_a_well_formed_command()
        => _validator.Validate(Valid()).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("admin_tenant")]
    [InlineData("emisor")]
    [InlineData("consultor")]
    [InlineData("EMISOR")]
    public void Accepts_every_tenant_role(string role)
        => _validator.Validate(Valid() with { Role = role }).IsValid.ShouldBeTrue();

    [Fact]
    public void Rejects_admin_sistema()
        => _validator.Validate(Valid() with { Role = "admin_sistema" }).IsValid.ShouldBeFalse();

    [Fact]
    public void Rejects_an_unknown_role()
        => _validator.Validate(Valid() with { Role = "supervisor" }).IsValid.ShouldBeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Rejects_a_blank_role(string role)
        => _validator.Validate(Valid() with { Role = role }).IsValid.ShouldBeFalse();

    [Fact]
    public void Rejects_a_blank_email()
        => _validator.Validate(Valid() with { Email = "" }).IsValid.ShouldBeFalse();

    [Fact]
    public void Rejects_a_blank_tenant()
        => _validator.Validate(Valid() with { TenantId = Guid.Empty }).IsValid.ShouldBeFalse();
}
