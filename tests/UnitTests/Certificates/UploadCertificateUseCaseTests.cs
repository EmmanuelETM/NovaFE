using NSubstitute;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Certificates.UploadCertificate;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Certificates;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Certificates;

public class UploadCertificateUseCaseTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string TenantRnc = "130862346";

    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ITenantRepository _tenants = Substitute.For<ITenantRepository>();
    private readonly ICertificateRepository _certificates = Substitute.For<ICertificateRepository>();
    private readonly ICertificateVault _vault = Substitute.For<ICertificateVault>();

    public UploadCertificateUseCaseTests()
    {
        _tenant.TenantId.Returns(TenantId);
        _tenant.HasValue.Returns(true);
        _tenants.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(Tenant.Register(Rnc.Create(TenantRnc).Value, "Acme SRL", null, TenantPlan.Business));
        _vault.StoreAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("vault-ref-1");
        _certificates.HasActiveCertificateAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns(false);
    }

    private UploadCertificateUseCase Sut() => new(
        LoggerFactory, new UploadCertificateCommandValidator(), Clock, _tenant, _tenants, _certificates, _vault);

    private static UploadCertificateCommand Command(string? holderRnc = null, string environment = "Test")
        => new(TestPkcs12.Generate(holderIdentifier: holderRnc ?? TenantRnc), TestPkcs12.DefaultPassword, environment);

    [Fact]
    public async Task Stores_the_pkcs12_and_registers_the_certificate()
    {
        var result = await Sut().Execute(Command());

        result.IsError.ShouldBeFalse();
        await _vault.Received(1).StoreAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _certificates.Received(1).AddAsync(Arg.Any<Certificate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deletes_the_stored_secret_when_the_certificate_is_rejected()
    {
        var result = await Sut().Execute(Command(holderRnc: "101999999"));

        result.FirstError.Code.ShouldBe("Certificate.RncMismatch");
        await _vault.Received(1).DeleteAsync("vault-ref-1", Arg.Any<CancellationToken>());
        await _certificates.DidNotReceive().AddAsync(Arg.Any<Certificate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_second_active_certificate_for_the_same_environment()
    {
        _certificates.HasActiveCertificateAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await Sut().Execute(Command());

        result.FirstError.Code.ShouldBe("Certificate.EnvironmentHasActiveCertificate");
        await _vault.DidNotReceive().StoreAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_when_the_request_has_no_tenant()
    {
        _tenant.TenantId.Returns((Guid?)null);
        _tenant.HasValue.Returns(false);

        var result = await Sut().Execute(Command());

        result.FirstError.Code.ShouldBe("Auth.TenantNotResolved");
    }

    [Fact]
    public async Task Rejects_an_unknown_environment_via_the_validator()
    {
        var result = await Sut().Execute(Command(environment: "Staging"));

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorOr.ErrorType.Validation);
    }
}
