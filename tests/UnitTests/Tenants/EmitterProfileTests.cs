using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;

namespace NovaFE.UnitTests.Tenants;

public class EmitterProfileTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    [Fact]
    public void Create_normalizes_and_keeps_the_fiscal_data()
    {
        var result = EmitterProfile.Create(
            TenantId,
            "  Av. 27 de Febrero 100  ",
            municipality: " 010100 ",
            province: null,
            phones: ["  809-555-0100 ", "", "  "],
            email: " facturacion@acme.do ",
            economicActivity: " Comercio ",
            DgiiEnvironment.Test);

        result.IsError.ShouldBeFalse();
        var profile = result.Value;
        profile.TenantId.ShouldBe(TenantId);
        profile.Address.ShouldBe("Av. 27 de Febrero 100");
        profile.Municipality.ShouldBe("010100");
        profile.Province.ShouldBeNull();
        profile.Phones.ShouldBe(["809-555-0100"]);   // blancos descartados
        profile.Email.ShouldBe("facturacion@acme.do");
        profile.EconomicActivity.ShouldBe("Comercio");
        profile.DefaultEnvironment.ShouldBe(DgiiEnvironment.Test);
    }

    [Fact]
    public void Create_rejects_a_blank_address()
    {
        var result = EmitterProfile.Create(
            TenantId, "   ", null, null, null, null, null, DgiiEnvironment.Test);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("EmitterProfile.AddressRequired");
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void Create_rejects_more_than_three_phones()
    {
        var result = EmitterProfile.Create(
            TenantId, "Calle 1", null, null,
            phones: ["1", "2", "3", "4"],
            null, null, DgiiEnvironment.Test);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("EmitterProfile.TooManyPhones");
    }

    [Fact]
    public void Update_replaces_every_field()
    {
        var profile = EmitterProfile.Create(
            TenantId, "Calle 1", "010100", "01", ["809-000-0000"], "a@a.do", "Old",
            DgiiEnvironment.Test).Value;

        var updated = profile.Update(
            "Calle 2", null, null, [], null, null, DgiiEnvironment.Production);

        updated.IsError.ShouldBeFalse();
        profile.Address.ShouldBe("Calle 2");
        profile.Municipality.ShouldBeNull();
        profile.Phones.ShouldBeEmpty();
        profile.Email.ShouldBeNull();
        profile.EconomicActivity.ShouldBeNull();
        profile.DefaultEnvironment.ShouldBe(DgiiEnvironment.Production);
    }

    [Fact]
    public void Update_rejects_a_blank_address_and_keeps_the_old_state()
    {
        var profile = EmitterProfile.Create(
            TenantId, "Calle 1", null, null, null, null, null, DgiiEnvironment.Test).Value;

        var updated = profile.Update("", null, null, null, null, null, DgiiEnvironment.Test);

        updated.IsError.ShouldBeTrue();
        profile.Address.ShouldBe("Calle 1");
    }
}
