using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-12950] Lock widgets (owner, 2026-11-17: "you should be able to lock the desktop in which case you should only
/// be able to move the desktop icons but not any other widgets"): while <see cref="AppPreferences.LockWidgets"/> is on,
/// a move on the locked desktop ([S-2008], [S-11879]) carries desktop icons only. A widget pressed and dragged stays
/// where it is (the press stays its own click or gesture); a mixed selection's move carries its icons and leaves its
/// widgets; a group ([S-11885]) is one unit, so a group holding any widget stays whole, its icons too. Edit mode
/// moves everything: editing is explicit. Pure.
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
    /// Whether the widget <paramref name="widgetId"/> of <paramref name="view"/> cannot be moved now: its unit (itself,
    /// or its whole group, [S-2451]) holds a widget the lock holds. False for an id the view does not hold.
    /// </summary>
    public static bool Pins(ViewState view, string widgetId, bool lockWidgets, bool editMode)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(widgetId);
        if (!lockWidgets || editMode)
        {
            return false;
        }

        var unit = WidgetGroups.Unit(view, widgetId);
        return view.Widgets.Any(w => unit.Contains(w.Id, StringComparer.Ordinal) && Holds(lockWidgets, editMode, IsIcon(w)));
    }

    /// <summary>
    /// What of <paramref name="ids"/> (a move's widgets, in whole units) may move: all of them unless the lock holds,
    /// then only the units that are desktop icons alone, in their order.
    /// </summary>
    public static List<string> Movable(ViewState view, IReadOnlyList<string> ids, bool lockWidgets, bool editMode)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(ids);
        return !lockWidgets || editMode ? [.. ids] : [.. ids.Where(id => !Pins(view, id, lockWidgets, editMode))];
    }
}
