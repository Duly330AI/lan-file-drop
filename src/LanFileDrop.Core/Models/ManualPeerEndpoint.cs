using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace LanFileDrop.Core.Models;

/// <summary>
/// A user-entered manual peer target (IP literal + port) that has been validated as
/// pointing at a local-network-safe address. This type performs no DNS resolution and
/// opens no sockets — it only parses and validates. Connecting is a later concern.
/// </summary>
public sealed record ManualPeerEndpoint
{
    public IPAddress Address { get; }
    public int Port { get; }

    private ManualPeerEndpoint(IPAddress address, int port)
    {
        Address = address;
        Port = port;
    }

    /// <summary>Canonical display form; IPv6 is bracketed, e.g. [::1]:5000.</summary>
    public string Display => Address.AddressFamily == AddressFamily.InterNetworkV6
        ? $"[{Address}]:{Port}"
        : $"{Address}:{Port}";

    /// <summary>
    /// Parses "host:port" where host must be an IP literal (no hostnames, no DNS).
    /// Returns true only for local-network-safe targets; <paramref name="error"/>
    /// carries a human-readable reason on failure.
    /// </summary>
    public static bool TryParse(string? input, out ManualPeerEndpoint? endpoint, out string? error)
    {
        endpoint = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Endpoint must not be empty or whitespace.";
            return false;
        }

        if (!TrySplitHostPort(input.Trim(), out var host, out var port, out error))
        {
            return false;
        }

        // IP literals only. Deliberately no Dns.GetHostAddresses: hostnames could resolve
        // to public or off-LAN targets, which this batch must not allow.
        if (!IPAddress.TryParse(host, out var address))
        {
            error = "Host must be a valid IP address (hostnames are not supported).";
            return false;
        }

        if (!IsLocalSafe(address, out error))
        {
            return false;
        }

        endpoint = new ManualPeerEndpoint(address, port);
        error = null;
        return true;
    }

    private static bool TrySplitHostPort(string input, out string host, out int port, out string? error)
    {
        host = string.Empty;
        port = 0;
        error = null;

        string portPart;
        if (input.StartsWith('['))
        {
            var close = input.IndexOf(']');
            if (close < 0)
            {
                error = "Bracketed IPv6 endpoint is missing a closing ']'.";
                return false;
            }

            host = input[1..close];
            var rest = input[(close + 1)..];
            if (!rest.StartsWith(':'))
            {
                error = "IPv6 endpoint must include a port, e.g. [::1]:5000.";
                return false;
            }

            portPart = rest[1..];
        }
        else
        {
            var lastColon = input.LastIndexOf(':');
            if (lastColon < 0)
            {
                error = "Endpoint must include a port, e.g. 192.168.0.10:5000.";
                return false;
            }

            // More than one colon without brackets is ambiguous: either an unbracketed
            // IPv6 literal (::1:5000) or a malformed host:port (a:b:c). Reject both.
            if (input.IndexOf(':') != lastColon)
            {
                error = "Ambiguous endpoint: wrap IPv6 addresses in brackets, e.g. [::1]:5000.";
                return false;
            }

            host = input[..lastColon];
            portPart = input[(lastColon + 1)..];
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            error = "Host must not be empty.";
            return false;
        }

        // NumberStyles.None rejects signs/whitespace, so "-1" and " 5" fail here.
        if (!int.TryParse(portPart, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort))
        {
            error = "Port must be a non-negative integer.";
            return false;
        }

        if (parsedPort is < 1 or > 65535)
        {
            error = "Port must be between 1 and 65535.";
            return false;
        }

        port = parsedPort;
        return true;
    }

    private static bool IsLocalSafe(IPAddress address, out string? error)
    {
        error = null;

        // Reject IPv4-mapped IPv6 (e.g. ::ffff:203.0.113.1) up front: IPAddress.IsLoopback
        // treats ::ffff:127.0.0.1 as loopback, and such forms could otherwise smuggle a
        // public IPv4 target past the IPv6 path. Require the plain IPv4 form instead.
        if (address.IsIPv4MappedToIPv6)
        {
            error = "IPv4-mapped IPv6 addresses are not accepted; use the plain IPv4 form.";
            return false;
        }

        // Covers IPv4 127.0.0.0/8 and IPv6 ::1.
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return IsLocalSafeIPv4(address.GetAddressBytes(), out error);
        }

        // Non-loopback IPv6 (unspecified ::, link-local, ULA, multicast, global) is not
        // accepted in this batch; only IPv6 loopback is allowed.
        error = "Only IPv6 loopback (::1) is accepted; use a private IPv4 address otherwise.";
        return false;
    }

    private static bool IsLocalSafeIPv4(byte[] b, out string? error)
    {
        error = null;

        if (b is [0, 0, 0, 0])
        {
            error = "Unspecified address 0.0.0.0 is not a valid target.";
            return false;
        }

        if (b is [255, 255, 255, 255])
        {
            error = "Broadcast address 255.255.255.255 is not a valid target.";
            return false;
        }

        // Multicast 224.0.0.0/4 and reserved 240.0.0.0/4.
        if (b[0] >= 224)
        {
            error = "Multicast/reserved addresses are not valid targets.";
            return false;
        }

        // Private and link-local ranges that are safe on a local network.
        var isPrivate =
            b[0] == 10 ||                             // 10.0.0.0/8
            (b[0] == 172 && b[1] is >= 16 and <= 31) || // 172.16.0.0/12
            (b[0] == 192 && b[1] == 168) ||           // 192.168.0.0/16
            (b[0] == 169 && b[1] == 254);             // 169.254.0.0/16 link-local

        if (!isPrivate)
        {
            error = "Only loopback and private LAN addresses are accepted.";
            return false;
        }

        return true;
    }
}
