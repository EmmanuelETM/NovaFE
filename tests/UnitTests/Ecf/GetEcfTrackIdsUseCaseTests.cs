using ErrorOr;
using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Dgii.Contracts;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Application.Ecf.GetEcfTrackIds;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Dgii;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Tenants;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Ecf;

public class GetEcfTrackIdsUseCaseTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IEcfRepository _ecf = Substitute.For<IEcfRepository>();
    private readonly ITenantRepository _tenants = Substitute.For<ITenantRepository>();
    private readonly IDgiiTokenProvider _tokenProvider = Substitute.For<IDgiiTokenProvider>();
    private readonly IDgiiQueryClient _queryClient = Substitute.For<IDgiiQueryClient>();

    public GetEcfTrackIdsUseCaseTests()
    {
        _currentTenant.TenantId.Returns(TenantId);
        _tenants.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(Tenant.Register(Rnc.FromStorage("132786262"), "AlMax Solutions EIRL", "AlMax"));
        _tokenProvider.GetTokenAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticationToken("bearer-xyz", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)));
    }

    private GetEcfTrackIdsUseCase Sut() => new(
        LoggerFactory, _currentTenant, _ecf, _tenants, _tokenProvider, _queryClient);

    private static IssuedEcf Signed()
    {
        var document = EcfTestData.CreditoFiscal();
        return IssuedEcf.FromSigned(
            document,
            new SignedEcf(EcfTestData.SignedAt, "<ECF/>", null, "aB3xZ9KkLlMm", "aB3xZ9", new string('a', 64),
                "https://ecf.dgii.gov.do/testecf/consultatimbre?x=1"),
            DgiiEnvironment.Test);
    }

    [Fact]
    public async Task Not_found_when_the_comprobante_does_not_belong_to_the_tenant()
    {
        _ecf.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((IssuedEcf?)null);

        var result = await Sut().Execute(new GetEcfTrackIdsQuery(Guid.NewGuid()));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Ecf.NotFound");
        await _queryClient.DidNotReceive().GetTrackIdsAsync(
            Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Queries_the_dgii_with_the_tenant_rnc_and_the_comprobante_encf()
    {
        var ecf = Signed();
        _ecf.GetByIdAsync(ecf.Id, Arg.Any<CancellationToken>()).Returns(ecf);
        _queryClient.GetTrackIdsAsync(
                DgiiEnvironment.Test, "bearer-xyz", "132786262", ecf.Encf.Value, Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From((IReadOnlyList<DgiiTrackIdEntry>)[new DgiiTrackIdEntry("TRACK-1", "Aceptado", null)]));

        var result = await Sut().Execute(new GetEcfTrackIdsQuery(ecf.Id));

        result.IsError.ShouldBeFalse(result.IsError ? result.FirstError.Description : "");
        result.Value.ShouldHaveSingleItem().TrackId.ShouldBe("TRACK-1");
    }

    [Fact]
    public async Task Propagates_the_environment_guard_error_from_the_client()
    {
        var ecf = Signed();
        _ecf.GetByIdAsync(ecf.Id, Arg.Any<CancellationToken>()).Returns(ecf);
        _queryClient.GetTrackIdsAsync(
                Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DgiiQueryErrors.NotAvailableInEnvironment("consultatrackids", DgiiEnvironment.Cert));

        var result = await Sut().Execute(new GetEcfTrackIdsQuery(ecf.Id));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Dgii.Query.NotAvailableInEnvironment");
    }
}
