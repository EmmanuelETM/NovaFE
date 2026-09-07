using System.Security.Cryptography;
using System.Text;
using NovaFE.Application.Webhooks.Interfaces;

namespace NovaFE.Infrastructure.Webhooks;

/// <summary>
/// <c>X-NovaFE-Signature: sha256=&lt;hex&gt;</c> con
/// <c>hex = HMAC_SHA256(secret, "{timestamp}.{body}")</c>. El timestamp firmado
/// (y enviado en <c>X-NovaFE-Timestamp</c>) es la defensa anti-replay.
/// </summary>
internal sealed class HmacWebhookSignature : IWebhookSignature
{
    public string Sign(string secret, long unixTimestamp, string body)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        ArgumentNullException.ThrowIfNull(body);

        var signedPayload = Encoding.UTF8.GetBytes($"{unixTimestamp}.{body}");
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signedPayload);

        return "sha256=" + Convert.ToHexStringLower(hash);
    }
}
