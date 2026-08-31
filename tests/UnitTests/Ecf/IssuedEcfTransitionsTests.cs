using NovaFE.Domain.Common;
using NovaFE.Domain.Dgii;
using NovaFE.Domain.Ecf;

namespace NovaFE.UnitTests.Ecf;

public class IssuedEcfTransitionsTests
{
    private static readonly DateTimeOffset Now = EcfTestData.SignedAt.AddMinutes(1);
    private static readonly IReadOnlyList<DgiiMessage> Messages = [new DgiiMessage(0, "OK")];

    private static IssuedEcf NewlySigned() => IssuedEcf.FromSigned(
        EcfTestData.CreditoFiscal(),
        new SignedEcf(EcfTestData.SignedAt, "<ECF/>", null, "aB3xZ9KkLlMm", "aB3xZ9", new string('a', 64),
            "https://ecf.dgii.gov.do/testecf/consultatimbre?x=1"),
        DgiiEnvironment.TestEcf);

    private static IssuedEcf Submitted()
    {
        var ecf = NewlySigned();
        ecf.MarkSubmitted("TRACK-1", Now).IsError.ShouldBeFalse();
        return ecf;
    }

    [Fact]
    public void MarkSubmitted_from_signed_records_the_track_id()
    {
        var ecf = NewlySigned();

        ecf.MarkSubmitted("TRACK-1", Now).IsError.ShouldBeFalse();

        ecf.Status.ShouldBe(EcfStatus.Submitted);
        ecf.TrackId.ShouldBe("TRACK-1");
        ecf.SubmittedAt.ShouldBe(Now);
        ecf.SubmissionAttempts.ShouldBe(1);
    }

    [Fact]
    public void MarkSubmitted_twice_is_an_invalid_transition()
    {
        var ecf = Submitted();

        var result = ecf.MarkSubmitted("TRACK-2", Now);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("IssuedEcf.InvalidTransition");
    }

    [Fact]
    public void MarkAccepted_from_submitted_sets_the_fiscal_state()
    {
        var ecf = Submitted();

        ecf.MarkAccepted(Now, conditional: false, Messages, sequenceUsable: true).IsError.ShouldBeFalse();

        ecf.Status.ShouldBe(EcfStatus.Accepted);
        ecf.DgiiStatusCode.ShouldBe(1);
        ecf.DgiiProcessedAt.ShouldBe(Now);
        ecf.DgiiMessages.ShouldBe(Messages);
        ecf.SequenceUsable.ShouldBe(true);
    }

    [Fact]
    public void MarkAccepted_conditional_lands_in_its_own_state()
    {
        var ecf = Submitted();

        ecf.MarkAccepted(Now, conditional: true, Messages, sequenceUsable: null).IsError.ShouldBeFalse();

        ecf.Status.ShouldBe(EcfStatus.AcceptedConditional);
        ecf.DgiiStatusCode.ShouldBe(4);
    }

    [Fact]
    public void MarkAccepted_straight_from_signed_is_allowed_for_the_rfce_path()
    {
        var ecf = NewlySigned();

        ecf.MarkAccepted(Now, conditional: false, Messages, sequenceUsable: true).IsError.ShouldBeFalse();

        ecf.Status.ShouldBe(EcfStatus.Accepted);
    }

    [Fact]
    public void MarkRejected_records_the_reason_and_the_sequence_flag()
    {
        var ecf = Submitted();

        ecf.MarkRejected(Now, [new DgiiMessage(11, "Firma inválida")], sequenceUsable: false).IsError.ShouldBeFalse();

        ecf.Status.ShouldBe(EcfStatus.Rejected);
        ecf.DgiiStatusCode.ShouldBe(2);
        ecf.SequenceUsable.ShouldBe(false);
        ecf.DgiiMessages[0].Value.ShouldBe("Firma inválida");
    }

    [Fact]
    public void MarkForReview_only_from_submitted()
    {
        Submitted().MarkForReview("sin resultado tras 3 consultas").IsError.ShouldBeFalse();
        NewlySigned().MarkForReview("x").FirstError.Code.ShouldBe("IssuedEcf.InvalidTransition");
    }

    [Fact]
    public void MarkFailed_only_from_signed()
    {
        NewlySigned().MarkFailed("DGII inalcanzable").IsError.ShouldBeFalse();
        Submitted().MarkFailed("x").FirstError.Code.ShouldBe("IssuedEcf.InvalidTransition");
    }

    [Fact]
    public void RequeueForRetry_from_failed_or_review_goes_back_to_signed()
    {
        var failed = NewlySigned();
        failed.MarkFailed("DGII inalcanzable");
        failed.RequeueForRetry().IsError.ShouldBeFalse();
        failed.Status.ShouldBe(EcfStatus.Signed);

        var review = Submitted();
        review.MarkForReview("x");
        review.RequeueForRetry().IsError.ShouldBeFalse();
        review.Status.ShouldBe(EcfStatus.Signed);
    }

    [Fact]
    public void RequeueForRetry_from_a_terminal_state_is_rejected()
    {
        var ecf = Submitted();
        ecf.MarkAccepted(Now, conditional: false, Messages, sequenceUsable: true);

        ecf.RequeueForRetry().FirstError.Code.ShouldBe("IssuedEcf.NotRetriable");
    }

    [Fact]
    public void Status_helpers_classify_the_lifecycle()
    {
        EcfStatus.Accepted.IsTerminal.ShouldBeTrue();
        EcfStatus.Rejected.IsTerminal.ShouldBeTrue();
        EcfStatus.Submitted.IsTerminal.ShouldBeFalse();
        EcfStatus.Failed.IsRetriable.ShouldBeTrue();
        EcfStatus.Review.IsRetriable.ShouldBeTrue();
        EcfStatus.Signed.IsRetriable.ShouldBeFalse();
    }
}
