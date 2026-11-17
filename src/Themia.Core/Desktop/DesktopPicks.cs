using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-11877] The locked desktop's one selection (<see cref="DesktopSelection"/>) as the widgets of a view
/// (owner, 2026-11-15: "the widgets in the desktop should also be selected with the lasso, and also moved
/// with everything as well then"): every widget on the desktop is in it, a desktop icon under its own key
/// (<see cref="DesktopItemKeys.Of"/>), any other widget under its card key
/// (<see cref="DesktopItemKeys.Widget"/>). There is no second selection outside edit mode: a card is
/// selected exactly when the desktop's selection holds its key, and a move carries what it holds. Edit
/// mode keeps its own selection (<see cref="WidgetSelection"/>), started empty as edit mode starts; both
/// take whole units, a lone widget or a whole group ([S-2451], [S-11885]). Pure.
/// </summary>
public static class DesktopPicks
{
    /// <summary>
    /// The key <paramref name="widget"/> is selected under on the locked desktop: an own icon's own key, a
    /// desktop item's key while the listing holds it (<paramref name="listed"/>), any other widget its card
    /// key; null for a desktop item the listing does not hold (it cannot be selected until it does).
    /// </summary>
    public static string? KeyOf(WidgetInstance widget, Func<string, bool> listed)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(listed);
        return widget.Config switch
        {
            IDesktopItemConfig { Item.Length: 0 } own => DesktopItemKeys.Own(own.ItemId),
            IDesktopItemConfig item => listed(item.Item) ? item.Item : null,
            _ => DesktopItemKeys.Widget(widget.Id),
        };
    }

    /// <summary>
    /// The ids of the widgets of <paramref name="view"/> the selection holds, in the view's order, [S-11885]
    /// grown to whole units: a member whose key is held brings its group (<see cref="WidgetGroups.Units"/>,
    /// edit mode's own rule, [S-2451]), so a lasso that touches one member selects the group.
    /// </summary>
    public static List<string> Selected(ViewState view, DesktopSelection selection, Func<WidgetInstance, string?> keyOf)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(keyOf);
        return WidgetGroups.Units(view, view.Widgets.Where(w => keyOf(w) is { } key && selection.IsSelected(key)).Select(w => w.Id));
    }

    /// <summary>
    /// [S-11885] Whether <paramref name="widget"/> of <paramref name="view"/> is drawn selected on the locked
    /// desktop: the selection holds its key or the key of a member of its group (its unit, [S-2451]).
    /// </summary>
    public static bool Selects(ViewState view, DesktopSelection selection, WidgetInstance widget, Func<WidgetInstance, string?> keyOf)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(keyOf);
        var unit = WidgetGroups.Unit(view, widget.Id);
        return (unit.Count == 0 ? [widget] : view.Widgets.Where(w => unit.Contains(w.Id, StringComparer.Ordinal)))
            .Any(w => keyOf(w) is { } key && selection.IsSelected(key));
    }

    /// <summary>
    /// [S-11879] What a move of the widget <paramref name="heldId"/> on the locked desktop carries: every
    /// selected widget of <paramref name="view"/>, icons and cards alike, when the held one is selected; the
    /// held one alone otherwise. [S-11885] Always in whole units: the held one's group comes with it, as every
    /// selected member's does (<see cref="WidgetGroups.Units"/>, [S-2451]). The held one is always in it.
    /// [S-12950] With Lock widgets on (<paramref name="lockWidgets"/>) only the units of desktop icons alone move
    /// (<see cref="WidgetLock.Movable"/>): a mixed selection's widgets stay put; a held widget never starts a move.
    /// </summary>
    public static List<string> Moving(ViewState view, DesktopSelection selection, string heldId, Func<WidgetInstance, string?> keyOf, bool lockWidgets = false)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(heldId);
        var held = view.Widgets.Find(w => string.Equals(w.Id, heldId, StringComparison.Ordinal));
        var selected = held is not null && Selects(view, selection, held, keyOf);
        var ids = selected ? Selected(view, selection, keyOf) : [];
        ids = WidgetGroups.Units(view, ids.Append(heldId));
        if (!ids.Contains(heldId, StringComparer.Ordinal))
        {
            ids.Add(heldId);
        }

        return WidgetLock.Movable(view, ids, lockWidgets, editMode: false);
    }
}

/// <summary>
/// [S-11878] When a move of desktop icons on the locked desktop becomes the shell's drag (the OLE/XAML item
/// drag, [S-5549]) that carries their files to another place: only for a move the pointer holds by an icon
/// whose block has something to give (<see cref="DesktopCommands.Drag"/>'s files), and only once the
/// pointer is off Themia's desktop (past every display's work area, over the taskbar, over an app's window
/// on the desktop) or over a drop target on it that is not the desktop itself (a folder icon, the Recycle
/// Bin's). Everywhere else on the desktop, other displays included, it stays the widgets' move ([S-2008],
/// [S-1591]); a card held is never dragged out. Pure.
/// </summary>
public static class DesktopDragOut
{
    /// <summary>Whether the move turns into the shell's drag now.</summary>
    /// <param name="heldIsIcon">The pointer holds a desktop icon (not another widget).</param>
    /// <param name="carriesFiles">What the move carries gives another target something (a file, a folder, a shortcut).</param>
    /// <param name="overDesktop">The pointer is over Themia's desktop: inside a surface's work area, no other window on top there.</param>
    /// <param name="overDropTarget">The pointer is over a widget on the desktop that takes a drop of files and is not moving itself.</param>
    public static bool Starts(bool heldIsIcon, bool carriesFiles, bool overDesktop, bool overDropTarget)
        => heldIsIcon && carriesFiles && (!overDesktop || overDropTarget);
}
