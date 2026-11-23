using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-13007] [S-13008] [S-13040]..[S-13043] The fences' layout on the desktop icon grid: a fence's rect from a drawn
/// rectangle or round icons, every fence and its icons laid on its cells, what it does not show, the paint order, and the
/// one drop computation with a move let go landing icons in a fence or out of one.
/// </summary>
public static partial class DesktopFences
{
    /// <summary>
    /// [S-13008] The rect of a fence made round <paramref name="icons"/> (the selected free icons' rects on a display of
    /// <paramref name="screen"/>): their bounding box with the title bar added above it, at least the size that shows all
    /// <paramref name="count"/> items without scrolling (<see cref="FenceGrid.Around"/>; <paramref name="cell"/> the icon's
    /// box in DIPs), snapped out to whole grid cells and kept on the display. Null with no icon or no usable screen.
    /// </summary>
    public static FracRect? AroundIcons(IReadOnlyList<FracRect> icons, int count, SizeDip cell, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(icons);
        if (icons.Count == 0 || !screen.IsUsable)
        {
            return null;
        }

        var title = FenceCells.TitleHeight(screen) * screen.Height;
        var left = icons.Min(r => r.X) * screen.Width;
        var top = icons.Min(r => r.Y) * screen.Height;
        var right = icons.Max(r => r.X + r.W) * screen.Width;
        var bottom = icons.Max(r => r.Y + r.H) * screen.Height;
        var need = FenceGrid.Around(count, cell);
        var width = Math.Max(right - left, need.Width);
        var height = Math.Max(bottom - top + title, need.Height - FenceGrid.TitleDip + title);
        var y = Math.Max(0, top - title);
        return Snap(new FracRect(left / screen.Width, y / screen.Height, width / screen.Width, height / screen.Height), screen);
    }

    /// <summary>
    /// [S-13007] A rectangle drawn on empty desktop (fractions of its display) as a fence's rect: snapped out to whole grid
    /// cells, at least <paramref name="min"/> DIPs, kept on the display. Null with no usable screen.
    /// </summary>
    public static FracRect? FromRect(FracRect drawn, SizeDip min, ScreenMetrics screen)
    {
        if (!screen.IsUsable || !drawn.IsFinite)
        {
            return null;
        }

        var w = Math.Max(drawn.W, min.Width / screen.Width);
        var h = Math.Max(drawn.H, min.Height / screen.Height);
        return Snap(drawn with { W = w, H = h }, screen);
    }

    /// <summary>Out to whole cells of the widget grid ([S-06]: the corner down to its line, the size up), then kept on the display ([S-09]).</summary>
    internal static FracRect Snap(FracRect rect, ScreenMetrics screen)
    {
        var (cols, rows) = GridGeometry.GridDims(screen);
        double Down(double f, int n) => GridSpace.Down(f * n) / (double)n;
        double Up(double f, int n) => Math.Min(1, GridSpace.Up(f * n) / (double)n);
        return GridGeometry.Clamp(new FracRect(Down(rect.X, cols), Down(rect.Y, rows), Up(rect.W, cols), Up(rect.H, rows)), screen);
    }

    /// <summary>
    /// [S-13040] [S-13042] [S-13043] Every fence of <paramref name="view"/> laid on the desktop icon grid of
    /// <paramref name="screen"/> (<paramref name="box"/>: an icon's box there, whole grid cells, <see cref="DesktopGrid.IconSize"/>):
    /// an item whose icon has left the view (hidden, deleted, cut, moved to another display) leaves its fence; the fence's
    /// rect is its title bar's grid rows and whole boxes under it (<see cref="FenceCells.Snap"/>); its scroll is kept between
    /// the top and its last row; and each item's icon stands on its cell, in the fence's order, where the body shows it, or
    /// tucked in the fence's corner where it does not (scrolled away, rolled up: drawn by no surface, <see cref="Hidden"/>).
    /// A search fence holds nothing. Runs in every change of the state (the host's). Returns whether anything changed.
    /// </summary>
    public static bool Lay(ViewState view, ScreenMetrics screen, (double W, double H) box)
    {
        ArgumentNullException.ThrowIfNull(view);
        var changed = false;
        var icons = IconsByKey(view);
        var usable = screen.IsUsable && box.W > 0 && box.H > 0;
        foreach (var on in On(view).ToList())
        {
            if (on.Fence.Items.RemoveAll(key => !icons.ContainsKey(key)) > 0)
            {
                changed = true;
            }

            if (!usable)
            {
                continue;
            }

            var snapped = FenceCells.Snap(on.Widget.Rect, on.Fence.RolledUp, screen, box);
            if (!Lattice.Same(snapped, on.Widget.Rect))
            {
                on.Widget.Rect = snapped;
                changed = true;
            }

            var layout = FenceCells.Of(on.Widget.Rect, on.Fence, screen, box).Clamped(on.Fence.Items.Count);
            if (layout.Scroll != on.Fence.Scroll)
            {
                on.Fence.Scroll = layout.Scroll;
                changed = true;
            }

            for (var i = 0; i < on.Fence.Items.Count; i++)
            {
                var icon = icons[on.Fence.Items[i]];
                var rect = layout.Shows(i) ? FenceCells.Cell(layout, i) : Tucked(on.Widget.Rect, box);
                if (!Lattice.Same(rect, icon.Rect))
                {
                    icon.Rect = rect;
                    changed = true;
                }
            }
        }

        return changed;
    }

    /// <summary>Where an item's icon waits while its fence does not show it: the fence's corner, inside the fence.</summary>
    private static FracRect Tucked(FracRect fence, (double W, double H) box)
        => new(fence.X, fence.Y, Math.Min(box.W, fence.W), Math.Min(box.H, fence.H));

    /// <summary>
    /// [S-13042] [S-13043] The widget ids of the icons <paramref name="view"/>'s fences do not show now (scrolled away, rolled
    /// up): no surface draws them, nothing selects them.
    /// </summary>
    public static HashSet<string> Hidden(ViewState view, ScreenMetrics screen, (double W, double H) box) => Hidden(FenceIndex.Of(view), screen, box);

    /// <summary>[S-15666] What the fences of a view already looked up (<paramref name="fences"/>) do not show now.</summary>
    public static HashSet<string> Hidden(FenceIndex fences, ScreenMetrics screen, (double W, double H) box)
    {
        ArgumentNullException.ThrowIfNull(fences);
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, (on, index)) in fences.Members)
        {
            if (on.Fence.RolledUp || !screen.IsUsable || !FenceCells.Of(on.Widget.Rect, on.Fence, screen, box).Clamped(on.Fence.Items.Count).Shows(index))
            {
                hidden.Add(id);
            }
        }

        return hidden;
    }

    /// <summary>
    /// [S-13042] The order <paramref name="view"/>'s widgets are painted in: the view's own ([S-874]), each fence's icons
    /// right over their fence, in its order.
    /// </summary>
    public static List<WidgetInstance> PaintOrder(ViewState view) => PaintOrder(FenceIndex.Of(view));

    /// <summary>[S-15666] The paint order of the view whose fences were already looked up (<paramref name="fences"/>).</summary>
    public static List<WidgetInstance> PaintOrder(FenceIndex fences)
    {
        ArgumentNullException.ThrowIfNull(fences);
        var view = fences.View;
        var members = fences.Members;
        if (members.Count == 0)
        {
            return [.. view.Widgets];
        }

        var byFence = members.GroupBy(m => m.Value.Fence.Widget.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Value.Index).Select(m => m.Key).ToList(), StringComparer.Ordinal);
        var widgets = new Dictionary<string, WidgetInstance>(StringComparer.Ordinal);
        foreach (var widget in view.Widgets)
        {
            widgets.TryAdd(widget.Id, widget);
        }

        var order = new List<WidgetInstance>(view.Widgets.Count);
        foreach (var widget in view.Widgets)
        {
            if (members.ContainsKey(widget.Id))
            {
                continue;
            }

            order.Add(widget);
            if (byFence.TryGetValue(widget.Id, out var icons))
            {
                order.AddRange(icons.Select(id => widgets[id]));
            }
        }

        return order;
    }

    /// <summary>
    /// [S-13041] The one drop computation for the free desktop and a fence: where an icon moved to
    /// <paramref name="landing"/> (already on the widget grid, the move's) lands. On a fence's body (its centre on one of the
    /// body's cells; a fence <paramref name="moving"/> carries, a rolled-up or a search fence aside) the cell of the place it
    /// takes there: the cell under it, never past the items that stay (a fence fills its cells in order) nor a row the body
    /// does not show. Anywhere else the landing itself. Both are cells of the one grid.
    /// </summary>
    public static FracRect DropCell(ViewState view, FracRect landing, ScreenMetrics screen, (double W, double H) box, IReadOnlySet<string> moving)
        => DropCell(FenceIndex.Of(view), landing, screen, box, moving);

    /// <summary>[S-15666] The one drop computation over a view whose fences were already looked up (a move asks it at every step).</summary>
    public static FracRect DropCell(FenceIndex fences, FracRect landing, ScreenMetrics screen, (double W, double H) box, IReadOnlySet<string> moving)
    {
        ArgumentNullException.ThrowIfNull(fences);
        ArgumentNullException.ThrowIfNull(moving);
        return Under(fences, landing, screen, box, moving) is { } at ? FenceCells.Cell(at.Layout, at.Slot) : landing;
    }

    /// <summary>[S-13041] The fence whose body <paramref name="landing"/>'s centre is on (the topmost), its layout and the place a drop there takes; null for none.</summary>
    private static (FenceOn Fence, FenceGrid.Layout Layout, int Slot)? Under(FenceIndex fences, FracRect landing, ScreenMetrics screen, (double W, double H) box, IReadOnlySet<string> moving)
    {
        if (!screen.IsUsable || !landing.IsFinite || !(box.W > 0) || !(box.H > 0))
        {
            return null;
        }

        var (cx, cy) = (landing.X + (landing.W / 2), landing.Y + (landing.H / 2));
        for (var f = fences.Fences.Count - 1; f >= 0; f--)
        {
            var on = fences.Fences[f];
            if (moving.Contains(on.Widget.Id) || on.Fence.RolledUp || on.Fence.Source == FenceSource.Search)
            {
                continue;
            }

            var layout = FenceCells.Of(on.Widget.Rect, on.Fence, screen, box).Clamped(on.Fence.Items.Count);
            var slot = layout.SlotAt(cx, cy);
            if (slot < 0)
            {
                continue;
            }

            var staying = on.Fence.Items.Count(key => !(fences.Icon(key) is { } icon && moving.Contains(icon.Id)));
            var first = layout.Scroll * layout.Columns;
            var last = first + Math.Max(0, layout.Shown - 1);
            return (on, layout, Math.Clamp(Math.Min(slot, staying), first, last));
        }

        return null;
    }

    /// <summary>
    /// [S-13041] [S-16461] A move of <paramref name="moved"/> (widget ids of <paramref name="view"/>, already at the rects they
    /// landed on) let go. Where it lands is decided once, for the whole move, by the icon the pointer holds
    /// (<paramref name="held"/>; none, as for icons nudged with the keys: the first moving icon in reading order): dropped on a
    /// fence's body, every desktop item's icon it carries is that fence's, the block in reading order from the place the held
    /// icon's cell stands for (<see cref="DropCell"/>), leaving any fence it was in, each on a cell of its own when the fence
    /// is laid, however many: one whose box was still outside the fence included ([S-16462]: a fence too small for them all
    /// takes them all and scrolls by rows to the rest). Dropped anywhere else, none is a fence's: each is a free icon where it
    /// stands ([S-16463]), and one whose centre stands on a fence (its box entered it before the pointer) steps to the nearest
    /// free cell, so no free icon is left under a fence. Never by an icon's own box (before 2026-11-23 each icon landed by
    /// its own centre, so one of several stayed outside: owner, "sometimes when one desktop icon enters first before the mouse
    /// entered it does not move one of the widgets inside"). An icon carried with its own fence stays in it; an own icon is
    /// never a fence's. The caller lays the fences after (<see cref="Lay"/>). Returns how many icons changed fence, place in
    /// one, or stepped off a fence.
    /// </summary>
    public static int Land(ViewState view, IReadOnlyCollection<string> moved, string? held, ScreenMetrics screen, (double W, double H) box)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(moved);
        var set = new HashSet<string>(moved, StringComparer.Ordinal);
        var fences = FenceIndex.Of(view);

        // The moving icons that land (both sources): not one its own moving fence carries.
        var icons = view.Widgets
            .Where(w => set.Contains(w.Id) && w.Config is IDesktopItemConfig config
                && !(config.Item.Length > 0 && fences.Holding(config.Item) is { } holder && set.Contains(holder.Widget.Id)))
            .OrderBy(w => w.Rect.Y).ThenBy(w => w.Rect.X).ThenBy(w => w.Id, StringComparer.Ordinal)
            .ToList();
        if (icons.Count == 0)
        {
            return 0;
        }

        // [S-16461] The one decision: the held icon's landing (a card or a fence held: no fence takes the icons).
        var lead = held is null ? icons[0] : icons.Find(w => string.Equals(w.Id, held, StringComparison.Ordinal));
        var target = lead is null ? null : Under(fences, lead.Rect, screen, box, set);

        var keyed = icons.Where(w => ((IDesktopItemConfig)w.Config).Item.Length > 0).ToList();
        string KeyOf(WidgetInstance w) => ((IDesktopItemConfig)w.Config).Item;
        var before = Places(view, keyed.Select(KeyOf));
        var rects = icons.ToDictionary(w => w.Id, w => w.Rect, StringComparer.Ordinal);
        TakeOut(view, keyed.Select(KeyOf));
        var inFence = new HashSet<string>(StringComparer.Ordinal);
        if (target is { } at)
        {
            // The block in reading order, the held icon on its cell where the fence's order allows it.
            var ahead = lead is null ? 0 : keyed.Count(w => icons.IndexOf(w) < icons.IndexOf(lead));
            var first = at.Layout.Scroll * at.Layout.Columns;
            var items = at.Fence.Fence.Items;
            var start = Math.Clamp(Math.Max(first, at.Slot - ahead), 0, items.Count);
            items.InsertRange(start, keyed.Select(KeyOf));
            inFence.UnionWith(keyed.Select(w => w.Id));
        }

        // [S-16463] A free icon never stays under a fence: one whose centre stands on a fence steps to the nearest free cell.
        foreach (var icon in icons.Where(w => !inFence.Contains(w.Id) && OnFence(fences, w.Rect, set)))
        {
            var room = view.Widgets.Where(w => !ReferenceEquals(w, icon) && !inFence.Contains(w.Id)).Select(w => w.Rect).ToList();
            if (DesktopGrid.NearestFree((icon.Rect.W, icon.Rect.H), icon.Rect.X, icon.Rect.Y, room, screen) is { } free)
            {
                icon.Rect = free;
            }
        }

        var after = Places(view, keyed.Select(KeyOf));
        return icons.Count(w => Changed(w) || !Lattice.Same(rects[w.Id], w.Rect));
        bool Changed(WidgetInstance w) => KeyOf(w).Length > 0 && before[KeyOf(w)] != after[KeyOf(w)];
    }

    /// <summary>[S-16463] Whether <paramref name="rect"/>'s centre stands on a fence of <paramref name="fences"/> that does not move (its title bar or body).</summary>
    private static bool OnFence(FenceIndex fences, FracRect rect, IReadOnlySet<string> moving)
    {
        var (cx, cy) = (rect.X + (rect.W / 2), rect.Y + (rect.H / 2));
        return fences.Fences.Any(on => !moving.Contains(on.Widget.Id)
            && cx > on.Widget.Rect.X && cx < on.Widget.Rect.Right && cy > on.Widget.Rect.Y && cy < on.Widget.Rect.Bottom);
    }

    /// <summary>Where each of <paramref name="keys"/> stands among <paramref name="view"/>'s fences: the fence's id and the place, or none.</summary>
    private static Dictionary<string, (string? Fence, int At)> Places(ViewState view, IEnumerable<string> keys)
    {
        var places = new Dictionary<string, (string?, int)>(DesktopItemKeys.Comparer);
        foreach (var key in keys)
        {
            places[key] = (null, -1);
            foreach (var on in On(view))
            {
                var at = on.Fence.Items.FindIndex(k => DesktopItemKeys.Comparer.Equals(k, key));
                if (at >= 0)
                {
                    places[key] = (on.Widget.Id, at);
                    break;
                }
            }
        }

        return places;
    }
}
