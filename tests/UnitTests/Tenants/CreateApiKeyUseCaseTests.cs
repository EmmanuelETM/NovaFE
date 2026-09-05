using ErrorOr;
using NSubstitute;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Application.Tenants.CreateApiKey;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Tenants;

public class CreateApiKeyUseCaseTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private readonly IEmitterProfileRepository _profiles = Substitute.For<IEmitterProfileRepository>();
    private readonly ICertificateRepository _certificates = Substitute.For<ICertificateRepository>();
    private readonly INcfSequenceRepository _sequences = Substitute.For<INcfSequenceRepository>();
    private readonly IApiKeyRepository _apiKeys = Substitute.For<IApiKeyRepository>();

    public CreateApiKeyUseCaseTests()
    {
        _profiles.GetByTenantAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(EmitterProfile.Create(
                TenantId, "Av. 27 de Febrero 100", "010100", "01",
                ["809-555-0100"], "f@acme.do", "Comercio", DgiiEnvironment.Test).Value);

        // Por defecto el contribuyente está listo en cualquier ambiente.
        _certificates.HasActiveForTenantAsync(TenantId, Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _sequences.HasAnyActiveRangeForTenantAsync(TenantId, Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private CreateApiKeyUseCase Sut() =>
        new(LoggerFactory, new CreateApiKeyCommandValidator(Clock), _profiles, _certificates, _sequences, _apiKeys);

    [Fact]
    public async Task Creates_a_key_bound_to_the_profile_default_environment()
    {
        var result = await Sut().Execute(new CreateApiKeyCommand(TenantId, "ERP contable", null, "emisor", null));

        result.IsError.ShouldBeFalse();
        result.Value.Token.ShouldStartWith("sk_nfe_test_");
        result.Value.Key.Environment.ShouldBe("Test");
        result.Value.Key.Role.ShouldBe("emisor");
        result.Value.Key.Label.ShouldBe("ERP contable");
        result.Value.Key.Prefix.ShouldBe(result.Value.Token[..16]);

        await _apiKeys.Received(1).AddAsync(
            Arg.Is<ApiKey>(k => k.TenantId == TenantId && k.Environment == DgiiEnvironment.Test),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Honours_an_explicit_environment()
    {
        var result = await Sut().Execute(new CreateApiKeyCommand(TenantId, null, "Production", "admin_tenant", null));

        result.IsError.ShouldBeFalse();
        result.Value.Token.ShouldStartWith("sk_nfe_prod_");
        result.Value.Key.Environment.ShouldBe("Production");
    }

    [Fact]
    public async Task Fails_when_the_emitter_profile_is_missing()
    {
        _profiles.GetByTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns((EmitterProfile?)null);

        var result = await Sut().Execute(new CreateApiKeyCommand(TenantId, null, null, "emisor", null));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("EmitterProfile.NotConfigured");
        await _apiKeys.DidNotReceive().AddAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Blocks_when_the_environment_has_no_active_certificate()
    {
        _certificates.HasActiveForTenantAsync(TenantId, DgiiEnvironment.Production, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await Sut().Execute(new CreateApiKeyCommand(TenantId, null, "Production", "emisor", null));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("ApiKey.EnvironmentNotReady");
        result.FirstError.Description.ShouldContain("certificado");
        await _apiKeys.DidNotReceive().AddAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Blocks_when_the_environment_has_no_sequence_range()
    {
        _sequences.HasAnyActiveRangeForTenantAsync(TenantId, DgiiEnvironment.Test, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await Sut().Execute(new CreateApiKeyCommand(TenantId, null, null, "emisor", null));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("ApiKey.EnvironmentNotReady");
        result.FirstError.Description.ShouldContain("secuencia");
    }

    [Fact]
    public async Task Rejects_an_unknown_environment()
    {
        var result = await Sut().Execute(new CreateApiKeyCommand(TenantId, null, "Staging", "emisor", null));

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);
    }

    [Fact]
    public async Task Rejects_an_expiration_in_the_past()
    {
        var result = await Sut().Execute(
            new CreateApiKeyCommand(TenantId, null, null, "emisor", Clock.GetUtcNow().AddDays(-1)));

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);
    }

    [Fact]
    public async Task Rejects_a_missing_role()
    {
        var result = await Sut().Execute(new CreateApiKeyCommand(TenantId, null, null, null, null));

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);
        await _apiKeys.DidNotReceive().AddAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_admin_sistema_as_a_tenant_key_role()
    {
        // admin_sistema es del operador, no una key de contribuyente.
        var result = await Sut().Execute(new CreateApiKeyCommand(TenantId, null, null, "admin_sistema", null));

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);
    }

    [Theory]
    [InlineData("admin_tenant")]
    [InlineData("emisor")]
    [InlineData("consultor")]
    public async Task Accepts_every_known_role(string role)
    {
        var result = await Sut().Execute(new CreateApiKeyCommand(TenantId, null, null, role, null));

        result.IsError.ShouldBeFalse();
        result.Value.Key.Role.ShouldBe(role);
    }
}
