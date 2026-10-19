namespace Themia.Core.Geometry;

/// <summary>Which side of its parent level a submenu of the properties menu stands on ([S-3733]).</summary>
public enum SubmenuSide
{
    /// <summary>Beside the parent's end edge (right, or left right to left): the first choice.</summary>
    End,

    /// <summary>Beside the parent's start edge: where the end side covers something.</summary>
    Start,

    /// <summary>Beside a level further up (the panel's far side): where both sides of the parent cover something.</summary>
    Beyond,
}

/// <summary>[S-3733] Where a submenu goes: its top-left corner, the side it took, and how much it covers (0: nothing).</summary>
public readonly record struct SubmenuSpot(double X, double Y, SubmenuSide Side, double Covered);

/// <summary>
/// [S-3733] Where a submenu of the properties menu stands: beside its parent level, its first line
/// level with the line that opened it, inside the work area, and wherever it can, covering neither
/// the selected widgets, the bars, nor its parent menu and the levels above it. Candidates in order:
/// the end side at the line, the start side at the line, then both sides slid up or down (the
/// nearest first, the end side first on a tie) to just clear of each thing in the way; then the
/// same beside the levels further up (beyond the panel, where the parent is hemmed in between the
/// panel and the screen's edge). The first that covers nothing wins, and only when every one covers
/// something the one that covers least.
/// Pure: the panel measures its levels and asks.
/// </summary>
public static class SubmenuPlacement
{
    /// <summary>
    /// How far a submenu laps over its parent's edge, as Windows draws it (over the parent's 4-DIP
    /// padding, never over a line): not counted as covering the parent.
    /// </summary>
    public const double Lap = 4;

    /// <summary>The gap a submenu slid clear of something keeps from it, and from the area's edge.</summary>
    public const double Gap = 4;

    /// <summary>
    /// [S-3733] Place a submenu of <paramref name="width"/> × <paramref name="height"/> beside
    /// <paramref name="parent"/>, its top at <paramref name="top"/> by preference, inside
    /// <paramref name="area"/>, covering as little as it can of <paramref name="avoid"/> (the
    /// selected widgets' box, the bars) and of <paramref name="levels"/> (the parent and every level
    /// above it, the panel included).
    /// </summary>
    public static SubmenuSpot Place(PixelRect parent, double top, double width, double height, PixelRect area,
        IReadOnlyList<PixelRect> avoid, IReadOnlyList<PixelRect> levels, bool rightToLeft = false)
    {
        ArgumentNullException.ThrowIfNull(avoid);
        ArgumentNullException.ThrowIfNull(levels);

        width = Math.Max(0, width);
        height = Math.Max(0, height);

        var low = area.X + Gap;
        var high = area.X + area.Width - Gap;
        double Across(double x) => Math.Max(low, Math.Min(x, high - width));

        var top0 = area.Y + Gap;
        var bottom0 = area.Y + area.Height - Gap;
        double Down(double y) => height > bottom0 - top0 ? top0 : Math.Max(top0, Math.Min(y, bottom0 - height));

        double End(PixelRect level) => Across(rightToLeft ? level.X - width + Lap : level.X + level.Width - Lap);
        double Start(PixelRect level) => Across(rightToLeft ? level.X + level.Width - Lap : level.X - width + Lap);
        (SubmenuSide Side, double X)[] sides = [(SubmenuSide.End, End(parent)), (SubmenuSide.Start, Start(parent))];
        var beyond = levels.Where(l => l != parent)
            .SelectMany(l => new (SubmenuSide Side, double X)[] { (SubmenuSide.Beyond, End(l)), (SubmenuSide.Beyond, Start(l)) })
            .Where(b => !sides.Any(s => Math.Abs(s.X - b.X) < 0.5))
            .DistinctBy(b => Math.Round(b.X))
            .ToArray();

        // The levels are counted without the lap: a submenu over its parent's padding covers no line.
        var counted = new List<PixelRect>(avoid.Count + levels.Count);
        counted.AddRange(avoid);
        counted.AddRange(levels.Select(l => new PixelRect(l.X + Lap, l.Y, Math.Max(0, l.Width - (2 * Lap)), l.Height)));

        var preferred = Down(top);

        // Slid: just under or just over each thing in the way, the nearest to the line first.
        var slid = new List<double>();
        foreach (var other in counted)
        {
            slid.Add(Down(other.Y + other.Height + Gap));
            slid.Add(Down(other.Y - Gap - height));
        }

        var ys = slid.Distinct().Where(y => y != preferred).OrderBy(y => Math.Abs(y - preferred)).ToList();
        var candidates = new List<(SubmenuSide Side, double X, double Y)>();
        foreach (var group in new[] { sides, beyond })
        {
            candidates.AddRange(group.Select(s => (s.Side, s.X, preferred)));
            foreach (var y in ys)
            {
                candidates.AddRange(group.Select(s => (s.Side, s.X, y)));
            }
        }

        SubmenuSpot? best = null;
        foreach (var (side, x, y) in candidates)
        {
            var covered = Covered(new PixelRect(x, y, width, height), counted);
            if (covered <= 0)
            {
                return new SubmenuSpot(x, y, side, 0);
            }

            if (best is not { } held || covered < held.Covered)
            {
                best = new SubmenuSpot(x, y, side, covered);
            }
        }

        return best ?? new SubmenuSpot(sides[0].X, preferred, SubmenuSide.End, 0);
    }

    /// <summary>How much of <paramref name="others"/> the rect covers: the sum of the overlaps' areas.</summary>
    public static double Covered(PixelRect rect, IEnumerable<PixelRect> others)
    {
        ArgumentNullException.ThrowIfNull(others);

        var sum = 0.0;
        foreach (var other in others)
        {
            var w = Math.Min(rect.X + rect.Width, other.X + other.Width) - Math.Max(rect.X, other.X);
            var h = Math.Min(rect.Y + rect.Height, other.Y + other.Height) - Math.Max(rect.Y, other.Y);
            if (w > 0 && h > 0)
            {
                sum += w * h;
            }
        }

        return sum;
    }
}
