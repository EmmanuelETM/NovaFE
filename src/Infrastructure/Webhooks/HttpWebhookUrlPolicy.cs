using System.Net;
using System.Net.Sockets;
using ErrorOr;
using NovaFE.Application.Webhooks;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Webhooks;

namespace NovaFE.Infrastructure.Webhooks;

/// <summary>
/// <see cref="IWebhookUrlPolicy"/> real: valida esquema, exige <c>https</c> si la
/// configuración lo pide, resuelve el host y rechaza si alguna dirección no es
/// públicamente ruteable (<see cref="PrivateAddressGuard"/>).
/// </summary>
internal sealed class HttpWebhookUrlPolicy(WebhookSettings settings) : IWebhookUrlPolicy
{
    public async Task<ErrorOr<Success>> EnsureAllowedAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return WebhookEndpointErrors.UrlNotAbsolute;

        if (settings.RequireHttps && uri.Scheme != Uri.UriSchemeHttps)
            return WebhookEndpointErrors.HttpsRequired;

        IPAddress[] addresses;

        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            addresses = IPAddress.TryParse(uri.DnsSafeHost, out var literal)
                ? [literal]
                : [];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, ct);
            }
            catch (Exception ex) when (ex is SocketException or ArgumentException)
            {
                return WebhookEndpointErrors.UnresolvableHost;
            }
        }

        if (addresses.Length == 0)
            return WebhookEndpointErrors.UnresolvableHost;

        return addresses.Any(PrivateAddressGuard.IsDisallowed)
            ? WebhookEndpointErrors.PrivateAddressNotAllowed
            : Result.Success;
    }
}
