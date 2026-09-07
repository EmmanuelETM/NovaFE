using System.Net;
using System.Net.Sockets;

namespace NovaFE.Application.Webhooks;

/// <summary>
/// Decide si una IP <b>no</b> es públicamente ruteable. Es la mitad pura del guard
/// anti-SSRF de los webhooks: un cliente no debe poder apuntar un endpoint a
/// <c>127.0.0.1</c>, a la red interna, o al endpoint de metadata de la nube
/// (<c>169.254.169.254</c>). La resolución DNS la hace <c>IWebhookUrlPolicy</c>.
/// </summary>
public static class PrivateAddressGuard
{
    public static bool IsDisallowed(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any))
            return true;

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsDisallowedV4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsDisallowedV6(address),
            _ => true,
        };
    }

    private static bool IsDisallowedV4(byte[] b) =>
        b[0] == 0                                   // 0.0.0.0/8
        || b[0] == 10                               // 10.0.0.0/8
        || (b[0] == 100 && b[1] is >= 64 and <= 127) // 100.64.0.0/10 CGNAT
        || (b[0] == 169 && b[1] == 254)             // 169.254.0.0/16 link-local (metadata)
        || (b[0] == 172 && b[1] is >= 16 and <= 31) // 172.16.0.0/12
        || (b[0] == 192 && b[1] == 168)             // 192.168.0.0/16
        || b[0] >= 224;                             // 224/4 multicast + 240/4 reservado

    private static bool IsDisallowedV6(IPAddress address)
    {
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            return true;

        // fc00::/7 — unique local addresses
        return (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
    }
}
