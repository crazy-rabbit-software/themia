using System.Globalization;
using Themia.Core.SystemData;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>[S-433] [S-11000] What every GPU atom asks of the hub and how its adapter is repaired: one rule for the reading, the history and the engines.</summary>
public static class GpuInterests
{
    /// <summary>[S-2513] Every adapter's sample, and the chosen adapter's series.</summary>
    public static SystemInterests Of(int adapter)
    {
        var key = FeedKey.Create(SystemSource.Gpu);
        return new SystemInterests([key], [new SeriesInterest(key, Math.Max(0, adapter).ToString(CultureInfo.InvariantCulture))]);
    }

    /// <summary>[S-433] A position that is none (negative, hand-edited) is the first adapter, loudly.</summary>
    public static IReadOnlyList<string> Repair(WidgetId kind, int adapter, Action<int> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (adapter >= 0)
        {
            return [];
        }

        set(0);
        return [$"{kind}: adapter {adapter} is not a position; using the first"];
    }
}
