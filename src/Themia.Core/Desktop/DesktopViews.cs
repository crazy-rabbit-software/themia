using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-5525]..[S-5529] Which desktop items each view shows, as pure rules (the owner's choice of 2026-11-16: "each
/// view chooses"). A view's <see cref="ViewState.DesktopIcons"/> is All, Chosen or None; the items it shows are the
/// item widgets it holds (a widget with a key), so a Chosen view's chosen set is exactly those, never a second list.
/// Own icons ([S-5600]) are the view's placed widgets under every value and are never touched here.
/// </summary>
public static class DesktopViews
{
    /// <summary>[S-5526] Whether a new item that was not dropped on this view arrives on it: an All view's alone.</summary>
    public static bool Receives(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.DesktopIcons == ViewDesktopIcons.All;
    }

    /// <summary>[S-5526] [S-5527] Whether a desktop item may stand on this view at all (dropped, placed, picked): every view but a None one.</summary>
    public static bool Holds(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.DesktopIcons != ViewDesktopIcons.None;
    }

    /// <summary>[S-5527] The keys of the desktop items this view shows: a Chosen view's chosen set.</summary>
    public static IReadOnlySet<string> Shown(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new HashSet<string>(
            view.Widgets.Select(w => w.Config).OfType<IDesktopItemConfig>().Select(c => c.Item).Where(key => key.Length > 0),
            DesktopItemKeys.Comparer);
    }

    /// <summary>
    /// [S-5528] Set a view's value. To Chosen: the items it shows now are its chosen set, nothing moves. To None:
    /// every item widget leaves it (its places are not kept; the step's undo brings them back). To All: nothing here;
    /// the caller then lays in what is missing (<see cref="Fill"/>, the listing and the view's display are the host's).
    /// Returns whether anything changed.
    /// </summary>
    public static bool Set(ViewState view, ViewDesktopIcons value)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!Enum.IsDefined(value) || view.DesktopIcons == value)
        {
            return false;
        }

        view.DesktopIcons = value;
        if (value == ViewDesktopIcons.None)
        {
            Hide(view, Shown(view));
        }

        return true;
    }

    /// <summary>
    /// [S-5528] [S-5521] Lay every one of <paramref name="items"/> this view does not show yet at the first free spot of
    /// the widget grid (<see cref="DesktopGrid.Arrive"/>, [S-11876]), in <see cref="DesktopItemOrder"/>, round the
    /// view's widgets and each other. Nothing on a None view. Returns the items that found no free spot (or every one,
    /// on a <paramref name="screen"/> not usable).
    /// </summary>
    public static IReadOnlyList<DesktopItem> Fill(
        ViewState view,
        IEnumerable<DesktopItem> items,
        ScreenMetrics screen,
        (double Width, double Height) cellDip,
        Func<DesktopItem, FracRect, WidgetInstance> create)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(create);
        if (!Holds(view))
        {
            return [];
        }

        var shown = new HashSet<string>(Shown(view), DesktopItemKeys.Comparer);
        var missing = DesktopItemOrder.Sort(items.Where(item => !shown.Contains(item.Key)).DistinctBy(item => item.Key, DesktopItemKeys.Comparer));
        if (missing.Count == 0)
        {
            return [];
        }

        if (!screen.IsUsable)
        {
            return missing;
        }

        var size = DesktopGrid.IconSize(new SizeDip(cellDip.Width, cellDip.Height), screen);
        var occupied = DesktopGrid.Occupied(view);
        var unplaced = new List<DesktopItem>();
        foreach (var item in missing)
        {
            if (DesktopGrid.Arrive(occupied, size, null, null, screen) is { } free)
            {
                view.Widgets.Add(create(item, free));
            }
            else
            {
                unplaced.Add(item);
            }
        }

        return unplaced;
    }

    /// <summary>
    /// [S-5527] Hide items from this view: their item widgets leave it (the files stay, other views keep theirs), and its
    /// value stays as it is: on an All view the item stays off it until the view is set to All again or the item is
    /// placed there. Returns how many widgets left.
    /// </summary>
    public static int Hide(ViewState view, IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(keys);
        var hidden = new HashSet<string>(keys, DesktopItemKeys.Comparer);
        return view.Widgets.RemoveAll(w => w.Config is IDesktopItemConfig { Item.Length: > 0 } config && hidden.Contains(config.Item));
    }

    /// <summary>
    /// [S-5525] Load: an undefined value is All, and a None view holding item widgets (a hand-edited file) loses them;
    /// each repair is said.
    /// </summary>
    public static IReadOnlyList<string> Repair(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var repairs = new List<string>();
        if (!Enum.IsDefined(view.DesktopIcons))
        {
            view.DesktopIcons = ViewDesktopIcons.All;
            repairs.Add($"view '{view.Id}' had an unknown desktop icons value; it shows all of them");
        }

        if (view.DesktopIcons == ViewDesktopIcons.None && Hide(view, Shown(view)) is > 0 and var dropped)
        {
            repairs.Add($"view '{view.Id}' shows no desktop icons but held {dropped}; dropped");
        }

        return repairs;
    }
}
