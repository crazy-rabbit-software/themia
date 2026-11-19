using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-13700]..[S-13708] The edit bar's <b>Unplaced icons</b> page (owner, 2026-11-19: "in edit mode there should be a
/// page with the desktop icons that are on the physical desktop folder but are not placed on the given view"): what it
/// lists for the view a display shows, how its search narrows it, what its entry's badge says, which fences take its
/// items and how a drag lays several of them out. Pure: the listing, the view and the screen are handed in.
/// </summary>
public static class UnplacedItems
{
    /// <summary>[S-13702] From this many items on, the page shows its search box.</summary>
    public const int SearchFrom = 9;

    /// <summary>[S-13701] The most the entry's badge counts; more says this and a plus.</summary>
    public const int BadgeMost = 99;

    /// <summary>[S-13705] A drag's block of icons is this many wide, row after row (the desktop's reading order, [S-11876]).</summary>
    public const int BlockColumns = 4;

    /// <summary>
    /// [S-13700] The desktop's items (the user's and the public desktop, as the shell lists them) that <paramref name="view"/>
    /// shows nowhere: no free icon of it and in no fence of it, in <see cref="DesktopItemOrder"/>. None on a view that shows no
    /// desktop icons (<see cref="DesktopViews.Holds"/>): there is nowhere on it to place one. The same list Add widget's
    /// "On the desktop" offers ([S-5523], <see cref="DesktopReconcile.Unplaced"/>).
    /// </summary>
    public static IReadOnlyList<DesktopItem> Of(ViewState view, IReadOnlyList<DesktopItem> listing)
        => DesktopReconcile.Unplaced(view, listing);

    /// <summary>[S-13702] Whether a list of <paramref name="count"/> items shows the search box.</summary>
    public static bool Searches(int count) => count >= SearchFrom;

    /// <summary>
    /// [S-13702] The items whose name holds every word of <paramref name="query"/> (any case, any order); all of them for a
    /// query of nothing but spaces. The order stays.
    /// </summary>
    public static IReadOnlyList<DesktopItem> Matching(IReadOnlyList<DesktopItem> items, string? query)
    {
        ArgumentNullException.ThrowIfNull(items);
        var words = (query ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0
            ? items
            : [.. items.Where(item => words.All(word => item.Name.Contains(word, StringComparison.OrdinalIgnoreCase)))];
    }

    /// <summary>[S-13701] The entry's badge for <paramref name="count"/> unplaced items: none for none, at most "99+".</summary>
    public static string? Badge(int count)
        => count <= 0 ? null : count > BadgeMost ? LocaleFormat.Integer(BadgeMost) + "+" : LocaleFormat.Integer(count);

    /// <summary>
    /// [S-13704] The fences of <paramref name="view"/> an item can be put in, in the view's order: every fence but a search
    /// fence, which holds no desktop item ([S-13023]).
    /// </summary>
    public static IReadOnlyList<FenceOn> Fences(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return [.. DesktopFences.On(view).Where(on => on.Fence.Source != FenceSource.Search)];
    }

    /// <summary>
    /// [S-13703] What a press on <paramref name="pressed"/> acts on (its place button, its drag): the whole selection while it
    /// holds the pressed item, else the pressed item alone. In the list's order.
    /// </summary>
    public static IReadOnlyList<string> Acting(IReadOnlyList<string> selected, string pressed)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(pressed);
        return selected.Contains(pressed, DesktopItemKeys.Comparer) ? selected : [pressed];
    }

    /// <summary>
    /// [S-13705] The rects of <paramref name="count"/> icons dragged from the page as one block, each an icon's box
    /// (<paramref name="icon"/>, fractions of the display): <see cref="BlockColumns"/> to a row, row after row, the first
    /// at the block's corner, in the desktop's reading order as its arrivals and Auto arrange lay icons ([S-11876], [S-5594]).
    /// </summary>
    public static IReadOnlyList<FracRect> Block(int count, (double W, double H) icon)
    {
        var columns = Math.Clamp(count, 1, BlockColumns);
        return [.. Enumerable.Range(0, Math.Max(0, count)).Select(i => new FracRect(i % columns * icon.W, i / columns * icon.H, icon.W, icon.H))];
    }
}
