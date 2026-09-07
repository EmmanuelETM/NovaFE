using System.Net;
using ErrorOr;
using NovaFE.Application.Webhooks;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Webhooks;

namespace NovaFE.IntegrationTests.Fixtures;

/// <summary>
/// Política de URL para las pruebas: sin DNS. Las IP literales pasan por el guard
/// real (pero se permite loopback, para que WireMock actúe de receptor); los
/// hostnames se permiten. Así los tests del guard anti-SSRF usan IPs privadas y
/// los de entrega usan <c>localhost</c>/<c>127.0.0.1</c>.
/// </summary>
internal sealed class TestWebhookUrlPolicy : IWebhookUrlPolicy
{
    public Task<ErrorOr<Success>> EnsureAllowedAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Task.FromResult<ErrorOr<Success>>(WebhookEndpointErrors.UrlNotAbsolute);

        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
            && IPAddress.TryParse(uri.DnsSafeHost, out var ip)
            && !IPAddress.IsLoopback(ip)
            && PrivateAddressGuard.IsDisallowed(ip))
            return Task.FromResult<ErrorOr<Success>>(WebhookEndpointErrors.PrivateAddressNotAllowed);

        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}
