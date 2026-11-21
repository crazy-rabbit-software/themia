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

    /// <summary>
    /// [S-13711] The page's title, one line over its icons (owner, 2026-11-21: "it should show a title though"): "Not on
    /// &lt;view&gt;", the name of the view the page's display shows. A name of nothing but spaces reads as the view it is.
    /// </summary>
    public static string Title(string? viewName)
        => Str.Unplaced_Page_Title.Format(("view", string.IsNullOrWhiteSpace(viewName) ? Str.Shell_View_DefaultName.Text() : viewName.Trim()));

    /// <summary>
    /// [S-13713] What a move of icons let go over the open page takes off the view (owner, 2026-11-21: "that way it should also
    /// be possible to move desktop icons back to it"): the desktop items among <paramref name="moving"/> (widget ids of
    /// <paramref name="view"/>), each by its key, in the move's order, once. An own icon ([S-5600]) stands for no file and is
    /// listed nowhere, a fence and a card are no icon: none of them. An icon a fence holds counts only when its fence is not
    /// moving too (a fence moves its icons with it, [S-13042]: dragging a fence over the page empties nothing).
    /// </summary>
    public static IReadOnlyList<string> Taken(ViewState view, IEnumerable<string> moving)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(moving);
        var ids = moving.ToList();
        var fencesMoving = new HashSet<string>(ids.Where(id => view.Find(id)?.Config is IHoldsDesktopItems), StringComparer.Ordinal);
        var members = DesktopFences.Members(view);
        var keys = new List<string>();
        var seen = new HashSet<string>(DesktopItemKeys.Comparer);
        foreach (var id in ids)
        {
            if (view.Find(id)?.Config is not IDesktopItemConfig { Item.Length: > 0 } icon
                || (members.TryGetValue(id, out var fence) && fencesMoving.Contains(fence.Fence.Widget.Id)))
            {
                continue;
            }

            if (seen.Add(icon.Item))
            {
                keys.Add(icon.Item);
            }
        }

        return keys;
    }

    /// <summary>
    /// [S-13713] The inverse of placing: <paramref name="keys"/> leave <paramref name="view"/>, out of its fences and their icons
    /// off it, so the page lists them again. The files stay and other views keep theirs. The view's Desktop icons value
    /// stays as it is, as Hide from this view and Remove from this view leave it ([S-5527], [S-5524]): an All view stays All
    /// (All decides where a <i>new</i> item arrives, [S-5526]; an item taken off it stays off until it is placed again or the
    /// view is set to All again), a Chosen view's chosen set loses them. Never on a None view (it shows none). Returns the keys
    /// that left the view, in <paramref name="keys"/>' order.
    /// </summary>
    public static IReadOnlyList<string> Unplace(ViewState view, IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(keys);
        var shown = DesktopViews.Shown(view);
        var leaving = keys.Where(shown.Contains).Distinct(DesktopItemKeys.Comparer).ToList();
        if (leaving.Count == 0)
        {
            return [];
        }

        DesktopFences.TakeOut(view, leaving);
        DesktopViews.Hide(view, leaving);
        return leaving;
    }

    /// <summary>
    /// [S-13712] The page's grid lines along one side of its icon area (owner, 2026-11-21: "it should show the grid too"): the
    /// desktop's own grid at that display's pitch (<paramref name="pitch"/>, DIPs: the edit-mode grid's cell, <c>GridGeometry</c>),
    /// from the area's corner, where the icons' boxes stand (each whole cells, [S-11875]): a line at every whole cell from the
    /// corner to <paramref name="extent"/>, both edges included, every <see cref="GridGeometry.MajorEvery"/>th from the corner the
    /// heavier one, as the desktop counts its lines from its corner. None for no room or no pitch.
    /// </summary>
    public static IReadOnlyList<(double At, bool Major)> GridLines(double extent, double pitch)
    {
        if (!(pitch > 0) || !double.IsFinite(pitch) || !(extent > 0) || !double.IsFinite(extent))
        {
            return [];
        }

        var count = (int)Math.Floor((extent / pitch) + 1e-6);
        return [.. Enumerable.Range(0, count + 1).Select(i => (Math.Min(i * pitch, extent), GridGeometry.IsMajorLine(i)))];
    }

    /// <summary>
    /// [S-13712] The desktop's grid cell on <paramref name="screen"/>, in DIPs: its width over its columns and its height over its
    /// rows (<see cref="GridGeometry.GridDims"/>, the edit-mode grid's), so an icon's box (whole cells of it) is whole cells on
    /// the page too.
    /// </summary>
    public static (double X, double Y) Pitch(ScreenMetrics screen)
    {
        var (cols, rows) = GridGeometry.GridDims(screen);
        return screen.IsUsable ? (screen.Width / cols, screen.Height / rows) : (GridGeometry.MinCellDip, GridGeometry.MinCellDip);
    }

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
    /// [S-13703] What a press on <paramref name="pressed"/> acts on (its drag): the whole selection while it
    /// holds the pressed item, else the pressed item alone. In the list's order.
    /// </summary>
    public static IReadOnlyList<string> Acting(IReadOnlyList<string> selected, string pressed)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(pressed);
        return selected.Contains(pressed, DesktopItemKeys.Comparer) ? selected : [pressed];
    }

    /// <summary>
    /// [S-13707] The box the desktop gives each icon on <paramref name="screen"/> (<paramref name="box"/>, fractions of it: the
    /// desktop's own, whole grid cells round its icon cell, [S-11875]) in DIPs: the cell an icon is drawn in on the page, so it
    /// stands at the size and in the cell it gets when placed (owner, 2026-11-19: "it should show the icons exactly how they
    /// would look like on the desktop"). The page never works out a cell of its own: whatever the desktop's cell becomes, so does
    /// the page's.
    /// </summary>
    public static SizeDip Dips((double W, double H) box, ScreenMetrics screen)
        => screen.IsUsable ? new SizeDip(box.W * screen.Width, box.H * screen.Height) : new SizeDip(box.W, box.H);

    /// <summary>[S-13707] How many icon boxes <paramref name="box"/> DIPs wide stand side by side in a row <paramref name="width"/> wide: at least one.</summary>
    public static int Columns(double width, double box)
        => box > 0 && double.IsFinite(width) && width > 0 ? Math.Max(1, (int)Math.Floor((width + 1e-6) / box)) : 1;

    /// <summary>
    /// [S-13707] Where <paramref name="count"/> icons stand on the page, in DIPs from its list's corner: as the desktop lays
    /// icons, box against box with no gap (every card paints its whole cell, [S-11884]), in reading order, as many to a row
    /// as <paramref name="width"/> holds (<see cref="Columns"/>), row after row.
    /// </summary>
    public static IReadOnlyList<DipRect> Cells(int count, double width, SizeDip box)
    {
        var columns = Columns(width, box.Width);
        return [.. Enumerable.Range(0, Math.Max(0, count)).Select(i => new DipRect(i % columns * box.Width, i / columns * box.Height, box.Width, box.Height))];
    }

    /// <summary>[S-13707] How tall the page's icons stand: whole rows of <paramref name="box"/>, none for none.</summary>
    public static double Height(int count, double width, SizeDip box)
    {
        var columns = Columns(width, box.Width);
        return count <= 0 ? 0 : (((count - 1) / columns) + 1) * box.Height;
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
