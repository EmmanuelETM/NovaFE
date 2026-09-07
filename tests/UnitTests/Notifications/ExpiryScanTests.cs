using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NovaFE.Application.Certificates.Contracts;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Notifications;
using NovaFE.Application.Sequences.Contracts;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;

namespace NovaFE.UnitTests.Notifications;

public class ExpiryScanTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private readonly ICertificateReadRepository _certs = Substitute.For<ICertificateReadRepository>();
    private readonly INcfSequenceReadRepository _sequences = Substitute.For<INcfSequenceReadRepository>();
    private readonly IWebhookOutbox _outbox = Substitute.For<IWebhookOutbox>();
    private readonly IExpiryNotificationLog _log = Substitute.For<IExpiryNotificationLog>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(Now);

    public ExpiryScanTests()
    {
        _certs.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        _sequences.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        _log.TryRecordAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((Func<CancellationToken, Task>)call[0]).Invoke(call.Arg<CancellationToken>()));
    }

    private ExpiryScan Sut() => new(
        _certs, _sequences, _outbox, _log, _uow, _clock, NullLogger<ExpiryScan>.Instance);

    private static CertificateDto Cert(DateTimeOffset validTo, string status = "Active") => new(
        Guid.CreateVersion7(), "Test", "130000001", "CN=x", "CN=ca", "abc",
        Now.AddYears(-1), validTo, status, null, Now.AddYears(-1));

    private static NcfSequenceDto Seq(
        long rangeTo = 100, long next = 1, DateOnly? expiresOn = null, bool active = true)
        => new(Guid.CreateVersion7(), "Test", 31, "E", 1, rangeTo, next,
            rangeTo, Math.Max(rangeTo - next + 1, 0), expiresOn, active, Now.AddYears(-1));

    private Task Run() => Sut().RunForCurrentTenantAsync(TenantId);

    private async Task AssertEnqueued(string type, int times = 1) =>
        await _outbox.Received(times).EnqueueAsync(
            Arg.Is<WebhookEventEnvelope>(e => e.Type == type), TenantId, Arg.Any<CancellationToken>());

    private async Task AssertNotEnqueued(string type) =>
        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Is<WebhookEventEnvelope>(e => e.Type == type), Arg.Any<Guid>(), Arg.Any<CancellationToken>());

    [Fact]
    public async Task Certificate_within_a_threshold_emits_certificate_expiring_once()
    {
        _certs.ListAsync(Arg.Any<CancellationToken>()).Returns([Cert(validTo: Now.AddDays(20))]);

        await Run();

        await AssertEnqueued("certificate.expiring");
        // 20 días cruza los umbrales 90 y 30 → se registran ambos, un solo webhook.
        await _log.Received().TryRecordAsync("certificate", Arg.Any<Guid>(), "expiring:90", Arg.Any<CancellationToken>());
        await _log.Received().TryRecordAsync("certificate", Arg.Any<Guid>(), "expiring:30", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Certificate_past_valid_to_emits_certificate_expired()
    {
        _certs.ListAsync(Arg.Any<CancellationToken>()).Returns([Cert(validTo: Now.AddDays(-1))]);

        await Run();

        await AssertEnqueued("certificate.expired");
        await AssertNotEnqueued("certificate.expiring");
    }

    [Fact]
    public async Task Certificate_far_from_expiry_emits_nothing()
    {
        _certs.ListAsync(Arg.Any<CancellationToken>()).Returns([Cert(validTo: Now.AddDays(200))]);

        await Run();

        await AssertNotEnqueued("certificate.expiring");
    }

    [Fact]
    public async Task Revoked_certificate_is_ignored()
    {
        _certs.ListAsync(Arg.Any<CancellationToken>()).Returns([Cert(validTo: Now.AddDays(5), status: "Revoked")]);

        await Run();

        await AssertNotEnqueued("certificate.expiring");
        await AssertNotEnqueued("certificate.expired");
    }

    [Fact]
    public async Task Already_notified_threshold_does_not_re_emit()
    {
        _certs.ListAsync(Arg.Any<CancellationToken>()).Returns([Cert(validTo: Now.AddDays(5))]);
        _log.TryRecordAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await Run();

        await AssertNotEnqueued("certificate.expiring");
    }

    [Fact]
    public async Task Exhausted_sequence_emits_sequence_exhausted()
    {
        _sequences.ListAsync(Arg.Any<CancellationToken>()).Returns([Seq(rangeTo: 10, next: 11)]);

        await Run();

        await AssertEnqueued("sequence.exhausted");
        await AssertNotEnqueued("sequence.low");
    }

    [Fact]
    public async Task Low_stock_sequence_emits_sequence_low()
    {
        // capacidad 10, umbral bajo = ceil(10*0.2)=2, restante = 2 → bajo.
        _sequences.ListAsync(Arg.Any<CancellationToken>()).Returns([Seq(rangeTo: 10, next: 9)]);

        await Run();

        await AssertEnqueued("sequence.low");
    }

    [Fact]
    public async Task Expired_sequence_emits_sequence_expired()
    {
        _sequences.ListAsync(Arg.Any<CancellationToken>())
            .Returns([Seq(expiresOn: new DateOnly(2025, 12, 31))]);

        await Run();

        await AssertEnqueued("sequence.expired");
        await AssertNotEnqueued("sequence.expiring");
    }

    [Fact]
    public async Task Sequence_near_expiry_emits_sequence_expiring()
    {
        _sequences.ListAsync(Arg.Any<CancellationToken>())
            .Returns([Seq(expiresOn: DateOnly.FromDateTime(Now.DateTime).AddDays(5))]);

        await Run();

        await AssertEnqueued("sequence.expiring");
    }

    [Fact]
    public async Task Inactive_sequence_is_ignored()
    {
        _sequences.ListAsync(Arg.Any<CancellationToken>())
            .Returns([Seq(rangeTo: 10, next: 11, active: false)]);

        await Run();

        await AssertNotEnqueued("sequence.exhausted");
    }
}
