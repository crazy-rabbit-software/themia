using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-12950] Lock widgets (owner, 2026-11-17: "you should be able to lock the desktop in which case you should only
/// be able to move the desktop icons but not any other widgets"): while <see cref="AppPreferences.LockWidgets"/> is on,
/// a move on the locked desktop ([S-2008], [S-11879]) carries desktop icons only. A widget pressed and dragged stays
/// where it is (the press stays its own click or gesture); a mixed selection's move carries its icons and leaves its
/// widgets. Edit mode moves everything: editing is explicit. Pure.
/// </summary>
public static class WidgetLock
{
    /// <summary>Whether <paramref name="widget"/> is a desktop icon (either source, [S-5600]): the one kind a lock leaves free.</summary>
    public static bool IsIcon(WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return widget.Config is IDesktopItemConfig;
    }

    /// <summary>
    /// Whether a widget stays where it is on a move: Lock widgets on, outside edit mode, and not a desktop icon.
    /// </summary>
    public static bool Holds(bool lockWidgets, bool editMode, bool isIcon) => lockWidgets && !editMode && !isIcon;

    /// <summary>
    /// [S-12953] Whether a widget may be in the locked desktop's one selection (<see cref="DesktopPicks"/>; owner,
    /// 2026-11-19: "when the widgets are locked you should also not be able to select them"): always a desktop icon
    /// (either source, a fence's items too: selecting and opening icons is the desktop's, as Windows' own), any other
    /// widget (a fence's card included) only while Lock widgets is off. Edit mode has its own selection
    /// (<see cref="WidgetSelection"/>), which the lock never touches.
    /// </summary>
    public static bool Selectable(bool lockWidgets, bool isIcon) => isIcon || !lockWidgets;

    /// <summary>
    /// Whether the widget <paramref name="widgetId"/> of <paramref name="view"/> cannot be moved now: the lock holds
    /// it. False for an id the view does not hold.
    /// </summary>
    public static bool Pins(ViewState view, string widgetId, bool lockWidgets, bool editMode)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(widgetId);
        if (!lockWidgets || editMode)
        {
            return false;
        }

        return view.Find(widgetId) is { } widget && Holds(lockWidgets, editMode, IsIcon(widget));
    }

    /// <summary>
    /// What of <paramref name="ids"/> (a move's widgets) may move: all of them unless the lock holds, then only the
    /// desktop icons, in their order.
    /// </summary>
    public static List<string> Movable(ViewState view, IReadOnlyList<string> ids, bool lockWidgets, bool editMode)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(ids);
        return !lockWidgets || editMode ? [.. ids] : [.. ids.Where(id => !Pins(view, id, lockWidgets, editMode))];
    }
}
