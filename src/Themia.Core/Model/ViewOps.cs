namespace Themia.Core.Model;

/// <summary>
/// The operations on the list of views: duplicate, reorder, delete, and the Ctrl+number shortcut.
/// Each one leaves every reference to a view — display assignments, the active view, the view for
/// new displays, the view bars' whitelists — pointing at a view that exists. [S-900]..[S-904]
/// </summary>
public static class ViewOps
{
    /// <summary>
    /// [S-900] A copy of a view, inserted right after it: the same name with " (Copy)", the same
    /// icon, and a deep copy of every widget under a fresh id. Shared data a widget only refers to —
    /// a note, an account, a habit list — stays shared, as in the original.
    /// </summary>
    /// <returns>The new view, or null when <paramref name="viewId"/> names no view.</returns>
    public static ViewState? Duplicate(AppState state, string viewId)
    {
        ArgumentNullException.ThrowIfNull(state);

        var index = state.Views.FindIndex(v => v.Id == viewId);
        if (index < 0)
        {
            return null;
        }

        var source = state.Views[index];
        var copy = ViewState.Create($"{source.Name} (Copy)");
        copy.Icon = source.Icon;
        copy.Color = source.Color;

        foreach (var widget in source.Widgets)
        {
            copy.Widgets.Add(widget.Clone());
        }

        state.Views.Insert(index + 1, copy);
        return copy;
    }

    /// <summary>
    /// [S-901] Move a view into another's place in the list, the views between shifting by one.
    /// By id, never by index: the view bar can offer a filtered subset of the views ([S-908]), and
    /// an index into that subset is not an index into the list. False when either id is unknown or
    /// they are the same view.
    /// </summary>
    public static bool Reorder(List<ViewState> views, string draggedId, string targetId)
    {
        ArgumentNullException.ThrowIfNull(views);

        var from = views.FindIndex(v => v.Id == draggedId);
        var to = views.FindIndex(v => v.Id == targetId);
        return from >= 0 && to >= 0 && Widgets.DeskListOrder.MoveTo(views, from, to);
    }

    /// <summary>
    /// [S-1707] Rename a view, from its page or the settings pane's menu. The name is trimmed; a
    /// blank one is refused and the old name kept, since a view with no name cannot be told apart
    /// in the pane, the bar or a switcher. False when nothing changed.
    /// </summary>
    public static bool Rename(AppState state, string viewId, string? name)
    {
        ArgumentNullException.ThrowIfNull(state);

        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || state.FindView(viewId) is not { } view || view.Name == trimmed)
        {
            return false;
        }

        view.Name = trimmed;
        return true;
    }

    /// <summary>
    /// [S-3810] Give a view's icon a colour, or none (null or empty) for the theme's text colour. The
    /// colour is stored as <c>#RRGGBB</c>; anything that does not name a colour is refused and the old
    /// one kept. False when nothing changed.
    /// </summary>
    public static bool SetColor(AppState state, string viewId, string? color)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.FindView(viewId) is not { } view)
        {
            return false;
        }

        var clean = CleanColor(color);
        if (clean is null && !string.IsNullOrWhiteSpace(color))
        {
            return false;
        }

        if (clean == view.Color)
        {
            return false;
        }

        view.Color = clean;
        return true;
    }

    /// <summary>[S-3811] A stored view colour as <c>#RRGGBB</c>, or null for none and for anything that is not a colour.</summary>
    public static string? CleanColor(string? color)
        => Widgets.HexColor.Normalise(color) is { Length: > 0 } hex ? hex : null;

    /// <summary>[S-901] Move a view one place up (-1) or down (+1). False at either end.</summary>
    public static bool MoveBy(List<ViewState> views, string viewId, int delta)
    {
        ArgumentNullException.ThrowIfNull(views);

        var from = views.FindIndex(v => v.Id == viewId);
        var to = from + delta;
        if (from < 0 || delta == 0 || to < 0 || to >= views.Count)
        {
            return false;
        }

        return Reorder(views, viewId, views[to].Id);
    }

    /// <summary>
    /// [S-902] Delete a view and everything in it. The last view cannot go: a desktop with no view
    /// has nowhere to put a widget.
    /// <para>
    /// Repairs, in the same change, everything that named it: displays showing it lose their
    /// assignment (the caller gives them a view again, [S-18]); the active view and the view for
    /// new displays fall back; each view bar's whitelist drops it, and a whitelist left empty is
    /// "all views" again rather than a bar with nothing on it.
    /// </para>
    /// </summary>
    public static bool Delete(AppState state, string viewId)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Views.Count <= 1 || state.Views.RemoveAll(v => v.Id == viewId) == 0)
        {
            return false;
        }

        foreach (var monitor in state.MonitorViews.Where(kv => kv.Value == viewId).Select(kv => kv.Key).ToList())
        {
            state.MonitorViews.Remove(monitor);
        }

        if (state.ActiveViewId == viewId)
        {
            state.ActiveViewId = state.Views[0].Id;
        }

        if (state.DefaultViewForNewDisplays == viewId)
        {
            state.DefaultViewForNewDisplays = null;
        }

        foreach (var bar in state.ViewBars.Values)
        {
            bar?.VisibleViews.RemoveAll(id => id == viewId);
        }

        return true;
    }

    /// <summary>
    /// [S-904] The view Ctrl+<paramref name="number"/> shows: the number-th view in the user's
    /// order (1-based), as in the original. Null outside 1..9 or past the last view.
    /// </summary>
    public static string? ForShortcut(IReadOnlyList<ViewState> views, int number)
    {
        ArgumentNullException.ThrowIfNull(views);
        return number is >= 1 and <= 9 && number <= views.Count ? views[number - 1].Id : null;
    }
}
