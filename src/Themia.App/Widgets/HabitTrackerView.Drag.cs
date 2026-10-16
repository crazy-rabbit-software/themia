using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Habits;
using Windows.Foundation;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1621] Drag a habit to reorder it (original S-13). Outside edit mode the card never captures
/// the pointer, so a row can: a press arms nothing until the pointer has moved
/// <see cref="HabitDrag.Threshold"/> (a click on the name still focuses, a double-click still
/// renames), then the row is lifted and follows the pointer while the rows it passes step aside.
/// Letting go commits the order once, through the same write as Move up / Move down, so a drag
/// that comes home writes nothing. A press on the tick box or inside the rename box arms nothing.
/// In edit mode the card takes the press first and captures it, so the widget moves instead.
/// <para>
/// Nothing is reparented while the pointer is captured: the rows stay where the list put them and
/// only their <see cref="TranslateTransform"/>s move, because removing a captured element from the
/// tree would end the drag.
/// </para>
/// </summary>
internal sealed partial class HabitTrackerView
{
    /// <summary>The pointer's distance from the viewport edge at which a drag scrolls the list.</summary>
    private const double AutoScrollEdge = 16;

    private HabitRow? _pressRow;
    private uint _pressPointer;
    private Point _pressAt;
    private bool _dragging;
    private int _dragFrom;
    private int _dragTo;
    private double _dragPitch;

    private void HookDrag(Border host)
    {
        host.PointerPressed += OnRowPointerPressed;
        host.PointerMoved += OnRowPointerMoved;
        host.PointerReleased += OnRowPointerReleased;
        host.PointerCaptureLost += OnRowPointerCaptureLost;
    }

    private void UnhookDrag(Border host)
    {
        host.PointerPressed -= OnRowPointerPressed;
        host.PointerMoved -= OnRowPointerMoved;
        host.PointerReleased -= OnRowPointerReleased;
        host.PointerCaptureLost -= OnRowPointerCaptureLost;
    }

    private void OnRowPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (_dragging || _editor is not null || !Writable || _rows.Count < 2)
        {
            return;
        }

        var row = _rows.FirstOrDefault(r => ReferenceEquals(r.Host, sender));
        if (row is null
            || !args.GetCurrentPoint(row.Host).Properties.IsLeftButtonPressed
            || row.IsInBox(args.OriginalSource as DependencyObject))
        {
            return;
        }

        _pressRow = row;
        _pressPointer = args.Pointer.PointerId;
        _pressAt = args.GetCurrentPoint(_list).Position;
    }

    private void OnRowPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_pressRow is not { } row || !ReferenceEquals(sender, row.Host) || args.Pointer.PointerId != _pressPointer)
        {
            return;
        }

        if (!args.GetCurrentPoint(row.Host).Properties.IsLeftButtonPressed)
        {
            // Released somewhere this row did not hear about.
            _pressRow = null;
            return;
        }

        var at = args.GetCurrentPoint(_list).Position;
        var dy = at.Y - _pressAt.Y;

        if (!_dragging)
        {
            if (!HabitDrag.IsDrag(at.X - _pressAt.X, dy) || !row.Host.CapturePointer(args.Pointer))
            {
                return;
            }

            _dragging = true;
            _dragFrom = _dragTo = _rows.IndexOf(row);
            _dragPitch = row.Host.ActualHeight;
            Canvas.SetZIndex(row.Host, 1);
            ItemFill.Paint(row.Host, ItemRole.Row, ItemState.Hover);
        }

        // Keep the lifted row inside the list: it may not be dragged off either end.
        var top = -_dragFrom * _dragPitch;
        var bottom = (_rows.Count - 1 - _dragFrom) * _dragPitch;
        Offset(row.Host).Y = Math.Clamp(dy, top, bottom);

        var target = HabitDrag.Target(_dragFrom, _rows.Count, dy, _dragPitch);
        if (target != _dragTo)
        {
            _dragTo = target;

            for (var i = 0; i < _rows.Count; i++)
            {
                if (i != _dragFrom)
                {
                    Offset(_rows[i].Host).Y = HabitDrag.Shift(i, _dragFrom, _dragTo, _dragPitch);
                }
            }
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
        if (_dragging && _pressRow is { } row && ReferenceEquals(sender, row.Host))
        {
            args.Handled = true;
            FinishDrag(commit: true);
        }

        _pressRow = null;
    }

    /// <summary>Capture taken away mid-drag (edit mode switched on, the list rebuilt): the rows go back.</summary>
    private void OnRowPointerCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        if (_dragging && _pressRow is { } row && ReferenceEquals(sender, row.Host))
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
        row.Host.ReleasePointerCaptures();
        ItemFill.Paint(row.Host, ItemRole.Row);
        Canvas.SetZIndex(row.Host, 0);

        foreach (var each in _rows)
        {
            Offset(each.Host).Y = 0;
        }

        // The pool raises its change synchronously, so the rows are rebuilt in their new order
        // before the next frame: there is no flash of the old order between the two.
        if (commit && _dragTo != _dragFrom)
        {
            _focusId = row.Id;
            Move(row.Id, _dragTo - _dragFrom);
        }
    }

    private void CancelDrag()
    {
        if (_dragging)
        {
            FinishDrag(commit: false);
        }

        _pressRow = null;
    }

    private static TranslateTransform Offset(UIElement element)
    {
        if (element.RenderTransform is not TranslateTransform shift)
        {
            shift = new TranslateTransform();
            element.RenderTransform = shift;
        }

        return shift;
    }
}
