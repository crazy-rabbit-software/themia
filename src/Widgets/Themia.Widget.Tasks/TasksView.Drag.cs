using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Tasks;
using Windows.Foundation;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-786] Drag a task to reorder it. Outside edit mode the card never captures the pointer, so a
/// row can: a press arms nothing until the pointer has moved <see cref="TaskLayout.DragThreshold"/>,
/// then the row is lifted, follows the pointer, and the rows it passes make way. Letting go commits
/// the order once, through the same path as Move up / Move down. In edit mode the card takes the
/// press first and captures it, so the row never sees the move and the widget moves instead.
/// </summary>
internal sealed partial class TasksView
{
    /// <summary>The pointer's distance from the viewport edge at which a drag scrolls the list.</summary>
    private const double AutoScrollEdge = 16;

    private Border? _pressRow;
    private uint _pressPointer;
    private Point _pressAt;
    private Point _grab;
    private bool _dragging;

    /// <summary>A drag just ended on this row: the tap that follows the release is not a click.</summary>
    private bool _dragEnded;

    private void HookDrag(Border row)
    {
        row.PointerPressed += OnRowPointerPressed;
        row.PointerMoved += OnRowPointerMoved;
        row.PointerReleased += OnRowPointerReleased;
        row.PointerCaptureLost += OnRowPointerCaptureLost;
    }

    private void UnhookDrag(Border row)
    {
        row.PointerPressed -= OnRowPointerPressed;
        row.PointerMoved -= OnRowPointerMoved;
        row.PointerReleased -= OnRowPointerReleased;
        row.PointerCaptureLost -= OnRowPointerCaptureLost;
    }

    private bool ConsumeDragEnd()
    {
        var ended = _dragEnded;
        _dragEnded = false;
        return ended;
    }

    private void OnRowPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        _dragEnded = false;

        if (sender is not Border { Tag: TaskItem } row || _editor is not null || _dragging)
        {
            return;
        }

        var point = args.GetCurrentPoint(row);

        // The marker is a button of its own: a press there ticks the task and arms nothing.
        if (!point.Properties.IsLeftButtonPressed || IsInMarker(args.OriginalSource as DependencyObject, row))
        {
            return;
        }

        _pressRow = row;
        _pressPointer = args.Pointer.PointerId;
        _pressAt = args.GetCurrentPoint(_rowsPanel).Position;
        _grab = point.Position;
    }

    private void OnRowPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_pressRow is not { Tag: TaskItem dragged } row || !ReferenceEquals(sender, row) || args.Pointer.PointerId != _pressPointer)
        {
            return;
        }

        if (!args.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
        {
            // Released somewhere this row did not hear about.
            _pressRow = null;
            return;
        }

        var at = args.GetCurrentPoint(_rowsPanel).Position;

        if (!_dragging)
        {
            if (!TaskLayout.IsDrag(at.X - _pressAt.X, at.Y - _pressAt.Y) || !row.CapturePointer(args.Pointer))
            {
                return;
            }

            _dragging = true;
            ToolTipService.SetToolTip(row, null);
            _rowsPanel.Lift(row);
            ItemFill.Paint(row, ItemRole.Row, ItemState.Hover);
        }

        // One column: the row moves up and down only. Two: it follows the pointer between them.
        var x = _rowsPanel.Columns > 1 ? at.X - _grab.X : 0;
        _rowsPanel.MoveLifted(new Point(x, at.Y - _grab.Y));

        var current = _rowsPanel.SlotOf(row);
        var target = TaskLayout.Retarget(_rowsPanel.Slots, current, at.X, at.Y,
            slot => _rowsPanel.ChildAt(slot) is Border { Tag: TaskItem other } && other.Completed == dragged.Completed);

        if (target != current)
        {
            _rowsPanel.MoveTo(row, target);
        }

        AutoScroll(args);
        args.Handled = true;
    }

    /// <summary>Near an edge of the viewport the list scrolls a little with each move, so a drag can reach a row out of sight.</summary>
    private void AutoScroll(PointerRoutedEventArgs args)
    {
        var y = args.GetCurrentPoint(_scroll).Position.Y;
        var step = y < AutoScrollEdge ? -Fluent.Space2 : y > _scroll.ActualHeight - AutoScrollEdge ? Fluent.Space2 : 0;

        if (step != 0)
        {
            _scroll.ChangeView(null, Math.Clamp(_scroll.VerticalOffset + step, 0, _scroll.ScrollableHeight), null, disableAnimation: true);
        }
    }

    private void OnRowPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (_dragging && ReferenceEquals(sender, _pressRow))
        {
            args.Handled = true;
            FinishDrag(commit: true);
        }

        _pressRow = null;
    }

    /// <summary>Capture taken away mid-drag (edit mode switched on, the window lost): the rows go back.</summary>
    private void OnRowPointerCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        if (_dragging && ReferenceEquals(sender, _pressRow))
        {
            FinishDrag(commit: false);
            _pressRow = null;
        }
    }

    private void FinishDrag(bool commit)
    {
        if (_pressRow is not { } row)
        {
            return;
        }

        _dragging = false;
        _dragEnded = true;
        row.ReleasePointerCaptures();
        ItemFill.Paint(row, ItemRole.Row);

        var ids = _rowsPanel.InSlotOrder().OfType<Border>().Select(b => b.Tag).OfType<TaskItem>().Select(t => t.Id).ToList();
        _rowsPanel.Settle();

        if (commit)
        {
            CommitOrder(ids);
        }
        else
        {
            _rowsPanel.ResetOrder();

            // A redraw held back while the row was lifted (a refresh, a tick) happens now.
            Render();
        }
    }

    private bool IsInMarker(DependencyObject? source, Border row)
    {
        var marker = _rows.FirstOrDefault(r => ReferenceEquals(r.Row, row)).Marker;

        for (var node = source; node is not null && !ReferenceEquals(node, row); node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, marker))
            {
                return true;
            }
        }

        return false;
    }

    private void CancelDrag()
    {
        if (_dragging)
        {
            FinishDrag(commit: false);
        }

        _pressRow = null;
        _dragEnded = false;
    }
}
