using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-12813] The forecast's default: the days it shows, nothing read for any.</summary>
public static class ForecastDefaults
{
    /// <summary><paramref name="days"/> days from <paramref name="today"/>, no sky and no figures (each drawn "--").</summary>
    public static IReadOnlyList<ForecastDay> Unread(DateOnly today, int days)
        => [.. Enumerable.Range(0, Math.Max(0, days)).Select(i => new ForecastDay(today.AddDays(i), null, null, null, null))];

    /// <summary>
    /// [S-16441] What the problem glyph says: a forecast kept through a failed refresh its <paramref name="stale"/> lines; with
    /// none, a failure's <paramref name="failure"/> where no state's row says it (<paramref name="stated"/>: a card one row
    /// tall, WR-10); else nothing.
    /// </summary>
    public static IReadOnlyList<string> Problems(IReadOnlyList<string> stale, IReadOnlyList<string> failure, bool stated)
    {
        ArgumentNullException.ThrowIfNull(stale);
        ArgumentNullException.ThrowIfNull(failure);
        return stale.Count > 0 ? stale : stated ? [] : failure;
    }
}
