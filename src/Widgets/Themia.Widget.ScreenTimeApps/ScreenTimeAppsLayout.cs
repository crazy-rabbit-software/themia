namespace Themia.Core.Widgets;

/// <summary>What the screen-time app list shows at one size ([S-3352]).</summary>
/// <param name="Rows">Whole rows of the list.</param>
/// <param name="Average">Each app's per-day average beside its time (a window only).</param>
/// <param name="Percent">Each app's share of the total.</param>
/// <param name="Names">Each app's name beside its icon; without it a row is the icon and the time.</param>
public readonly record struct ScreenTimeAppsPlan(int Rows, bool Average, bool Percent, bool Names);

/// <summary>[S-3352] The app list's ladder.</summary>
public static class ScreenTimeAppsLayout
{
    /// <summary>From this width a list row has room for an app's average beside its time.</summary>
    public const double AverageFrom = 300;

    /// <summary>From this width a list row has room for an app's share.</summary>
    public const double PercentFrom = 200;

    /// <summary>
    /// From this width a list row has room for an app's name beside its icon and the longest time
    /// ("23h 59m"); under it the name was squeezed to a letter, so the row drops it for the time.
    /// </summary>
    public const double NamesFrom = 160;

    /// <summary>
    /// [S-3352] The list: as many whole rows as fit (at most <paramref name="wanted"/>), then the
    /// columns each row has width for — the time always, the name from 160, the share from 200, a
    /// window's average from 300.
    /// </summary>
    public static ScreenTimeAppsPlan Apps(WidgetArea area, double rowHeight, int wanted, bool window, bool showAverage, bool showPercent)
    {
        if (!area.IsMeasured)
        {
            return new ScreenTimeAppsPlan(0, false, false, false);
        }

        var rows = Math.Min(Math.Max(0, wanted), Disclosure.WholeRows(area.Height - ScreenTimeLayout.Gap, rowHeight));
        return new ScreenTimeAppsPlan(
            rows,
            window && showAverage && area.Width >= AverageFrom,
            showPercent && area.Width >= PercentFrom,
            area.Width >= NamesFrom);
    }
}
