using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;

namespace NovaFE.UnitTests.Tenants;

public class TenantTests
{
    private static Rnc SomeRnc => Rnc.Create("101672919").Value;

    [Fact]
    public void Register_starts_active_and_trims_names()
    {
        var tenant = Tenant.Register(SomeRnc, "  Acme SRL  ", "  Acme  ", TenantPlan.Developer);

        tenant.Id.ShouldNotBe(Guid.Empty);
        tenant.Rnc.Value.ShouldBe("101672919");
        tenant.LegalName.ShouldBe("Acme SRL");
        tenant.TradeName.ShouldBe("Acme");
        tenant.Plan.ShouldBe(TenantPlan.Developer);
        tenant.Status.ShouldBe(TenantStatus.Active);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_maps_blank_trade_name_to_null(string? tradeName)
    {
        var tenant = Tenant.Register(SomeRnc, "Acme", tradeName, TenantPlan.Business);

        tenant.TradeName.ShouldBeNull();
    }

    [Fact]
    public void Suspend_then_activate_moves_status()
    {
        var tenant = Tenant.Register(SomeRnc, "Acme", null, TenantPlan.Developer);

        tenant.Suspend();
        tenant.Status.ShouldBe(TenantStatus.Suspended);

        tenant.Activate();
        tenant.Status.ShouldBe(TenantStatus.Active);
    }
}
