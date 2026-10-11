using System.Net;
using System.Net.Sockets;
using Themia.Core.Widgets;

namespace Themia.Core.Net;

/// <summary>
/// [S-3840] Leave for one request to reach a host on this computer or the local network: given only
/// for the host of an address the user typed themselves into a setting (a widget's feed or camera
/// address, an account's mail or DAV server), and only when that host, as typed, already names the
/// local network (<see cref="LocalGrants.ForTyped(Uri)"/>). It is bound to that host: a redirect to
/// another host, or an address read out of what came back (a feed's links and pictures), carries none.
/// </summary>
/// <param name="Host">The host as typed: an IP literal or a local name, without IPv6 brackets.</param>
public sealed record LocalGrant(string Host);

/// <summary>[S-3840]..[S-3843] Who gets a <see cref="LocalGrant"/>, and what it lets through.</summary>
public static class LocalGrants
{
    /// <summary>
    /// [S-3841] The suffixes that say "this network" by name: mDNS, the router-made names, RFC 8375's
    /// home.arpa, and the names a machine calls itself. A public name (rss.example.com) is not one of
    /// them even when the local DNS points it at 192.168.x — the user cannot see where a public name
    /// resolves, so that is exactly the rebinding the guard exists to stop; type the address or the
    /// local name instead.
    /// </summary>
    private static readonly string[] LocalSuffixes =
    [
        ".local",
        ".lan",
        ".home",
        ".home.arpa",
        ".internal",
        ".localdomain",
        ".localhost",
    ];

    /// <summary>
    /// [S-3841] The grant an address the user typed earns: an IP literal of the kind a grant can open
    /// (<see cref="Grantable"/>), or a local name (a single label such as <c>nas</c>, <c>localhost</c>,
    /// or one ending in a <see cref="LocalSuffixes"/> suffix). Null for anything else: a public name,
    /// a public address (which needs no grant), a link-local or reserved literal (never opened).
    /// </summary>
    public static LocalGrant? ForTyped(Uri? typed)
    {
        if (typed is null || !typed.IsAbsoluteUri)
        {
            return null;
        }

        return ForTypedHost(typed.IdnHost);
    }

    /// <summary><see cref="ForTyped(Uri)"/> for a bare host (an IMAP server's name).</summary>
    public static LocalGrant? ForTypedHost(string? host)
    {
        var bare = Strip(host);
        if (bare.Length == 0)
        {
            return null;
        }

        if (NetworkGuard.TryLiteral(bare, out var literal))
        {
            return NetworkGuard.IsBlocked(literal) && Grantable(literal) ? new LocalGrant(bare) : null;
        }

        return IsLocalName(bare) ? new LocalGrant(bare.TrimEnd('.').ToLowerInvariant()) : null;
    }

    /// <summary>Whether <paramref name="grant"/> is for <paramref name="host"/> (case, brackets and a trailing dot aside).</summary>
    public static bool Covers(LocalGrant? grant, string? host)
        => grant is not null && host is not null && ConnectRules.SameHost(grant.Host, host);

    /// <summary>
    /// [S-3841] A name that says it is on this network: one label (<c>nas</c>, <c>printer</c>,
    /// <c>localhost</c>), or ending in a local suffix.
    /// </summary>
    public static bool IsLocalName(string host)
    {
        var name = Strip(host).TrimEnd('.');
        if (name.Length == 0 || NetworkGuard.TryLiteral(name, out _))
        {
            return false;
        }

        if (!name.Contains('.', StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var suffix in LocalSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// [S-3842] The local addresses a grant can open: loopback, the private ranges (10/8, 172.16/12,
    /// 192.168/16), carrier-grade NAT 100.64/10 (Tailscale and similar), IPv6 unique-local fc00::/7
    /// and site-local fec0::/10. Never, whatever was typed: link-local (169.254/16 with the cloud
    /// metadata address, fe80::/10), "this network" 0/8 and ::, reserved 240/4, the documentation
    /// ranges, multicast.
    /// </summary>
    public static bool Grantable(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var v4 = NetworkGuard.EmbeddedV4(address) ?? address;

        if (v4.AddressFamily == AddressFamily.InterNetwork)
        {
            Span<byte> o = stackalloc byte[4];
            if (!v4.TryWriteBytes(o, out var written) || written != 4)
            {
                return false;
            }

            return o[0] == 127
                || o[0] == 10
                || (o[0] == 172 && (o[1] & 0xF0) == 16)
                || (o[0] == 192 && o[1] == 168)
                || (o[0] == 100 && (o[1] & 0xC0) == 0x40);
        }

        if (v4.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        if (IPAddress.IPv6Loopback.Equals(v4))
        {
            return true;
        }

        Span<byte> b = stackalloc byte[16];
        if (!v4.TryWriteBytes(b, out var length) || length != 16)
        {
            return false;
        }

        return (b[0] & 0xFE) == 0xFC                       // unique-local fc00::/7
            || (b[0] == 0xFE && (b[1] & 0xC0) == 0xC0);    // site-local fec0::/10
    }

    private static string Strip(string? host) => (host ?? string.Empty).Trim().TrimStart('[').TrimEnd(']');
}
