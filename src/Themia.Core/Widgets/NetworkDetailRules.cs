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

    /// <summary>"↓ 1.2 MB/s  ↑ 30 KB/s".</summary>
    public static string Rates(Locale locale, double down, double up)
        => Strings.Format(locale, Str.Details_Network_Rates, ("down", Formatting.Rate(locale, down)), ("up", Formatting.Rate(locale, up)));

    /// <summary>
    /// [S-2415] The sheet: download as the hero, upload under it, then what this session received and
    /// sent, the busiest adapter's local address and (when asked for) the public address; one list row
    /// per counted adapter, busiest first ([S-2416]), its rates and address. No sample yet is waiting;
    /// a sample with no adapter is "No connection".
    /// </summary>
    /// <param name="addresses">Each adapter's local address by its id, as <see cref="LocalAddress"/> chose it.</param>
    public static DetailSheet Sheet(Locale locale, ThroughputSample? sample, IReadOnlyDictionary<string, string> addresses, PublicAddressReading publicAddress)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        if (sample is null)
        {
            return DetailSheet.Waiting;
        }

        var facts = new List<DetailFact>(4)
        {
            new(Strings.Get(locale, Str.Details_Network_Received), Formatting.Bytes(locale, sample.SessionDown)),
            new(Strings.Get(locale, Str.Details_Network_Sent), Formatting.Bytes(locale, sample.SessionUp)),
        };

        var busiest = sample.Interfaces.FirstOrDefault(i => addresses.ContainsKey(i.Id));
        if (busiest.Id is not null && addresses.TryGetValue(busiest.Id, out var local))
        {
            facts.Add(new(Strings.Get(locale, Str.Details_Network_LocalAddress), local));
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
            .Select(i => new DetailRow(i.Id, i.Name, Rates(locale, i.Down, i.Up), Detail: addresses.GetValueOrDefault(i.Id)))
            .ToList();

        var caption = Strings.Format(locale, Str.Details_Network_Up, ("rate", Formatting.Rate(locale, sample.Up)));

        return sample.Interfaces.Count == 0
            ? new DetailSheet("--", Strings.Get(locale, Str.Details_Network_NoConnection), facts, rows, HeroAvailable: false)
            : new DetailSheet(Formatting.Rate(locale, sample.Down), caption, facts, rows);
    }
}
