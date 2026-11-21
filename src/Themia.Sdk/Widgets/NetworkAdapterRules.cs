using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-14961] One network adapter a network kind's Adapter line offers: as Windows' network connections name it.
/// </summary>
/// <param name="Id">Its id (Windows' GUID): what a config stores, so a rename is still the same adapter.</param>
/// <param name="Name">Its name as Windows shows it ("Wi-Fi", "Ethernet 2").</param>
/// <param name="Medium">What kind of adapter it is.</param>
/// <param name="Connected">Whether it is up now: one that is not is offered all the same, its figures "--" while it is down.</param>
public sealed record NetworkAdapterEntry(string Id, string Name, AdapterMedium Medium, bool Connected);

/// <summary>
/// [S-14960]..[S-14964] The network kinds' Adapter setting, pure: which adapters the line lists, the line's options, what a
/// chosen adapter reads from the hub's one throughput sample (its own rates, totals, address and link), and what an
/// adapter that is not there shows: its facts "--" with the reason, never another adapter's.
/// </summary>
public static class NetworkAdapterRules
{
    /// <summary>A stored adapter id as it is compared: trimmed, null for Automatic (none, or blank).</summary>
    public static string? Normalise(string? id) => string.IsNullOrWhiteSpace(id) ? null : id.Trim();

    /// <summary>
    /// [S-14961] Whether Windows' adapter is one the line offers: what the throughput counts ([S-1192]) whether it is up
    /// or not (loopback, a tunnel, a capture mirror and a filter binding never are), and no Wi-Fi Direct virtual adapter
    /// (Windows' own "Local Area Connection* 1", which its settings do not list either).
    /// </summary>
    public static bool Offered(bool loopbackOrTunnel, string name, string? description, IEnumerable<string> adapterNames)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(adapterNames);
        return !loopbackOrTunnel
            && name.Length > 0
            && !NetworkRules.IsCaptureMirror(name, description)
            && !NetworkRules.IsFilterBinding(name, adapterNames)
            && !(description?.Contains("Wi-Fi Direct Virtual Adapter", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>[S-14961] The adapters in the line's order: connected first, then by name as Windows sorts them (case-blind).</summary>
    public static IReadOnlyList<NetworkAdapterEntry> Ordered(IEnumerable<NetworkAdapterEntry> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        return [.. adapters
            .DistinctBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(a => a.Connected)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Whether two lists offer the same: the line is built again only when they do not ([S-14962]).</summary>
    public static bool Same(IReadOnlyList<NetworkAdapterEntry> a, IReadOnlyList<NetworkAdapterEntry> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a.SequenceEqual(b);
    }

    /// <summary>
    /// [S-14962] The Adapter line's options: Automatic (the value empty) first, then each adapter by its name (one that is
    /// down "Ethernet (not connected)"); a stored adapter that is not in the list stays offered, by the name it was chosen
    /// under, "not connected", so the choice is never silently lost. The current value is the stored id or empty.
    /// </summary>
    public static (IReadOnlyList<(string Value, string Text)> Options, string Current) Options(
        Locale locale, IReadOnlyList<NetworkAdapterEntry> adapters, string? chosen, string? chosenName)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var options = new List<(string Value, string Text)> { (string.Empty, Strings.Get(locale, Str.NetworkAdapter_Automatic)) };
        foreach (var adapter in Ordered(adapters))
        {
            options.Add((adapter.Id, adapter.Connected ? adapter.Name : NotConnected(locale, adapter.Name)));
        }

        var id = Normalise(chosen);
        if (id is null)
        {
            return (options, string.Empty);
        }

        var held = options.FindIndex(o => string.Equals(o.Value, id, StringComparison.OrdinalIgnoreCase));
        if (held < 0)
        {
            options.Add((id, NotConnected(locale, Named(locale, chosenName))));
            return (options, id);
        }

        return (options, options[held].Value);
    }

    /// <summary>The adapter <paramref name="id"/> as the line lists it, or null where it is not listed.</summary>
    public static NetworkAdapterEntry? Find(IEnumerable<NetworkAdapterEntry> adapters, string? id)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        var wanted = Normalise(id);
        return wanted is null ? null : adapters.FirstOrDefault(a => string.Equals(a.Id, wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// [S-14963] The hub's one throughput sample as one adapter reads it: that adapter's rates as the totals, its session
    /// as the session's, itself the only adapter (so the busiest is it: its address and link). Null where the sample has
    /// no such adapter (unplugged, disabled, gone): never another adapter's figures. A blank id is the sample itself.
    /// </summary>
    public static ThroughputSample? Narrow(ThroughputSample sample, string? id)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var wanted = Normalise(id);
        if (wanted is null)
        {
            return sample;
        }

        foreach (var adapter in sample.Interfaces)
        {
            if (string.Equals(adapter.Id, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return new ThroughputSample(adapter.Down, adapter.Up, [adapter], adapter.SessionDown, adapter.SessionUp);
            }
        }

        return null;
    }

    /// <summary>
    /// [S-14963] Why a chosen adapter's figures are "--": "Ethernet is not connected" (by the name it was chosen under),
    /// or, with no name kept, "The chosen adapter is not connected".
    /// </summary>
    public static string Gone(Locale locale, string? name)
        => name is { Length: > 0 }
            ? Strings.Format(locale, Str.NetworkAdapter_Gone, ("adapter", name))
            : Strings.Get(locale, Str.NetworkAdapter_GoneUnnamed);

    /// <summary>
    /// [S-14963] The network sheet ([S-2415]) as a network kind with the Adapter setting reads it: Automatic (null) is the
    /// sheet as it always was (the busiest adapter's address and link, every counted adapter's traffic); a chosen adapter
    /// is the sheet of <see cref="Narrow"/>; a chosen adapter the sample has not is every fact "--", absent, with
    /// <see cref="Gone"/> as the reason. No sample yet is waiting, whatever is chosen.
    /// </summary>
    public static DetailSheet Sheet(Locale locale, ThroughputSample? sample, IReadOnlyDictionary<string, string> addresses, PublicAddressReading publicAddress, IHoldsAdapter? held)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var id = Normalise(held?.Adapter);
        if (sample is null || id is null)
        {
            return NetworkDetailRules.Sheet(locale, sample, addresses, publicAddress);
        }

        if (Narrow(sample, id) is { } one)
        {
            return NetworkDetailRules.Sheet(locale, one, addresses, publicAddress);
        }

        var why = Gone(locale, held?.AdapterName);
        DetailFact Fact(Str label, string key) => DetailFact.Unavailable(Strings.Get(locale, label)) with { Key = key };
        return new DetailSheet(
            "--",
            why,
            [
                Fact(Str.Details_Network_Download, "download"),
                Fact(Str.Details_Network_Upload, "upload"),
                Fact(Str.Details_Network_LocalAddress, "localAddress"),
                Fact(Str.Details_Network_Received, "received"),
                Fact(Str.Details_Network_Sent, "sent"),
                Fact(Str.Details_Network_LinkSpeed, "linkSpeed"),
            ],
            [],
            Absent: true,
            Note: why,
            HeroAvailable: false);
    }

    /// <summary>
    /// [S-14963] [S-1190] What the hub's network series reads for <paramref name="instance"/> (an adapter's id; empty for
    /// every counted adapter together): download (the value) and upload (the second), or why there is none: no
    /// statistics, no adapter at all ("no connection"), or the chosen one not there (<see cref="Gone"/>, unnamed: the
    /// series knows the id alone).
    /// </summary>
    public static MeterReading Reading(Locale locale, ThroughputSample? sample, string? instance)
    {
        if (sample is null)
        {
            return MeterReading.Unavailable(Strings.Get(locale, Str.WidgetsB_Meters_Unavailable));
        }

        if (Normalise(instance) is not null)
        {
            if (Narrow(sample, instance) is not { } one)
            {
                return MeterReading.Unavailable(Gone(locale, null));
            }

            sample = one;
        }
        else if (sample.Interfaces.Count == 0)
        {
            return MeterReading.Unavailable(Strings.Get(locale, Str.WidgetsB_Network_NoConnection));
        }

        // The fraction only marks a sample: the chart is window-scaled and plots the values.
        return new MeterReading(
            ChartScale.Fraction(sample.Down, Sources.NetworkChartFloor),
            Formatting.Rate(locale, sample.Down),
            true,
            Value: sample.Down,
            Second: sample.Up);
    }

    /// <summary>
    /// [S-14964] A stored adapter repaired at load: a blank id is Automatic, and a name kept without an id is dropped. An id
    /// no adapter of this PC has is kept as it is (an adapter unplugged, a dock away): it shows "--" with the reason
    /// until it is back, and the line still offers it.
    /// </summary>
    public static IReadOnlyList<string> Repair(IHoldsAdapter held, string kind)
    {
        ArgumentNullException.ThrowIfNull(held);
        var notes = new List<string>(1);
        var id = Normalise(held.Adapter);
        if (!string.Equals(id, held.Adapter, StringComparison.Ordinal))
        {
            notes.Add($"{kind}: adapter '{held.Adapter}' is not an id; using {(id is null ? "Automatic" : id)}");
            held.Adapter = id;
        }

        if (id is null && held.AdapterName is not null)
        {
            notes.Add($"{kind}: adapter name '{held.AdapterName}' without an adapter; dropped");
            held.AdapterName = null;
        }

        return notes;
    }

    private static string Named(Locale locale, string? name)
        => name is { Length: > 0 } ? name : Strings.Get(locale, Str.NetworkAdapter_Unnamed);

    private static string NotConnected(Locale locale, string name)
        => Strings.Format(locale, Str.NetworkAdapter_NotConnected, ("adapter", name));
}
