using Themia.Core.Shell;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-1970]..[S-1972] The arrow keys in edit mode: move the selection as one block, or resize every
/// selected widget by its right or bottom edge. Built on the drag's own rules, so a key lands
/// exactly where a drag of the same distance would: the block snapped and kept in reach of the
/// allowed area ([S-1584], [S-6608]), a resize settling only the edge it moves ([S-31a]).
/// </summary>
public static class KeyboardNudge
{
    /// <summary>One press as a fractional delta on each axis.</summary>
    public static (double Dx, double Dy) Delta(NudgeDirection direction, NudgeStep step, ScreenMetrics screen)
    {
        if (!screen.IsUsable)
        {
            return (0, 0);
        }

        var (cols, rows) = GridGeometry.GridDims(screen);
        var (dx, dy) = step switch
        {
            NudgeStep.Far => ((double)KeyboardNudgeDefaults.FarCells / cols, (double)KeyboardNudgeDefaults.FarCells / rows),
            _ => (1.0 / cols, 1.0 / rows),
        };

        return direction switch
        {
            NudgeDirection.Left => (-dx, 0),
            NudgeDirection.Right => (dx, 0),
            NudgeDirection.Up => (0, -dy),
            _ => (0, dy),
        };
    }

    /// <summary>
    /// [S-1970] Move a block of rects one press. The block keeps its shape, on the grid ([S-06]).
    /// </summary>
    /// <param name="apart">[S-11959] With widgets kept apart: the widgets that stay, which the block may not come over (it then stays).</param>
    public static List<FracRect> Move(IReadOnlyList<FracRect> rects, NudgeDirection direction, NudgeStep step, ScreenMetrics screen, IReadOnlyList<FracRect>? apart = null)
    {
        ArgumentNullException.ThrowIfNull(rects);

        if (rects.Count == 0 || !screen.IsUsable)
        {
            return [.. rects];
        }

        var (dx, dy) = Delta(direction, step, screen);
        var delta = BlockGeometry.Move(rects, dx, dy, screen);
        if (apart is not null)
        {
            delta = BlockGeometry.Apart(rects, delta, default, apart, screen);
        }

        return [.. rects.Select(r => BlockGeometry.Apply(r, delta, screen))];
    }

    /// <summary>
    /// [S-1971] Resize one rect one press: Right and Down grow it, Left and Up shrink it, always by the
    /// right or bottom edge so the top-left corner stays put. The [S-08] floor and the visible
    /// minimum ([S-6608]) stop the edge. [S-2445] The widget's sizing's <paramref name="limits"/> stop it
    /// at its Min and Max too; [S-14500] a press is one whole cell on the grid.
    /// </summary>
    public static FracRect Resize(FracRect rect, NudgeDirection direction, NudgeStep step, ScreenMetrics screen, SizeLimits? limits = null)
    {
        if (!screen.IsUsable || !rect.IsFinite)
        {
            return rect;
        }

        var (dx, dy) = Delta(direction, step, screen);
        var handle = direction is NudgeDirection.Left or NudgeDirection.Right ? ResizeHandle.Right : ResizeHandle.Bottom;
        if (limits is not null && dx != 0)
        {
            dx = Math.Sign(dx) * Press(limits.Width, dx);
        }

        if (limits is not null && dy != 0)
        {
            dy = Math.Sign(dy) * Press(limits.Height, dy);
        }

        var resized = GridGeometry.Resize(rect, handle, dx, dy, screen);
        return GridGeometry.Settle(resized, screen, handle, limits);
    }

    /// <summary>
    /// How far one press moves the edge on an axis: [S-11807] on the grid at least one whole cell (a finer press would
    /// round back up to the cells it started from).
    /// </summary>
    private static double Press(AxisLimits axis, double asked)
    {
        var press = Math.Abs(asked);
        return axis.OnGrid ? Math.Max(press, 1.0 / axis.Lines) : press;
    }
}
