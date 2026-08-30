using NovaFE.Domain.Dgii;

namespace NovaFE.UnitTests.Dgii;

public class AuthenticationTokenTests
{
    private static readonly DateTimeOffset Issued = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Expires = Issued.AddHours(1);

    private static AuthenticationToken Token => new("token-abc", Issued, Expires);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_rejects_a_blank_value(string value)
        => Should.Throw<ArgumentException>(() => new AuthenticationToken(value, Issued, Expires));

    [Fact]
    public void Ctor_rejects_expiry_at_or_before_issue()
        => Should.Throw<ArgumentException>(() => new AuthenticationToken("t", Issued, Issued));

    [Fact]
    public void IsExpired_flips_at_the_expiry_instant()
    {
        Token.IsExpired(Expires.AddSeconds(-1)).ShouldBeFalse();
        Token.IsExpired(Expires).ShouldBeTrue();
    }

    [Fact]
    public void NeedsRenewal_is_true_once_inside_the_buffer()
    {
        var buffer = TimeSpan.FromMinutes(5);

        Token.NeedsRenewal(Expires.AddMinutes(-6), buffer).ShouldBeFalse();
        Token.NeedsRenewal(Expires.AddMinutes(-5), buffer).ShouldBeTrue();
        Token.NeedsRenewal(Expires.AddMinutes(-1), buffer).ShouldBeTrue();
    }

    [Fact]
    public void RemainingLifetime_never_goes_negative()
    {
        Token.RemainingLifetime(Issued.AddMinutes(20)).ShouldBe(TimeSpan.FromMinutes(40));
        Token.RemainingLifetime(Expires.AddMinutes(10)).ShouldBe(TimeSpan.Zero);
    }
}
