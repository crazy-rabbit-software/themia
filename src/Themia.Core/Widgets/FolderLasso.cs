namespace Themia.Core.Widgets;

/// <summary>[S-6412] Where a left press in the folder list landed, which decides what dragging from it does.</summary>
public enum FolderPress
{
    /// <summary>On an item's icon or name: a click selects it, a drag carries it out ([S-1332]).</summary>
    Item,

    /// <summary>
    /// On an item's empty part (a row beside its name, a tile around its icon and label): a click
    /// selects it, a drag draws a band, as Explorer's full-row Details view does.
    /// </summary>
    ItemBlank,

    /// <summary>Between or below the items: a click clears the selection, a drag draws a band.</summary>
    Background,
}

/// <summary>[S-6413] How a band combines with what was selected when it started.</summary>
public enum BandMode
{
    /// <summary>No modifier: the band's items, nothing else.</summary>
    Replace,

    /// <summary>Shift: the band's items added to the earlier selection.</summary>
    Add,

    /// <summary>Ctrl: each item under the band flips its earlier state, as Explorer's Ctrl+drag does.</summary>
    Toggle,
}

/// <summary>
/// [S-6412]..[S-6415] The folder widget's rubber band (marquee) selection, as Explorer has it, kept
/// pure: where a band may start, how it combines with the selection, which items it covers (from the
/// layout's geometry, so items not built yet ([S-1887]) are covered too) and how fast the list scrolls
/// while the band is dragged past an edge.
/// </summary>
public static class FolderLasso
{
    /// <summary>How deep inside the list's top and bottom edge the pointer starts the scroll.</summary>
    public const double EdgeZone = 24;

    /// <summary>DIPs per second the list scrolls for each DIP the pointer is into the zone or past the edge.</summary>
    public const double Gain = 20;

    /// <summary>The fastest the list scrolls under a band, in DIPs per second.</summary>
    public const double MaxSpeed = 2400;

    /// <summary>The longest frame step taken: a stalled frame must not throw the list a page.</summary>
    public const double MaxStepSeconds = 0.1;

    /// <summary>[S-6412] The press kind from what the pointer is over.</summary>
    public static FolderPress Classify(bool onItem, bool onIconOrName)
        => !onItem ? FolderPress.Background : onIconOrName ? FolderPress.Item : FolderPress.ItemBlank;

    /// <summary>[S-6413] Ctrl toggles, Shift adds (Ctrl wins when both are held), neither replaces.</summary>
    public static BandMode Mode(bool ctrl, bool shift) => ctrl ? BandMode.Toggle : shift ? BandMode.Add : BandMode.Replace;

    /// <summary>The band kept inside the list's content (0..width, 0..height), as Explorer's stops at the view.</summary>
    public static BandRect Clamp(BandRect band, double width, double height)
    {
        static double Into(double value, double max) => Math.Clamp(value, 0, Math.Max(0, max));

        return new BandRect(Into(band.X1, width), Into(band.Y1, height), Into(band.X2, width), Into(band.Y2, height));
    }

    /// <summary>
    /// [S-6414] The items a band overlaps, in order. Items sit in a grid of <paramref name="columns"/>
    /// cells of <paramref name="cellWidth"/> × <paramref name="cellHeight"/> from the content's origin
    /// (a list is one column the content's width); a cell counts when the band overlaps it at all.
    /// </summary>
    public static IReadOnlyList<int> Hits(BandRect band, int count, int columns, double cellWidth, double cellHeight)
    {
        if (count <= 0 || cellWidth <= 0 || cellHeight <= 0)
        {
            return [];
        }

        columns = Math.Max(1, columns);
        var hits = new List<int>();
        var rows = (count + columns - 1) / columns;
        var firstRow = Math.Max(0, (int)Math.Floor(band.Top / cellHeight) - 1);
        var lastRow = Math.Min(rows - 1, (int)Math.Ceiling((band.Top + band.Height) / cellHeight) + 1);

        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var index = (row * columns) + column;

                if (index >= count)
                {
                    break;
                }

                if (band.Hits(column * cellWidth, row * cellHeight, cellWidth, cellHeight))
                {
                    hits.Add(index);
                }
            }
        }

        return hits;
    }

    /// <summary>
    /// [S-6415] The list's scroll speed (DIPs per second, negative = up) with the band's pointer at
    /// <paramref name="y"/> in a list <paramref name="viewportHeight"/> tall: nothing in the middle,
    /// growing with how deep the pointer is into the edge zone or past the edge, capped.
    /// </summary>
    public static double ScrollVelocity(double y, double viewportHeight)
    {
        if (viewportHeight <= 0 || double.IsNaN(y))
        {
            return 0;
        }

        // A short list keeps a middle where nothing scrolls.
        var zone = Math.Min(EdgeZone, viewportHeight / 4);

        if (y < zone)
        {
            return -Math.Min(MaxSpeed, (zone - y) * Gain);
        }

        if (y > viewportHeight - zone)
        {
            return Math.Min(MaxSpeed, (y - (viewportHeight - zone)) * Gain);
        }

        return 0;
    }

    /// <summary>[S-6415] The offset one frame of <paramref name="seconds"/> later, kept inside 0..<paramref name="maxOffset"/>.</summary>
    public static double Step(double offset, double velocity, double seconds, double maxOffset)
    {
        var dt = Math.Clamp(double.IsFinite(seconds) ? seconds : 0, 0, MaxStepSeconds);
        return Math.Clamp(offset + (velocity * dt), 0, Math.Max(0, maxOffset));
    }
}
