using System.Net;
using LanFileDrop.Core.Models;

namespace LanFileDrop.Core.Tests.Models;

public class ManualPeerEndpointTests
{
    [Theory]
    [InlineData("127.0.0.1:5000")]        // loopback IPv4
    [InlineData("[::1]:5000")]            // loopback IPv6
    [InlineData("192.168.1.10:5000")]    // 192.168.0.0/16
    [InlineData("10.1.2.3:5000")]        // 10.0.0.0/8
    [InlineData("172.16.0.1:5000")]      // lower edge of 172.16.0.0/12
    [InlineData("172.31.255.254:5000")]  // upper edge of 172.16.0.0/12
    [InlineData("169.254.10.20:5000")]   // link-local
    [InlineData("192.168.0.1:65535")]    // max port
    [InlineData("[::1]:65535")]          // IPv6 loopback max port
    [InlineData(" 127.0.0.1:5000 ")]     // surrounding whitespace is trimmed
    public void TryParse_AcceptsLocalSafeTargets(string input)
    {
        var ok = ManualPeerEndpoint.TryParse(input, out var endpoint, out var error);

        Assert.True(ok, error);
        Assert.NotNull(endpoint);
    }

    [Theory]
    [InlineData("172.15.0.1:5000")]      // just below 172.16.0.0/12
    [InlineData("172.32.0.1:5000")]      // just above 172.16.0.0/12
    [InlineData("203.0.113.1:5000")]     // public (RFC 5737 documentation)
    [InlineData("0.0.0.0:5000")]         // unspecified
    [InlineData("255.255.255.255:5000")] // broadcast
    [InlineData("224.0.0.1:5000")]       // multicast
    [InlineData("999.1.1.1:5000")]       // invalid IP
    [InlineData("example.local:5000")]   // hostname
    [InlineData("192.168.0.1:0")]        // port 0
    [InlineData("192.168.0.1:65536")]    // port too large
    [InlineData("[::1]:65536")]          // IPv6 port too large
    [InlineData("127.0.0.1")]            // port missing
    [InlineData("127.0.0.1:")]           // empty port
    [InlineData("127.0.0.1:abc")]        // non-numeric port
    [InlineData("127.0.0.1:-1")]         // negative port
    [InlineData("127.0.0.1:5000:extra")] // trailing junk / ambiguous
    [InlineData("[::1]")]                // bracketed, port missing
    [InlineData("[::1]:")]               // bracketed, empty port
    [InlineData("[::ffff:127.0.0.1]:5000")] // IPv4-mapped IPv6 not supported yet
    [InlineData("localhost:5000")]       // hostname, no DNS
    [InlineData("   ")]                   // whitespace
    [InlineData("")]                      // empty
    [InlineData(null)]                     // null
    public void TryParse_RejectsUnsafeOrInvalidTargets(string? input)
    {
        var ok = ManualPeerEndpoint.TryParse(input, out var endpoint, out var error);

        Assert.False(ok);
        Assert.Null(endpoint);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryParse_NormalizesAddressAndPort()
    {
        var ok = ManualPeerEndpoint.TryParse("192.168.5.42:5001", out var endpoint, out _);

        Assert.True(ok);
        Assert.NotNull(endpoint);
        Assert.Equal(IPAddress.Parse("192.168.5.42"), endpoint!.Address);
        Assert.Equal(5001, endpoint.Port);
        Assert.Equal("192.168.5.42:5001", endpoint.Display);
    }

    [Fact]
    public void TryParse_BracketsIPv6InDisplay()
    {
        var ok = ManualPeerEndpoint.TryParse("[::1]:5002", out var endpoint, out _);

        Assert.True(ok);
        Assert.NotNull(endpoint);
        Assert.Equal("[::1]:5002", endpoint!.Display);
    }

    [Fact]
    public void TryParse_RejectsUnbracketedIPv6()
    {
        var ok = ManualPeerEndpoint.TryParse("::1:5000", out var endpoint, out var error);

        Assert.False(ok);
        Assert.Null(endpoint);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
