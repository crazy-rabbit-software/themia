using System.Globalization;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-6310] Each logical processor's load, from <c>\Processor Information(*)\% Processor Time</c>:
/// the counter Task Manager's per-core graphs read, and the same measure (time not idle) as the
/// meter's total from <c>GetSystemTimes</c>, so the cores and the number agree.
/// </summary>
public static class CpuCoreRules
{
    /// <summary>
    /// The loads in processor order (group, then number), 0..1. Instances read <c>"0,7"</c>; the
    /// totals (<c>"_Total"</c>, <c>"0,_Total"</c>) and any name that is not a processor are left out,
    /// and a reading that is not a number is null (that core is drawn unknown, never as 0 %).
    /// </summary>
    public static IReadOnlyList<double?> Loads(IEnumerable<(string Instance, double Percent)> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var cores = new List<(int Group, int Number, double? Load)>();
        var seen = new HashSet<(int, int)>();

        foreach (var (instance, percent) in readings)
        {
            if (Parse(instance) is not { } id || !seen.Add(id))
            {
                continue;
            }

            cores.Add((id.Group, id.Number, double.IsFinite(percent) ? Math.Clamp(percent / 100, 0, 1) : null));
        }

        cores.Sort((a, b) => a.Group != b.Group ? a.Group.CompareTo(b.Group) : a.Number.CompareTo(b.Number));
        return [.. cores.Select(c => c.Load)];
    }

    /// <summary>A processor instance's group and number: <c>"1,3"</c> is (1, 3); a total or anything else is null.</summary>
    public static (int Group, int Number)? Parse(string? instance)
    {
        if (string.IsNullOrEmpty(instance))
        {
            return null;
        }

        var comma = instance.IndexOf(',', StringComparison.Ordinal);
        if (comma <= 0 || comma == instance.Length - 1)
        {
            return null;
        }

        return int.TryParse(instance.AsSpan(0, comma), NumberStyles.None, CultureInfo.InvariantCulture, out var group)
            && int.TryParse(instance.AsSpan(comma + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? (group, number)
            : null;
    }
}
