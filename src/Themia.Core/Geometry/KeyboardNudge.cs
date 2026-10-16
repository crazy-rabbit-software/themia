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
            NudgeStep.Fine => (1 / screen.Width, 1 / screen.Height),
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
    /// [S-1970] Move a block of rects one press. The block keeps its shape; the grid applies unless
    /// it is off or the step is <see cref="NudgeStep.Fine"/>.
    /// </summary>
    public static List<FracRect> Move(IReadOnlyList<FracRect> rects, NudgeDirection direction, NudgeStep step, ScreenMetrics screen, bool snapEnabled)
    {
        ArgumentNullException.ThrowIfNull(rects);

        if (rects.Count == 0 || !screen.IsUsable)
        {
            return [.. rects];
        }

        var (dx, dy) = Delta(direction, step, screen);
        var delta = BlockGeometry.Move(rects, dx, dy, screen, snapEnabled && step != NudgeStep.Fine);
        return [.. rects.Select(r => BlockGeometry.Apply(r, delta, screen))];
    }

    /// <summary>
    /// [S-1971] Resize one rect one press: Right and Down grow it, Left and Up shrink it, always by the
    /// right or bottom edge so the top-left corner stays put. The [S-08] floor and the visible
    /// minimum ([S-6608]) stop the edge.
    /// </summary>
    public static FracRect Resize(FracRect rect, NudgeDirection direction, NudgeStep step, ScreenMetrics screen, bool snapEnabled)
    {
        if (!screen.IsUsable || !rect.IsFinite)
        {
            return rect;
        }

        var (dx, dy) = Delta(direction, step, screen);
        var handle = direction is NudgeDirection.Left or NudgeDirection.Right ? ResizeHandle.Right : ResizeHandle.Bottom;
        var resized = GridGeometry.Resize(rect, handle, dx, dy, screen);
        return GridGeometry.Settle(resized, screen, snapEnabled && step != NudgeStep.Fine, handle);
    }
}
