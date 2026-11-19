using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.Widget.Fence;

/// <summary>
/// [S-13005] Drops on a fence: the desktop's own drag of its icons moves its desktop items in, at the place the preview
/// shows (as placed; a fence with a standing sort takes them into its order); files from outside go into the user's desktop
/// folder through the shell's own drop target (copy, move, link, the right-drag menu, virtual files) and arrive in this
/// fence; over a folder item of the fence the drop is that folder's own. Never in edit mode.
/// <para>
/// [S-13021] While the desktop's drag is over the fence its items show where they would land: a see-through ghost of each
/// dragged item at its slot, the fence's other items stepping aside (gliding, the canvas's reposition transition); let go,
/// the ghosts become the items in place, so the drop glides in rather than jumps. The owner, 2026-11-19: "when placing them
/// inside it should smoothly move them there and it should show a ghost preview instead of just the blue line."
/// </para>
/// </summary>
internal sealed partial class FenceView : IWidgetDropTarget
{
    /// <summary>[S-5587] The folder item a drag hovers (its own shell target answers), or null.</summary>
    private string? _dropKey;

    /// <summary>[S-13021] The order the fence shows while the desktop's drag is over it (its items, the dragged ones at their slots); null otherwise.</summary>
    private List<string>? _preview;

    /// <summary>[S-13021] The ghost cells of dragged items this fence does not hold, by key: kept after a drop until the items arrive in them.</summary>
    private readonly Dictionary<string, Cell> _ghosts = new(DesktopItemKeys.Comparer);

    DropEffect IWidgetDropTarget.DragOver(IWidgetDrag drag, Point local)
    {
        try
        {
            // [S-13023] A search fence holds no file: nothing is dropped into it.
            if (!Acts || WidgetId is null || Searches)
            {
                return DropEffect.None;
            }

            var (cell, index) = Hit(local);
            if (cell is { Item.Kind: DesktopItemKind.Folder } && !_host.DesktopItems.Dragging(cell.Key))
            {
                SetDropKey(cell.Key);
                ShowPreview(null);
                drag.Place = cell.Item.Name;
                return SurfaceDrops.Single(drag.ForwardOver(cell.Key, DropEffect.None));
            }

            SetDropKey(null);
            drag.Place = _title.Text;
            if (Fences.DeskDragRunning)
            {
                ShowPreview(index);
                return DropEffect.Move;
            }

            ShowPreview(null);
            return Fences.DesktopFolder is { } desktop && !drag.IsEmpty ? SurfaceDrops.Single(drag.ForwardOver(desktop, DropEffect.None)) : DropEffect.None;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a drag over a fence failed", ex);
            return DropEffect.None;
        }
    }

    void IWidgetDropTarget.DragLeave()
    {
        SetDropKey(null);
        ShowPreview(null);
    }

    DropEffect IWidgetDropTarget.Drop(IWidgetDrag drag, Point local)
    {
        try
        {
            if (!Acts || WidgetId is not { } id || Searches)
            {
                return DropEffect.None;
            }

            var (cell, index) = Hit(local);
            if (cell is { Item.Kind: DesktopItemKind.Folder } && !_host.DesktopItems.Dragging(cell.Key))
            {
                return drag.ForwardDrop(cell.Key, DropEffect.None);
            }

            if (Fences.DeskDragRunning)
            {
                // [S-13021] The ghosts stay where they stand: the items arrive in them (none came in: they go).
                _landing = true;

                // A standing sort decides the order: the drop goes at the end of the stored one.
                var effect = Fences.DropDeskDrag(id, _config.Sort is null ? StoredIndex(index) : _config.Items.Count);
                _landing = effect != DropEffect.None;
                return effect;
            }

            return drag.IsEmpty ? DropEffect.None : Fences.DropFiles(id, drag);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a drop on a fence failed", ex);
            return DropEffect.None;
        }
        finally
        {
            SetDropKey(null);
            ShowPreview(null);
            _landing = false;
        }
    }

    /// <summary>The item under a point of the view (its DIPs) and the place in the shown order a drop there goes; over the title bar, the end.</summary>
    private (Cell? Cell, int Index) Hit(Point local)
    {
        if (_config.RolledUp || local.Y < FenceGrid.TitleHeight)
        {
            return (null, _shown.Count);
        }

        var cell = CellSize;
        var width = Math.Max(0, ActualWidth);
        var columns = FenceGrid.Columns(width, cell);
        var y = local.Y - FenceGrid.TitleHeight + _scroll.VerticalOffset;
        var at = FenceGrid.IndexAt(local.X, y, columns, cell, width, _shown.Count);
        var index = FenceGrid.InsertAt(local.X, y, columns, cell, width, _shown.Count);
        return (at >= 0 && _cells.TryGetValue(_shown[at], out var hit) ? hit : null, index);
    }

    /// <summary>A place in the shown order as a place in the stored one (they are one order while no sort stands).</summary>
    private int StoredIndex(int shown)
        => shown >= _shown.Count ? _config.Items.Count : Math.Max(0, _config.Items.FindIndex(k => DesktopItemKeys.Comparer.Equals(k, _shown[shown])));

    private void SetDropKey(string? key)
    {
        if (!string.Equals(_dropKey, key, StringComparison.OrdinalIgnoreCase))
        {
            _dropKey = key;
            PaintCells();
        }
    }

    /// <summary>[S-13021] A drop is landing: the ghosts are kept for the items to arrive in, not taken down with the preview.</summary>
    private bool _landing;

    /// <summary>
    /// [S-13021] The preview of the desktop's drag let go before shown item <paramref name="index"/> (a standing sort: at the
    /// end), or none: the dragged items at their slots as ghosts (this fence's own dragged items fade in their new place),
    /// the others stepping aside; none puts every item back.
    /// </summary>
    private void ShowPreview(int? index)
    {
        if (index is not { } at)
        {
            if (_preview is null)
            {
                return;
            }

            _preview = null;
            if (!_landing)
            {
                DropGhosts();
            }

            Layout();
            PaintCells();
            return;
        }

        var dragged = _fixture.On ? _fixture.Dragged : Fences.DraggedItems;
        if (dragged.Count == 0)
        {
            return;
        }

        var order = FenceGrid.Preview(_shown, dragged.Select(i => i.Key).ToList(), _config.Sort is null ? at : _shown.Count, DesktopItemKeys.Comparer);
        if (_preview is not null && _preview.SequenceEqual(order, DesktopItemKeys.Comparer))
        {
            return;
        }

        foreach (var item in dragged)
        {
            if (!_cells.ContainsKey(item.Key) && !_ghosts.ContainsKey(item.Key))
            {
                var ghost = MakeCell(item.Key);
                ghost.Item = item;
                ghost.Name.Text = item.Name;
                ghost.Root.IsHitTestVisible = false;
                _ghosts[item.Key] = ghost;
                _canvas.Children.Add(ghost.Root);
                _loads.Add(LoadIconAsync(ghost));
            }
        }

        _preview = order;
        Layout();
        PaintCells();
    }

    /// <summary>[S-13021] Take every ghost down.</summary>
    private void DropGhosts()
    {
        foreach (var ghost in _ghosts.Values)
        {
            DropCell(ghost);
        }

        _ghosts.Clear();
    }

    /// <summary>[S-13021] A ghost the item has now arrived in: it becomes that item's cell where it stands (no jump), or null.</summary>
    private Cell? Adopt(string key)
    {
        if (!_ghosts.Remove(key, out var ghost))
        {
            return null;
        }

        ghost.Root.IsHitTestVisible = true;
        return ghost;
    }

    /// <summary>[S-13021] Whether <paramref name="key"/> is drawn as a ghost now: a dragged item in the preview.</summary>
    private bool Ghosted(string key)
        => _preview is not null && (_ghosts.ContainsKey(key) || (_fixture.On ? _fixture.Dragged.Exists(i => DesktopItemKeys.Comparer.Equals(i.Key, key)) : _host.DesktopItems.Dragging(key)));
}
