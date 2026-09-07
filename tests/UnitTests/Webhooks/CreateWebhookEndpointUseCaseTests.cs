using ErrorOr;
using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Webhooks;
using NovaFE.Application.Webhooks.CreateEndpoint;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Webhooks;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Webhooks;

public class CreateWebhookEndpointUseCaseTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly IWebhookUrlPolicy _urlPolicy = Substitute.For<IWebhookUrlPolicy>();
    private readonly IWebhookEndpointRepository _endpoints = Substitute.For<IWebhookEndpointRepository>();
    private readonly WebhookSettings _settings = new() { MaxEndpointsPerTenant = 3, RequireHttps = true };

    public CreateWebhookEndpointUseCaseTests()
    {
        _tenant.TenantId.Returns(TenantId);
        _tenant.HasValue.Returns(true);
        _urlPolicy.EnsureAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        _endpoints.CountAsync(Arg.Any<CancellationToken>()).Returns(0);
    }

    private CreateWebhookEndpointUseCase Sut() => new(
        LoggerFactory, new CreateWebhookEndpointCommandValidator(), _tenant, _settings, _urlPolicy, _endpoints);

    private static CreateWebhookEndpointCommand Command(
        string? url = "https://hooks.example.com/nova", string[]? events = null)
        => new(url, events ?? ["ecf.accepted", "ecf.rejected"], "ERP");

    [Fact]
    public async Task Creates_the_endpoint_and_returns_the_secret_once()
    {
        var result = await Sut().Execute(Command());

        result.IsError.ShouldBeFalse();
        result.Value.Secret.ShouldStartWith("whsec_");
        result.Value.Endpoint.Url.ShouldBe("https://hooks.example.com/nova");
        result.Value.Endpoint.Events.ShouldBe(["ecf.accepted", "ecf.rejected"]);
        result.Value.Endpoint.Enabled.ShouldBeTrue();

        await _endpoints.Received(1).AddAsync(
            Arg.Is<WebhookEndpoint>(e => e.TenantId == TenantId && e.Secret == result.Value.Secret),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_when_the_tenant_limit_is_reached()
    {
        _endpoints.CountAsync(Arg.Any<CancellationToken>()).Returns(3);

        var result = await Sut().Execute(Command());

        result.FirstError.Code.ShouldBe("WebhookEndpoint.LimitReached");
        await _endpoints.DidNotReceive().AddAsync(Arg.Any<WebhookEndpoint>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_when_the_url_policy_rejects_the_url()
    {
        _urlPolicy.EnsureAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(WebhookEndpointErrors.PrivateAddressNotAllowed);

        var result = await Sut().Execute(Command());

        result.FirstError.Code.ShouldBe("WebhookEndpoint.PrivateAddressNotAllowed");
        await _endpoints.DidNotReceive().AddAsync(Arg.Any<WebhookEndpoint>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_an_unknown_event_at_the_validator()
    {
        var result = await Sut().Execute(Command(events: ["ecf.boom"]));

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);
    }

    [Fact]
    public async Task Fails_without_a_tenant()
    {
        _tenant.TenantId.Returns((Guid?)null);

        var result = await Sut().Execute(Command());

        result.FirstError.Code.ShouldBe("Auth.TenantNotResolved");
    }
}
