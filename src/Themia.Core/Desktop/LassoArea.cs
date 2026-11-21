using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-13018] A lasso drawn over empty desktop names an area (the owner, 2026-11-19: "when creating a lasso over a certain
/// area it should show the option to add a widget over that area or add a fence over that area"): at its release a small
/// menu offers Add widget here and Create fence here. These are its rules, pure: when the area is offered, the area on
/// the grid, the icons a fence made there takes in, and a new widget laid into it.
/// </summary>
public static class LassoArea
{
    /// <summary>The smallest area offered, in grid cells each way: smaller is a selection gesture that missed, not an area.</summary>
    public const int MinCells = 2;

    /// <summary>
    /// The area a lasso of <paramref name="band"/> (fractions of its display) offers, snapped out to whole grid cells and
    /// kept on the display; null when it touched anything (<paramref name="caught"/>: then it is a selection), is smaller
    /// than <see cref="MinCells"/> cells either way, or the display is not measured.
    /// </summary>
    public static FracRect? Offered(FracRect band, int caught, ScreenMetrics screen)
    {
        if (caught > 0 || !screen.IsUsable || !band.IsFinite)
        {
            return null;
        }

        var (cols, rows) = GridGeometry.GridDims(screen);
        if ((band.W * cols) < MinCells - Lattice.Epsilon || (band.H * rows) < MinCells - Lattice.Epsilon)
        {
            return null;
        }

        return DesktopFences.Snap(band, screen);
    }

    /// <summary>
    /// The free desktop items of <paramref name="view"/> whose icon's middle lies inside <paramref name="area"/>, in the
    /// view's order: what a fence made on the area takes in (the snapped area may reach icons the band itself missed).
    /// </summary>
    public static IReadOnlyList<string> IconsInside(ViewState view, FracRect area)
    {
        ArgumentNullException.ThrowIfNull(view);
        return [.. view.Widgets
            .Where(w => w.Config is IDesktopItemConfig { Item.Length: > 0 } && Inside(w.Rect, area))
            .Select(w => ((IDesktopItemConfig)w.Config).Item)];
    }

    /// <summary>
    /// [S-2444] [S-11807] Lay <paramref name="widget"/> (new) into <paramref name="area"/>: its rect the area fitted into the
    /// kind's sizing on the grid, anchored at the area's top-left (<see cref="SizeFit.Refit"/>): as large as the area
    /// within what the kind allows, never larger; [S-14502] what it shows of a count that follows its card it reads from
    /// that box.
    /// </summary>
    public static void Fill(WidgetInstance widget, FracRect area, ScreenMetrics screen, double scale)
    {
        ArgumentNullException.ThrowIfNull(widget);
        widget.Rect = area;
        if (!screen.IsUsable)
        {
            return;
        }

        SizeFit.Refit(widget, screen, scale);
    }

    private static bool Inside(FracRect icon, FracRect area)
    {
        var x = icon.X + (icon.W / 2);
        var y = icon.Y + (icon.H / 2);
        return x >= area.X && x < area.X + area.W && y >= area.Y && y < area.Y + area.H;
    }
}
