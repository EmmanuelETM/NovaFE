using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.IssueEcf;
using NovaFE.Application.Ecf.ValidateEcf;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Ecf;

public class ValidateEcfUseCaseTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ITenantRepository _tenants = Substitute.For<ITenantRepository>();
    private readonly IEmitterProfileRepository _profiles = Substitute.For<IEmitterProfileRepository>();

    public ValidateEcfUseCaseTests()
    {
        _tenant.TenantId.Returns(TenantId);
        _tenants.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(Tenant.Register(Rnc.FromStorage("132786262"), "AlMax Solutions EIRL", "AlMax"));
        _profiles.GetByTenantAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(EmitterProfile.Create(TenantId, "Av. 27 de Febrero 100", "010100", "01",
                ["809-555-0100"], "f@almax.do", "Comercio", DgiiEnvironment.Test).Value);
    }

    private ValidateEcfUseCase Sut() => new(
        LoggerFactory, new IssueEcfCommandValidator(Clock), _tenant, _tenants, _profiles, Clock);

    private static IssueEcfCommand Command(int type = 31) => new()
    {
        Type = type,
        IncomeType = "01",
        IssueDate = new DateOnly(2026, 1, 10),
        Buyer = new EcfBuyerPayload(Name: "Cliente SRL", Rnc: "131880681"),
        Payment = new EcfPaymentPayload("credit", new DateOnly(2026, 2, 10),
            [new EcfPaymentMethodPayload("check_transfer", 2360m)]),
        Lines = [new EcfLinePayload("Servicio", Kind: "service", Quantity: 1, UnitPrice: 2000m, ItbisRate: 1, UnitOfMeasure: "43")],
    };

    [Fact]
    public async Task A_valid_payload_returns_the_calculated_totals_without_a_real_sequence()
    {
        var result = await Sut().Execute(Command());

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
        var dto = result.Value;
        dto.Type.ShouldBe(31);
        dto.MontoTotal.ShouldBe(2360m);
        dto.TotalItbis.ShouldBe(360m);
        dto.SampleEncf.ShouldStartWith("E31");
        dto.SequenceExpiresOnEstimate.ShouldBe(new DateOnly(2027, 12, 31));

        await _tenants.Received(1).GetByIdAsync(TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_type_without_sequence_expiry_has_no_estimate()
    {
        var command = Command(type: 32) with
        {
            Buyer = new EcfBuyerPayload(Name: "Consumidor Final"),
            Payment = new EcfPaymentPayload("cash", null, [new EcfPaymentMethodPayload("cash", 590m)]),
            Lines = [new EcfLinePayload("Almuerzo", Kind: "good", Quantity: 1, UnitPrice: 500m, ItbisRate: 1, UnitOfMeasure: "43")],
        };

        var result = await Sut().Execute(command);

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
        result.Value.SequenceExpiresOnEstimate.ShouldBeNull();
    }

    [Fact]
    public async Task Propagates_the_structural_matrix_error_without_touching_any_sequence()
    {
        // Tipo 31 exige identificar al comprador con montos altos — sin RNC ni
        // cédula, la matriz de EcfDocument.Create lo rechaza.
        var command = Command() with { Buyer = null };

        var result = await Sut().Execute(command);

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task Fails_when_the_emitter_profile_is_not_configured()
    {
        _profiles.GetByTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns((EmitterProfile?)null);

        var result = await Sut().Execute(Command());

        result.FirstError.Code.ShouldBe("EmitterProfile.NotConfigured");
    }

    [Fact]
    public async Task Rejects_a_malformed_payload_before_touching_the_tenant()
    {
        var result = await Sut().Execute(Command() with { Type = 99 });

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorOr.ErrorType.Validation);
        await _tenants.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
