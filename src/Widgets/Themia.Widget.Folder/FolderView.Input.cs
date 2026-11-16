using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Files;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;
using Windows.System;

namespace Themia.App.Widgets;

/// <summary>[S-1327]/[S-1328]/[S-1332] Pointer, keys, selection visuals ([S-6620]..[S-6629]), and drag and drop.</summary>
internal sealed partial class FolderView : IWidgetDropTarget
{
    private const string DropScope = "folder-drop";

    private readonly List<IDisposable> _itemHovers = [];

    /// <summary>The item a left press landed on, for its release ([S-6621]).</summary>
    private int? _pressed;

    /// <summary>[S-6626] Whether the widget has the keyboard: a selection without it is drawn inactive.</summary>
    private bool _active;

    /// <summary>[S-6412] The item whose empty part was pressed: a click there selects it on release.</summary>
    private int? _blankPress;

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
        _scrollContent.Tapped += OnAreaTapped;
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

    /// <summary>[S-2031] A click in the file area is the widget's (a selection): it never opens the folder over it.</summary>
    private static void OnAreaTapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

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

        // [S-6626] A press takes the keyboard lease ([S-800]): the selection is the active one again.
        _active = true;
        _blankPress = null;
        _pressed = null;

        // [S-6412] Only the icon and the name carry the item; the rest of a row or tile is a click.
        var source = e.OriginalSource as DependencyObject;
        var press = FolderGesture.Classify(
            visual is not null,
            visual is not null && (IsWithin(source, visual.IconBox) || IsWithin(source, visual.Name)));

        if (visual is not null && press == FolderPress.Item)
        {
            // [S-6621] A plain press on a selected item waits for the release, so a drag carries the whole selection.
            _pressed = visual.Index;
            _selection.Press(visual.Index, ctrl, shift);

            var paths = _selection.IsSelected(visual.Entry.FullPath) ? _selection.Selected : [visual.Entry.FullPath];
            _gesture.BeginPress(point.Position.X, point.Position.Y, FolderPress.Item, paths);
        }
        else
        {
            _blankPress = visual?.Index;
            _selection.CancelPress();
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

        if (_gesture.Move(point.Position.X, point.Position.Y) == GestureStep.StartDrag)
        {
            // The pressed row or tile is the drag's element: its picture is what follows the pointer.
            UIElement from = _pressed is { } index && index < _visuals.Count ? _visuals[index].Root : _scrollContent;
            StartDragOut(_gesture.DragPaths, _gesture.Generation, from, e.GetCurrentPoint(from));
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var wasDrag = _gesture.Release();
        var blank = _blankPress;
        var pressed = _pressed;
        _blankPress = null;
        _pressed = null;

        if (wasDrag)
        {
            _selection.CancelPress();
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);

        if (blank is { } beside && beside < _visuals.Count)
        {
            // [S-6412] A click beside the name selects the item, as Explorer's full-row select; it
            // never opens (single-click mode opens from the icon or the name).
            _selection.Click(beside, ctrl, shift);
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

        if (pressed == visual.Index)
        {
            _selection.Release(visual.Index);
            RefreshSelectionVisuals();
        }

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
        _selection.CancelPress();
        _pressed = null;
        _blankPress = null;
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_config.ClickMode == ItemClickMode.Double && VisualAt(e.OriginalSource) is { } visual)
        {
            e.Handled = true;
            OpenEntry(visual.Entry);
        }
    }

    // ---- selection visuals ----------------------------------------------------------------------

    private void HookItem(ItemVisual visual)
    {
        void Entered(object sender, PointerRoutedEventArgs e)
        {
            visual.Hovered = true;
            Paint(visual);
            PointItem(visual, on: true);
        }

        void Exited(object sender, PointerRoutedEventArgs e)
        {
            visual.Hovered = false;
            Paint(visual);
            PointItem(visual, on: false);
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
        // The focused tile places its full name in its own Paint; with no focused tile there is none.
        var focus = _selection.FocusIndex;
        if (focus < 0 || focus >= _visuals.Count || !_visuals[focus].Tile)
        {
            _fullName.Hide();
        }

        foreach (var visual in _visuals)
        {
            Paint(visual);
        }
    }

    /// <summary>
    /// [S-6627] One item's look, the desktop's: hover, selection (inactive without the keyboard), the
    /// focus after keyboard use, a drop target, all through the one selection chrome laid over the
    /// item's fill. [S-6629] A selected tile's cut-short name shows in full.
    /// </summary>
    private void Paint(ItemVisual visual)
    {
        var path = visual.Entry.FullPath;
        var cue = new SelectionCue(
            _selection.IsSelected(path),
            visual.Hovered || PointedElsewhere(visual), // [S-11945] the file pointed at elsewhere on the view, as under the pointer
            _selection.IsFocus(path),
            _selection.Cues,
            _active,
            visual.DropTarget);

        SelectionChrome.Paint(visual.Chrome, cue);

        if (visual.Tile && cue.Focused)
        {
            _fullName.Update(visual.Chrome, visual.Name, cue, renaming: _renaming is not null);
        }
    }

    // ---- keys -----------------------------------------------------------------------------------

    /// <summary>[S-6626] The selection stays, drawn inactive, while the user is in another widget or window.</summary>
    public bool KeepsSelectionWhenLeft => true;

    /// <inheritdoc />
    public bool HandleKey(WidgetKey key)
    {
        if (_renaming is not null && key != WidgetKey.Escape)
        {
            // The rename box has the caret; the lease routes nothing else to a view while text has focus.
            return false;
        }

        // [S-6626] Keys arrive only while the widget holds the lease.
        _active = key != WidgetKey.Deactivate;

        // [S-6623] The selection keys, the same in every item-selecting widget.
        if (key is not (WidgetKey.Escape or WidgetKey.Deactivate) && MoveCursor(key))
        {
            PointCursor();
            return true;
        }

        switch (key)
        {
            case WidgetKey.Enter:
                OpenSelection();
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
                FileMenuEntries.CopyAsPath(_selection.Selected);
                return true;

            case WidgetKey.Properties:
                // [S-6417] Explorer's Alt+Enter: the selection's properties, or this folder's with none.
                ShowProperties();
                return true;

            case WidgetKey.Back:
                if (!Navigates)
                {
                    return false;
                }

                GoBack();
                return true;

            case WidgetKey.Escape:
                // Escape, a press on the empty space, edit mode ([S-802]): drop the focus, the
                // selection and any open rename; the paste target (this folder) is untouched.
                CancelRename();
                _typeAhead.Reset();
                _selection.Clear();
                RefreshSelectionVisuals();
                PointCursor();
                return true;

            case WidgetKey.Deactivate:
                // [S-6626] The user went to another widget or window: the selection stays, drawn
                // inactive as the desktop's; the focus cue and a half-typed name go.
                CommitRename();
                _typeAhead.Reset();
                _selection.Deactivate();
                RefreshSelectionVisuals();
                PointCursor();
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

        _active = true;
        _selection.Type(index);
        RealizeThrough(index);
        RefreshSelectionVisuals();
        BringIntoView(index);
        return true;
    }

    /// <summary>[S-6623] A selection key through the one rule set, grid aware; false for any other key.</summary>
    private bool MoveCursor(WidgetKey key)
    {
        var columns = _plan?.Grid?.Columns ?? 1;
        var row = _plan?.Grid?.PitchY ?? _rowPitch;
        var pageRows = Math.Max(1, (int)(_scroll.ViewportHeight / Math.Max(1, row)));

        if (!_selection.Key(key, ShiftDown(), columns, pageRows))
        {
            return false;
        }

        // [S-1887] End, Page down or Ctrl+A may reach an item not built yet.
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
    /// [S-1332] Drag the items out as XAML's own drag (<see cref="ItemDrag"/>), started here, inside
    /// the pointer handler, with that event's pointer: a drag loop started after it never saw the mouse
    /// until the button was let go. The gesture's generation is checked first, so a release before the
    /// start cancels it rather than starting a drag with the button up.
    /// </summary>
    private void StartDragOut(IReadOnlyList<string> paths, int generation, UIElement from, Microsoft.UI.Input.PointerPoint point)
    {
        if (paths.Count == 0 || _detached || generation != _gesture.Generation)
        {
            return;
        }

        _selection.CancelPress();
        _ = DragOutAsync(new ItemDragOffer(paths, DropEffect.Copy | DropEffect.Move | DropEffect.Link), generation, from, point);
    }

    /// <summary>The drag itself, to its drop or its cancel. The gesture is reset when it ends, unless a press since then owns it.</summary>
    private async Task DragOutAsync(ItemDragOffer offer, int generation, UIElement from, Microsoft.UI.Input.PointerPoint point)
    {
        try
        {
            var effect = await ItemDrag.StartAsync(from, point, offer, () => DragOutGhost(offer.Paths, from, point));
            Themia.Core.Diagnostics.Log.Debug(DropScope, $"a drag of {offer.Paths.Count} item(s) out of the folder ended: {effect}");
        }
        catch (Exception ex)
        {
            Themia.Core.Diagnostics.Log.Error(DropScope, "the drag failed", ex);
        }
        finally
        {
            if (generation == _gesture.Generation)
            {
                _gesture.Release();
            }
        }
    }

    /// <summary>
    /// [S-1332] [S-5564] What the drag out shows under the pointer, as Explorer's: the pressed item's icon
    /// (the bitmap its row or tile already shows, a photo's thumbnail included) in front, up to two more
    /// of the dragged items' icons stacked behind it, and for several the number of dragged items in a
    /// badge; never a name. Composed at once from pixels the view holds (<see cref="ItemDrag.Ghost"/>).
    /// Null while the pressed item still shows its glyph: the drag then shows the row or tile itself.
    /// </summary>
    private DragPicture? DragOutGhost(IReadOnlyList<string> paths, UIElement from, Microsoft.UI.Input.PointerPoint point)
    {
        if (_pressed is not { } index || index >= _visuals.Count)
        {
            return null;
        }

        var pressed = _visuals[index];
        if (pressed.IconBox is not { ActualWidth: > 0, ActualHeight: > 0 } box || ItemDrag.IconOf(pressed.Image.Source) is not { } front)
        {
            return null;
        }

        var icons = new List<GhostIcon> { front };
        var dragged = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        foreach (var visual in _visuals)
        {
            if (icons.Count >= DragGhost.MaxShown)
            {
                break;
            }

            if (!ReferenceEquals(visual, pressed) && dragged.Contains(visual.Entry.FullPath) && ItemDrag.IconOf(visual.Image.Source) is { } other)
            {
                icons.Add(other);
            }
        }

        // As drawn on the surface: through the card's content scale ([S-850]).
        var square = box.TransformToVisual(null).TransformBounds(new Rect(0, 0, box.ActualWidth, box.ActualHeight));
        var pointer = from.TransformToVisual(null).TransformPoint(point.Position);
        return ItemDrag.Ghost(icons, paths.Count, square.Width, new Point(pointer.X - square.X, pointer.Y - square.Y), XamlRoot?.RasterizationScale ?? 1.0);
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
