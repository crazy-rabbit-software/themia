using System.Net;
using System.Net.Sockets;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>What the network guard decided about one address.</summary>
public enum GuardVerdict
{
    /// <summary>A public address, or the guard is off.</summary>
    Allowed = 0,

    /// <summary>Loopback, private, link-local or reserved, and no grant for this host opens it ([S-3840]).</summary>
    Blocked,

    /// <summary>The host name did not resolve here and no proxy will resolve it instead.</summary>
    Unresolvable,
}

/// <summary>[S-967] Where a request is allowed to go.</summary>
public enum NetReach
{
    /// <summary>Anywhere public; the local network only for the host a request's <see cref="Net.LocalGrant"/> names ([S-3840]).</summary>
    Internet = 0,

    /// <summary>This computer only (a loopback service such as the hardware monitor's web server).</summary>
    ThisMachine,
}

/// <summary>
/// [S-1301] The request-forgery guard the original app put in front of every address a user (or a
/// feed) supplies: the classic <c>netguard::check_url</c>, ported rule for rule.
/// <para>
/// A feed or a web page is somebody else's text, and the address it makes this app fetch is the
/// thing an attacker controls. Without a guard, a hostile feed link or favicon can make
/// the app request <c>http://127.0.0.1/…</c>, a router's admin page or the cloud metadata endpoint
/// at <c>169.254.169.254</c>. So by default an address that resolves to loopback, a private range,
/// link-local, carrier-grade NAT, a documentation range or anything reserved is refused — every
/// address the name maps to, not the first. There is no switch: [S-3840] a self-hosted feed, camera
/// or server is reached through the address the user typed for it, which earns that one host a
/// <see cref="Net.LocalGrant"/>.
/// </para>
/// <para>
/// Pure: the caller resolves the name and asks the system proxy, and hands both facts in. That is
/// also the original's [NG-07] relaxation: a request that travels through a proxy is resolved by
/// the proxy, so failing to resolve the name here says nothing about it — but an IP literal, or a
/// name that does resolve here, is still checked, because being proxied is not a bypass.
/// </para>
/// </summary>
public static class NetworkGuard
{
    /// <summary>True when <paramref name="address"/> is somewhere a widget must not reach by default.</summary>
    public static bool IsBlocked(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // [S-961] An IPv6 address that carries an IPv4 one is judged as the IPv4 address it
            // carries: IPv4-mapped (::ffff:a.b.c.d), the deprecated IPv4-compatible form
            // (::a.b.c.d) and the NAT64 well-known prefix (64:ff9b::a.b.c.d), which a NAT64
            // gateway turns back into a.b.c.d — so none of them is a way around the IPv4 rules.
            return EmbeddedV4(address) is { } carried ? IsBlockedV4(carried) : IsBlockedV6(address);
        }

        return address.AddressFamily != AddressFamily.InterNetwork || IsBlockedV4(address);
    }

    /// <summary>
    /// The verdict for one host, for a <paramref name="reach"/>: <see cref="NetReach.Internet"/> is
    /// <see cref="Judge(string, IReadOnlyList{IPAddress}?, bool, Net.LocalGrant?)"/>; <see cref="NetReach.ThisMachine"/>
    /// admits loopback only — every address, whatever grant came along, and never through a proxy.
    /// </summary>
    public static (GuardVerdict Verdict, IPAddress? Culprit) Judge(
        string host,
        IReadOnlyList<IPAddress>? resolved,
        bool proxied,
        Net.LocalGrant? grant,
        NetReach reach)
    {
        if (reach == NetReach.Internet)
        {
            return Judge(host, resolved, proxied, grant);
        }

        if (TryLiteral(host, out var literal))
        {
            return IsLoopback(literal) ? (GuardVerdict.Allowed, null) : (GuardVerdict.Blocked, literal);
        }

        if (resolved is null || resolved.Count == 0)
        {
            return (GuardVerdict.Unresolvable, null);
        }

        foreach (var address in resolved)
        {
            if (!IsLoopback(address))
            {
                return (GuardVerdict.Blocked, address);
            }
        }

        return (GuardVerdict.Allowed, null);
    }

    /// <summary>The IPv4 address an IPv6 one carries (mapped, compatible or NAT64), or null.</summary>
    public static IPAddress? EmbeddedV4(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4();
        }

        Span<byte> b = stackalloc byte[16];
        if (!address.TryWriteBytes(b, out var written) || written != 16)
        {
            return null;
        }

        // NAT64 well-known prefix 64:ff9b::/96 (RFC 6052).
        var nat64 = b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B && b[4..12].IndexOfAnyExcept((byte)0) < 0;

        // IPv4-compatible ::a.b.c.d (RFC 4291, deprecated). :: and ::1 are not IPv4 addresses.
        var compatible = b[..12].IndexOfAnyExcept((byte)0) < 0 && !(b[12] == 0 && b[13] == 0 && b[14] == 0 && b[15] <= 1);

        return nat64 || compatible ? new IPAddress(b[12..]) : null;
    }

    private static bool IsLoopback(IPAddress address)
        => IPAddress.IsLoopback(EmbeddedV4(address) ?? address);

    /// <summary>
    /// The verdict for one host.
    /// </summary>
    /// <param name="host">The URI's host (an IPv6 literal may carry its brackets).</param>
    /// <param name="resolved">Every address the name resolved to, or null when it did not resolve.</param>
    /// <param name="proxied">Whether the request goes through a proxy, which resolves the name itself.</param>
    /// <param name="grant">
    /// [S-3840] The leave the request carries, or null. It opens the local addresses a grant may open
    /// (<see cref="Net.LocalGrants.Grantable"/>) for its own host only; for any other host it is no grant.
    /// </param>
    public static (GuardVerdict Verdict, IPAddress? Culprit) Judge(
        string host,
        IReadOnlyList<IPAddress>? resolved,
        bool proxied,
        Net.LocalGrant? grant)
    {
        var granted = Net.LocalGrants.Covers(grant, host);

        bool Refused(IPAddress address) => IsBlocked(address) && !(granted && Net.LocalGrants.Grantable(address));

        // An IP literal needs no resolver and is checked as written.
        if (TryLiteral(host, out var literal))
        {
            return Refused(literal) ? (GuardVerdict.Blocked, literal) : (GuardVerdict.Allowed, null);
        }

        if (resolved is null || resolved.Count == 0)
        {
            return proxied ? (GuardVerdict.Allowed, null) : (GuardVerdict.Unresolvable, null);
        }

        // [S-962] Every address the name maps to, not the first.
        foreach (var address in resolved)
        {
            if (Refused(address))
            {
                return (GuardVerdict.Blocked, address);
            }
        }

        return (GuardVerdict.Allowed, null);
    }

    /// <summary>Whether <paramref name="host"/> is an IP literal, with or without IPv6 brackets.</summary>
    public static bool TryLiteral(string host, out IPAddress address)
    {
        var stripped = (host ?? string.Empty).Trim().TrimStart('[').TrimEnd(']');

        // IPAddress.TryParse accepts "1" and "1.2" as IPv4 shorthands, which a URI host never is
        // once System.Uri has canonicalised it; requiring a dot or a colon keeps a bare name like
        // "intranet" from being read as 0.0.0.1.
        if ((stripped.Contains('.') || stripped.Contains(':')) && IPAddress.TryParse(stripped, out var parsed))
        {
            address = parsed;
            return true;
        }

        address = IPAddress.None;
        return false;
    }

    /// <summary>The sentence a refused address is shown with.</summary>
    public static string Explain(GuardVerdict verdict, string host) => Explain(Strings.Current, verdict, host);

    /// <summary><see cref="Explain(GuardVerdict, string)"/> in <paramref name="locale"/>.</summary>
    public static string Explain(Locale locale, GuardVerdict verdict, string host) => verdict switch
    {
        GuardVerdict.Blocked => Strings.Format(locale, Str.Shell_Net_BlockedLocal, ("host", host)),
        GuardVerdict.Unresolvable => Strings.Format(locale, Str.Shell_Net_NotFound, ("host", host)),
        _ => string.Empty,
    };

    private static bool IsBlockedV4(IPAddress address)
    {
        Span<byte> o = stackalloc byte[4];
        if (!address.TryWriteBytes(o, out var written) || written != 4)
        {
            return true;
        }

        return o[0] == 127                                    // loopback 127/8
            || o[0] == 10                                     // private 10/8
            || (o[0] == 172 && (o[1] & 0xF0) == 16)           // private 172.16/12
            || (o[0] == 192 && o[1] == 168)                   // private 192.168/16
            || (o[0] == 169 && o[1] == 254)                   // link-local, incl. cloud metadata
            || o[0] == 0                                      // "this network" 0/8, incl. 0.0.0.0
            || (o[0] & 0xF0) == 0xF0                          // reserved 240/4, incl. broadcast
            || (o[0] == 100 && (o[1] & 0xC0) == 0x40)         // carrier-grade NAT 100.64/10
            || (o[0] == 192 && o[1] == 0 && o[2] == 2)        // documentation 192.0.2/24
            || (o[0] == 198 && o[1] == 51 && o[2] == 100)     // documentation 198.51.100/24
            || (o[0] == 203 && o[1] == 0 && o[2] == 113);     // documentation 203.0.113/24
    }

    private static bool IsBlockedV6(IPAddress address)
    {
        if (IPAddress.IPv6Loopback.Equals(address) || IPAddress.IPv6Any.Equals(address))
        {
            return true;
        }

        Span<byte> b = stackalloc byte[16];
        if (!address.TryWriteBytes(b, out var written) || written != 16)
        {
            return true;
        }

        return (b[0] == 0xFE && (b[1] & 0xC0) == 0x80)                    // link-local fe80::/10
            || (b[0] == 0xFE && (b[1] & 0xC0) == 0xC0)                     // site-local fec0::/10 (deprecated, still private)
            || (b[0] & 0xFE) == 0xFC                                       // unique-local fc00::/7
            || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8); // documentation 2001:db8::/32
    }
}
