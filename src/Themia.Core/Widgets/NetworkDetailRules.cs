using System.Net;
using System.Net.Sockets;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-2417] Where the public-address lookup stands.</summary>
public enum PublicAddressState
{
    /// <summary>The setting is off: the widget never asks.</summary>
    Off = 0,

    /// <summary>Asked, no answer yet.</summary>
    Checking = 1,

    /// <summary>An answer: <see cref="PublicAddressReading.Address"/>.</summary>
    Known = 2,

    /// <summary>Asked and failed (offline, refused, a captive portal's page).</summary>
    Failed = 3,
}

/// <summary>[S-2417] The public address as the widget shows it.</summary>
public readonly record struct PublicAddressReading(PublicAddressState State, string? Address = null);

/// <summary>[S-2415]..[S-2419] The network details widget's rules, pure.</summary>
public static class NetworkDetailRules
{
    /// <summary>"1023.9 MB/s".</summary>
    public const double HeroCharacters = 11;

    /// <summary>[S-2418] How long a public address is trusted before it is asked for again.</summary>
    public static readonly TimeSpan PublicAddressLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// [S-2418] The body of the public-address service, validated: one IPv4 or IPv6 address and
    /// nothing else, normalised. Anything else (an error page, a captive portal) is null — the text is
    /// somebody else's, and it would be shown on the desktop.
    /// </summary>
    public static string? PublicAddress(string? body)
    {
        var text = body?.Trim();

        if (string.IsNullOrEmpty(text) || text.Length > 45 || !text.All(c => char.IsAsciiHexDigit(c) || c is '.' or ':'))
        {
            return null;
        }

        return IPAddress.TryParse(text, out var address) ? address.ToString() : null;
    }

    /// <summary>
    /// [S-2419] The address worth showing for an adapter: its first IPv4 unicast that is not
    /// link-local (169.254/16), else its first global IPv6. Null when it has neither.
    /// </summary>
    public static string? LocalAddress(IEnumerable<IPAddress> unicast)
    {
        ArgumentNullException.ThrowIfNull(unicast);
        var all = unicast.ToList();

        var v4 = all.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IsLinkLocal4(a) && !IPAddress.IsLoopback(a));
        if (v4 is not null)
        {
            return v4.ToString();
        }

        var v6 = all.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6 && !a.IsIPv6LinkLocal && !a.IsIPv6SiteLocal && !IPAddress.IsLoopback(a));
        return v6?.ToString();
    }

    private static bool IsLinkLocal4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }

    /// <summary>"↓ 1.2 MB/s".</summary>
    public static string DownRate(Locale locale, double down)
        => Strings.Format(locale, Str.Details_Network_DownRate, ("rate", Formatting.Rate(locale, down)));

    /// <summary>"↑ 30 KB/s".</summary>
    public static string UpRate(Locale locale, double up)
        => Strings.Format(locale, Str.Details_Network_UpRate, ("rate", Formatting.Rate(locale, up)));

    /// <summary>
    /// [S-2416] Whether an adapter earns a row: it has an address or has moved data since Themia
    /// started. Windows keeps idle virtual adapters up (Wi-Fi Direct's "Local Area Connection* 10", a
    /// switch with nothing on it); their zeros are still in the totals, but a row each is noise.
    /// </summary>
    public static bool Listed(InterfaceRate adapter, IReadOnlyDictionary<string, string> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return addresses.ContainsKey(adapter.Id) || adapter.SessionDown > 0 || adapter.SessionUp > 0 || adapter.Total > 0;
    }

    /// <summary>[S-2415] A link speed as Windows Settings writes it: "1 Gbps", "2.5 Gbps", "866.7 Mbps", "100 Mbps".</summary>
    public static string LinkSpeed(Locale locale, long bitsPerSecond)
    {
        if (bitsPerSecond <= 0)
        {
            return "--";
        }

        if (bitsPerSecond >= 1_000_000_000)
        {
            var gbps = bitsPerSecond / 1e9;
            return Strings.Format(locale, Str.Details_Network_Gbps, ("value", LocaleFormat.Number(locale, gbps, Digits(gbps))));
        }

        var mbps = bitsPerSecond / 1e6;
        return Strings.Format(locale, Str.Details_Network_Mbps, ("value", LocaleFormat.Number(locale, mbps, Digits(mbps))));
    }

    /// <summary>One decimal only when it says something: "866.7", but "100", not "100.0".</summary>
    private static int Digits(double value) => Math.Round(value, 1) % 1 == 0 ? 0 : 1;

    /// <summary>[S-2416] The row symbol for an adapter's kind.</summary>
    public static RowSymbol Symbol(AdapterMedium medium) => medium switch
    {
        AdapterMedium.Ethernet => RowSymbol.Wired,
        AdapterMedium.Wireless => RowSymbol.Wireless,
        AdapterMedium.Cellular => RowSymbol.Cellular,
        _ => RowSymbol.Network,
    };

    /// <summary>
    /// [S-2415] The sheet, as Task Manager's Wi-Fi/Ethernet page reads: download as the hero, captioned
    /// with the connection it comes over (the busiest adapter, "Download over Wi-Fi"); then upload,
    /// the busiest adapter's local address, what this session received and sent, its link speed and
    /// (when asked for) the public address — most wanted first, so a small tile keeps the rates. One
    /// list row per counted adapter with an address or traffic, busiest first ([S-2416]): its kind's
    /// icon, its name, download (and upload beside it on a wide row), and its address and link speed under it. No sample yet is waiting; a sample with no adapter is
    /// "No connection".
    /// </summary>
    /// <param name="addresses">Each adapter's local address by its id, as <see cref="LocalAddress"/> chose it.</param>
    public static DetailSheet Sheet(Locale locale, ThroughputSample? sample, IReadOnlyDictionary<string, string> addresses, PublicAddressReading publicAddress)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        if (sample is null)
        {
            return DetailSheet.Waiting;
        }

        var busiest = sample.Interfaces.Count > 0 ? sample.Interfaces[0] : (InterfaceRate?)null;
        var facts = new List<DetailFact>(6)
        {
            new(Strings.Get(locale, Str.Details_Network_Upload), Formatting.Rate(locale, sample.Up)),
        };

        if (busiest is { } top && addresses.TryGetValue(top.Id, out var local))
        {
            facts.Add(new(Strings.Get(locale, Str.Details_Network_LocalAddress), local));
        }

        facts.Add(new(Strings.Get(locale, Str.Details_Network_Received), Formatting.Bytes(locale, sample.SessionDown)));
        facts.Add(new(Strings.Get(locale, Str.Details_Network_Sent), Formatting.Bytes(locale, sample.SessionUp)));

        if (busiest is { LinkBitsPerSecond: > 0 } link)
        {
            facts.Add(new(Strings.Get(locale, Str.Details_Network_LinkSpeed), LinkSpeed(locale, link.LinkBitsPerSecond)));
        }

        switch (publicAddress.State)
        {
            case PublicAddressState.Known when publicAddress.Address is { } address:
                facts.Add(new(Strings.Get(locale, Str.Details_Network_PublicAddress), address));
                break;
            case PublicAddressState.Checking:
                facts.Add(new(Strings.Get(locale, Str.Details_Network_PublicAddress), Strings.Get(locale, Str.Details_Network_Checking), false));
                break;
            case PublicAddressState.Failed:
            case PublicAddressState.Known:
                facts.Add(DetailFact.Unavailable(Strings.Get(locale, Str.Details_Network_PublicAddress)));
                break;
            default:
                break;
        }

        var rows = sample.Interfaces
            .Where(i => Listed(i, addresses))
            .Select(i => new DetailRow(
                i.Id,
                i.Name,
                DownRate(locale, i.Down),
                Second: UpRate(locale, i.Up),
                Detail: AdapterLine(locale, addresses.GetValueOrDefault(i.Id), i.LinkBitsPerSecond),
                Symbol: Symbol(i.Medium)))
            .ToList();

        if (busiest is not { } over)
        {
            return new DetailSheet("--", Strings.Get(locale, Str.Details_Network_NoConnection), facts, rows, HeroAvailable: false);
        }

        var caption = Strings.Format(locale, Str.Details_Network_Over, ("adapter", over.Name));
        return new DetailSheet(Formatting.Rate(locale, sample.Down), caption, facts, rows);
    }

    /// <summary>[S-2416] The line under an adapter: its address and link speed, whichever it has; null with neither.</summary>
    public static string? AdapterLine(Locale locale, string? address, long linkBitsPerSecond)
    {
        var speed = linkBitsPerSecond > 0 ? LinkSpeed(locale, linkBitsPerSecond) : null;
        return (address, speed) switch
        {
            ({ } a, { } s) => $"{a} · {s}",
            ({ } a, null) => a,
            (null, { } s) => s,
            _ => null,
        };
    }
}
