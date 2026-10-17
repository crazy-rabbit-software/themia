using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Diagnostics;
using Themia.Core.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Themia.App.Shell;

/// <summary>
/// [S-3802] The one way a settings list is put in order: drag a row onto another (the dragged item
/// takes the target's place), or press Alt+Shift+Up / Down on a focused row. No list has arrow
/// buttons or Move up / Move down entries. The Views page, the list editors
/// (<see cref="FluentLayout.ListEditor{T}"/>) and the mail columns all use this, so the drag
/// threshold, what a press inside a control does and where the focus goes after a keyboard move are
/// the same everywhere.
/// <para>
/// One instance is shared by the rows of one list. The drag is started by hand from the pointer
/// rather than through <c>CanDrag</c>: a row's header can be a button, which takes the press for
/// itself, and a drag that waits for an unhandled press never begins. A mouse press that travels
/// past the threshold starts it; touch keeps panning the page. A press in one of the row's own
/// controls (a text box selects text by dragging) never starts one.
/// </para>
/// <para>
/// Both the drop and the keyboard move run after the event has been handled, because the move
/// rebuilds the list and with it the row the event is still being raised on. After a keyboard move
/// the moved item's new row takes the focus back. UI thread only.
/// </para>
/// </summary>
/// <param name="drop">Put the dragged item where the target is (dragged, target).</param>
/// <param name="move">Move the item one place (-1 up, +1 down); false when it is at that end.</param>
public sealed class RowReorder<TKey>(Action<TKey, TKey> drop, Func<TKey, int, bool> move)
    where TKey : notnull
{
    private const string Scope = "reorder";

    /// <summary>The smallest pointer travel, in DIPs, that turns a press on a row into a drag ([S-913]'s threshold).</summary>
    private const double DragThreshold = 4;

    /// <summary>
    /// The item whose row takes the focus once the list a keyboard move rebuilt is back. Static
    /// because a page may rebuild with a new instance; one keyboard move is under way at a time.
    /// </summary>
    private static TKey? _refocus;

    /// <summary>The item being dragged, or none.</summary>
    private TKey? _dragged;
    private bool _dragging;

    /// <summary>
    /// Make <paramref name="frame"/> the row of <paramref name="key"/>: draggable, a drop target
    /// for the list's other rows, and answering the reorder shortcut.
    /// </summary>
    /// <param name="blocksDrag">Also refuses a drag that starts on this element (beyond the row's controls).</param>
    public void Attach(FrameworkElement frame, TKey key, Func<DependencyObject, bool>? blocksDrag = null)
    {
        ArgumentNullException.ThrowIfNull(frame);

        AttachDrag(frame, key, blocksDrag);
        AttachKeys(frame, key);

        if (_refocus is { } refocus && EqualityComparer<TKey>.Default.Equals(refocus, key))
        {
            _refocus = default;
            frame.Loaded += (_, _) =>
            {
                if (FocusManager.FindFirstFocusableElement(frame) is Control first)
                {
                    first.Focus(FocusState.Keyboard);
                }
            };
        }
    }

    private void AttachKeys(FrameworkElement frame, TKey key)
    {
        // Tunnelling, so a row's own arrow handling (an expander's header) never sees the shortcut.
        frame.PreviewKeyDown += (_, args) =>
        {
            var arrow = args.Key switch
            {
                VirtualKey.Up => ReorderArrow.Up,
                VirtualKey.Down => ReorderArrow.Down,
                _ => (ReorderArrow?)null,
            };

            if (arrow is not { } pressed)
            {
                return;
            }

            var delta = ReorderKeys.Delta(pressed, IsKeyDown(VirtualKey.Menu), IsKeyDown(VirtualKey.Shift), IsKeyDown(VirtualKey.Control));
            if (delta == 0)
            {
                return;
            }

            args.Handled = true;
            Later(frame, "a keyboard move", () =>
            {
                _refocus = key;
                if (!move(key, delta))
                {
                    _refocus = default; // ignore: the first item has nowhere further up to go, the last nowhere down.
                }
            });
        };
    }

    private void AttachDrag(FrameworkElement frame, TKey key, Func<DependencyObject, bool>? blocksDrag)
    {
        Windows.Foundation.Point? pressed = null;
        frame.AllowDrop = true;

        frame.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, args) =>
        {
            var point = args.GetCurrentPoint(frame);
            pressed = args.Pointer.PointerDeviceType == PointerDeviceType.Mouse && point.Properties.IsLeftButtonPressed
                ? point.Position
                : null;
        }), handledEventsToo: true);

        frame.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => pressed = null), handledEventsToo: true);
        frame.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler((_, _) => pressed = null), handledEventsToo: true);

        frame.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, args) =>
        {
            if (pressed is not { } start || _dragging)
            {
                return;
            }

            var point = args.GetCurrentPoint(frame);
            if (!point.Properties.IsLeftButtonPressed)
            {
                pressed = null;
                return;
            }

            if (Math.Abs(point.Position.X - start.X) < DragThreshold && Math.Abs(point.Position.Y - start.Y) < DragThreshold)
            {
                return;
            }

            pressed = null;
            if (args.OriginalSource is DependencyObject source && InsideControl(source, frame, blocksDrag))
            {
                return;
            }

            _dragged = key;
            _dragging = true;
            _ = DragAsync(frame, point);
        }), handledEventsToo: true);

        frame.DragStarting += (_, args) => args.Data.RequestedOperation = DataPackageOperation.Move;
        frame.DropCompleted += (_, _) => EndDrag();

        frame.DragOver += (_, args) =>
        {
            if (_dragging && _dragged is { } dragged && !EqualityComparer<TKey>.Default.Equals(dragged, key))
            {
                args.AcceptedOperation = DataPackageOperation.Move;
                args.DragUIOverride.IsGlyphVisible = false;
                args.DragUIOverride.IsCaptionVisible = false;
                args.Handled = true;
            }
        };

        frame.Drop += (_, args) =>
        {
            if (!_dragging || _dragged is not { } dragged || EqualityComparer<TKey>.Default.Equals(dragged, key))
            {
                return;
            }

            args.Handled = true;
            EndDrag();
            Later(frame, "a drop", () => drop(dragged, key));
        };
    }

    private void EndDrag()
    {
        _dragging = false;
        _dragged = default;
    }

    private async Task DragAsync(UIElement element, PointerPoint point)
    {
        try
        {
            await element.StartDragAsync(point);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "a row's drag could not start", ex);
        }
        finally
        {
            EndDrag();
        }
    }

    /// <summary>Run <paramref name="action"/> once the event in progress has finished.</summary>
    private static void Later(FrameworkElement frame, string what, Action action)
    {
        if (frame.DispatcherQueue is not { } queue || !queue.TryEnqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _refocus = default;
                Log.Error(Scope, $"could not reorder a list after {what}", ex);
            }
        }))
        {
            Log.Error(Scope, $"could not queue the reorder after {what}");
        }
    }

    /// <summary>
    /// Whether the press began somewhere a drag must not start: in one of the row's own controls
    /// (a text box, a switch, a list, a slider, a button, a picker) or wherever the list says.
    /// </summary>
    private static bool InsideControl(DependencyObject source, FrameworkElement frame, Func<DependencyObject, bool>? blocksDrag)
    {
        for (var node = source; node is not null && node != frame; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
        {
            if (node is TextBox or PasswordBox or RichEditBox or AutoSuggestBox or NumberBox or ToggleSwitch or ComboBox
                or Slider or Button or CheckBox or RadioButton or CalendarDatePicker or DatePicker or TimePicker or SplitButton)
            {
                return true;
            }

            if (blocksDrag?.Invoke(node) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsKeyDown(VirtualKey key)
        => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
}
