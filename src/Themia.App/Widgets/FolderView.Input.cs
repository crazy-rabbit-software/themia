using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Files;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Foundation;
using Windows.System;

namespace Themia.App.Widgets;

/// <summary>[S-1327]/[S-1328]/[S-1332] Pointer, keys, selection visuals, and drag and drop.</summary>
internal sealed partial class FolderView : IWidgetDropTarget
{
    private const string DropScope = "folder-drop";

    private readonly List<IDisposable> _itemHovers = [];

    /// <summary>A press on an already-selected item: the selection is narrowed on release, not press, so a multi-item drag keeps it.</summary>
    private int? _deferredClick;

    /// <summary>The selection when an additive (Ctrl) rubber band started.</summary>
    private IReadOnlyCollection<string>? _bandBefore;

    /// <summary>Each item's box in the scroll content's coordinates, taken when a band starts.</summary>
    private List<(int Index, Rect Box)> _bandBoxes = [];

    /// <summary>The cursor ring shows only once the keys have moved it, as Explorer's focus rectangle.</summary>
    private bool _keyboardActive;

    private ItemVisual? _dropVisual;

    private void AttachInput()
    {
        _scrollContent.PointerPressed += OnPointerPressed;
        _scrollContent.PointerMoved += OnPointerMoved;
        _scrollContent.PointerReleased += OnPointerReleased;
        _scrollContent.PointerCanceled += OnPointerCanceled;
        _scrollContent.PointerCaptureLost += OnPointerCanceled;
        _scrollContent.RightTapped += OnBackgroundRightTapped;
        _scrollContent.DoubleTapped += OnDoubleTapped;
        _scrollContent.PointerEntered += OnPointerEntered;

        // [S-1332] Drops come from the surface's one OLE drop target ([S-1021]) through
        // IWidgetDropTarget below, not from XAML's drag events.
    }

    // ---- hit testing ----------------------------------------------------------------------------

    /// <summary>The item an event started in, by walking up to the row or tile that carries its index.</summary>
    private ItemVisual? VisualAt(object? source)
    {
        for (var element = source as DependencyObject; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is FrameworkElement { Tag: int index } fe && ReferenceEquals(VisualTreeHelper.GetParent(fe), _items))
            {
                return index >= 0 && index < _visuals.Count ? _visuals[index] : null;
            }

            if (ReferenceEquals(element, _scrollContent))
            {
                return null;
            }
        }

        return null;
    }

    private ItemVisual? VisualAtPoint(Point point)
    {
        foreach (var element in VisualTreeHelper.FindElementsInHostCoordinates(point, _scrollContent))
        {
            if (VisualAt(element) is { } visual)
            {
                return visual;
            }
        }

        return null;
    }

    // ---- pointer --------------------------------------------------------------------------------

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_scrollContent);
        var props = point.Properties;

        // [S-1326] Mouse back / forward, as in Explorer.
        if (props.IsXButton1Pressed || props.IsXButton2Pressed)
        {
            e.Handled = true;

            if (props.IsXButton1Pressed)
            {
                GoBack();
            }
            else
            {
                GoForward();
            }

            return;
        }

        if (!props.IsLeftButtonPressed)
        {
            return;
        }

        if (_renaming is { } renaming && !IsWithin(e.OriginalSource as DependencyObject, renaming.Box))
        {
            CommitRename();
        }

        var ctrl = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
        var visual = VisualAt(e.OriginalSource);
        _keyboardActive = false;

        if (visual is not null)
        {
            if (!ctrl && !shift && _selection.IsSelected(visual.Entry.FullPath))
            {
                _deferredClick = visual.Index;
            }
            else
            {
                _deferredClick = null;
                _selection.Click(visual.Index, ctrl, shift);
            }

            var paths = _selection.IsSelected(visual.Entry.FullPath) ? _selection.Selected : [visual.Entry.FullPath];
            _gesture.BeginPress(point.Position.X, point.Position.Y, onItem: true, paths);
        }
        else
        {
            _deferredClick = null;
            _bandBefore = ctrl ? [.. _selection.Selected] : null;
            _gesture.BeginPress(point.Position.X, point.Position.Y, onItem: false);
            _bandBoxes = [.. _visuals.Select(v => (v.Index, v.Root.TransformToVisual(_scrollContent).TransformBounds(new Rect(0, 0, v.Root.ActualWidth, v.Root.ActualHeight))))];
        }

        RefreshSelectionVisuals();
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_gesture.Pressed)
        {
            return;
        }

        var point = e.GetCurrentPoint(_scrollContent);

        if (!point.Properties.IsLeftButtonPressed)
        {
            // The release happened somewhere this element never heard of: the gesture is over.
            EndGesture();
            return;
        }

        switch (_gesture.Move(point.Position.X, point.Position.Y))
        {
            case GestureStep.StartDrag:
                StartDragOut(_gesture.DragPaths, _gesture.Generation);
                break;

            case GestureStep.Band:
                if (_scrollContent.PointerCaptures is not { Count: > 0 })
                {
                    _scrollContent.CapturePointer(e.Pointer);
                }

                DrawBand(_gesture.BandRect);
                break;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var (wasDrag, wasBand) = _gesture.Release();
        HideBand();
        _scrollContent.ReleasePointerCaptures();

        if (wasDrag || wasBand)
        {
            _deferredClick = null;
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
        var visual = VisualAt(e.OriginalSource);

        if (visual is null)
        {
            // [S-1327] A click on the background clears the selection (Ctrl keeps it).
            if (!ctrl && !shift && !_selection.IsEmpty)
            {
                _selection.Clear();
                RefreshSelectionVisuals();
            }

            return;
        }

        if (_deferredClick == visual.Index)
        {
            _selection.Click(visual.Index, ctrl: false, shift: false);
            RefreshSelectionVisuals();
        }

        _deferredClick = null;

        // [S-1327] Single-click mode opens on a plain click; Ctrl/Shift clicks only ever select.
        if (_config.ClickMode == ItemClickMode.Single && !ctrl && !shift && e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Pen)
        {
            OpenEntry(visual.Entry);
        }
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e) => EndGesture();

    private void EndGesture()
    {
        _gesture.Release();
        _deferredClick = null;
        HideBand();
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_config.ClickMode == ItemClickMode.Double && VisualAt(e.OriginalSource) is { } visual)
        {
            e.Handled = true;
            OpenEntry(visual.Entry);
        }
    }

    /// <summary>[S-1328] Draw the band and select what it covers.</summary>
    private void DrawBand(BandRect rect)
    {
        Canvas.SetLeft(_band, rect.Left);
        Canvas.SetTop(_band, rect.Top);
        _band.Width = Math.Max(1, rect.Width);
        _band.Height = Math.Max(1, rect.Height);
        _band.Visibility = Visibility.Visible;

        var hits = _bandBoxes.Where(b => rect.Hits(b.Box.X, b.Box.Y, b.Box.Width, b.Box.Height)).Select(b => b.Index);
        _selection.Band(hits, _bandBefore);
        RefreshSelectionVisuals();
    }

    private void HideBand() => _band.Visibility = Visibility.Collapsed;

    // ---- selection visuals ----------------------------------------------------------------------

    private void HookItem(ItemVisual visual)
    {
        void Entered(object sender, PointerRoutedEventArgs e)
        {
            visual.Hovered = true;
            Paint(visual);
        }

        void Exited(object sender, PointerRoutedEventArgs e)
        {
            visual.Hovered = false;
            Paint(visual);
        }

        visual.Root.PointerEntered += Entered;
        visual.Root.PointerExited += Exited;
        visual.Root.PointerCanceled += Exited;

        _itemHovers.Add(new Unhook(() =>
        {
            visual.Root.PointerEntered -= Entered;
            visual.Root.PointerExited -= Exited;
            visual.Root.PointerCanceled -= Exited;
        }));
    }

    private sealed class Unhook(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }

    private void RefreshSelectionVisuals()
    {
        foreach (var visual in _visuals)
        {
            Paint(visual);
        }
    }

    /// <summary>
    /// One item's look, as WinUI's own list draws it: hover and selection share the subtle fill, a
    /// selected row adds the accent pill, a selected tile the accent outline; the keyboard cursor and
    /// a drop target get the accent ring.
    /// </summary>
    private void Paint(ItemVisual visual)
    {
        var selected = _selection.IsSelected(visual.Entry.FullPath);
        var focused = _keyboardActive && string.Equals(_selection.Focus, visual.Entry.FullPath, StringComparison.OrdinalIgnoreCase);
        var tile = visual.Pill is null;

        visual.Root.Background = selected && visual.Hovered
            ? FluentLayout.Themed("SubtleFillColorTertiaryBrush", Colors.Gray)
            : selected || visual.Hovered ? Fluent.RowHover() : Fluent.RowRest();

        if (visual.Pill is { } pill)
        {
            pill.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }

        var ring = focused || visual.DropTarget || (tile && selected);
        visual.Root.BorderBrush = ring
            ? FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue)
            : FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
    }

    // ---- keys -----------------------------------------------------------------------------------

    /// <inheritdoc />
    public bool HandleKey(WidgetKey key)
    {
        if (_renaming is not null && key != WidgetKey.Escape)
        {
            // The rename box has the caret; the lease routes nothing else to a view while text has focus.
            return false;
        }

        switch (key)
        {
            case WidgetKey.Up: return MoveCursor(FolderMove.Up, ShiftDown());
            case WidgetKey.Down: return MoveCursor(FolderMove.Down, ShiftDown());
            case WidgetKey.Left: return MoveCursor(FolderMove.Left, false);
            case WidgetKey.Right: return MoveCursor(FolderMove.Right, false);
            case WidgetKey.ShiftLeft: return MoveCursor(FolderMove.Left, true);
            case WidgetKey.ShiftRight: return MoveCursor(FolderMove.Right, true);
            case WidgetKey.Home: return MoveCursor(FolderMove.Home, ShiftDown());
            case WidgetKey.End: return MoveCursor(FolderMove.End, ShiftDown());
            case WidgetKey.PageUp: return MoveCursor(FolderMove.PageUp, ShiftDown());
            case WidgetKey.PageDown: return MoveCursor(FolderMove.PageDown, ShiftDown());

            case WidgetKey.Enter:
                OpenSelection();
                return true;

            case WidgetKey.Space:
                if (_selection.FocusIndex is var focus and >= 0)
                {
                    _selection.Click(focus, ctrl: CtrlDown(), shift: false);
                    RefreshSelectionVisuals();
                }

                return true;

            case WidgetKey.Delete:
                _ = DeleteSelectionAsync();
                return true;

            case WidgetKey.Rename:
                RenameSelection();
                return true;

            case WidgetKey.Menu:
                ShowMenuFromKeyboard();
                return true;

            case WidgetKey.Copy:
                CopySelection(cut: false);
                return true;

            case WidgetKey.Cut:
                CopySelection(cut: true);
                return true;

            case WidgetKey.Paste:
                _ = PasteAsync(CurrentPath);
                return true;

            case WidgetKey.SelectAll:
                _selection.SelectAll();
                RefreshSelectionVisuals();
                return true;

            case WidgetKey.Back:
                if (!Navigates)
                {
                    return false;
                }

                GoBack();
                return true;

            case WidgetKey.Escape:
                // Also how the lease says it ended ([S-802]): drop the cursor, the selection and any
                // open rename; the paste target (this folder) is untouched.
                CancelRename();
                _keyboardActive = false;
                _typeAhead.Reset();
                _selection.Clear();
                RefreshSelectionVisuals();
                return true;

            default:
                return false;
        }
    }

    /// <summary>The name typed so far ([S-1993]).</summary>
    private readonly TypeAhead _typeAhead = new();

    /// <summary>
    /// [S-1993] Type-ahead ([S-1980]), as in Explorer: a name's first letters select that item alone
    /// and move the cursor to it; the same letter again steps through the items starting with it.
    /// </summary>
    public bool HandleText(char character)
    {
        if (_detached || _renaming is not null || _shown.Count == 0)
        {
            return false;
        }

        if (_typeAhead.Type(character, Environment.TickCount64, [.. _shown.Select(e => e.Name)], _selection.FocusIndex) is not { } index)
        {
            return _typeAhead.Prefix.Length > 0;
        }

        _keyboardActive = true;
        _selection.Click(index, ctrl: false, shift: false);
        RealizeThrough(index);
        RefreshSelectionVisuals();
        BringIntoView(index);
        return true;
    }

    private bool MoveCursor(FolderMove move, bool extend)
    {
        var columns = _plan?.Grid?.Columns ?? 1;
        var row = _plan?.Grid?.CellHeight ?? _plan?.RowHeight ?? Fluent.RowHeightCompact;
        var pageRows = Math.Max(1, (int)(_scroll.ViewportHeight / Math.Max(1, row)));

        _keyboardActive = true;
        _selection.Move(move, columns, pageRows, extend);

        // [S-1887] End or Page down may land on an item not built yet.
        RealizeThrough(_selection.FocusIndex);
        RefreshSelectionVisuals();

        BringIntoView(_selection.FocusIndex);
        return true;
    }

    /// <summary>
    /// Scroll an item into view. A row realised just now ([S-1887]) has no layout yet, and asking
    /// before it has one scrolls to where the panel guessed it was: lay out first.
    /// </summary>
    private void BringIntoView(int index)
    {
        if (index < 0 || index >= _visuals.Count)
        {
            return;
        }

        _scroll.UpdateLayout();
        _visuals[index].Root.StartBringIntoView();
    }

    // ---- drag out -------------------------------------------------------------------------------

    /// <summary>
    /// [S-1332] Hand the dragged items to an OLE drag. Posted, not run inside the pointer handler: OLE's
    /// modal loop must not nest in XAML's input dispatch. The gesture's generation is checked when it
    /// runs, so a release in between cancels it rather than starting a drag with the button up.
    /// </summary>
    private void StartDragOut(IReadOnlyList<string> paths, int generation)
    {
        if (paths.Count == 0 || DispatcherQueue is not { } queue)
        {
            return;
        }

        _deferredClick = null;

        queue.TryEnqueue(() =>
        {
            if (_detached || generation != _gesture.Generation)
            {
                return;
            }

            try
            {
                DragDropInterop.DragOut(paths, DropEffect.Copy | DropEffect.Move | DropEffect.Link, Window());
            }
            catch (Exception ex)
            {
                Themia.Core.Diagnostics.Log.Error(DropScope, "the drag failed", ex);
            }
            finally
            {
                _gesture.Release();
            }
        });
    }

    // ---- drop in --------------------------------------------------------------------------------

    /// <summary>Where a drop at <paramref name="local"/> (this view's DIPs) goes: the folder row under the pointer, else the folder on screen.</summary>
    private (string Destination, ItemVisual? Folder) DropDestination(Point local)
    {
        // Root coordinates: FindElementsInHostCoordinates hit-tests against the window's content.
        if (_scroll.Visibility == Visibility.Visible && VisualAtPoint(TransformToVisual(null).TransformPoint(local)) is { Entry.IsDirectory: true } folder)
        {
            return (folder.Entry.FullPath, folder);
        }

        return (CurrentPath, null);
    }

    /// <summary>
    /// [S-1332] What a drop into <paramref name="destination"/> does: plain drop moves, Ctrl copies
    /// (<see cref="FileDrops.Choose"/>); virtual files can only be copied. None when every dragged item
    /// is already there or is the destination's own ancestor (<see cref="FileDrops.Pastable"/>), so the
    /// drag says "nothing to do" instead of offering a move that would not move anything.
    /// </summary>
    private static DropEffect DropEffectFor(WidgetDrag drag, string destination)
    {
        if (drag.Paths.Count == 0)
        {
            return drag.VirtualFiles > 0 && drag.Allowed.HasFlag(DropEffect.Copy) ? DropEffect.Copy : DropEffect.None;
        }

        var effect = FileDrops.Choose(drag.Ctrl, drag.Shift, drag.Allowed & (DropEffect.Copy | DropEffect.Move));
        return effect != DropEffect.None && FileDrops.Pastable(drag.Paths, destination, effect).Count > 0 ? effect : DropEffect.None;
    }

    /// <inheritdoc />
    DropEffect IWidgetDropTarget.DragOver(WidgetDrag drag, Point local)
    {
        if (CurrentPath.Length == 0)
        {
            SetDropVisual(null);
            return DropEffect.None;
        }

        var (destination, folder) = DropDestination(local);
        var effect = DropEffectFor(drag, destination);

        if (effect == DropEffect.None)
        {
            SetDropVisual(null);
            drag.EndForward();
            return DropEffect.None;
        }

        // The shell's target for that folder has the last word (a read-only share answers None).
        var answered = SurfaceDrops.Single(drag.ForwardOver(destination, effect));
        SetDropVisual(answered == DropEffect.None ? null : folder);
        drag.Place = FolderPath.DisplayName(destination);
        return answered;
    }

    /// <inheritdoc />
    void IWidgetDropTarget.DragLeave() => SetDropVisual(null);

    private void SetDropVisual(ItemVisual? folder)
    {
        if (ReferenceEquals(folder, _dropVisual))
        {
            return;
        }

        if (_dropVisual is { } old)
        {
            old.DropTarget = false;
            Paint(old);
        }

        _dropVisual = folder;

        if (folder is not null)
        {
            folder.DropTarget = true;
            Paint(folder);
        }
    }

    /// <summary>
    /// [S-1332] Files from Explorer, Outlook or another folder widget, into the folder row under the
    /// pointer or the folder on screen, carried out by that folder's shell drop target with the effect
    /// <see cref="DropEffectFor"/> chose. The watcher ([S-1333]) shows the result.
    /// </summary>
    DropEffect IWidgetDropTarget.Drop(WidgetDrag drag, Point local)
    {
        SetDropVisual(null);

        if (CurrentPath.Length == 0)
        {
            return DropEffect.None;
        }

        var (destination, _) = DropDestination(local);
        var effect = DropEffectFor(drag, destination);

        if (effect == DropEffect.None)
        {
            return DropEffect.None;
        }

        var done = drag.ForwardDrop(destination, effect);
        Themia.Core.Diagnostics.Log.Debug(DropScope, $"drop of {drag.ItemCount} item(s) into the folder: {done}");
        return done;
    }

    private static bool IsWithin(DependencyObject? element, DependencyObject ancestor)
    {
        for (var at = element; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (ReferenceEquals(at, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
