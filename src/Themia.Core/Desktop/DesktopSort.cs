using Themia.Core.Geometry;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>[S-5555] What the desktop's Sort by orders its items by, as Explorer's desktop menu offers.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DesktopSortBy>))]
public enum DesktopSortBy
{
    /// <summary>By name: the order new items are laid out in (<see cref="DesktopItemOrder"/>).</summary>
    Name,

    /// <summary>By size, smallest first.</summary>
    Size,

    /// <summary>By item type (a file's extension), then by name.</summary>
    ItemType,

    /// <summary>By the date modified, newest first.</summary>
    DateModified,
}

/// <summary>
/// [S-5555] What a sort reads of one item beyond its name: a file's size, its type (its extension, lower
/// case, empty for none) and when it was last written. A shell item and anything that could not be read
/// has none (<see cref="None"/>) and sorts by its name alone.
/// </summary>
public readonly record struct DesktopSortFacts(long Size, string Type, DateTime Modified)
{
    /// <summary>Nothing known: no size, no type, no date.</summary>
    public static DesktopSortFacts None => new(0, string.Empty, DateTime.MinValue);
}

/// <summary>
/// [S-5555] [S-5556] The desktop's Sort by, as pure rules: the order of the items for a criterion, and the
/// cells that order takes on a display's grid. Nothing here touches a file.
/// </summary>
public static class DesktopSort
{
    /// <summary>
    /// [S-5555] <paramref name="items"/> in the order of <paramref name="by"/>. Whatever the criterion,
    /// shell items come first, then folders, then files and shortcuts, as Explorer's desktop keeps them;
    /// inside each the criterion decides and the name order (<see cref="DesktopItemOrder"/>) breaks a
    /// tie, so the order is total and sorting twice changes nothing. Shell items have no size, type or
    /// date and folders no size or type: those stay in name order.
    /// </summary>
    public static IReadOnlyList<DesktopItem> Order(IEnumerable<DesktopItem> items, DesktopSortBy by, Func<DesktopItem, DesktopSortFacts> facts)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(facts);

        // By name first; every ordering below is stable, so a tie keeps the name order.
        var named = DesktopItemOrder.Sort(items);
        if (by == DesktopSortBy.Name)
        {
            return named;
        }

        var read = named.Select(item => (Item: item, Facts: item.Kind == DesktopItemKind.ShellItem ? DesktopSortFacts.None : facts(item))).ToList();
        var ranked = read.OrderBy(entry => Rank(entry.Item.Kind));
        var ordered = by switch
        {
            DesktopSortBy.Size => ranked.ThenBy(entry => entry.Item.Kind == DesktopItemKind.Folder ? 0 : entry.Facts.Size),
            DesktopSortBy.ItemType => ranked.ThenBy(entry => entry.Item.Kind == DesktopItemKind.Folder ? string.Empty : entry.Facts.Type, StringComparer.OrdinalIgnoreCase),
            _ => ranked.ThenByDescending(entry => entry.Facts.Modified),
        };

        return [.. ordered.Select(entry => entry.Item)];
    }

    /// <summary>
    /// [S-5556] [S-11876] The spots <paramref name="count"/> sorted items take, in their order: the widget
    /// grid's free spots for an icon of <paramref name="size"/> in reading order from the work area's top-left
    /// corner (<see cref="DesktopGrid.FirstFree"/>), round <paramref name="others"/> (the view's widgets that
    /// are no desktop icon and stay where they are). Null from the first item on that finds no free spot:
    /// those stay where they were.
    /// </summary>
    public static IReadOnlyList<FracRect?> Arrange(int count, IReadOnlyList<FracRect> others, (double W, double H) size, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(others);
        var cells = new FracRect?[Math.Max(0, count)];
        var occupied = new List<FracRect>(others);
        for (var i = 0; i < cells.Length; i++)
        {
            if (DesktopGrid.Arrive(occupied, size, null, null, screen) is not { } free)
            {
                break;
            }

            cells[i] = free;
        }

        return cells;
    }

    private static int Rank(DesktopItemKind kind) => kind switch
    {
        DesktopItemKind.ShellItem => 0,
        DesktopItemKind.Folder => 1,
        _ => 2,
    };
}
