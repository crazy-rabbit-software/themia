using Themia.Core.Desktop;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Services;

/// <summary>
/// [S-1592] The selection commands: copy, cut, paste, delete and layering. Each acts
/// on the whole selection; the ones that produce or move widgets start a placement ([S-1587]).
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>The selection's view, when something is selected and the view still exists.</summary>
    private ViewState? SelectionView => Selection.IsEmpty ? null : State.FindView(Selection.ViewId);

    /// <summary>[S-72] Snapshot the selection onto the clipboard.</summary>
    public void CopySelected()
    {
        if (SelectionView is { } view)
        {
            Clipboard.Copy(view, Selection.Ids);
        }
    }

    /// <summary>[S-73] Mark the selection to be moved by the next paste. Nothing moves yet.</summary>
    public void CutSelected()
    {
        if (SelectionView is { } view)
        {
            Clipboard.Cut(view, Selection.Ids);
        }
    }

    /// <summary>[S-74] Paste: the clipboard's widgets follow the pointer until a click drops them.</summary>
    public void Paste()
    {
        if (Clipboard.Payload is { } payload)
        {
            BeginPlacement(payload);
        }
    }

    /// <summary>[S-1592] [S-5524] Edit mode's Delete: every selected widget off this view in one write (<see cref="DesktopDelete"/>; no file touched).</summary>
    public void DeleteSelected()
    {
        if (SelectionView is null)
        {
            return;
        }

        var removal = DesktopDelete.Of([.. Selection.Ids.Select(id => new DeskDeleting(id, null))], editMode: true, State.Preferences.LockWidgets);
        RemoveWidgets(removal.Widgets);
        Select(null);
    }

    /// <summary>
    /// [S-881] [S-14320] The widgets <paramref name="ids"/> taken off the desktop, from every view that holds them: one undoable
    /// step of the one history (Ctrl+Z, here and on the desktop, puts them back where they were, with their config) and the way
    /// back offered, as Delete asks nothing first ([B9]). No file is touched: a fence's items stay on the free desktop where it
    /// showed them ([S-13009]; the undo brings it back with them). Edit mode's Delete and the desktop's both come here.
    /// </summary>
    private void RemoveWidgets(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var gone = new HashSet<string>(ids, StringComparer.Ordinal);
        CloseRenameFor(null);
        Mutate(state =>
        {
            foreach (var view in state.Views)
            {
                var fences = view.Widgets.Where(w => gone.Contains(w.Id) && w.Config is IHoldsDesktopItems).ToList();
                view.Widgets.RemoveAll(w => gone.Contains(w.Id));
                foreach (var fence in fences)
                {
                    PutBackFenceItems(view, fence);
                }
            }
        }, undo: UndoAction.Delete);
        OfferUndo(Str.Shell_Undo_DeletedWidgets.Plural(gone.Count));
    }

    /// <summary>
    /// [S-874]/[S-1592] Bring the selection to the front, or send it to the back, keeping the order
    /// the selected widgets had among themselves.
    /// </summary>
    public void ZOrderSelected(bool front)
    {
        if (SelectionView is not { } view)
        {
            return;
        }

        var ids = SelectionRules.InView(view, Selection.Ids);
        if (!front)
        {
            ids.Reverse();
        }

        Mutate(state =>
        {
            foreach (var id in ids)
            {
                // ignore: false only means that widget was already at the front (back)
                _ = front ? view.BringToFront(id) : view.SendToBack(id);
            }
        }, undo: UndoAction.Arrange);
    }

    /// <summary>[S-1582] Ctrl+A: everything on the display under the pointer.</summary>
    public void SelectAllUnderCursor() => (SurfaceUnderCursor() ?? _windows.FirstOrDefault())?.SelectAll();
}
