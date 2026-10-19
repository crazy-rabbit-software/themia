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

/// <summary>
/// [S-6412]/[S-6415] The folder widget's own part of the rubber band, kept pure: where a band may
/// start and how fast the list scrolls while the band is dragged past an edge. How the band combines
/// and what it covers are every item-selecting widget's ([S-6624]/[S-6625], <see cref="SelectionBand"/>).
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
