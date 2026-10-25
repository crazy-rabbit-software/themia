namespace Themia.Core.Geometry;

/// <summary>
/// [S-3522] Where a menu that drops down from an edit-bar button stands (the settings menu under ⚙):
/// under the bar, its start edge level with the button's (right to left: its end edges), slid along
/// to stay inside the work area, and stepped down clear of what it would cover (the view bar)
/// wherever the work area leaves room below; never above the work area's top. Pure: the surface
/// measures the button and the menu and asks.
/// </summary>
public static class DropDownPlacement
{
    /// <summary>The gap under the bar, as a flyout hangs from its button, and from what it steps clear of.</summary>
    public const double Gap = 4;

    /// <summary>
    /// The menu's top-left corner: <paramref name="button"/> and <paramref name="bar"/> where they are,
    /// the menu <paramref name="width"/> × <paramref name="height"/>, inside <paramref name="area"/>,
    /// clear of each of <paramref name="avoid"/> where there is room.
    /// </summary>
    public static (double X, double Y) Place(PixelRect button, PixelRect bar, double width, double height, PixelRect area, IReadOnlyList<PixelRect> avoid, bool rightToLeft)
    {
        ArgumentNullException.ThrowIfNull(avoid);

        var x = rightToLeft ? button.X + button.Width - width : button.X;
        x = Math.Max(area.X, Math.Min(x, area.X + area.Width - width));

        var y = bar.Y + bar.Height + Gap;
        var bottom = area.Y + area.Height;

        // Step down below each thing in the way, top first; one pass is enough, as each step only goes down.
        foreach (var other in avoid.OrderBy(r => r.Y))
        {
            if (Overlaps(x, y, width, height, other) && other.Y + other.Height + Gap + height <= bottom)
            {
                y = other.Y + other.Height + Gap;
            }
        }

        y = Math.Max(area.Y, Math.Min(y, bottom - height));
        return (x, y);
    }

    private static bool Overlaps(double x, double y, double width, double height, PixelRect other)
        => x < other.X + other.Width && other.X < x + width && y < other.Y + other.Height && other.Y < y + height;
}
