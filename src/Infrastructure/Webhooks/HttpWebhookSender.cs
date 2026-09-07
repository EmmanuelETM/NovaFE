using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using NovaFE.Application.Webhooks.Interfaces;
using Microsoft.Extensions.Logging;

namespace NovaFE.Infrastructure.Webhooks;

/// <summary>
/// <see cref="IWebhookSender"/> real. Un <c>HttpClient</c> con nombre y timeout
/// corto (<c>Webhooks:DeliveryTimeoutSeconds</c>), <b>sin</b> handler de
/// reintento: los reintentos los planifica el outbox. Antes de conectar re-corre
/// la política de URL (defensa contra DNS rebinding).
/// </summary>
internal sealed class HttpWebhookSender(
    HttpClient httpClient,
    IWebhookSignature signature,
    IWebhookUrlPolicy urlPolicy,
    TimeProvider timeProvider,
    ILogger<HttpWebhookSender> logger) : IWebhookSender
{
    public const string HttpClientName = "webhooks";

    public async Task<WebhookDeliveryAttempt> SendAsync(WebhookDeliveryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allowed = await urlPolicy.EnsureAllowedAsync(request.Url, ct);
        if (allowed.IsError)
            return new WebhookDeliveryAttempt(false, null, $"url rechazada: {allowed.FirstError.Description}");

        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        using var message = new HttpRequestMessage(HttpMethod.Post, request.Url)
        {
            Content = new StringContent(request.Body, Encoding.UTF8, "application/json"),
        };
        message.Headers.UserAgent.ParseAdd("NovaFE-Webhooks/1");
        message.Headers.TryAddWithoutValidation("X-NovaFE-Event", request.EventType);
        message.Headers.TryAddWithoutValidation("X-NovaFE-Delivery", request.DeliveryId);
        message.Headers.TryAddWithoutValidation(
            "X-NovaFE-Timestamp", timestamp.ToString(CultureInfo.InvariantCulture));
        message.Headers.TryAddWithoutValidation(
            "X-NovaFE-Signature", signature.Sign(request.Secret, timestamp, request.Body));

        try
        {
            using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
            var code = (int)response.StatusCode;

            return response.IsSuccessStatusCode
                ? new WebhookDeliveryAttempt(true, code, null)
                : new WebhookDeliveryAttempt(false, code, $"HTTP {code} {response.ReasonPhrase}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new WebhookDeliveryAttempt(false, null, "timeout");
        }
        catch (HttpRequestException ex)
        {
            logger.LogDebug(ex, "Entrega de webhook a {Url} falló por transporte", request.Url);
            return new WebhookDeliveryAttempt(false, null, ex.Message);
        }
    }
}
