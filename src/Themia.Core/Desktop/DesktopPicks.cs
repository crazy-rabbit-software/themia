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
/// mode keeps its own selection (<see cref="WidgetSelection"/>), started empty as edit mode starts. Pure.
/// </summary>
public static class DesktopPicks
{
    /// <summary>
    /// The key <paramref name="widget"/> is selected under on the locked desktop: an own icon's own key, a
    /// desktop item's key while the listing holds it (<paramref name="listed"/>), any other widget its card
    /// key; null for a desktop item the listing does not hold (it cannot be selected until it does).
    /// [S-12953] With Lock widgets on (<paramref name="widgetsLocked"/>) a widget that is not a desktop icon has no key:
    /// it is not in the desktop's selection at all, so no click, Ctrl/Shift+click, band, Ctrl+A or arrow reaches it, it
    /// draws no selection, and a selection that held it lets it go at the next layout. A fence is such a widget; the
    /// items it holds are desktop icons and keep their own keys (they are laid out apart, [S-13011]).
    /// </summary>
    public static string? KeyOf(WidgetInstance widget, Func<string, bool> listed, bool widgetsLocked = false)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(listed);
        return widget.Config switch
        {
            IDesktopItemConfig { Item.Length: 0 } own => DesktopItemKeys.Own(own.ItemId),
            IDesktopItemConfig item => listed(item.Item) ? item.Item : null,
            _ => WidgetLock.Selectable(widgetsLocked, isIcon: false) ? DesktopItemKeys.Widget(widget.Id) : null,
        };
    }

    /// <summary>
    /// The ids of the widgets of <paramref name="view"/> the selection holds, in the view's order.
    /// </summary>
    public static List<string> Selected(ViewState view, DesktopSelection selection, Func<WidgetInstance, string?> keyOf)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(keyOf);
        return [.. view.Widgets.Where(w => keyOf(w) is { } key && selection.IsSelected(key)).Select(w => w.Id)];
    }

    /// <summary>
    /// [S-11877] Whether <paramref name="widget"/> is drawn selected on the locked desktop: the selection holds its key.
    /// </summary>
    public static bool Selects(DesktopSelection selection, WidgetInstance widget, Func<WidgetInstance, string?> keyOf)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(keyOf);
        return keyOf(widget) is { } key && selection.IsSelected(key);
    }

    /// <summary>
    /// [S-11879] Whether a press on a widget card (not a desktop icon, whose own press picks it) goes to the desktop's one
    /// selection: outside edit mode, the left button, with Ctrl (toggle) or Shift (the range). A plain press stays the
    /// widget's own (its content, its click, its move). [S-12953] Never while Lock widgets is on
    /// (<paramref name="widgetsLocked"/>): a locked widget is not picked at all, the press stays its own.
    /// </summary>
    public static bool PicksOnPress(bool editMode, bool leftButton, bool ctrl, bool shift, bool widgetsLocked = false)
        => !editMode && leftButton && (ctrl || shift) && WidgetLock.Selectable(widgetsLocked, isIcon: false);

    /// <summary>
    /// [S-11879] What a move of the widget <paramref name="heldId"/> on the locked desktop carries: every
    /// selected widget of <paramref name="view"/>, icons and cards alike, when the held one is selected; the
    /// held one alone otherwise. The held one is always in it.
    /// [S-12950] With Lock widgets on (<paramref name="lockWidgets"/>) only desktop icons move
    /// (<see cref="WidgetLock.Movable"/>): a mixed selection's widgets stay put; a held widget never starts a move.
    /// </summary>
    public static List<string> Moving(ViewState view, DesktopSelection selection, string heldId, Func<WidgetInstance, string?> keyOf, bool lockWidgets = false)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(heldId);
        var held = view.Widgets.Find(w => string.Equals(w.Id, heldId, StringComparison.Ordinal));
        var selected = held is not null && Selects(selection, held, keyOf);
        var ids = selected ? Selected(view, selection, keyOf) : [];
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

    /// <summary>
    /// [S-11878] [S-13041] [S-13047] Whether the widget under the pointer is a drop target the move is handed to: one that
    /// takes a drop of files (a folder icon, the Recycle Bin's, an app's shortcut), but never a fence nor an icon a fence holds
    /// (owner, 2026-11-20: "when moving an item into a fence the preview in my mouse changes, this should still not be. same
    /// preview under my mouse"). A fence's icons are its cells to the desktop's own icons: a move over them lands on their cell
    /// by the move itself, the same picture under the pointer throughout. Before, each of them, a card of its own drawn over the
    /// fence, took the move as a drop target when it took files, so the move became the shell's drag halfway into the fence.
    /// </summary>
    /// <param name="takesDrops">The widget takes a drop of files.</param>
    /// <param name="fenced">It is a fence or an icon a fence holds (<see cref="DesktopFences.FenceAt"/>).</param>
    public static bool DropTarget(bool takesDrops, bool fenced) => takesDrops && !fenced;
}
