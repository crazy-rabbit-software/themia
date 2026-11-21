using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-13040] A fence as a region of the desktop icon grid, in fractions of its display: its title bar is
/// <see cref="FenceGrid.TitleCells"/> rows of the widget grid ([S-06]), its body under it whole icon boxes (a desktop
/// icon's box on that grid, <see cref="DesktopGrid.IconSize"/>), so every cell of a fence is a cell a free icon could
/// stand on: same box, same pitch, on the same grid lines. The cells themselves are <see cref="FenceGrid.Layout"/>'s, the
/// one layout. Pure.
/// </summary>
public static class FenceCells
{
    /// <summary>[S-13040] The title bar's height on <paramref name="screen"/>, as a fraction of it: whole grid rows; none on an unusable screen.</summary>
    public static double TitleHeight(ScreenMetrics screen)
        => screen.IsUsable ? FenceGrid.TitleCells / (double)GridGeometry.GridDims(screen).Rows : 0;

    /// <summary>
    /// [S-13040] The body of the fence at <paramref name="fence"/> as cells of <paramref name="box"/>, from its scroll; a
    /// rolled-up fence's body has no height (it shows none).
    /// </summary>
    public static FenceGrid.Layout Of(FracRect fence, IHoldsDesktopItems config, ScreenMetrics screen, (double W, double H) box)
    {
        ArgumentNullException.ThrowIfNull(config);
        var title = TitleHeight(screen);
        return new FenceGrid.Layout(fence.X, fence.Y + title, fence.W, config.RolledUp ? 0 : Math.Max(0, fence.H - title), box.W, box.H, Math.Max(0, config.Scroll));
    }

    /// <summary>The box of item <paramref name="index"/> of <paramref name="layout"/>, as a rect of its display.</summary>
    public static FracRect Cell(FenceGrid.Layout layout, int index)
    {
        var (x, y, w, h) = layout.Cell(index);
        return new FracRect(x, y, w, h);
    }

    /// <summary>
    /// [S-13040] [S-13012] A fence's rect as the grid holds it: its corner on the nearest grid lines, its width whole boxes
    /// (at least one), its height its title bar's rows and whole boxes under it (at least one), or the title bar's alone
    /// while rolled up. Unchanged on an unusable screen or box.
    /// </summary>
    public static FracRect Snap(FracRect fence, bool rolledUp, ScreenMetrics screen, (double W, double H) box)
    {
        if (!screen.IsUsable || !fence.IsFinite || !(box.W > 0) || !(box.H > 0))
        {
            return fence;
        }

        var grid = GridGeometry.GridDims(screen);
        var title = FenceGrid.TitleCells / (double)grid.Rows;
        var across = Math.Max(1, GridSpace.Nearest(fence.W / box.W));
        var down = Math.Max(1, GridSpace.Nearest((fence.H - title) / box.H));
        return new FracRect(
            grid.X(grid.Col(fence.X)),
            grid.Y(grid.Row(fence.Y)),
            across * box.W,
            rolledUp ? title : title + (down * box.H));
    }
}
