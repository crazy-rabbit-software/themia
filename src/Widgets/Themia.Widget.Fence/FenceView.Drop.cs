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
/// [S-13005] Drops on a fence: the desktop's own drag of its icons moves its desktop items in, at the place the mark shows
/// (as placed; a fence with a standing sort takes them into its order); files from outside go into the user's desktop
/// folder through the shell's own drop target (copy, move, link, the right-drag menu, virtual files) and arrive in this
/// fence; over a folder item of the fence the drop is that folder's own. Never in edit mode.
/// </summary>
internal sealed partial class FenceView : IWidgetDropTarget
{
    /// <summary>[S-5587] The folder item a drag hovers (its own shell target answers), or null.</summary>
    private string? _dropKey;

    /// <summary>[S-13005] Where the desktop's drag would go in, shown as a thin mark between cells.</summary>
    private readonly Border _dropMark = new() { Width = FamilyShape.Stroke, IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    private bool _dropMarkAdded;

    DropEffect IWidgetDropTarget.DragOver(IWidgetDrag drag, Point local)
    {
        try
        {
            if (!Acts || WidgetId is null)
            {
                return DropEffect.None;
            }

            var (cell, index) = Hit(local);
            if (cell is { Item.Kind: DesktopItemKind.Folder } && !_host.DesktopItems.Dragging(cell.Key))
            {
                SetDropKey(cell.Key);
                ShowMark(null);
                drag.Place = cell.Item.Name;
                return SurfaceDrops.Single(drag.ForwardOver(cell.Key, DropEffect.None));
            }

            SetDropKey(null);
            drag.Place = _title.Text;
            if (Fences.DeskDragRunning)
            {
                ShowMark(index);
                return DropEffect.Move;
            }

            ShowMark(null);
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
        ShowMark(null);
    }

    DropEffect IWidgetDropTarget.Drop(IWidgetDrag drag, Point local)
    {
        try
        {
            if (!Acts || WidgetId is not { } id)
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
                // A standing sort decides the order: the drop goes at the end of the stored one.
                return Fences.DropDeskDrag(id, _config.Sort is null ? StoredIndex(index) : _config.Items.Count);
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
            ShowMark(null);
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

    /// <summary>[S-13005] The mark before shown item <paramref name="index"/> (after the last for the end), or none.</summary>
    private void ShowMark(int? index)
    {
        if (index is not { } at || _config.Sort is not null)
        {
            _dropMark.Visibility = Visibility.Collapsed;
            return;
        }

        if (!_dropMarkAdded)
        {
            _dropMark.Background = Family.Brush(ColourRole.Accent);
            _canvas.Children.Add(_dropMark);
            _dropMarkAdded = true;
        }

        var cell = CellSize;
        var width = Math.Max(0, ActualWidth);
        var columns = FenceGrid.Columns(width, cell);
        var r = at < _shown.Count ? FenceGrid.Cell(at, columns, cell, width) : FenceGrid.Cell(Math.Max(0, _shown.Count - 1), columns, cell, width) is var last && _shown.Count > 0
            ? last with { X = last.X + last.Width }
            : FenceGrid.Cell(0, columns, cell, width);
        Canvas.SetLeft(_dropMark, r.X - (FamilyShape.Stroke / 2));
        Canvas.SetTop(_dropMark, r.Y + DesktopCell.HighlightTop);
        _dropMark.Height = Math.Max(0, r.Height - DesktopCell.HighlightTop - DesktopCell.Below);
        _dropMark.Visibility = Visibility.Visible;
    }
}
