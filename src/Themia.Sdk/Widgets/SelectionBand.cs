namespace Themia.Core.Widgets;

/// <summary>A rectangle between two pointer positions, in one space: a view's own DIPs, or the screen's pixels for the desktop's band ([S-5544]).</summary>
public readonly record struct BandRect(double X1, double Y1, double X2, double Y2)
{
    public double Left => Math.Min(X1, X2);

    public double Top => Math.Min(Y1, Y2);

    public double Width => Math.Abs(X2 - X1);

    public double Height => Math.Abs(Y2 - Y1);

    /// <summary>Whether a box at (x, y, w, h) overlaps the band at all, as the desktop's band selects.</summary>
    public bool Hits(double x, double y, double w, double h)
        => x < Left + Width && x + w > Left && y < Top + Height && y + h > Top;
}

/// <summary>[S-6625] How a band combines with what was selected when it started.</summary>
public enum BandMode
{
    /// <summary>No modifier: the band's items, nothing else.</summary>
    Replace,

    /// <summary>Shift: the band's items added to the earlier selection.</summary>
    Add,

    /// <summary>Ctrl: each item under the band flips its earlier state, as the desktop's Ctrl+drag does.</summary>
    Toggle,
}

/// <summary>An item's box, in the same DIPs as the band.</summary>
public readonly record struct ItemBox(double X, double Y, double Width, double Height);

/// <summary>
/// [S-6624]/[S-6625] The desktop's rubber band (marquee), drawn from empty desktop ([S-5544]; no
/// widget draws one inside its own box): how far the pointer must travel before a press is a band, how the band combines with the
/// selection, and which items it covers. Where the band is, across the displays, is Core's <c>DesktopBand</c>.
/// Pure; the look is <c>SelectionChrome</c>'s ([S-6627]).
/// </summary>
public static class SelectionBand
{
    /// <summary>A 5 DIP dead zone, the desktop's drag rectangle: below it a press is still a click.</summary>
    public const double Threshold = 5;

    /// <summary>Whether a pointer that moved (dx, dy) from its press has left the dead zone.</summary>
    public static bool PastThreshold(double dx, double dy) => (dx * dx) + (dy * dy) >= Threshold * Threshold;

    /// <summary>[S-6625] Ctrl toggles, Shift adds (Ctrl wins when both are held), neither replaces.</summary>
    public static BandMode Mode(bool ctrl, bool shift) => ctrl ? BandMode.Toggle : shift ? BandMode.Add : BandMode.Replace;

    /// <summary>[S-6624] The items whose box the band overlaps at all, in order (positions in <paramref name="boxes"/>).</summary>
    public static IReadOnlyList<int> Hits(BandRect band, IReadOnlyList<ItemBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        var hits = new List<int>();

        for (var i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];

            if (box.Width > 0 && box.Height > 0 && band.Hits(box.X, box.Y, box.Width, box.Height))
            {
                hits.Add(i);
            }
        }

        return hits;
    }
}
