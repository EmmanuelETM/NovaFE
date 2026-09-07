using System.Net;
using NovaFE.Application.Webhooks;

namespace NovaFE.UnitTests.Webhooks;

public class PrivateAddressGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")] // metadata de la nube
    [InlineData("100.64.0.1")]      // CGNAT
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]       // multicast
    [InlineData("::1")]
    [InlineData("fe80::1")]         // link-local
    [InlineData("fd00::1")]         // unique local
    public void Disallows_non_public_addresses(string ip)
        => PrivateAddressGuard.IsDisallowed(IPAddress.Parse(ip)).ShouldBeTrue();

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.215.14")]   // example.com
    [InlineData("2606:4700:4700::1111")]
    public void Allows_public_addresses(string ip)
        => PrivateAddressGuard.IsDisallowed(IPAddress.Parse(ip)).ShouldBeFalse();

    [Fact]
    public void Disallows_an_ipv4_mapped_private_v6_address()
        => PrivateAddressGuard.IsDisallowed(IPAddress.Parse("::ffff:10.0.0.1")).ShouldBeTrue();
}
