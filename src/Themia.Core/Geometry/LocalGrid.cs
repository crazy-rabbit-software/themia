namespace Themia.Core.Geometry;

/// <summary>
/// [S-11890] One layer of the local grid: the grid's lines inside <paramref name="Region"/> (the surface's DIPs),
/// drawn at <paramref name="Opacity"/> over the layers before it.
/// </summary>
public readonly record struct GridLevel(DipRect Region, double Opacity);

/// <summary>
/// [S-11874] The local grid of a move on the locked desktop (owner, 2026-11-15: "it should not actually go
/// into edit mode but show the grid slightly around where i drag the widget to"): only the grid lines near
/// where the moving block will land, strongest at the target box and fading out over
/// <see cref="RadiusCells"/> cells around it, and the target's own cells lit. Pure: the surface draws what
/// this says, the tests pin it.
/// <para>
/// [S-11886] The fade keeps the target box's rectangle (owner, 2026-11-15: "it should also not be round
/// anymore"): a line's strength is <see cref="Strength"/> of the point, a smooth fall from the box's edges
/// (the larger of the two axis distances) to the box grown by the radius on every side, rounded up to
/// <see cref="Steps"/> strengths (<see cref="Levels"/>); nothing at or past the radius is drawn. Lines stand where the edit-mode overlay draws them (<see cref="GridFit.LineDip"/>),
/// so a card's edge and its line share their pixels. The strongest line is <see cref="PeakOpacity"/>, and
/// the grid fades out slower than it came (<see cref="FadeOutMs"/>).
/// </para>
/// </summary>
public static class LocalGrid
{
    /// <summary>[S-11886] How far round the target box, in grid cells, the lines fade out (owner, 2026-11-15: "the fade out of the grid should be bigger"): 6, twice the first 3.</summary>
    public const double RadiusCells = 6;

    /// <summary>[S-11874] How many strengths the lines are drawn in: the falloff rounded up to these steps reads as smooth.</summary>
    public const int Steps = 10;

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
    public static double Distance(DipRect box, double x, double y)
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
    public static double Strength(DipRect box, double x, double y, double radius)
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
    public static DipRect Lit(FracRect target, ScreenMetrics screen)
    {
        if (!screen.IsUsable || !target.IsFinite)
        {
            return default;
        }

        var left = GridGeometry.WholeDip(target.X * screen.Width);
        var top = GridGeometry.WholeDip(target.Y * screen.Height);
        var right = GridGeometry.WholeDip(target.Right * screen.Width);
        var bottom = GridGeometry.WholeDip(target.Bottom * screen.Height);
        return new DipRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
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
    /// [S-11890] The local grid round <paramref name="target"/> as <see cref="Steps"/> nested layers of the whole grid,
    /// each clipped to a rectangle: layer k holds the lines where the strength (<see cref="Strength"/>) is above
    /// (k-1)/<see cref="Steps"/>, the box grown by the distance where <see cref="Falloff"/> falls to that, and is drawn
    /// at the opacity that, over the layers under it, makes a line there <see cref="PeakOpacity"/> times its strength
    /// rounded up to a step. The same picture as cutting every line into stretches at their own strengths, but the
    /// lines are drawn once for the screen and a new target only moves the clips. Empty for an unusable screen.
    /// </summary>
    public static IReadOnlyList<GridLevel> Levels(FracRect target, ScreenMetrics screen, double radiusCells = RadiusCells)
    {
        var box = Lit(target, screen);
        if (!screen.IsUsable || box.Width <= 0 || box.Height <= 0)
        {
            return [];
        }

        var radius = RadiusDip(screen, radiusCells);
        var levels = new GridLevel[Steps];
        var under = 0.0;
        for (var k = 1; k <= Steps; k++)
        {
            // Strength > (k-1)/Steps: nearer the box than where the smoothstep falls to it.
            var reach = radius * (1 - InverseSmoothstep((k - 1) / (double)Steps));
            var region = new DipRect(box.X - reach, box.Y - reach, box.Width + (2 * reach), box.Height + (2 * reach));

            // Over the layers below (together `under`), this one brings a line here to PeakOpacity * k / Steps.
            var wanted = PeakOpacity * k / Steps;
            levels[k - 1] = new GridLevel(region, 1 - ((1 - wanted) / (1 - under)));
            under = wanted;
        }

        return levels;
    }

    /// <summary>The t in 0..1 whose smoothstep (3t² - 2t³) is <paramref name="y"/> (0..1).</summary>
    internal static double InverseSmoothstep(double y)
        => 0.5 - Math.Sin(Math.Asin(1 - (2 * Math.Clamp(y, 0, 1))) / 3);

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
