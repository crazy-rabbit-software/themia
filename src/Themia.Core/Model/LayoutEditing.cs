namespace Themia.Core.Model;

/// <summary>
/// [S-15526] [S-13917] The rules of a container's layout editing, pure (the surface draws and asks; these decide). Three
/// states: the locked desktop (a container is one thing: any press on it stands for it), edit mode (the same: it is selected,
/// moved and resized as a widget, its parts never picked) and its layout being edited (its parts are picked, moved and
/// resized; the container itself and its other days hold still; the rest of the desktop is away).
/// </summary>
public static class LayoutEditing
{
    /// <summary>
    /// The widget a press on a card stands for. <paramref name="widgetId"/> is the card's widget (for a copied day's card, the
    /// part it copies) and <paramref name="copyOwner"/> the container whose copied day the card is in (null for a real card).
    /// A part stands for itself only while its container's layout is edited (<paramref name="editing"/>); a copied day always
    /// stands for its container; anything else for itself.
    /// </summary>
    public static string Stands(ViewState view, string widgetId, string? copyOwner, bool editMode, string? editing)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(widgetId);
        if (copyOwner is not null)
        {
            return editMode ? copyOwner : Repeaters.Outermost(view, widgetId)?.Widget.Id ?? widgetId;
        }

        if (view.Find(widgetId)?.Config is IRepeater || Repeaters.Outermost(view, widgetId) is not { } holder)
        {
            return widgetId;
        }

        return editMode && string.Equals(editing, holder.Widget.Id, StringComparison.Ordinal) ? widgetId : holder.Widget.Id;
    }

    /// <summary>
    /// Whether a click on <paramref name="standsId"/> starts its layout editing: edit mode, no layout edited yet, a container,
    /// and it was the one widget selected on its view before the press (the first click selects it, the second edits it).
    /// </summary>
    public static bool Enters(ViewState view, string standsId, bool editMode, string? editing, string? selectionViewId, IReadOnlyList<string> selectedBefore)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selectedBefore);
        return editMode && editing is null
            && view.Find(standsId)?.Config is IRepeater
            && string.Equals(selectionViewId, view.Id, StringComparison.Ordinal)
            && selectedBefore.Count == 1 && string.Equals(selectedBefore[0], standsId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether a press on <paramref name="standsId"/> holds still: it is the container whose layout is edited (its empty room,
    /// another of its days), which is not picked, moved or resized then; the press lets go of the part selected.
    /// </summary>
    public static bool HeldStill(string? editing, string standsId) => editing is not null && string.Equals(editing, standsId, StringComparison.Ordinal);

    /// <summary>
    /// Whether <paramref name="editing"/>'s layout editing goes on after the selection changed: edit mode, the container still
    /// on the view, and nothing selected or only its own parts (anything else selected ends it).
    /// </summary>
    public static bool GoesOn(ViewState view, string editing, bool editMode, string? selectionViewId, IReadOnlyList<string> selected)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selected);
        if (!editMode || view.Find(editing)?.Config is not IRepeater)
        {
            return false;
        }

        if (selected.Count == 0)
        {
            return true;
        }

        var parts = new HashSet<string>(Repeaters.Descendants(view, editing), StringComparer.Ordinal);
        return string.Equals(selectionViewId, view.Id, StringComparison.Ordinal) && selected.All(parts.Contains);
    }

    /// <summary>
    /// The parts a move let go removed (owner, 2026-11-21: "when you drag them out of it they are removed"): those of
    /// <paramref name="before"/> (the container's parts before the drop) that moved and are not its parts after it.
    /// </summary>
    public static HashSet<string> Removed(IReadOnlyList<string> before, IReadOnlyCollection<string> moved, IReadOnlyList<string> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(moved);
        ArgumentNullException.ThrowIfNull(after);
        return [.. before.Where(id => moved.Contains(id) && !after.Contains(id, StringComparer.Ordinal))];
    }
}
