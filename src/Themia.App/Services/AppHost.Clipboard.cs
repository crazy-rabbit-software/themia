using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Services;

/// <summary>
/// [S-1592] The selection commands: copy, cut, paste, duplicate, move, delete and layering. Each acts
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

    /// <summary>[S-1589] Duplicate: copies of the selection follow the pointer until a click drops them.</summary>
    public void DuplicateSelected() => PlaceSelection(PlacementMode.Copy);

    /// <summary>[S-1590] Move: the selection itself follows the pointer, to this display or any other.</summary>
    public void MoveSelected() => PlaceSelection(PlacementMode.Move);

    private void PlaceSelection(PlacementMode mode)
    {
        if (SelectionView is { } view && PlacementPayload.FromView(view, Selection.Ids, mode) is { } payload)
        {
            BeginPlacement(payload);
        }
    }

    /// <summary>Delete every selected widget in one write.</summary>
    public void DeleteSelected()
    {
        if (SelectionView is not { } view)
        {
            return;
        }

        var ids = Selection.Ids.ToList();
        Mutate(_ =>
        {
            view.Widgets.RemoveAll(w => ids.Contains(w.Id));
        }, undo: UndoAction.Delete);
        Select(null);

        // [S-881] Delete asks nothing first ([B9]): the notice is the way back.
        OfferUndo(Str.Shell_Undo_DeletedWidgets.Plural(ids.Count));
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
