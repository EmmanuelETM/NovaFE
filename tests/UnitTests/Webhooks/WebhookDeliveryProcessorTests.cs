using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Webhooks;
using NovaFE.Application.Webhooks.Delivery;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Webhooks;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Webhooks;

public class WebhookDeliveryProcessorTests : UseCaseTestBase
{
    private readonly IWebhookOutbox _outbox = Substitute.For<IWebhookOutbox>();
    private readonly IWebhookEndpointRepository _endpoints = Substitute.For<IWebhookEndpointRepository>();
    private readonly IWebhookSender _sender = Substitute.For<IWebhookSender>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly WebhookSettings _settings = new()
    {
        MaxAttempts = 3,
        AutoDisableAfterConsecutiveFailures = 2,
        BackoffLadder = [TimeSpan.FromMinutes(1)],
    };

    private readonly WebhookEndpoint _endpoint;
    private readonly WebhookDeliveryItem _item;

    public WebhookDeliveryProcessorTests()
    {
        _endpoint = WebhookEndpoint.Create(
            Guid.CreateVersion7(), "https://hooks.example.com/x", ["ecf.accepted"], null, "whsec_x").Value;
        _item = new WebhookDeliveryItem(
            Guid.CreateVersion7(), _endpoint.TenantId, _endpoint.Id, "evt_1", "ecf.accepted", "{}", Attempts: 0);

        _endpoints.GetAsync(_endpoint.Id, Arg.Any<CancellationToken>()).Returns(_endpoint);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((Func<CancellationToken, Task>)call[0]).Invoke(call.Arg<CancellationToken>()));
    }

    private WebhookDeliveryProcessor Sut() =>
        new(_outbox, _endpoints, _sender, _settings, _uow, Clock, NullLogger<WebhookDeliveryProcessor>.Instance);

    [Fact]
    public async Task A_2xx_marks_the_row_delivered_and_clears_the_endpoint_failures()
    {
        _sender.SendAsync(Arg.Any<WebhookDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new WebhookDeliveryAttempt(true, 200, null));

        await Sut().ProcessAsync(_item);

        await _outbox.Received(1).CompleteAsync(_item.RowId, 200, Arg.Any<CancellationToken>());
        _endpoint.ConsecutiveFailures.ShouldBe(0);
        await _outbox.DidNotReceive().RescheduleAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_500_reschedules_with_backoff_and_counts_a_failure()
    {
        _sender.SendAsync(Arg.Any<WebhookDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new WebhookDeliveryAttempt(false, 500, "HTTP 500"));

        await Sut().ProcessAsync(_item);

        await _outbox.Received(1).RescheduleAsync(
            _item.RowId, Clock.GetUtcNow() + TimeSpan.FromMinutes(1), 1, 500, "HTTP 500", Arg.Any<CancellationToken>());
        _endpoint.ConsecutiveFailures.ShouldBe(1);
    }

    [Fact]
    public async Task The_last_allowed_attempt_marks_the_row_dead()
    {
        _sender.SendAsync(Arg.Any<WebhookDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new WebhookDeliveryAttempt(false, null, "timeout"));
        var lastTry = _item with { Attempts = 2 }; // 3rd attempt, MaxAttempts = 3

        await Sut().ProcessAsync(lastTry);

        await _outbox.Received(1).MarkDeadAsync(_item.RowId, null, "timeout", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repeated_failures_auto_disable_the_endpoint()
    {
        _sender.SendAsync(Arg.Any<WebhookDeliveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new WebhookDeliveryAttempt(false, 503, "down"));

        await Sut().ProcessAsync(_item with { Attempts = 0 });
        await Sut().ProcessAsync(_item with { Attempts = 1 });

        _endpoint.Enabled.ShouldBeFalse();
        _endpoint.DisabledReason.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_disabled_endpoint_drops_the_delivery()
    {
        _endpoint.SetEnabled(false);

        await Sut().ProcessAsync(_item);

        await _outbox.Received(1).CompleteAsync(_item.RowId, 0, Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().SendAsync(Arg.Any<WebhookDeliveryRequest>(), Arg.Any<CancellationToken>());
    }
}
