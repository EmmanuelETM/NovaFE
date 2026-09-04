using ErrorOr;
using NSubstitute;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.CreateApiKey;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Tenants;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Tenants;

public class CreateApiKeyUseCaseTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private readonly ITenantReadRepository _tenants = Substitute.For<ITenantReadRepository>();
    private readonly IApiKeyRepository _apiKeys = Substitute.For<IApiKeyRepository>();

    public CreateApiKeyUseCaseTests()
    {
        _tenants.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantDto(TenantId, "132786262", "Acme SRL", null, "Business", "Active", Clock.GetUtcNow()));
    }

    private CreateApiKeyUseCase Sut() =>
        new(LoggerFactory, new CreateApiKeyCommandValidator(Clock), _tenants, _apiKeys);

    [Fact]
    public async Task Creates_a_key_and_returns_the_token_once()
    {
        var result = await Sut().Execute(new CreateApiKeyCommand(TenantId, "ERP contable", null));

        result.IsError.ShouldBeFalse();
        result.Value.Token.ShouldStartWith("nfe_");
        result.Value.Key.Label.ShouldBe("ERP contable");
        result.Value.Key.Prefix.ShouldBe(result.Value.Token[..12]);
        result.Value.Key.TenantId.ShouldBe(TenantId);

        await _apiKeys.Received(1).AddAsync(
            Arg.Is<ApiKey>(k => k.TenantId == TenantId && k.KeyHash.Length == 64),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_when_the_tenant_does_not_exist()
    {
        _tenants.GetByIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns((TenantDto?)null);

        var result = await Sut().Execute(new CreateApiKeyCommand(TenantId, null, null));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Tenant.NotFound");
        await _apiKeys.DidNotReceive().AddAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_an_expiration_in_the_past()
    {
        var result = await Sut().Execute(
            new CreateApiKeyCommand(TenantId, null, Clock.GetUtcNow().AddDays(-1)));

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);
    }
}
