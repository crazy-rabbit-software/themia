using Themia.Core.Geometry;

namespace Themia.Core.Model;

/// <summary>
/// Moving widgets between views — by pasting, or by dragging onto another display.
/// <para>
/// These live here, on the state, rather than in the window that triggers them: they are the only
/// operations that touch <b>two</b> views at once, so getting them wrong duplicates or loses a
/// widget. Keeping them next to the model means they can be tested without a display attached.
/// </para>
/// </summary>
public static class WidgetTransfer
{
    /// <summary>
    /// Whether a drag that ended over another display should move the widget there.
    /// <para>
    /// Only when the destination shows a <i>different</i> view: when both displays show the same
    /// view the widget already appears on both, so "moving" it would be a no-op that nonetheless
    /// rewrote its position against the wrong screen.
    /// </para>
    /// </summary>
    public static bool ShouldHandOff(bool droppedOnThisDisplay, string sourceViewId, string? targetViewId)
        => !droppedOnThisDisplay
            && targetViewId is not null
            && !string.Equals(targetViewId, sourceViewId, StringComparison.Ordinal);

    /// <summary>
    /// [S-75]/[S-1586] Land a placement: for a move, take the originals out of whatever view they are
    /// in, then add the planned widgets to the target view — all in one go, so a move across views
    /// can neither duplicate a widget nor drop it, and a move back into its own view leaves exactly
    /// one of each.
    /// <para>
    /// A moved widget that still exists lands as <b>itself</b>: the live instance, with whatever its
    /// config became since the snapshot, takes the planned rect. Only an original deleted meanwhile
    /// is put back from the snapshot.
    /// </para>
    /// </summary>
    public static void ApplyPlacement(this AppState state, PastePlan plan, string targetViewId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(plan);

        var removed = new List<WidgetInstance>();
        foreach (var id in plan.RemoveIds)
        {
            foreach (var view in state.Views)
            {
                if (view.Find(id) is not { } widget)
                {
                    continue;
                }

                view.Widgets.Remove(widget);
                removed.Add(widget);
                break;
            }
        }

        var target = state.FindView(targetViewId) ?? state.ActiveView();

        foreach (var planned in plan.Added)
        {
            var original = removed.Find(w => string.Equals(w.Id, planned.Id, StringComparison.Ordinal));
            if (original is null)
            {
                target.Widgets.Add(planned);
                continue;
            }

            removed.Remove(original);
            original.Rect = planned.Rect;
            target.Widgets.Add(original);
        }

        target.Widgets.AddRange(removed);
    }

    /// <summary>
    /// The ids a move would take out of the state: each one that still exists in some view.
    /// </summary>
    public static HashSet<string> RemovalBlock(AppState state, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(ids);

        return new HashSet<string>(ids.Where(id => state.Views.Exists(v => v.Find(id) is not null)), StringComparer.Ordinal);
    }
}
