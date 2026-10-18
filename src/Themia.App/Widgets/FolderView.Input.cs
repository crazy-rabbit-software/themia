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
using Themia.Sdk.WinUI;
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

    /// <summary>The selection when a Shift or Ctrl rubber band started ([S-6413]).</summary>
    private IReadOnlyCollection<string>? _bandBefore;

    private BandMode _bandMode;

    /// <summary>[S-6412] The item whose empty part was pressed: a click there selects it on release.</summary>
    private int? _blankPress;

    /// <summary>[S-6415] The band's pointer in the scroller's own coordinates, for the edge scroll.</summary>
    private Point _bandPointer;

    private bool _bandTicking;
    private TimeSpan? _bandFrame;

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
        _blankPress = null;

        // [S-6412] Only the icon and the name carry the item; the rest of a row or tile starts a band.
        var source = e.OriginalSource as DependencyObject;
        var press = FolderLasso.Classify(
            visual is not null,
            visual is not null && (IsWithin(source, visual.IconBox) || IsWithin(source, visual.Name)));

        if (visual is not null && press == FolderPress.Item)
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
            _gesture.BeginPress(point.Position.X, point.Position.Y, FolderPress.Item, paths);
        }
        else
        {
            _deferredClick = null;
            _blankPress = visual?.Index;
            _bandMode = FolderLasso.Mode(ctrl, shift);
            _bandBefore = _bandMode == BandMode.Replace ? null : [.. _selection.Selected];
            _gesture.BeginPress(point.Position.X, point.Position.Y, press);
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

                _blankPress = null;
                _bandPointer = e.GetCurrentPoint(_scroll).Position;
                DrawBand(_gesture.BandRect);
                UpdateBandScroll();
                break;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var (wasDrag, wasBand) = _gesture.Release();
        HideBand();
        _scrollContent.ReleasePointerCaptures();
        var blank = _blankPress;
        _blankPress = null;

        if (wasDrag || wasBand)
        {
            _deferredClick = null;
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);

        if (blank is { } pressed && pressed < _visuals.Count)
        {
            // [S-6412] A click beside the name selects the item, as Explorer's full-row select; it
            // never opens (single-click mode opens from the icon or the name).
            _selection.Click(pressed, ctrl, shift);
            RefreshSelectionVisuals();
            return;
        }

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
        _blankPress = null;
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

    /// <summary>
    /// [S-1328] Draw the band and select what it covers. The cover is worked out from the layout's
    /// cells ([S-6414]), so rows the list has not built yet ([S-1887]) are selected too.
    /// </summary>
    private void DrawBand(BandRect raw)
    {
        var rect = FolderLasso.Clamp(raw, _scrollContent.ActualWidth, _scrollContent.ActualHeight);
        Canvas.SetLeft(_band, rect.Left);
        Canvas.SetTop(_band, rect.Top);
        _band.Width = Math.Max(1, rect.Width);
        _band.Height = Math.Max(1, rect.Height);
        _band.Visibility = Visibility.Visible;

        var (columns, cellWidth, cellHeight) = _plan?.Grid is { } grid
            ? (Math.Max(1, grid.Columns), grid.CellWidth, grid.CellHeight)
            : (1, _items.ActualWidth, _plan?.RowHeight ?? FolderLayout.RowHeight(_config.Rows));

        _selection.Band(FolderLasso.Hits(rect, _shown.Count, columns, cellWidth, cellHeight), _bandBefore, _bandMode);
        RefreshSelectionVisuals();
    }

    private void HideBand()
    {
        _band.Visibility = Visibility.Collapsed;
        StopBandScroll();
    }

    // ---- the band's edge scroll ([S-6415]) ------------------------------------------------------

    /// <summary>Run the frame tick only while the band's pointer is in an edge zone; stop it otherwise.</summary>
    private void UpdateBandScroll()
    {
        var scrolls = _gesture.BandWasActive && _gesture.Pressed
            && FolderLasso.ScrollVelocity(_bandPointer.Y, _scroll.ActualHeight) != 0;

        if (scrolls && !_bandTicking)
        {
            _bandTicking = true;
            _bandFrame = null;
            CompositionTarget.Rendering += OnBandFrame;
        }
        else if (!scrolls)
        {
            StopBandScroll();
        }
    }

    private void StopBandScroll()
    {
        if (_bandTicking)
        {
            _bandTicking = false;
            CompositionTarget.Rendering -= OnBandFrame;
        }
    }

    /// <summary>One frame of edge scroll: move the list, then stretch the band to where the pointer now is over the content.</summary>
    private void OnBandFrame(object? sender, object args)
    {
        try
        {
            if (_detached || !_gesture.Pressed)
            {
                StopBandScroll();
                return;
            }

            var now = (args as RenderingEventArgs)?.RenderingTime ?? TimeSpan.Zero;
            var seconds = _bandFrame is { } last ? (now - last).TotalSeconds : 0;
            _bandFrame = now;

            var velocity = FolderLasso.ScrollVelocity(_bandPointer.Y, _scroll.ActualHeight);
            if (velocity == 0)
            {
                StopBandScroll();
                return;
            }

            var from = _scroll.VerticalOffset;
            var to = FolderLasso.Step(from, velocity, seconds, _scroll.ScrollableHeight);

            if (to != from)
            {
                _scroll.ChangeView(null, to, null, disableAnimation: true);
            }

            // The transform still has this frame's offset; the content point is where the new one puts it.
            var at = _scroll.TransformToVisual(_scrollContent).TransformPoint(_bandPointer);
            if (_gesture.Move(at.X, at.Y + (to - from)) == GestureStep.Band)
            {
                DrawBand(_gesture.BandRect);
            }
        }
        catch (Exception ex)
        {
            // A frame callback: nothing may escape to the compositor.
            StopBandScroll();
            Themia.Core.Diagnostics.Log.Error(Scope, "the band's edge scroll failed", ex);
        }
    }

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

        // [S-6513] The item layer's row, in the item's state.
        ItemFill.Paint(visual.Root, ItemRole.Row, selected && visual.Hovered ? ItemState.SelectedHover
            : selected ? ItemState.Selected
            : visual.Hovered ? ItemState.Hover
            : ItemState.Rest);

        if (visual.Pill is { } pill)
        {
            pill.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }

        var ring = focused || visual.DropTarget || (tile && selected);
        visual.Root.BorderBrush = ring
            ? ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue)
            : ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
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
                // [S-6417] Shift+F10 is Explorer's way to the full shell menu; the Menu key opens ours.
                if (ShiftDown())
                {
                    ShowShellMenuFromKeyboard();
                }
                else
                {
                    ShowMenuFromKeyboard();
                }

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

            case WidgetKey.CopyAsPath:
                // [S-6417] Explorer's Ctrl+Shift+C: the selection's paths, quoted, or nothing with none.
                CopyPaths(_selection.Selected);
                return true;

            case WidgetKey.Properties:
                // [S-6417] Explorer's Alt+Enter: the selection's properties, or this folder's with none.
                ShowProperties();
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
                _widgetHost.FileOps.DragOut(paths, DropEffect.Copy | DropEffect.Move | DropEffect.Link, Window());
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
    private static DropEffect DropEffectFor(IWidgetDrag drag, string destination)
    {
        if (drag.Paths.Count == 0)
        {
            return drag.VirtualFiles > 0 && drag.Allowed.HasFlag(DropEffect.Copy) ? DropEffect.Copy : DropEffect.None;
        }

        var effect = FileDrops.Choose(drag.Ctrl, drag.Shift, drag.Allowed & (DropEffect.Copy | DropEffect.Move));
        return effect != DropEffect.None && FileDrops.Pastable(drag.Paths, destination, effect).Count > 0 ? effect : DropEffect.None;
    }

    /// <inheritdoc />
    DropEffect IWidgetDropTarget.DragOver(IWidgetDrag drag, Point local)
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
    DropEffect IWidgetDropTarget.Drop(IWidgetDrag drag, Point local)
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
