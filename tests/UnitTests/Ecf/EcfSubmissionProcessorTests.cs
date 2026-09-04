using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NovaFE.Application.Dgii.Contracts;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Application.Ecf.Submission;
using NovaFE.Domain.Common;
using NovaFE.Domain.Dgii;
using NovaFE.Domain.Ecf;

namespace NovaFE.UnitTests.Ecf;

public class EcfSubmissionProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 21, 14, 30, 0, TimeSpan.Zero);

    private readonly IEcfRepository _ecfRepo = Substitute.For<IEcfRepository>();
    private readonly IEcfSubmissionQueue _queue = Substitute.For<IEcfSubmissionQueue>();
    private readonly IDgiiTokenProvider _tokens = Substitute.For<IDgiiTokenProvider>();
    private readonly IDgiiSubmissionClient _client = Substitute.For<IDgiiSubmissionClient>();
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly EcfSubmissionSettings _settings = new();

    public EcfSubmissionProcessorTests()
        => _tokens.GetTokenAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticationToken("bearer-xyz", Now, Now.AddHours(1)));

    private EcfSubmissionProcessor Sut() => new(
        _ecfRepo, _queue, _tokens, _client, _settings, _clock, NullLogger<EcfSubmissionProcessor>.Instance);

    private IssuedEcf Signed(bool rfce = false)
    {
        var doc = rfce ? EcfTestData.Consumo() : EcfTestData.CreditoFiscal();
        var ecf = IssuedEcf.FromSigned(
            doc,
            new SignedEcf(Now, "<ECF/>", rfce ? "<RFCE/>" : null, "aB3xZ9KkLlMm", "aB3xZ9", new string('a', 64),
                "https://ecf.dgii.gov.do/testecf/consultatimbre?x=1"),
            DgiiEnvironment.Test);
        _ecfRepo.GetByIdAsync(ecf.Id, Arg.Any<CancellationToken>()).Returns(ecf);
        return ecf;
    }

    private static EcfSubmissionWorkItem Item(IssuedEcf ecf, EcfSubmissionKind kind, int attempts = 0, string? trackId = null)
        => new(Guid.NewGuid(), ecf.Id, ecf.TenantId, DgiiEnvironment.Test, kind, attempts, trackId);

    [Fact]
    public async Task Submit_success_records_the_track_id_and_schedules_a_poll()
    {
        var ecf = Signed();
        _client.SubmitEcfAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DgiiSubmissionReceipt("TRACK-1", "Recibido"));

        var item = Item(ecf, EcfSubmissionKind.Submit);
        await Sut().ProcessAsync(item);

        ecf.Status.ShouldBe(EcfStatus.Submitted);
        ecf.TrackId.ShouldBe("TRACK-1");
        await _ecfRepo.Received(1).UpdateAsync(ecf, Arg.Any<CancellationToken>());
        await _queue.Received(1).RescheduleAsync(item.Id, EcfSubmissionKind.Poll,
            Now + _settings.FirstPollDelay, 0, "TRACK-1", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rfce_submit_resolves_synchronously_from_the_receipt_code()
    {
        var ecf = Signed(rfce: true);
        _client.SubmitRfceAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DgiiRfceReceipt(1, "Aceptado", [new DgiiMessage(0, "OK")], ecf.Encf.Value, true));

        var item = Item(ecf, EcfSubmissionKind.Submit);
        await Sut().ProcessAsync(item);

        ecf.Status.ShouldBe(EcfStatus.Accepted);
        await _queue.Received(1).CompleteAsync(item.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_transport_failure_backs_off()
    {
        var ecf = Signed();
        _client.SubmitEcfAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Errors.Http.Unreachable);

        var item = Item(ecf, EcfSubmissionKind.Submit, attempts: 1);
        await Sut().ProcessAsync(item);

        ecf.Status.ShouldBe(EcfStatus.Signed);
        await _queue.Received(1).RescheduleAsync(item.Id, EcfSubmissionKind.Submit,
            Now + _settings.SubmitBackoff[1], 2, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_transport_failure_gives_up_after_the_backoff_ladder()
    {
        var ecf = Signed();
        _client.SubmitEcfAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Errors.Http.Timeout);

        var item = Item(ecf, EcfSubmissionKind.Submit, attempts: _settings.SubmitBackoff.Count);
        await Sut().ProcessAsync(item);

        ecf.Status.ShouldBe(EcfStatus.Failed);
        await _queue.Received(1).MarkDeadAsync(item.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_gateway_rejection_is_not_retried()
    {
        var ecf = Signed();
        _client.SubmitEcfAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DgiiSubmissionErrors.NoTrackId("XSD inválido"));

        var item = Item(ecf, EcfSubmissionKind.Submit);
        await Sut().ProcessAsync(item);

        ecf.Status.ShouldBe(EcfStatus.Failed);
        await _queue.Received(1).MarkDeadAsync(item.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Poll_accepted_marks_the_comprobante_and_completes_the_row()
    {
        var ecf = Signed();
        ecf.MarkSubmitted("TRACK-1", Now);
        _client.GetResultAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), "TRACK-1", Arg.Any<CancellationToken>())
            .Returns(new DgiiEcfResult(1, "Aceptado", [], true, Now));

        var item = Item(ecf, EcfSubmissionKind.Poll, trackId: "TRACK-1");
        await Sut().ProcessAsync(item);

        ecf.Status.ShouldBe(EcfStatus.Accepted);
        ecf.DgiiStatusText.ShouldBe("Aceptado");
        ecf.DgiiReceivedAt.ShouldBe(Now);
        await _queue.Received(1).CompleteAsync(item.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Poll_rejected_records_the_sequence_flag()
    {
        var ecf = Signed();
        ecf.MarkSubmitted("TRACK-1", Now);
        _client.GetResultAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), "TRACK-1", Arg.Any<CancellationToken>())
            .Returns(new DgiiEcfResult(2, "Rechazado", [new DgiiMessage(11, "Firma inválida")], false, Now));

        var item = Item(ecf, EcfSubmissionKind.Poll, trackId: "TRACK-1");
        await Sut().ProcessAsync(item);

        ecf.Status.ShouldBe(EcfStatus.Rejected);
        ecf.SequenceUsable.ShouldBe(false);
    }

    [Fact]
    public async Task Poll_in_process_reschedules_down_the_ladder()
    {
        var ecf = Signed();
        ecf.MarkSubmitted("TRACK-1", Now);
        _client.GetResultAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), "TRACK-1", Arg.Any<CancellationToken>())
            .Returns(new DgiiEcfResult(3, "En Proceso", [], null, null));

        var item = Item(ecf, EcfSubmissionKind.Poll, attempts: 1, trackId: "TRACK-1");
        await Sut().ProcessAsync(item);

        ecf.Status.ShouldBe(EcfStatus.Submitted);
        await _queue.Received(1).RescheduleAsync(item.Id, EcfSubmissionKind.Poll,
            Now + _settings.PollLadder[1], 2, "TRACK-1", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Poll_that_never_resolves_ends_in_manual_review()
    {
        var ecf = Signed();
        ecf.MarkSubmitted("TRACK-1", Now);
        _client.GetResultAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), "TRACK-1", Arg.Any<CancellationToken>())
            .Returns(new DgiiEcfResult(3, "En Proceso", [], null, null));

        var item = Item(ecf, EcfSubmissionKind.Poll, attempts: _settings.PollLadder.Count, trackId: "TRACK-1");
        await Sut().ProcessAsync(item);

        ecf.Status.ShouldBe(EcfStatus.Review);
        await _queue.Received(1).CompleteAsync(item.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollOnce_applies_a_terminal_result_and_reports_resolved()
    {
        var ecf = Signed();
        ecf.MarkSubmitted("TRACK-1", Now);
        _client.GetResultAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), "TRACK-1", Arg.Any<CancellationToken>())
            .Returns(new DgiiEcfResult(4, "Aceptado Condicional", [], true, Now));

        var resolved = await Sut().PollOnceAsync(ecf.Id);

        resolved.ShouldBeTrue();
        ecf.Status.ShouldBe(EcfStatus.AcceptedConditional);
        await _queue.DidNotReceive().RescheduleAsync(
            Arg.Any<Guid>(), Arg.Any<EcfSubmissionKind>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollOnce_returns_not_resolved_while_the_dgii_is_still_processing()
    {
        var ecf = Signed();
        ecf.MarkSubmitted("TRACK-1", Now);
        _client.GetResultAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<string>(), "TRACK-1", Arg.Any<CancellationToken>())
            .Returns(new DgiiEcfResult(3, "En Proceso", [], null, null));

        (await Sut().PollOnceAsync(ecf.Id)).ShouldBeFalse();
        ecf.Status.ShouldBe(EcfStatus.Submitted);
    }
}
