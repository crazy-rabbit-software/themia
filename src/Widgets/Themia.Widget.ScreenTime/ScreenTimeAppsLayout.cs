using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the screen-time app list shows ([S-11510]): its config's, at every size.</summary>
/// <param name="Rows">The rows its card holds ([S-14500]); the config's before it is measured.</param>
/// <param name="Lines">[S-6712] The lines every row shows: the second holds the share and a window's average.</param>
/// <param name="Lead">The lead's size: the app's icon, 16 on a one-line row, 32 on a two-line one.</param>
public readonly record struct ScreenTimeAppsPlan(int Rows, int Lines, double Lead);

/// <summary>[S-11510] The app list on the family's rows ([S-6711], [S-6712]), from its config alone.</summary>
public static class ScreenTimeAppsLayout
{
    /// <summary>
    /// [S-11510] [S-14500] The list as its config says it and its card holds it: <paramref name="held"/> rows (what its room
    /// holds at their optimal, <see cref="RowsIn"/>; the config's Rows, its natural, before it is measured), its lines (two
    /// only with figures for the second), the icon by the lines.
    /// </summary>
    public static ScreenTimeAppsPlan Apps(ScreenTimeAppsConfig config, int held = 0)
    {
        ArgumentNullException.ThrowIfNull(config);
        var shown = config.RowsShown;
        return new ScreenTimeAppsPlan(held > 0 ? held : shown.Rows, shown.Lines, shown.Lines > 1 ? FamilyShape.IconLarge : FamilyShape.Icon);
    }

    /// <summary>
    /// [S-11510] [S-14810] How many rows of <paramref name="lines"/> a list <paramref name="height"/> tall reaches at their
    /// one fixed size (owner, 2026-11-20: "the last item will be cutoff to some extent then"), at least one; none before it
    /// is measured.
    /// </summary>
    public static int RowsIn(double height, int lines, ItemGrid grid)
        => double.IsFinite(height) && height > 0 ? ItemSize.Count(height, FamilyShape.RowHeight(lines), grid, KitSizing.MinRows, KitSizing.MaxRows) : 0;
}
