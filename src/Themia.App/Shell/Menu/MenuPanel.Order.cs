using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>[S-3732] An ordered list's lines, dragged up and down inside their level.</summary>
internal partial class MenuPanel
{
    /// <summary>[S-3732] A list's line being dragged (or pressed, before it travels far enough).</summary>
    private LineDrag? _drag;

    /// <summary>[S-3732] A drag just ended: the click its release raises is not a click on the line.</summary>
    private bool _swallowClick;

    /// <summary>[S-3732] The line a keyboard move moved: it takes the focus back once its list is drawn again.</summary>
    private string? _refocus;

    /// <summary>[S-3732] A press on a list's line, and the drag it became once it travelled far enough (<see cref="ListDrag"/>).</summary>
    private sealed class LineDrag(Level level, LineView view, double startY)
    {
        public Level Level { get; } = level;

        public LineView View { get; } = view;

        public ListDrag Gesture { get; } = new(startY);
    }

    /// <summary>
    /// [S-3732] A line of an ordered list (<see cref="MenuOrder"/>): a mouse press that travels up or
    /// down past the threshold drags it among the list's lines, a drop line showing where it lands,
    /// and the release moves the item there (one change, saved once); [S-3748] Ctrl+Up / Down (and
    /// Alt+Shift+Up / Down, [S-3802]) moves it a place, the focus staying on it. Its tooltip and
    /// automation help say both. Drawn in the menu's own window with the pointer captured by the
    /// list (<see cref="DragMoved"/>), never an OLE drag: the surface takes no drops and a drag image
    /// would hide the list it is dropped into.
    /// </summary>
    private void Orderable(Level level, LineView view)
    {
        var button = view.Button;
        var hint = Str.Menu_ReorderHint.Text();
        ToolTipService.SetToolTip(button, hint);
        AutomationProperties.SetHelpText(button, hint);

        button.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            var point = e.GetCurrentPoint(level.Lines);
            _drag = e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && point.Properties.IsLeftButtonPressed
                ? new LineDrag(level, view, point.Position.Y)
                : null;
        }), handledEventsToo: true);

        button.AddHandler(PointerMovedEvent, new PointerEventHandler((_, e) => DragMoved(level, view, e)), handledEventsToo: true);
        button.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => DragReleased(view)), handledEventsToo: true);

        // Tunnelling, so the line's own arrow keys (a submenu's) never see the shortcut.
        button.PreviewKeyDown += (_, e) =>
        {
            var arrow = e.Key switch
            {
                Windows.System.VirtualKey.Up => ReorderArrow.Up,
                Windows.System.VirtualKey.Down => ReorderArrow.Down,
                _ => (ReorderArrow?)null,
            };

            if (arrow is not { } pressed || view.Node.Order is not { } order)
            {
                return;
            }

            var delta = ReorderKeys.MenuDelta(pressed, IsKeyDown(Windows.System.VirtualKey.Menu), IsKeyDown(Windows.System.VirtualKey.Shift), IsKeyDown(Windows.System.VirtualKey.Control));
            if (delta == 0)
            {
                return;
            }

            e.Handled = true;
            var to = order.Index + delta;
            if (to < 0 || to >= order.Count)
            {
                return; // ignore: the first item has nowhere further up to go, the last nowhere down.
            }

            _refocus = view.Node.Key;
            Later(() => order.Move(order.Index, to));
        };
    }

    /// <summary>
    /// [S-3732] [S-3747] The pointer moved over a pressed line: past the threshold the drag starts and
    /// the list itself takes the pointer, so the line's button lets go of it (its release raises no
    /// click) and the moves and the release come to the list (<see cref="OnListMoved"/>,
    /// <see cref="OnListReleased"/>, <see cref="OnListCaptureLost"/>).
    /// </summary>
    private void DragMoved(Level level, LineView view, PointerRoutedEventArgs e)
    {
        if (_drag is not { } drag || !ReferenceEquals(drag.View, view) || drag.Gesture.Active)
        {
            return;
        }

        var point = e.GetCurrentPoint(level.Lines);
        if (!drag.Gesture.Move(point.Position.Y, point.Properties.IsLeftButtonPressed))
        {
            if (!point.Properties.IsLeftButtonPressed)
            {
                _drag = null;
            }

            return;
        }

        _swallowClick = true;
        view.Root.Opacity = 0.5;
        CloseFrom(_levels.IndexOf(level) + 1);

        level.Lines.PointerMoved -= OnListMoved;
        level.Lines.PointerMoved += OnListMoved;
        level.Lines.PointerReleased -= OnListReleased;
        level.Lines.PointerReleased += OnListReleased;
        level.Lines.PointerCaptureLost -= OnListCaptureLost;
        level.Lines.PointerCaptureLost += OnListCaptureLost;
        if (!level.Lines.CapturePointer(e.Pointer))
        {
            Log.Debug(Scope, "a list's drag could not take the pointer; the line's own release ends it");
        }

        ShowDrop(drag, point.Position.Y);
    }

    /// <summary>[S-3732] The list holds the pointer while one of its lines is dragged over it.</summary>
    private void OnListMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is { Gesture.Active: true } drag && ReferenceEquals(sender, drag.Level.Lines))
        {
            ShowDrop(drag, e.GetCurrentPoint(drag.Level.Lines).Position.Y);
        }
    }

    private void OnListReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is { } drag && ReferenceEquals(sender, drag.Level.Lines))
        {
            Finish(drag.Gesture.Release());
        }
    }

    /// <summary>[S-3747] A capture lost with the button up is the release being handled: it drops; with it down it cancels.</summary>
    private void OnListCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is { } drag && ReferenceEquals(sender, drag.Level.Lines))
        {
            Finish(drag.Gesture.CaptureLost(e.GetCurrentPoint(drag.Level.Lines).Properties.IsLeftButtonPressed));
        }
    }

    /// <summary>[S-3732] The line's own release: a click before the drag started, or a drag the list could not take the pointer for.</summary>
    private void DragReleased(LineView view)
    {
        if (_drag is { } drag && ReferenceEquals(drag.View, view))
        {
            Finish(drag.Gesture.Release());
        }
    }

    private static bool IsKeyDown(Windows.System.VirtualKey key)
        => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    /// <summary>[S-3732] The lines of the dragged line's list, top to bottom: the level's lines of the same order.</summary>
    private static List<LineView> OrderLines(LineDrag drag)
        => [.. drag.Level.Views.OfType<LineView>().Where(v => v.Node.Order is { } o && o.Move == drag.View.Node.Order!.Move)];

    /// <summary>[S-3732] Draw the drop line in the gap the pointer is nearest (<see cref="ListDrop.Gap"/>).</summary>
    private void ShowDrop(LineDrag drag, double y)
    {
        var lines = OrderLines(drag);
        if (lines.Count == 0)
        {
            return;
        }

        var boxes = lines.Select(v => v.Root.TransformToVisual(drag.Level.Lines).TransformBounds(new Rect(0, 0, v.Root.ActualWidth, v.Root.ActualHeight))).ToList();
        var gap = ListDrop.Gap([.. boxes.Select(b => b.Y + (b.Height / 2))], y);
        if (!drag.Gesture.Aim(gap))
        {
            return;
        }

        var at = gap < boxes.Count ? boxes[gap].Y : boxes[^1].Y + boxes[^1].Height;
        var overlay = drag.Level.Overlay;
        overlay.Children.Clear();
        var mark = new Rectangle
        {
            Height = 2,
            Width = Math.Max(0, boxes[0].Width - (Fluent.Space2 * 2)),
            RadiusX = 1,
            RadiusY = 1,
            Fill = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue),
        };
        Canvas.SetLeft(mark, boxes[0].X + Fluent.Space2);
        Canvas.SetTop(mark, at - 1);
        overlay.Children.Add(mark);
    }

    /// <summary>[S-3732] The drag is abandoned (its list drawn again or closed): nothing moves.</summary>
    private void EndDrag(bool drop)
    {
        if (_drag is { } drag)
        {
            Finish(drop ? drag.Gesture.Release() : drag.Gesture.Cancel());
        }
    }

    /// <summary>[S-3732] The drag is over: the drop line goes, and where it was dropped the item moves there, once.</summary>
    private void Finish(ListDragEnd end)
    {
        if (_drag is not { } drag)
        {
            return;
        }

        _drag = null;
        drag.View.Root.Opacity = 1;
        drag.Level.Overlay.Children.Clear();
        drag.Level.Lines.PointerMoved -= OnListMoved;
        drag.Level.Lines.PointerReleased -= OnListReleased;
        drag.Level.Lines.PointerCaptureLost -= OnListCaptureLost;
        drag.Level.Lines.ReleasePointerCaptures();
        if (end == ListDragEnd.None)
        {
            return;
        }

        // The click a release may still raise runs before this is queued back: it is swallowed, then the next counts.
        Later(() => _swallowClick = false);

        if (end == ListDragEnd.Drop && drag.View.Node.Order is { } order)
        {
            var to = ListDrop.Target(order.Index, drag.Gesture.Gap);
            if (to != order.Index)
            {
                Later(() => order.Move(order.Index, to));
            }
        }
    }

#if DEBUG
    /// <summary>
    /// Debug renders only: show the deepest open level's ordered line <paramref name="from"/> being
    /// dragged, its drop line in <paramref name="gap"/> ([S-3732]), as a pointer would hold it.
    /// </summary>
    internal bool DebugDrag(int from, int gap)
    {
        if (_levels.Count == 0)
        {
            return false;
        }

        var level = _levels[^1];
        var lines = level.Views.OfType<LineView>().Where(v => v.Node.Order is not null).ToList();
        if (from < 0 || from >= lines.Count || gap < 0 || gap > lines.Count)
        {
            return false;
        }

        UpdateLayout();
        var drag = new LineDrag(level, lines[from], 0);
        drag.Gesture.Move(ListDrag.Threshold, buttonDown: true);
        _drag = drag;
        lines[from].Root.Opacity = 0.5;
        var middles = lines.Select(v => v.Root.TransformToVisual(level.Lines).TransformPoint(new Point(0, v.Root.ActualHeight / 2)).Y).ToList();
        ShowDrop(drag, gap == 0 ? double.NegativeInfinity : middles[gap - 1] + 1);
        return true;
    }
#endif
}
