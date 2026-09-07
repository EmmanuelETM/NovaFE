namespace NovaFE.Application.Webhooks.Interfaces;

/// <summary>
/// Calcula el header <c>X-NovaFE-Signature</c>: <c>sha256=&lt;hex&gt;</c> donde
/// <c>hex = HMAC_SHA256(secret, "{timestamp}.{body}")</c>. Ver <c>docs/webhooks.md</c>.
/// </summary>
public interface IWebhookSignature
{
    string Sign(string secret, long unixTimestamp, string body);
}
