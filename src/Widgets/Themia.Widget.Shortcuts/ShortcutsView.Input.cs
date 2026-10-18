using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Shortcuts;
using Windows.Foundation;
using Windows.System;

namespace Themia.App.Widgets;

/// <summary>
/// The Shortcuts widget's input: selection by click, Ctrl, Shift and rubber band ([S-1390]), the
/// keyboard while the widget holds the lease ([S-1390]), the item menu ([S-1387]), rename in place,
/// remove with one step of undo, and search ([S-1385]).
/// <para>
/// Pointer presses are never marked handled and never captured on the press: in edit mode the card
/// takes the pointer for a drag and this view simply stops seeing the gesture (the Scratchpad's
/// rule). The rubber band captures only once the pointer has moved past a small threshold.
/// </para>
/// </summary>
internal sealed partial class ShortcutsView
{
    /// <summary>How far the pointer must travel from a background press before it is a band, not a click.</summary>
    private const double BandThreshold = 5;

    private Point? _bandStart;
    private bool _bandActive;
    private bool _cellPressed;
    private VirtualKeyModifiers _pressModifiers;

    private Cell? _renaming;
    private TextBox? _renameBox;

    // ---- cells ---------------------------------------------------------------------------------

    private void OnCellPressed(object sender, PointerRoutedEventArgs args)
    {
        if (sender is not Border { Tag: Cell cell })
        {
            return;
        }

        // The root's handler runs after this one (the press bubbles); it must not start a band.
        _cellPressed = true;
        _pressModifiers = args.KeyModifiers;

        if (!args.GetCurrentPoint(cell.Root).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_renaming == cell)
        {
            return;
        }

        var ctrl = args.KeyModifiers.HasFlag(VirtualKeyModifiers.Control);
        var shift = args.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);

        _selection.Click(Order(), cell.Item.Id, ctrl, shift);
        PaintAll();

        // [S-1410] A press on an item may become a drag to reorder it.
        ArmDrag(cell, args);
    }

    private void OnCellTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is not Border { Tag: Cell cell } || _config.ClickMode != ItemClickMode.Single)
        {
            return;
        }

        // A modified click selects; only a plain one opens.
        if (_pressModifiers.HasFlag(VirtualKeyModifiers.Control) || _pressModifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            return;
        }

        args.Handled = true;
        Open([cell]);
    }

    private void OnCellDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    {
        if (sender is not Border { Tag: Cell cell } || _config.ClickMode != ItemClickMode.Double || _renaming == cell)
        {
            return;
        }

        args.Handled = true;
        Open([cell]);
    }

    private void OnCellRightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        if (sender is not Border { Tag: Cell cell })
        {
            return;
        }

        _selection.ContextPress(cell.Item.Id);
        PaintAll();

        try
        {
            ItemMenu.Show(cell.Root, args, MenuFor(cell));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not open a shortcut's menu", ex);
        }
    }

    // ---- the rubber band ([S-1390]) -----------------------------------------------------------------

    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        var onCell = _cellPressed;
        _cellPressed = false;

        if (onCell || _detached || _cells.Count == 0 || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Only a press on the items' own area: the search box is not background.
        var point = args.GetCurrentPoint(_surface).Position;
        var scroll = args.GetCurrentPoint(_scroll).Position;

        if (scroll.Y < 0 || scroll.Y > _scroll.ActualHeight)
        {
            return;
        }

        _bandStart = point;
        _pressModifiers = args.KeyModifiers;
    }

    private void OnRootPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (MoveDrag(args))
        {
            return;
        }

        if (_bandStart is not { } start || _detached)
        {
            return;
        }

        if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // The button came up where this view never saw it (the card had the pointer): not a band.
            EndBand();
            return;
        }

        var point = args.GetCurrentPoint(_surface).Position;

        if (!_bandActive)
        {
            if (Math.Abs(point.X - start.X) < BandThreshold && Math.Abs(point.Y - start.Y) < BandThreshold)
            {
                return;
            }

            if (!CapturePointer(args.Pointer))
            {
                EndBand();
                return;
            }

            _bandActive = true;
            _selection.BeginBand(additive: _pressModifiers.HasFlag(VirtualKeyModifiers.Control));
            // The fill is the accent at a fifth, the stroke the accent itself: Explorer's marquee. A
            // brush of its own, made per band so it follows the theme: the themed brush is shared,
            // and dimming it would dim the accent everywhere.
            var accent = ThemeBrush.Of("AccentFillColorDefaultBrush", Microsoft.UI.Colors.DodgerBlue);
            _band.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                (accent as Microsoft.UI.Xaml.Media.SolidColorBrush)?.Color ?? Microsoft.UI.Colors.DodgerBlue) { Opacity = 0.2 };
            _band.Stroke = accent;
            _band.Visibility = Visibility.Visible;
        }

        var band = new Rect(
            Math.Min(start.X, point.X),
            Math.Min(start.Y, point.Y),
            Math.Abs(point.X - start.X),
            Math.Abs(point.Y - start.Y));

        Canvas.SetLeft(_band, band.X);
        Canvas.SetTop(_band, band.Y);
        _band.Width = band.Width;
        _band.Height = band.Height;

        var touched = new List<string>();

        foreach (var cell in _cells)
        {
            var bounds = cell.Root.TransformToVisual(_surface).TransformBounds(new Rect(0, 0, cell.Root.ActualWidth, cell.Root.ActualHeight));
            var overlap = band;
            overlap.Intersect(bounds);

            if (!overlap.IsEmpty && overlap.Width > 0 && overlap.Height > 0)
            {
                touched.Add(cell.Item.Id);
            }
        }

        _selection.Band(Order(), touched);
        PaintAll();
        args.Handled = true;
    }

    private void OnRootPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (ReleaseDrag())
        {
            args.Handled = true;
            return;
        }

        if (_bandStart is null)
        {
            return;
        }

        if (!_bandActive)
        {
            // A background click that never became a band clears the selection, as in Explorer.
            if (_selection.Clear())
            {
                PaintAll();
            }
        }

        EndBand();
    }

    private void OnRootPointerLost(object sender, PointerRoutedEventArgs args)
    {
        EndBand();
        EndReorder(commit: false);
    }

    /// <summary>Tear the band down on every way out: release, capture lost, cancel, a missing button-up.</summary>
    private void EndBand()
    {
        _bandStart = null;

        if (!_bandActive)
        {
            return;
        }

        _bandActive = false;
        _selection.EndBand();
        _band.Visibility = Visibility.Collapsed;
        ReleasePointerCaptures();
    }

    // ---- keys ([S-1390]) -------------------------------------------------------------------------

    /// <inheritdoc />
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        var order = Order();

        switch (key)
        {
            case WidgetKey.Escape:
                _typeAhead.Reset();
                if (_renaming is not null)
                {
                    EndRename(commit: false);
                    return true;
                }

                if (_searchOpen)
                {
                    CloseSearch();
                    return true;
                }

                var had = _selection.Clear();
                PaintAll();
                return had;

            case WidgetKey.Enter:
                var chosen = Selected();
                if (chosen.Count == 0)
                {
                    return false;
                }

                Open(chosen);
                return true;

            case WidgetKey.Space:
                if (!_selection.ToggleCursor())
                {
                    return false;
                }

                PaintAll();
                return true;

            case WidgetKey.Delete:
                return Remove(Selected());

            case WidgetKey.Rename:
                if (CursorCell() is not { } renamed || _selection.Count > 1)
                {
                    return false;
                }

                BeginRename(renamed);
                return true;

            case WidgetKey.MoveUp or WidgetKey.MoveDown:
                if (_renaming is not null || CursorCell() is not { } moved)
                {
                    return false;
                }

                Move(moved, key == WidgetKey.MoveUp ? -1 : +1);
                return true;

            case WidgetKey.Menu:
                if (CursorCell() is not { } target)
                {
                    return false;
                }

                ShowMenuAt(target);
                return true;

            default:
                if (!_selection.Move(order, key, Columns()))
                {
                    return false;
                }

                PaintAll();
                CursorCell()?.Root.StartBringIntoView();
                return true;
        }
    }

    /// <summary>The name typed so far ([S-1995]).</summary>
    private readonly TypeAhead _typeAhead = new();

    /// <summary>
    /// [S-1995] Type-ahead ([S-1980]): a shortcut's first letters select it alone and move the cursor
    /// to it. Not while its rename box or the search box is open: those are typed into.
    /// </summary>
    public bool HandleText(char character)
    {
        if (_detached || _renaming is not null || _searchOpen || _cells.Count == 0)
        {
            return false;
        }

        var current = _selection.Cursor is { } id ? _cells.FindIndex(cell => cell.Item.Id == id) : -1;
        if (_typeAhead.Type(character, Environment.TickCount64, [.. _cells.Select(cell => cell.Name.Text)], current) is not { } index)
        {
            return _typeAhead.Prefix.Length > 0;
        }

        _selection.Click(Order(), _cells[index].Item.Id, ctrl: false, shift: false);
        PaintAll();
        _cells[index].Root.StartBringIntoView();
        return true;
    }

    /// <summary>How many items an Up / Down step skips: the grid's columns, or the list's.</summary>
    private int Columns() => _config.Style == ShortcutsStyle.Grid
        ? Math.Max(1, _shape.Tiles.Columns)
        : Math.Max(1, _shape.Rows.Columns);

    private Cell? CursorCell() => _selection.Cursor is { } id ? _cells.Find(cell => cell.Item.Id == id) : null;

    /// <summary>The selected cells in visible order, or the cursor's when nothing is selected.</summary>
    private List<Cell> Selected()
    {
        var ids = _selection.Ordered(Order());

        if (ids.Count == 0 && CursorCell() is { } cursor)
        {
            return [cursor];
        }

        return _cells.Where(cell => ids.Contains(cell.Item.Id)).ToList();
    }

    private void SelectAll()
    {
        _selection.SelectAll(Order());
        PaintAll();
    }

    // ---- the item menu ([S-1387]) --------------------------------------------------------------------

    private void ShowMenuAt(Cell cell)
    {
        try
        {
            ItemMenu.Open(cell.Root, MenuFor(cell));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not open a shortcut's menu", ex);
        }
    }

    private IEnumerable<ItemMenuEntry> MenuFor(Cell cell)
    {
        var selected = Selected();
        var many = selected.Count > 1 && selected.Contains(cell);

        foreach (var entry in ShortcutRules.Menu(cell.Target, many ? selected.Count : 1, cell.Item.Layout, RecycleBinState.CanEmpty(_widgetHost.RecycleBin.Current, _widgetHost.RecycleBin.Emptying)))
        {
            if (entry.Command is not { } command)
            {
                yield return ItemMenuEntry.Separator;
                continue;
            }

            yield return command switch
            {
                ShortcutCommand.Open => new(Glyphs.Open,
                    cell.Item.IsLayout ? ShortcutsStr.WidgetsC_Shortcuts_Menu_OpenLayout.Text() : Str.Common_Open.Text(),
                    () => Open([cell]), Disabled: entry.Disabled),
                ShortcutCommand.EditLayout => new(Glyphs.Edit, ShortcutsStr.Shortcut_EditLayoutMenu.Text(), () => _widgetHost.AppLayouts.Edit(cell.Item.Id),
                    Disabled: !_widgetHost.AppLayouts.CanEdit),
                ShortcutCommand.OpenAll => new(Glyphs.Open, ShortcutsStr.WidgetsC_Shortcuts_Menu_OpenMany.Plural(selected.Count), () => Open(selected)),
                ShortcutCommand.CopyUrl => new(Glyphs.Copy, ShortcutsStr.Shortcut_CopyUrl.Text(), () => Copy(cell.Target.Value)),
                ShortcutCommand.CopyPath => new(Glyphs.Copy, ShortcutsStr.Shortcut_CopyPath.Text(), () => Copy(cell.Target.Value)),
                ShortcutCommand.OpenFileLocation => new(Glyphs.Folder, Str.Shortcut_OpenFileLocation.Text(), () => Reveal(cell)),
                ShortcutCommand.Rename => new(Glyphs.Rename, Str.Common_Rename.Text(), () => BeginRename(cell), Disabled: entry.Disabled, Quick: QuickCommand.Rename),

                // [S-4864] The Recycle Bin widget's own Empty ([S-4863]): Windows asks first.
                ShortcutCommand.EmptyRecycleBin => new(Glyphs.RecycleBin, ShortcutsStr.RecycleBin_EmptyEllipsis.Text(), EmptyBin, Disabled: entry.Disabled),
                _ => new(Glyphs.Delete,
                    many ? ShortcutsStr.WidgetsC_Shortcuts_Menu_RemoveMany.Plural(selected.Count) : Str.Common_Remove.Text(),
                    () => Remove(many ? selected : [cell]), Danger: true),
            };
        }
    }

    /// <summary>[S-4864] Empty the bin through the one implementation ([S-4863]). Inert in a preview card.</summary>
    private void EmptyBin()
    {
        if (!_detached && _surfaceHost?.IsPreview != true)
        {
            _ = _widgetHost.RecycleBin.EmptyAsync();
        }
    }

    /// <summary>
    /// The one launch path: each target is classified again by <see cref="ShellLauncher"/> at the
    /// moment it opens. Inert in a preview card.
    /// </summary>
    private void Open(IReadOnlyList<Cell> cells)
    {
        if (_detached || _surfaceHost?.IsPreview == true)
        {
            return;
        }

        foreach (var cell in cells)
        {
            if (cell.Item.IsLayout)
            {
                // [S-1407] On the display this widget is on.
                _widgetHost.AppLayouts.Launch(cell.Item.Layout, Window());
            }
            else if (cell.Target.IsValid)
            {
                _widgetHost.Launcher.Open(cell.Item.Target);
            }
        }
    }

    private void Reveal(Cell cell)
    {
        if (_surfaceHost?.IsPreview != true)
        {
            _widgetHost.Launcher.OpenContainingFolder(cell.Item.Target);
        }
    }

    private void Copy(string text)
    {
        if (!_widgetHost.CopyText(text))
        {
            Log.Warn(Scope, "could not copy a shortcut's target to the clipboard");
        }
    }

    /// <summary>[S-3802] Alt+Shift+Up / Down: the cursor's item one place earlier or later.</summary>
    private void Move(Cell cell, int delta)
    {
        if (ShortcutRules.Move(_config.Items, cell.Item.Id, delta))
        {
            Save();
            Changed(rebuild: true);
        }
    }

    /// <summary>
    /// Remove items, remembering where they were for one step of "Undo remove" on the card's menu:
    /// the original's Ctrl+Z op log does not exist here, and a remove one key away deserves a way back.
    /// </summary>
    private bool Remove(IReadOnlyList<Cell> cells)
    {
        if (cells.Count == 0)
        {
            return false;
        }

        var removed = new List<(int Index, ShortcutItem Item)>();

        foreach (var cell in cells)
        {
            var index = _config.Items.IndexOf(cell.Item);

            if (index >= 0)
            {
                removed.Add((index, cell.Item));
            }
        }

        if (removed.Count == 0)
        {
            return false;
        }

        foreach (var (_, item) in removed)
        {
            _config.Items.Remove(item);
        }

        _lastRemoved = removed.OrderBy(entry => entry.Index).ToList();
        _selection.Clear();
        Save();
        Changed(rebuild: true);
        return true;
    }

    private void UndoRemove()
    {
        if (_lastRemoved is not { Count: > 0 } removed)
        {
            return;
        }

        _lastRemoved = null;

        foreach (var (index, item) in removed)
        {
            _config.Items.Insert(Math.Min(index, _config.Items.Count), item);
        }

        Save();
        Changed(rebuild: true);
    }

    // ---- rename in place ---------------------------------------------------------------------------

    private void BeginRename(Cell cell)
    {
        EndRename(commit: true);

        if (_detached || !_cells.Contains(cell) || cell.Name.Parent is not Panel parent)
        {
            return;
        }

        var box = new TextBox
        {
            Text = cell.Item.Label.Length > 0 ? cell.Item.Label : NameOf(cell.Item),
            PlaceholderText = LaunchTargets.DisplayName(null, cell.Item.Target, _config.ShowFileExtension),
            MinHeight = 0,
            Padding = new Thickness(Fluent.Space1, 2, Fluent.Space1, 2),
            IsSpellCheckEnabled = false,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (parent is Grid)
        {
            Grid.SetColumn(box, Grid.GetColumn(cell.Name));
        }

        box.KeyDown += OnRenameKeyDown;
        box.LostFocus += OnRenameLostFocus;
        box.GotFocus += OnTextGotFocus;

        cell.Name.Visibility = Visibility.Collapsed;
        parent.Children.Insert(parent.Children.IndexOf(cell.Name) + 1, box);

        _renaming = cell;
        _renameBox = box;

        box.Focus(FocusState.Programmatic);
        box.SelectAll();
    }

    private void OnRenameKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter)
        {
            args.Handled = true;
            EndRename(commit: true);
        }
        else if (args.Key == VirtualKey.Escape)
        {
            args.Handled = true;
            EndRename(commit: false);
        }
    }

    private void OnRenameLostFocus(object sender, RoutedEventArgs args) => EndRename(commit: true);

    /// <summary>
    /// Close the rename box. A name equal to the derived one, or empty, clears the label so the item
    /// goes back to following its target.
    /// </summary>
    private void EndRename(bool commit)
    {
        if (_renaming is not { } cell || _renameBox is not { } box)
        {
            return;
        }

        _renaming = null;
        _renameBox = null;

        box.KeyDown -= OnRenameKeyDown;
        box.LostFocus -= OnRenameLostFocus;
        box.GotFocus -= OnTextGotFocus;

        if (box.Parent is Panel parent)
        {
            parent.Children.Remove(box);
        }

        cell.Name.Visibility = Visibility.Visible;
        _surfaceHost?.RequestTextInput(false);

        if (!commit || _detached)
        {
            return;
        }

        var typed = box.Text.Trim();
        var derived = LaunchTargets.DisplayName(null, cell.Item.Target, _config.ShowFileExtension);
        var label = typed == derived ? string.Empty : typed;

        if (label == cell.Item.Label)
        {
            return;
        }

        cell.Item.Label = label;
        Save();
        Changed(rebuild: true);
    }

    // ---- search ([S-1385]) -------------------------------------------------------------------------

    private void OnSearchButton(object sender, RoutedEventArgs args) => ToggleSearch();

    private void ToggleSearch()
    {
        if (_searchOpen)
        {
            CloseSearch();
            return;
        }

        _searchOpen = true;
        Relayout();
        _search.Focus(FocusState.Programmatic);
    }

    private void CloseSearch()
    {
        if (!_searchOpen)
        {
            return;
        }

        _searchOpen = false;
        _query = string.Empty;
        _search.Text = string.Empty;
        Relayout();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs args)
    {
        if (_query == _search.Text)
        {
            return;
        }

        _query = _search.Text;
        Relayout();
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Escape)
        {
            args.Handled = true;
            CloseSearch();
        }
        else if (args.Key == VirtualKey.Enter && _cells.Count > 0)
        {
            // Enter in the box opens the first match: search, then go.
            args.Handled = true;
            Open([_cells[0]]);
        }
    }

    /// <summary>A text box in the widget asks the shell's keyboard lease for typing ([S-801]).</summary>
    private void OnTextGotFocus(object sender, RoutedEventArgs args) => _surfaceHost?.RequestTextInput(true);

    private void OnTextLostFocus(object sender, RoutedEventArgs args) => _surfaceHost?.RequestTextInput(false);
}
