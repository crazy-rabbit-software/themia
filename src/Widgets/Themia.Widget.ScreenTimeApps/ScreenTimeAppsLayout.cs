using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the screen-time app list shows at one size ([S-3352]).</summary>
/// <param name="Rows">Whole rows of the list.</param>
/// <param name="Lines">[S-6712] The lines every row shows: the second holds the share and a window's average.</param>
/// <param name="Lead">The lead's size: the app's icon, 32 from a width of 180, else 16.</param>
/// <param name="Names">Each app's name beside its icon; without it a row is the icon and the time.</param>
public readonly record struct ScreenTimeAppsPlan(int Rows, int Lines, double Lead, bool Names);

/// <summary>[S-3352] The app list's ladder on the family's rows ([S-6711], [S-6712]).</summary>
public static class ScreenTimeAppsLayout
{
    /// <summary>From this width a row leads with the app's icon at 32 (as a list's avatar, [S-6704]).</summary>
    public const double LeadFrom = 180;

    /// <summary>
    /// From this width a list row has room for an app's name beside its icon and the longest time
    /// ("23h 59m"); under it the name was squeezed to a letter, so the row drops it for the time.
    /// </summary>
    public const double NamesFrom = 160;

    /// <summary>
    /// [S-3352] The list: as many whole rows as fit the view's area less the card's bottom inset (at
    /// most <paramref name="wanted"/>), each of one line, or of two from a regular tile both ways when
    /// the row has figures for its second line (<paramref name="figures"/>: the share, a window's
    /// average); the icon at 32 from a width of 180; the name from 160.
    /// </summary>
    public static ScreenTimeAppsPlan Apps(WidgetArea area, int wanted, bool figures)
    {
        if (!area.IsMeasured)
        {
            return new ScreenTimeAppsPlan(0, 1, FamilyShape.Icon, false);
        }

        var lines = figures ? RowRules.Lines(area, 2) : 1;
        var rows = Math.Min(Math.Max(0, wanted), RowRules.Fit(area.Height - FamilySpace.Inset, lines));
        return new ScreenTimeAppsPlan(
            rows,
            lines,
            area.Width >= LeadFrom ? FamilyShape.IconLarge : FamilyShape.Icon,
            area.Width >= NamesFrom);
    }

    /// <summary>[S-3350] Whether a row has figures for its second line: the share when shown, the average over a window when shown.</summary>
    public static bool Figures(bool window, bool showAverage, bool showPercent) => showPercent || (window && showAverage);
}
