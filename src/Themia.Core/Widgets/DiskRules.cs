using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>One volume as the disk meter names it: its root, its label, and whether it is removable.</summary>
/// <param name="Root">The drive root, <c>C:\</c>.</param>
/// <param name="Label">The volume label ("System"), or empty.</param>
/// <param name="Removable">A removable drive rather than a fixed one.</param>
public readonly record struct DiskVolume(string Root, string Label, bool Removable);

/// <summary>Reads and writes per second on one volume, from the performance counters.</summary>
public readonly record struct DiskRates(double Read, double Write)
{
    /// <summary>[S-1820] The drive's throughput: reads and writes together, what the reading and the chart show.</summary>
    public double Total => Read + Write;
}

/// <summary>[S-1820] What one disk sample amounts to.</summary>
public enum DiskReadingState
{
    /// <summary>A rate: the reading and the next point on the chart.</summary>
    Rate = 0,

    /// <summary>The counters' first collection, which lists no rates yet: a gap, never a 0.</summary>
    Waiting = 1,

    /// <summary>The counters answer, but not for this drive (unplugged, or not a volume they list).</summary>
    DriveMissing = 2,

    /// <summary>The counters could not be opened: the meter is unavailable.</summary>
    NoCounters = 3,
}

/// <summary>[S-1820] One disk sample: its state, and the rates when it is <see cref="DiskReadingState.Rate"/>.</summary>
public readonly record struct DiskReading(DiskReadingState State, DiskRates Rates = default);

/// <summary>
/// [S-1160]..[S-1163] [S-1820]..[S-1822] The disk meter's rules: how a drive is named and matched
/// to its performance-counter instance, whose throughput the meter shows and how it is charted.
/// Pure, so each is tested.
/// </summary>
public static class DiskRules
{
    /// <summary>
    /// [S-1821] The chart's lowest top: 1 MB/s. A drive is never truly idle — the system writes a
    /// few hundred KB/s in the background — and a lower top drew that murmur as a busy disk.
    /// </summary>
    public const double ChartFloor = 1024 * 1024;

    /// <summary>[S-1821] Throughput against the window's peak, rounded up to a doubling of <see cref="ChartFloor"/>.</summary>
    public static ChartScale Scale => ChartScale.Doubling(ChartFloor);

    /// <summary>
    /// [S-6271] The read and write bars' shares: each rate against the top the graph would have over
    /// the recent totals and this sample's (<see cref="Scale"/>), so the bars and the graph read on
    /// one scale and a background murmur stays a sliver.
    /// </summary>
    public static (double Read, double Write) BarShares(DiskRates rates, ReadOnlySpan<double> recentTotals)
    {
        var top = Scale.Ceiling(recentTotals, [rates.Total]);
        return (Share(rates.Read, top), Share(rates.Write, top));
    }

    private static double Share(double rate, double top) => ChartScale.Fraction(Clean(rate), top);

    /// <summary>
    /// [S-1160] The root a setting names: <c>c</c>, <c>C:</c> and <c>C:\</c> are all <c>C:\</c>.
    /// Null for anything that is not a drive letter — a folder, a UNC path, a hand-edited typo.
    /// </summary>
    public static string? Root(string? drive)
    {
        var text = drive?.Trim() ?? string.Empty;

        if (text.Length is < 1 or > 3 || !char.IsAsciiLetter(text[0]))
        {
            return null;
        }

        if (text.Length >= 2 && text[1] != ':')
        {
            return null;
        }

        if (text.Length == 3 && text[2] != '\\' && text[2] != '/')
        {
            return null;
        }

        return $"{char.ToUpperInvariant(text[0])}:\\";
    }

    /// <summary>
    /// [S-1162] The <c>\LogicalDisk(*)</c> instance for a root: <c>C:\</c> is <c>C:</c>. Matched
    /// ignoring case, because the counter names the instance however the volume manager spelled it.
    /// </summary>
    public static string Instance(string root) => (Root(root) ?? root).TrimEnd('\\');

    /// <summary>
    /// [S-1162] A drive's rates out of the counter's instances. A drive the counters do not list
    /// (just mounted, or a counter that is not answering) is null — never the system-wide total,
    /// which would put the same number on every disk widget (the original's themia-releases#49).
    /// </summary>
    public static DiskRates? RatesFor(string root, IReadOnlyDictionary<string, DiskRates> byInstance)
    {
        ArgumentNullException.ThrowIfNull(byInstance);
        return byInstance.TryGetValue(Instance(root), out var rates) ? rates : null;
    }

    /// <summary>
    /// [S-1162] Pair the read and write instance lists into rates per drive letter. The
    /// <c>_Total</c> instance and anything that is not a drive (a volume mounted in a folder shows as
    /// <c>HarddiskVolume3</c>) are left out; an instance missing from one list counts as 0 there.
    /// </summary>
    public static Dictionary<string, DiskRates> Pair(
        IEnumerable<(string Instance, double Value)> reads,
        IEnumerable<(string Instance, double Value)> writes)
    {
        ArgumentNullException.ThrowIfNull(reads);
        ArgumentNullException.ThrowIfNull(writes);

        var result = new Dictionary<string, DiskRates>(StringComparer.OrdinalIgnoreCase);

        foreach (var (instance, value) in reads)
        {
            if (Root(instance) is { } root)
            {
                var key = Instance(root);
                result[key] = new DiskRates(Clean(value), result.TryGetValue(key, out var r) ? r.Write : 0);
            }
        }

        foreach (var (instance, value) in writes)
        {
            if (Root(instance) is { } root)
            {
                var key = Instance(root);
                result[key] = new DiskRates(result.TryGetValue(key, out var r) ? r.Read : 0, Clean(value));
            }
        }

        return result;
    }

    /// <summary>[S-1163] Every drive together: the sum of the drive-letter instances (never <c>_Total</c> as well, which would count twice).</summary>
    public static DiskRates Total(IReadOnlyDictionary<string, DiskRates> byInstance)
    {
        ArgumentNullException.ThrowIfNull(byInstance);

        double read = 0, write = 0;
        foreach (var rates in byInstance.Values)
        {
            read += rates.Read;
            write += rates.Write;
        }

        return new DiskRates(read, write);
    }

    /// <summary>
    /// [S-1822] The rates the meter shows: one drive's (<paramref name="root"/>), or every drive's
    /// together. Null when there is no rate (yet), which the meter shows as a gap, never a 0.
    /// </summary>
    public static DiskRates? RatesOf(Themia.Core.Model.DiskScope scope, string root, IReadOnlyDictionary<string, DiskRates> byInstance)
    {
        ArgumentNullException.ThrowIfNull(byInstance);

        return scope == Themia.Core.Model.DiskScope.AllDrives
            ? byInstance.Count == 0 ? null : Total(byInstance)
            : RatesFor(root, byInstance);
    }

    /// <summary>
    /// [S-1820] One sample out of a collection (null when the counters could not be opened): the
    /// scope's rates, or why there are none.
    /// </summary>
    public static DiskReading Read(Themia.Core.Model.DiskScope scope, string root, IReadOnlyDictionary<string, DiskRates>? byInstance)
    {
        if (byInstance is null)
        {
            return new DiskReading(DiskReadingState.NoCounters);
        }

        if (RatesOf(scope, root, byInstance) is { } rates)
        {
            return new DiskReading(DiskReadingState.Rate, rates);
        }

        // A first collection lists nothing at all; a later one that lists other drives but not this
        // one is about a drive that is not there.
        return byInstance.Count == 0
            ? new DiskReading(DiskReadingState.Waiting)
            : new DiskReading(DiskReadingState.DriveMissing);
    }

    private static double Clean(double value) => double.IsFinite(value) && value > 0 ? value : 0;

    /// <summary>[S-1161] The header title, as Explorer names a drive: "System (C:)", "Local Disk (C:)", "USB Drive (E:)".</summary>
    public static string Title(DiskVolume volume) => Title(Strings.Current, volume);

    /// <summary><see cref="Title(DiskVolume)"/> in <paramref name="locale"/>.</summary>
    public static string Title(Locale locale, DiskVolume volume)
    {
        var letter = Instance(volume.Root);
        var name = volume.Label.Trim();

        if (name.Length == 0)
        {
            name = Strings.Get(locale, volume.Removable ? Str.WidgetsB_Disk_UsbDrive : Str.WidgetsB_Disk_LocalDisk);
        }

        return Strings.Format(locale, Str.WidgetsB_Disk_Title, ("name", name), ("letter", letter));
    }
}
