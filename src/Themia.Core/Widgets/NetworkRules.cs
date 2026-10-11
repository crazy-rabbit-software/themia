using System.Net;
using System.Text;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>One adapter's cumulative byte counters at one instant.</summary>
/// <param name="Id">Stable identity (the adapter GUID): a rename is still the same adapter.</param>
/// <param name="Name">The friendly name ("Wi-Fi", "Ethernet").</param>
public readonly record struct InterfaceCounters(string Id, string Name, long Received, long Sent);

/// <summary>One adapter's rates over the last interval, and what it moved since the meter started.</summary>
public readonly record struct InterfaceRate(string Id, string Name, double Down, double Up, long SessionDown, long SessionUp)
{
    public double Total => Down + Up;
}

/// <summary>[S-1190] One throughput sample: the totals, each adapter's share, and the session totals.</summary>
public sealed record ThroughputSample(double Down, double Up, IReadOnlyList<InterfaceRate> Interfaces, long SessionDown, long SessionUp);

/// <summary>
/// [S-1190] Per-adapter rates and session totals from cumulative counters.
/// <para>
/// Per adapter, not one sum: an adapter's counters reset when it is disabled, reconnected or
/// replaced, and with a single sum one adapter's reset made the whole delta negative and threw away
/// the other adapters' real traffic for that interval. Here only the adapter that reset reads 0 for
/// one sample (its new counters become its baseline), and an adapter that appears starts from a
/// baseline rather than reporting its whole lifetime as one second's traffic.
/// </para>
/// <para>
/// Single-threaded by contract (the UI thread's tick). Session totals only ever add positive
/// deltas, so a reset never makes "since start" go backwards.
/// </para>
/// </summary>
public sealed class ThroughputTracker
{
    private readonly Dictionary<string, Baseline> _baselines = new(StringComparer.OrdinalIgnoreCase);
    private long _lastTicks;
    private bool _started;
    private long _sessionDown;
    private long _sessionUp;

    /// <summary>Take one set of counters at <paramref name="ticksMs"/> (a monotonic clock) and return the rates since the last.</summary>
    public ThroughputSample Update(IReadOnlyList<InterfaceCounters> counters, long ticksMs)
    {
        ArgumentNullException.ThrowIfNull(counters);

        var elapsed = _started ? (ticksMs - _lastTicks) / 1000.0 : 0;
        _lastTicks = ticksMs;
        _started = true;

        var rates = new List<InterfaceRate>(counters.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        double down = 0, up = 0;

        foreach (var c in counters)
        {
            if (!seen.Add(c.Id))
            {
                continue;
            }

            double rateDown = 0, rateUp = 0;

            if (_baselines.TryGetValue(c.Id, out var last))
            {
                var dr = c.Received - last.Received;
                var ds = c.Sent - last.Sent;

                // A fall is a reset, not traffic: this adapter reads 0 once and starts again.
                if (dr >= 0 && ds >= 0)
                {
                    last = last with { SessionDown = last.SessionDown + dr, SessionUp = last.SessionUp + ds };
                    _sessionDown += dr;
                    _sessionUp += ds;

                    if (elapsed > 0)
                    {
                        rateDown = dr / elapsed;
                        rateUp = ds / elapsed;
                    }
                }
            }
            else
            {
                last = new Baseline(c.Received, c.Sent, 0, 0);
            }

            _baselines[c.Id] = last with { Received = c.Received, Sent = c.Sent };
            down += rateDown;
            up += rateUp;
            rates.Add(new InterfaceRate(c.Id, c.Name, rateDown, rateUp, last.SessionDown, last.SessionUp));
        }

        // An adapter that went away keeps no baseline: if it comes back, its counters are new.
        foreach (var id in _baselines.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _baselines.Remove(id);
        }

        return new ThroughputSample(down, up, NetworkRules.ByActivity(rates), _sessionDown, _sessionUp);
    }

    private readonly record struct Baseline(long Received, long Sent, long SessionDown, long SessionUp);
}

/// <summary>[S-1190]..[S-1192] The network meter's rules as pure functions.</summary>
public static class NetworkRules
{
    /// <summary>
    /// [S-1192] A packet-capture filter mirror (Npcap/WinPcap binds one per adapter, e.g.
    /// "Ethernet-Npcap Packet Driver (NPCAP)-0000"): it re-reports the adapter beneath it, so
    /// counting it doubles every figure (the original's themia-releases#33). Matched on the name or
    /// the description, case-insensitively. The standalone "Npcap Loopback Adapter" is real,
    /// distinct traffic and is not a mirror.
    /// </summary>
    public static bool IsCaptureMirror(string? name, string? description)
        => Contains(name, "pcap packet driver") || Contains(description, "pcap packet driver");

    /// <summary>
    /// [S-1192] Whether an adapter's traffic is the machine's network traffic: up, not loopback or
    /// a tunnel (the machine talking to itself, or a VPN counted twice), and not a capture mirror.
    /// </summary>
    public static bool Counts(bool up, bool loopbackOrTunnel, string? name, string? description)
        => up && !loopbackOrTunnel && !IsCaptureMirror(name, description);

    /// <summary>
    /// [S-1192] An NDIS filter binding listed as an adapter of its own: "WiFi-QoS Packet
    /// Scheduler-0000", "WiFi-WFP Native MAC Layer LightWeight Filter-0000", a vendor's capture
    /// filter. Windows names each one "&lt;adapter&gt;-&lt;filter&gt;-NNNN" and every one re-reports the
    /// adapter's own counters, so counting them multiplied the machine's throughput by however many
    /// filters were bound (seven on the machine this was found on). Recognised by that shape, against
    /// the names of the other adapters, so an adapter merely called "Lab-0001" is not dropped.
    /// </summary>
    public static bool IsFilterBinding(string name, IEnumerable<string> adapterNames)
    {
        ArgumentNullException.ThrowIfNull(adapterNames);

        if (name.Length < 6 || name[^5] != '-' || !name[^4..].All(char.IsAsciiDigit))
        {
            return false;
        }

        return adapterNames.Any(other => other.Length > 0
            && !string.Equals(other, name, StringComparison.OrdinalIgnoreCase)
            && name.Length > other.Length + 6
            && name.StartsWith(other + "-", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Busiest first; ties (all idle) by name, so the list does not shuffle at rest.</summary>
    public static IReadOnlyList<InterfaceRate> ByActivity(IEnumerable<InterfaceRate> rates)
        => rates.OrderByDescending(r => r.Total)
            .ThenByDescending(r => r.SessionDown + r.SessionUp)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool Contains(string? text, string what)
        => text is not null && text.Contains(what, StringComparison.OrdinalIgnoreCase);
}
