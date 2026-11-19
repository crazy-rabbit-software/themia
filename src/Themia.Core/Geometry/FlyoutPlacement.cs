using Themia.Sdk.Kit;
using M = Themia.Sdk.Kit.ShellMetrics.Flyout;

namespace Themia.Core.Geometry;

/// <summary>[S-2325] Where a page's flyout stands (DIPs, the surface's): its box and where along its top the beak's tip is.</summary>
public readonly record struct FlyoutSpot(double X, double Y, double Width, double Height, double Tip);

/// <summary>
/// [S-2344] A window in the selection's page through which a selected widget under it is seen: its box in
/// the panel's own DIPs (0, 0 its top-left, the beak not counted) and which of its corners are the panel's
/// round corners (rounded with them; every other corner is square).
/// </summary>
public readonly record struct ShineWindow(PixelRect Box, bool TopLeft, bool TopRight, bool BottomRight, bool BottomLeft);

/// <summary>
/// [S-2325] [S-2326] A page's flyout hung from its entry, Karpo's (karpo-app, docs/widgets.md §578, §584;
/// <c>FlyoutPlacement</c>, <c>Beak</c>): 580 wide, centred on its entry, 12 under the bar, pushed in to
/// stay 12 from the work area's sides; as tall as its page up to 70 % of the work area (then it scrolls)
/// and never past the work area's foot less that margin; its beak's tip over the entry's middle, kept on
/// the straight of the top (the corner's radius and half the beak in from either end), so a flyout
/// pushed in at a screen edge still points at its entry. Pure: the surface measures and asks.
/// </summary>
public static class FlyoutPlacement
{
    /// <summary>
    /// The flyout of a page <paramref name="content"/> DIPs tall, hung from <paramref name="entry"/> on
    /// <paramref name="bar"/>, inside <paramref name="area"/> (the work area).
    /// </summary>
    public static FlyoutSpot Place(PixelRect entry, PixelRect bar, PixelRect area, double content)
    {
        var width = Math.Min(M.Width, Math.Max(0, area.Width - (2 * M.EdgeMargin)));
        var middle = entry.X + (entry.Width / 2.0);
        var x = middle - (width / 2);
        x = Math.Max(area.X + M.EdgeMargin, Math.Min(x, area.X + area.Width - M.EdgeMargin - width));

        var y = bar.Y + bar.Height + M.Gap;
        var room = Math.Min(Math.Floor(area.Height * M.MostShare), area.Y + area.Height - M.EdgeMargin - y);
        var height = Math.Max(0, Math.Min(Math.Ceiling(content), room));
        return new FlyoutSpot(Math.Round(x), y, width, height, Tip(middle - Math.Round(x), width));
    }

    /// <summary>
    /// [S-11707] The add page under <paramref name="bar"/> (Karpo's §593): exactly as wide as the bar and
    /// flush with its sides, the flyout's gap under it, no beak (its tip NaN); as tall as its page
    /// (<paramref name="content"/>) up to 70 % of <paramref name="area"/>, never past its foot less the margin.
    /// </summary>
    public static FlyoutSpot Under(PixelRect bar, PixelRect area, double content)
    {
        var y = bar.Y + bar.Height + M.Gap;
        var room = Math.Min(Math.Floor(area.Height * M.MostShare), area.Y + area.Height - M.EdgeMargin - y);
        var height = Math.Max(0, Math.Min(Math.Ceiling(content), room));
        return new FlyoutSpot(bar.X, y, bar.Width, height, double.NaN);
    }

    /// <summary>
    /// [S-13707] A page's flyout as wide as the bar (the Unplaced icons page, <see cref="Settings.SettingsPages.BarWide"/>): the
    /// add page's box (<see cref="Under"/>: the bar's width, flush with its sides, as tall as its page up to 70 % of the work
    /// area), still hung from its entry: its beak's tip over <paramref name="entry"/>'s middle, kept on the straight of the top.
    /// </summary>
    public static FlyoutSpot Wide(PixelRect entry, PixelRect bar, PixelRect area, double content)
    {
        var spot = Under(bar, area, content);
        return spot with { Tip = Tip(entry.X + (entry.Width / 2.0) - spot.X, spot.Width) };
    }

    /// <summary>
    /// [S-2344] Where the selected widgets (<paramref name="widgets"/>, the surface's DIPs as their cards are
    /// drawn now) lie under the panel of a flyout standing at <paramref name="spot"/>: one window per covered
    /// part, cut to the panel, never two over the same spot (a later widget gives up what an earlier one
    /// already shows, so the ground is thinned once), side by side windows of one height or one width made
    /// one. A widget beside the panel or only under its beak has none.
    /// </summary>
    public static IReadOnlyList<ShineWindow> ShineThrough(FlyoutSpot spot, IReadOnlyList<PixelRect> widgets)
    {
        ArgumentNullException.ThrowIfNull(widgets);
        var panel = new PixelRect(0, 0, spot.Width, spot.Height);
        var pieces = new List<PixelRect>();
        foreach (var widget in widgets)
        {
            if (GridGeometry.Intersect(new PixelRect(widget.X - spot.X, widget.Y - spot.Y, widget.Width, widget.Height), panel) is not { } covered)
            {
                continue;
            }

            var left = new List<PixelRect> { covered };
            foreach (var earlier in pieces)
            {
                left = [.. left.SelectMany(part => Without(part, earlier))];
            }

            pieces.AddRange(left);
        }

        Merge(pieces);
        return [.. pieces.Select(p => new ShineWindow(
            p,
            TopLeft: p.X <= 0 && p.Y <= 0,
            TopRight: p.X + p.Width >= panel.Width && p.Y <= 0,
            BottomRight: p.X + p.Width >= panel.Width && p.Y + p.Height >= panel.Height,
            BottomLeft: p.X <= 0 && p.Y + p.Height >= panel.Height))];
    }

    /// <summary><paramref name="a"/> less <paramref name="b"/>: up to four boxes (a band above, one below, one either side).</summary>
    private static IEnumerable<PixelRect> Without(PixelRect a, PixelRect b)
    {
        if (GridGeometry.Intersect(a, b) is not { } cut)
        {
            yield return a;
            yield break;
        }

        if (cut.Y > a.Y)
        {
            yield return new PixelRect(a.X, a.Y, a.Width, cut.Y - a.Y);
        }

        if (cut.Y + cut.Height < a.Y + a.Height)
        {
            yield return new PixelRect(a.X, cut.Y + cut.Height, a.Width, a.Y + a.Height - (cut.Y + cut.Height));
        }

        if (cut.X > a.X)
        {
            yield return new PixelRect(a.X, cut.Y, cut.X - a.X, cut.Height);
        }

        if (cut.X + cut.Width < a.X + a.Width)
        {
            yield return new PixelRect(cut.X + cut.Width, cut.Y, a.X + a.Width - (cut.X + cut.Width), cut.Height);
        }
    }

    /// <summary>Two boxes that share a whole side made one, until none do: no seam where joined widgets meet.</summary>
    private static void Merge(List<PixelRect> pieces)
    {
        for (var merged = true; merged;)
        {
            merged = false;
            for (var i = 0; i < pieces.Count && !merged; i++)
            {
                for (var j = i + 1; j < pieces.Count && !merged; j++)
                {
                    if (Joined(pieces[i], pieces[j]) is { } one)
                    {
                        pieces[i] = one;
                        pieces.RemoveAt(j);
                        merged = true;
                    }
                }
            }
        }
    }

    private static PixelRect? Joined(PixelRect a, PixelRect b)
    {
        if (a.Y == b.Y && a.Height == b.Height && (a.X + a.Width == b.X || b.X + b.Width == a.X))
        {
            return new PixelRect(Math.Min(a.X, b.X), a.Y, a.Width + b.Width, a.Height);
        }

        if (a.X == b.X && a.Width == b.Width && (a.Y + a.Height == b.Y || b.Y + b.Height == a.Y))
        {
            return new PixelRect(a.X, Math.Min(a.Y, b.Y), a.Width, a.Height + b.Height);
        }

        return null;
    }

    /// <summary>[S-2326] The beak's tip <paramref name="at"/> DIPs along a top <paramref name="width"/> wide, held to its straight; its middle where it is too short.</summary>
    public static double Tip(double at, double width)
    {
        var least = ShellMetrics.OverlayRadius + (M.BeakWidth / 2);
        var most = width - least;
        return most < least ? width / 2 : Math.Clamp(at, least, most);
    }
}
