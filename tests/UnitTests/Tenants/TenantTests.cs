using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;

namespace NovaFE.UnitTests.Tenants;

public class TenantTests
{
    private static Rnc SomeRnc => Rnc.Create("101672919").Value;

    [Fact]
    public void Register_starts_active_and_trims_names()
    {
        var tenant = Tenant.Register(SomeRnc, "  Acme SRL  ", "  Acme  ");

        tenant.Id.ShouldNotBe(Guid.Empty);
        tenant.Rnc.Value.ShouldBe("101672919");
        tenant.LegalName.ShouldBe("Acme SRL");
        tenant.TradeName.ShouldBe("Acme");
        tenant.Status.ShouldBe(TenantStatus.Active);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_maps_blank_trade_name_to_null(string? tradeName)
    {
        var tenant = Tenant.Register(SomeRnc, "Acme", tradeName);

        tenant.TradeName.ShouldBeNull();
    }

    [Fact]
    public void Suspend_then_activate_moves_status()
    {
        var tenant = Tenant.Register(SomeRnc, "Acme", null);

        tenant.Suspend().IsError.ShouldBeFalse();
        tenant.Status.ShouldBe(TenantStatus.Suspended);

        tenant.Activate().IsError.ShouldBeFalse();
        tenant.Status.ShouldBe(TenantStatus.Active);
    }

    [Fact]
    public void Suspend_twice_is_a_conflict()
    {
        var tenant = Tenant.Register(SomeRnc, "Acme", null);

        tenant.Suspend();
        var result = tenant.Suspend();

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.AlreadySuspended");
    }

    [Fact]
    public void Activate_when_not_suspended_is_a_conflict()
    {
        var tenant = Tenant.Register(SomeRnc, "Acme", null);

        var result = tenant.Activate();

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.NotSuspended");
    }
}
