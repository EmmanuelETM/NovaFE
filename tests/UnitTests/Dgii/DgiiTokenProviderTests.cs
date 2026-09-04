using ErrorOr;
using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Application.Signing.Contracts;
using NovaFE.Application.Signing.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Dgii;
using NovaFE.Infrastructure.Dgii;
using NovaFE.UnitTests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace NovaFE.UnitTests.Dgii;

public class DgiiTokenProviderTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DgiiEnvironment Env = DgiiEnvironment.Test;

    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly IDgiiTokenCache _cache = Substitute.For<IDgiiTokenCache>();
    private readonly IDgiiAuthClient _authClient = Substitute.For<IDgiiAuthClient>();
    private readonly ICertificateSigner _signer = Substitute.For<ICertificateSigner>();

    public DgiiTokenProviderTests()
    {
        _tenant.TenantId.Returns(TenantId);
        _authClient.GetSeedAsync(Env, Arg.Any<CancellationToken>()).Returns("<Semilla><valor>x</valor></Semilla>");
        _signer.SignAsync(Arg.Any<string>(), Env, Arg.Any<CancellationToken>())
            .Returns(new SignedXmlResult("<signed/>", "sigval", "sigval"));
        _authClient.ValidateSeedAsync(Env, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FreshToken());
    }

    private DgiiTokenProvider Sut() => new(
        _tenant, _cache, _authClient, _signer,
        new DgiiTokenGate(),
        Options.Create(new DgiiOptions { TokenRenewalBufferMinutes = 5 }),
        Clock,
        NullLogger<DgiiTokenProvider>.Instance);

    private AuthenticationToken FreshToken()
        => new("token-fresh", Clock.GetUtcNow(), Clock.GetUtcNow().AddHours(1));

    [Fact]
    public async Task Returns_the_cached_token_without_calling_dgii()
    {
        _cache.GetAsync(TenantId, Env, Arg.Any<CancellationToken>()).Returns(FreshToken());

        var result = await Sut().GetTokenAsync(Env);

        result.Value.Value.ShouldBe("token-fresh");
        await _authClient.DidNotReceive().GetSeedAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authenticates_and_caches_on_a_cache_miss()
    {
        _cache.GetAsync(TenantId, Env, Arg.Any<CancellationToken>()).Returns((AuthenticationToken?)null);

        var result = await Sut().GetTokenAsync(Env);

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe("token-fresh");
        Received.InOrder(() =>
        {
            _authClient.GetSeedAsync(Env, Arg.Any<CancellationToken>());
            _signer.SignAsync(Arg.Any<string>(), Env, Arg.Any<CancellationToken>());
            _authClient.ValidateSeedAsync(Env, Arg.Any<string>(), Arg.Any<CancellationToken>());
        });
        await _cache.Received(1).SetAsync(TenantId, Env, Arg.Any<AuthenticationToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renews_a_token_that_is_within_the_buffer()
    {
        var almostGone = new AuthenticationToken(
            "token-old", Clock.GetUtcNow().AddMinutes(-57), Clock.GetUtcNow().AddMinutes(3));
        _cache.GetAsync(TenantId, Env, Arg.Any<CancellationToken>()).Returns(almostGone);

        var result = await Sut().GetTokenAsync(Env);

        result.Value.Value.ShouldBe("token-fresh");
        await _authClient.Received(1).GetSeedAsync(Env, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_when_the_request_has_no_tenant()
    {
        _tenant.TenantId.Returns((Guid?)null);

        (await Sut().GetTokenAsync(Env)).FirstError.Code.ShouldBe("Auth.TenantNotResolved");
    }

    [Fact]
    public async Task Propagates_a_seed_request_failure()
    {
        _cache.GetAsync(TenantId, Env, Arg.Any<CancellationToken>()).Returns((AuthenticationToken?)null);
        _authClient.GetSeedAsync(Env, Arg.Any<CancellationToken>())
            .Returns(DgiiAuthErrors.SeedRequestFailed(503));

        var result = await Sut().GetTokenAsync(Env);

        result.FirstError.Code.ShouldBe("Dgii.Auth.SeedRequestFailed");
        await _cache.DidNotReceive().SetAsync(
            Arg.Any<Guid>(), Arg.Any<DgiiEnvironment>(), Arg.Any<AuthenticationToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Propagates_a_signing_failure()
    {
        _cache.GetAsync(TenantId, Env, Arg.Any<CancellationToken>()).Returns((AuthenticationToken?)null);
        _signer.SignAsync(Arg.Any<string>(), Env, Arg.Any<CancellationToken>())
            .Returns(Error.Failure("Certificate.NoActiveCertificate", "no cert"));

        var result = await Sut().GetTokenAsync(Env);

        result.FirstError.Code.ShouldBe("Certificate.NoActiveCertificate");
        await _authClient.DidNotReceive().ValidateSeedAsync(
            Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
