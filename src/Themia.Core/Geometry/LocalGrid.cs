namespace Themia.Core.Geometry;

/// <summary>One stretch of a grid line the local grid draws, in the surface's DIPs, with its strength (0..1).</summary>
public readonly record struct GridSegment(double X1, double Y1, double X2, double Y2, double Alpha);

/// <summary>
/// [S-11874] The local grid of a move on the locked desktop (owner, 2026-11-15: "it should not actually go
/// into edit mode but show the grid slightly around where i drag the widget to"): only the grid lines near
/// where the moving block will land, strongest at the target box and fading out over
/// <see cref="RadiusCells"/> cells around it, and the target's own cells lit. Pure: the surface draws what
/// this says, the tests pin it.
/// <para>
/// [S-11886] The fade keeps the target box's rectangle (owner, 2026-11-15: "it should also not be round
/// anymore"): a line's strength is <see cref="Strength"/> of the point, a smooth fall from the box's edges
/// (the larger of the two axis distances) to the box grown by the radius on every side. Each line is cut into
/// <see cref="SegmentsPerCell"/> stretches a cell, each at the strength of its middle; a stretch at or past
/// the radius is not drawn. Lines stand where the edit-mode overlay draws them (<see cref="GridFit.LineDip"/>),
/// so a card's edge and its line share their pixels. The strongest line is <see cref="PeakOpacity"/>, and
/// the grid fades out slower than it came (<see cref="FadeOutMs"/>).
/// </para>
/// </summary>
public static class LocalGrid
{
    /// <summary>[S-11886] How far round the target box, in grid cells, the lines fade out (owner, 2026-11-15: "the fade out of the grid should be bigger"): 6, twice the first 3.</summary>
    public const double RadiusCells = 6;

    /// <summary>[S-11886] How many stretches each cell of a line is cut into, each at its own strength: the fade's edge is smooth.</summary>
    public const int SegmentsPerCell = 4;

    /// <summary>
    /// [S-11886] The strongest line's opacity, on the target box (owner, 2026-11-15: "should be less visible"):
    /// 0.2, half the first local grid's 0.4, about the edit-mode mesh's 0.18 ([S-06b]).
    /// </summary>
    public const double PeakOpacity = 0.2;

    /// <summary>How long the local grid takes to fade in as a move starts, in milliseconds.</summary>
    public const double FadeInMs = 120;

    /// <summary>[S-11886] How long it takes to fade out as the block lands (owner: "fade out a little slower"), in milliseconds.</summary>
    public const double FadeOutMs = 360;

    /// <summary>
    /// The strength of a line stretch <paramref name="distance"/> DIPs from the target, for a fade over
    /// <paramref name="radius"/> DIPs: 1 on it, falling smoothly (smoothstep) to 0 at the radius and past
    /// it. A non-positive radius draws the target alone.
    /// </summary>
    public static double Falloff(double distance, double radius)
    {
        if (!double.IsFinite(distance) || distance <= 0)
        {
            return 1;
        }

        if (!(radius > 0) || distance >= radius)
        {
            return 0;
        }

        var t = 1 - (distance / radius);
        return t * t * (3 - (2 * t));
    }

    /// <summary>The distance in DIPs from (<paramref name="x"/>, <paramref name="y"/>) to <paramref name="box"/>: 0 on or inside it.</summary>
    public static double Distance(PixelRect box, double x, double y)
    {
        var dx = Math.Max(0, Math.Max(box.X - x, x - (box.X + box.Width)));
        var dy = Math.Max(0, Math.Max(box.Y - y, y - (box.Y + box.Height)));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// [S-11886] The strength (0..1) at (<paramref name="x"/>, <paramref name="y"/>) of the rectangular fade
    /// about <paramref name="box"/> over <paramref name="radius"/> DIPs: 1 on or inside the box, then
    /// <see cref="Falloff"/> of the point's distance from the box's edges taken per axis as the larger of the
    /// two, so the fade keeps the box's rectangle and grows evenly outward from it (no ellipse).
    /// </summary>
    public static double Strength(PixelRect box, double x, double y, double radius)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            return 0;
        }

        var dx = Math.Max(0, Math.Max(box.X - x, x - (box.X + box.Width)));
        var dy = Math.Max(0, Math.Max(box.Y - y, y - (box.Y + box.Height)));
        return Falloff(Math.Max(dx, dy), radius > 0 && double.IsFinite(radius) ? radius : 0);
    }

    /// <summary>
    /// The target box in the surface's DIPs, on the lines the grid draws: what the local grid lights and
    /// measures from. Empty for an unusable screen.
    /// </summary>
    public static PixelRect Lit(FracRect target, ScreenMetrics screen)
    {
        if (!screen.IsUsable || !target.IsFinite)
        {
            return default;
        }

        var left = GridGeometry.WholeDip(target.X * screen.Width);
        var top = GridGeometry.WholeDip(target.Y * screen.Height);
        var right = GridGeometry.WholeDip(target.Right * screen.Width);
        var bottom = GridGeometry.WholeDip(target.Bottom * screen.Height);
        return new PixelRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>The fade's radius in DIPs on <paramref name="screen"/>: <paramref name="radiusCells"/> of the grid's wider pitch.</summary>
    public static double RadiusDip(ScreenMetrics screen, double radiusCells = RadiusCells)
    {
        if (!screen.IsUsable)
        {
            return 0;
        }

        var (cols, rows) = GridGeometry.GridDims(screen);
        return Math.Max(0, radiusCells) * Math.Max(screen.Width / cols, screen.Height / rows);
    }

    /// <summary>
    /// The stretches of grid line to draw round <paramref name="target"/> (the box the moving block lands
    /// in, fractions of the screen), each a <see cref="SegmentsPerCell"/>th of a cell long with its strength
    /// (<see cref="Strength"/>); none past the fade, none off the screen. Empty for an unusable screen.
    /// </summary>
    public static IReadOnlyList<GridSegment> Lines(FracRect target, ScreenMetrics screen, double radiusCells = RadiusCells)
    {
        var box = Lit(target, screen);
        if (!screen.IsUsable || box.Width <= 0 || box.Height <= 0)
        {
            return [];
        }

        var (cols, rows) = GridGeometry.GridDims(screen);
        var pitchX = screen.Width / cols;
        var pitchY = screen.Height / rows;
        var radius = RadiusDip(screen, radiusCells);

        // The lines and cells that can reach: the box grown by the radius, on the screen.
        var c0 = Math.Max(0, (int)Math.Floor((box.X - radius) / pitchX));
        var c1 = Math.Min(cols, (int)Math.Ceiling((box.X + box.Width + radius) / pitchX));
        var r0 = Math.Max(0, (int)Math.Floor((box.Y - radius) / pitchY));
        var r1 = Math.Min(rows, (int)Math.Ceiling((box.Y + box.Height + radius) / pitchY));

        double X(int c) => GridFit.LineDip(c, cols, screen.Width);
        double Y(int r) => GridFit.LineDip(r, rows, screen.Height);

        var segments = new List<GridSegment>();
        const int n = SegmentsPerCell;

        // Vertical lines, each cell of them cut in n.
        for (var c = c0; c <= c1; c++)
        {
            var x = X(c);
            for (var r = r0; r < r1; r++)
            {
                var (top, bottom) = (Y(r), Y(r + 1));
                for (var k = 0; k < n; k++)
                {
                    var (y1, y2) = (top + ((bottom - top) * k / n), top + ((bottom - top) * (k + 1) / n));
                    var alpha = Strength(box, x, (y1 + y2) / 2, radius);
                    if (alpha > 0)
                    {
                        segments.Add(new GridSegment(x, y1, x, y2, alpha));
                    }
                }
            }
        }

        // Horizontal lines, each cell of them cut in n.
        for (var r = r0; r <= r1; r++)
        {
            var y = Y(r);
            for (var c = c0; c < c1; c++)
            {
                var (left, right) = (X(c), X(c + 1));
                for (var k = 0; k < n; k++)
                {
                    var (x1, x2) = (left + ((right - left) * k / n), left + ((right - left) * (k + 1) / n));
                    var alpha = Strength(box, (x1 + x2) / 2, y, radius);
                    if (alpha > 0)
                    {
                        segments.Add(new GridSegment(x1, y, x2, y, alpha));
                    }
                }
            }
        }

        return segments;
    }

    /// <summary>
    /// [S-11881] The outlines a move on the locked desktop shows (owner, 2026-11-15: "when moving the widgets
    /// around in non edit mode it should show the borders too"): every moving widget's at full strength, and
    /// each other widget's at the local grid's strength where it comes nearest the target box
    /// (<see cref="Strength"/> at the point of its box nearest the target's centre: 1 where it overlaps the
    /// target), so the neighbours' marks fade with the grid; a widget past the fade is left out. Keyed by
    /// widget id; <paramref name="others"/> are the standing widgets' rects. Empty for an unusable screen.
    /// </summary>
    public static Dictionary<string, double> Borders(FracRect target, IEnumerable<string> moving, IEnumerable<(string Id, FracRect Rect)> others, ScreenMetrics screen, double radiusCells = RadiusCells)
    {
        ArgumentNullException.ThrowIfNull(moving);
        ArgumentNullException.ThrowIfNull(others);
        var shown = new Dictionary<string, double>(StringComparer.Ordinal);
        var box = Lit(target, screen);
        if (!screen.IsUsable || box.Width <= 0 || box.Height <= 0)
        {
            return shown;
        }

        foreach (var id in moving)
        {
            shown[id] = 1;
        }

        var radius = RadiusDip(screen, radiusCells);
        var (cx, cy) = (box.X + (box.Width / 2), box.Y + (box.Height / 2));
        foreach (var (id, rect) in others)
        {
            if (shown.ContainsKey(id) || !rect.IsFinite)
            {
                continue;
            }

            var other = Lit(rect, screen);
            var x = Math.Clamp(cx, other.X, other.X + other.Width);
            var y = Math.Clamp(cy, other.Y, other.Y + other.Height);
            var alpha = Strength(box, x, y, radius);
            if (alpha > 0)
            {
                shown[id] = alpha;
            }
        }

        return shown;
    }
}
