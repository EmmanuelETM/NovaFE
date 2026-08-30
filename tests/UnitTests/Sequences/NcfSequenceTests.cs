using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;

namespace NovaFE.UnitTests.Sequences;

public class NcfSequenceTests
{
    private static readonly DateOnly AuthorizedOn = new(2026, 3, 10);
    private static readonly DateOnly Today = new(2026, 8, 30);

    private static NcfSequence Authorize(
        EcfType? type = null,
        DgiiEnvironment? environment = null,
        char series = 'E',
        long from = 1,
        long to = 100)
        => NcfSequence.Authorize(
            environment ?? DgiiEnvironment.TestEcf,
            type ?? EcfType.CreditoFiscal,
            series, from, to, AuthorizedOn).Value;

    [Fact]
    public void Authorize_sets_the_pointer_to_the_start_of_the_range()
    {
        var sequence = Authorize(from: 5, to: 20);

        sequence.Next.ShouldBe(5);
        sequence.Capacity.ShouldBe(16);
        sequence.Remaining.ShouldBe(16);
        sequence.Active.ShouldBeTrue();
    }

    [Fact]
    public void Authorize_derives_expiry_as_dec_31_of_the_following_year()
        => Authorize(type: EcfType.CreditoFiscal).ExpiresOn.ShouldBe(new DateOnly(2027, 12, 31));

    [Theory]
    [InlineData(32)]
    [InlineData(34)]
    public void Authorize_leaves_expiry_null_for_types_without_sequence_expiry(int code)
        => Authorize(type: EcfType.FromValue(code)).ExpiresOn.ShouldBeNull();

    [Theory]
    [InlineData('P')]
    [InlineData('A')]
    public void Authorize_rejects_an_invalid_series(char series)
        => NcfSequence.Authorize(DgiiEnvironment.TestEcf, EcfType.Consumo, series, 1, 10, AuthorizedOn)
            .FirstError.Code.ShouldBe("Sequence.InvalidSeries");

    [Theory]
    [InlineData(0, 10)]
    [InlineData(50, 40)]
    public void Authorize_rejects_an_invalid_range(long from, long to)
        => NcfSequence.Authorize(DgiiEnvironment.TestEcf, EcfType.Consumo, 'E', from, to, AuthorizedOn)
            .FirstError.Code.ShouldBe("Sequence.InvalidRange");

    [Fact]
    public void Authorize_forces_cert_ecf_to_start_at_one()
        => NcfSequence.Authorize(DgiiEnvironment.CertEcf, EcfType.CreditoFiscal, 'E', 5, 100, AuthorizedOn)
            .FirstError.Code.ShouldBe("Sequence.CertEcfMustStartAtOne");

    [Fact]
    public void Authorize_caps_the_cert_ecf_range_at_ten_million()
        => NcfSequence.Authorize(DgiiEnvironment.CertEcf, EcfType.CreditoFiscal, 'E', 1, 10_000_001, AuthorizedOn)
            .FirstError.Code.ShouldBe("Sequence.CertEcfRangeTooLarge");

    [Fact]
    public void Allocate_hands_out_consecutive_numbers_and_advances_the_pointer()
    {
        var sequence = Authorize(type: EcfType.CreditoFiscal, from: 1, to: 3);

        sequence.Allocate(Today).Value.Value.ShouldBe("E310000000001");
        sequence.Allocate(Today).Value.Value.ShouldBe("E310000000002");
        sequence.Allocate(Today).Value.Value.ShouldBe("E310000000003");
        sequence.Next.ShouldBe(4);
        sequence.Remaining.ShouldBe(0);
    }

    [Fact]
    public void Allocate_fails_once_the_range_is_exhausted()
    {
        var sequence = Authorize(from: 1, to: 1);
        sequence.Allocate(Today);

        sequence.Allocate(Today).FirstError.Code.ShouldBe("Sequence.RangeExhausted");
    }

    [Fact]
    public void Allocate_fails_when_the_range_is_expired()
    {
        var sequence = Authorize(type: EcfType.CreditoFiscal);
        var afterExpiry = new DateOnly(2028, 1, 1);

        sequence.Allocate(afterExpiry).FirstError.Code.ShouldBe("Sequence.RangeExpired");
    }

    [Fact]
    public void Allocate_fails_when_the_range_is_inactive()
    {
        var sequence = Authorize();
        sequence.Deactivate();

        sequence.Allocate(Today).FirstError.Code.ShouldBe("Sequence.RangeInactive");
    }

    [Fact]
    public void Expiry_check_runs_before_the_stock_check()
    {
        var sequence = Authorize(type: EcfType.CreditoFiscal, from: 1, to: 1);
        sequence.Allocate(Today); // exhausts the range

        // Expired *and* exhausted: the expiry error wins (RF-07.4).
        sequence.Allocate(new DateOnly(2028, 6, 1)).FirstError.Code.ShouldBe("Sequence.RangeExpired");
    }

    [Fact]
    public void Low_stock_trips_at_twenty_percent_remaining()
    {
        var sequence = Authorize(from: 1, to: 10);

        for (var i = 0; i < 7; i++)
            sequence.Allocate(Today);

        sequence.Remaining.ShouldBe(3);
        sequence.IsLowStock.ShouldBeFalse();

        sequence.Allocate(Today);
        sequence.Remaining.ShouldBe(2);
        sequence.IsLowStock.ShouldBeTrue();
    }
}
