using System.Security.Cryptography;
using System.Text;
using NovaFE.Infrastructure.Webhooks;

namespace NovaFE.UnitTests.Webhooks;

public class HmacWebhookSignatureTests
{
    private readonly HmacWebhookSignature _sut = new();

    [Fact]
    public void Signature_has_the_sha256_prefix_and_64_hex_chars()
    {
        var signature = _sut.Sign("whsec_abc", 1_700_000_000, "{\"a\":1}");

        signature.ShouldStartWith("sha256=");
        signature["sha256=".Length..].Length.ShouldBe(64);
        signature["sha256=".Length..].ShouldAllBe(c => Uri.IsHexDigit(c) && !char.IsUpper(c));
    }

    [Fact]
    public void Signs_the_timestamped_payload_and_a_consumer_can_verify_it()
    {
        const string secret = "whsec_test";
        const long timestamp = 1_725_000_000;
        const string body = "{\"id\":\"evt_1\",\"type\":\"ecf.accepted\"}";

        var signature = _sut.Sign(secret, timestamp, body);

        // Lo que haría el consumidor: recomputar sobre "{timestamp}.{body}".
        var expected = "sha256=" + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}")));

        signature.ShouldBe(expected);
    }

    [Fact]
    public void Any_input_change_changes_the_signature()
    {
        var baseline = _sut.Sign("s", 1000, "body");

        _sut.Sign("s2", 1000, "body").ShouldNotBe(baseline);
        _sut.Sign("s", 1001, "body").ShouldNotBe(baseline);
        _sut.Sign("s", 1000, "body!").ShouldNotBe(baseline);
    }
}
