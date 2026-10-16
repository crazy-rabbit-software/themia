using System.Globalization;

namespace Themia.Core.Widgets;

/// <summary>How hard a reading is pressing against its limit. The view maps it to a theme colour.</summary>
public enum GpuPressure
{
    Normal = 0,
    High = 1,
    Critical = 2,
}

/// <summary>One adapter's load for one sample.</summary>
/// <param name="Name">What the driver calls the adapter.</param>
/// <param name="Utilization">0..100: the busiest engine.</param>
public sealed record GpuReading(string Name, double Utilization)
{
    /// <summary>
    /// False while the load is not yet a real figure: the counters are a rate and need a second
    /// reading. The driver's own figure is real at once.
    /// </summary>
    public bool HasLoad { get; init; } = true;

    /// <summary>[S-6322] The engine types' loads, for the Engines style; empty while there is no load yet.</summary>
    public IReadOnlyList<GpuEngineLoad> Engines { get; init; } = [];
}

/// <summary>[S-6322] The engine types the GPU's Engines style draws, in the order it draws them.</summary>
public enum GpuEngineKind
{
    ThreeD = 0,
    Copy = 1,
    VideoEncode = 2,
    VideoDecode = 3,
}

/// <summary>[S-6322] One engine type's load: its busiest engine, 0..100.</summary>
public readonly record struct GpuEngineLoad(GpuEngineKind Kind, double Percent);

/// <summary>
/// The decisions behind the GPU meter that do not need a GPU to test: which adapters a config
/// means, how a performance-counter instance name maps onto an adapter, how one load figure is
/// made out of a few hundred engine counters.
/// </summary>
public static class GpuRules
{
    /// <summary>
    /// [S-433] The adapters a widget shows. Negative and repeated entries are dropped, order is kept,
    /// and an empty or absent list means the primary adapter — a widget never draws nothing because
    /// its selection was emptied.
    /// </summary>
    public static List<int> ResolveAdapters(IReadOnlyList<int>? raw)
    {
        var selected = SelectedAdapters(raw);
        return selected.Count > 0 ? selected : [0];
    }

    /// <summary>
    /// [S-433] The selection the settings picker ticks: the same clean-up, but no primary-adapter
    /// default. Unticking the last adapter has to leave the picker empty, not tick adapter 0 again
    /// behind the user's back — the default is a drawing concern, not a picker one.
    /// </summary>
    public static List<int> SelectedAdapters(IReadOnlyList<int>? raw)
    {
        var result = new List<int>();

        if (raw is null)
        {
            return result;
        }

        foreach (var index in raw)
        {
            if (index >= 0 && !result.Contains(index))
            {
                result.Add(index);
            }
        }

        return result;
    }

    /// <summary>[S-433] Two or more adapters stack as panels; one fills the widget.</summary>
    public static bool Stacked(int count) => count >= 2;

    /// <summary>The token an adapter's counters are filed under: <c>luid_0x{high}_0x{low}</c>, lower case.</summary>
    public static string LuidKey(uint high, uint low)
        => string.Create(CultureInfo.InvariantCulture, $"luid_0x{high:x8}_0x{low:x8}");

    /// <summary>
    /// [S-434] The adapter a counter instance belongs to.
    /// <para>
    /// Engine instances read <c>pid_1234_luid_0x0_0xabcd_phys_0_eng_3_engtype_3D</c> and memory
    /// instances <c>luid_0x0_0xabcd_phys_0</c>. The original matched with "starts with the LUID",
    /// which is never true of the <c>pid_</c>-prefixed engine names, so load collapsed to zero the
    /// moment an adapter was chosen. The token is found wherever it sits instead.
    /// </para>
    /// </summary>
    /// <returns>The lower-case LUID token, or null when the name carries none.</returns>
    public static string? LuidOf(string instance)
    {
        if (string.IsNullOrEmpty(instance))
        {
            return null;
        }

        var start = instance.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        var rest = instance[start..];
        var end = rest.IndexOf("_phys_", StringComparison.OrdinalIgnoreCase);
        var token = end < 0 ? rest : rest[..end];

        return token.ToLowerInvariant();
    }

    /// <summary>
    /// The engine a counter instance measures, within its adapter — <c>phys_0_eng_3</c> — so the
    /// per-process instances of one engine can be added up. Null when the name has no engine part.
    /// </summary>
    public static string? EngineOf(string instance)
    {
        if (string.IsNullOrEmpty(instance))
        {
            return null;
        }

        var start = instance.IndexOf("phys_", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        var rest = instance[start..];
        var end = rest.IndexOf("_engtype_", StringComparison.OrdinalIgnoreCase);
        var token = end < 0 ? rest : rest[..end];

        return token.Contains("_eng_", StringComparison.OrdinalIgnoreCase) ? token.ToLowerInvariant() : null;
    }

    /// <summary>
    /// [S-434] One load figure for an adapter, from its engine counters.
    /// <para>
    /// The counters are per process <i>and</i> per engine, so each engine's share is the sum across
    /// processes, and the adapter's load is its busiest engine — the figure Task Manager shows. The
    /// original summed only the 3D engine, which reads near zero while the card decodes video or
    /// runs compute. Junk readings (negative, not finite) count as nothing; the result is 0..100.
    /// </para>
    /// </summary>
    public static double Utilization(IEnumerable<(string Instance, double Percent)> engines, string luid)
    {
        ArgumentNullException.ThrowIfNull(engines);

        var perEngine = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var (instance, percent) in engines)
        {
            if (!double.IsFinite(percent) || percent <= 0)
            {
                continue;
            }

            if (!string.Equals(LuidOf(instance), luid, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var engine = EngineOf(instance) ?? string.Empty;
            perEngine[engine] = perEngine.GetValueOrDefault(engine) + percent;
        }

        var busiest = 0.0;
        foreach (var value in perEngine.Values)
        {
            busiest = Math.Max(busiest, value);
        }

        return Math.Clamp(busiest, 0, 100);
    }

    /// <summary>
    /// [S-435] The load the meter reports: the driver's own figure when the driver gives one, the
    /// counters otherwise, clamped to 0..100.
    /// <para>
    /// On NVIDIA cards the driver's figure wins because the counters under-report a game running in
    /// borderless full-screen — a pegged card read as 4%. Elsewhere there is only the counter.
    /// </para>
    /// </summary>
    public static double ChooseUtilization(double? driver, double counters)
    {
        var value = driver is { } d && double.IsFinite(d) ? d : counters;
        return double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;
    }

    /// <summary>
    /// [S-6322] Which of the engines the Engines style draws a counter instance measures, from its
    /// <c>_engtype_</c> part (<c>engtype_3D</c>, <c>engtype_VideoDecode</c>); null for every other
    /// type (compute, video processing, security, a vendor's own names), which no row shows.
    /// </summary>
    public static GpuEngineKind? EngineKindOf(string instance)
    {
        if (string.IsNullOrEmpty(instance))
        {
            return null;
        }

        var start = instance.IndexOf("_engtype_", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        var type = instance[(start + "_engtype_".Length)..].Replace(" ", string.Empty, StringComparison.Ordinal);

        return type.ToUpperInvariant() switch
        {
            "3D" => GpuEngineKind.ThreeD,
            "COPY" => GpuEngineKind.Copy,
            "VIDEOENCODE" => GpuEngineKind.VideoEncode,
            "VIDEODECODE" => GpuEngineKind.VideoDecode,
            _ => null,
        };
    }

    /// <summary>
    /// [S-6322] Each engine type's load on one adapter, in <see cref="GpuEngineKind"/> order, for
    /// the types the adapter has (an engine counted at 0 % is still there; one never listed is not
    /// drawn). A type's load is its busiest engine: per-engine sums across processes, then the
    /// maximum, as the adapter's own load is ([S-434]); 0..100.
    /// </summary>
    public static IReadOnlyList<GpuEngineLoad> EngineLoads(IEnumerable<(string Instance, double Percent)> engines, string luid)
    {
        ArgumentNullException.ThrowIfNull(engines);

        var perEngine = new Dictionary<string, (GpuEngineKind Kind, double Sum)>(StringComparer.Ordinal);

        foreach (var (instance, percent) in engines)
        {
            if (EngineKindOf(instance) is not { } kind
                || !string.Equals(LuidOf(instance), luid, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var engine = EngineOf(instance) ?? kind.ToString();
            var add = double.IsFinite(percent) && percent > 0 ? percent : 0;
            perEngine[engine] = (kind, perEngine.GetValueOrDefault(engine, (kind, 0)).Sum + add);
        }

        var busiest = new SortedDictionary<GpuEngineKind, double>();

        foreach (var (kind, sum) in perEngine.Values)
        {
            busiest[kind] = Math.Max(busiest.GetValueOrDefault(kind), Math.Clamp(sum, 0, 100));
        }

        return [.. busiest.Select(pair => new GpuEngineLoad(pair.Key, pair.Value))];
    }

    /// <summary>[S-432] Load pressure: high from 70%, critical from 90%.</summary>
    public static GpuPressure LoadPressure(double percent) => percent switch
    {
        >= 90 => GpuPressure.Critical,
        >= 70 => GpuPressure.High,
        _ => GpuPressure.Normal,
    };

    /// <summary>
    /// [S-430] The adapter's name as a card title: the model, without the vendor and trademark noise
    /// the driver reports — "NVIDIA GeForce RTX 4060 Ti" is "RTX 4060 Ti", "Intel(R) UHD Graphics
    /// 770" is "UHD Graphics 770". A name that would be left empty is kept whole.
    /// </summary>
    public static string ShortName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "GPU";
        }

        var cleaned = name
            .Replace("(R)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("(TM)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("®", string.Empty, StringComparison.Ordinal)
            .Replace("™", string.Empty, StringComparison.Ordinal);

        string[] noise = ["NVIDIA", "GeForce", "AMD", "Intel", "Microsoft"];
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => !noise.Contains(word, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        return words.Length == 0 ? name.Trim() : string.Join(' ', words);
    }
}
