using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.Widget.Fence;

/// <summary>
/// [S-10902] [S-10903] [S-13011] The fence's items: one cell each, drawn as a desktop icon is (the shell's icon with its
/// overlay over its name, the desktop icon's highlight, a cut or hidden item faded), every gesture the desktop's.
/// </summary>
internal sealed partial class FenceView
{
    /// <summary>The cells by item key, and the keys in the order they are shown.</summary>
    private readonly Dictionary<string, Cell> _cells = new(DesktopItemKeys.Comparer);
    private List<string> _shown = [];

    /// <summary>[S-13013] The standing sort's order of the items, asked once per change of the items or the sort; null to ask again.</summary>
    private IReadOnlyList<string>? _order;
    private DesktopSortBy? _orderBy;

    /// <summary>A left press on an item that may become its drag: the item and where it was pressed (the view's DIPs).</summary>
    private (string Key, Point At, bool Modified)? _press;

    /// <summary>The icon loads under way, for a picture that waits for them (the sample).</summary>
    private readonly List<Task> _loads = [];

    /// <summary>[S-5543] The cell showing its rename box.</summary>
    private Cell? _renameCell;

    /// <summary>One item's cell.</summary>
    private sealed class Cell
    {
        public required string Key { get; init; }

        public required Grid Root { get; init; }

        public required Border Chrome { get; init; }

        public required Grid IconHost { get; init; }

        public required Image Image { get; init; }

        public required TextBlock Name { get; init; }

        public required StackPanel Stack { get; init; }

        public DesktopItem Item { get; set; } = null!;

        public TextBox? Box { get; set; }

        public IDisposable? Hover { get; set; }

        public bool Hovered { get; set; }

        public int IconRequest { get; set; }

        public int IconPixels { get; set; }

        /// <summary>[S-5564] The icon's picture for a drag's ghost, as last drawn.</summary>
        public GhostIcon? Ghost { get; set; }
    }

    /// <summary>[S-13013] The items in the order the fence shows them: a preview's own, else its config's (by its standing sort).</summary>
    private List<DesktopItem> ShownItems()
    {
        if (_fixture.On)
        {
            return [.. _fixture.Items];
        }

        if (_order is null || _orderBy != _config.Sort)
        {
            _orderBy = _config.Sort;
            _order = _config.Sort is { } by && _config.Items.Count > 1 ? Fences.Order(_config.Items, by) : [.. _config.Items];
        }

        var desk = _host.DesktopItems;
        return [.. _order.Select(key => desk.Find(key) ?? new DesktopItem(key, DesktopItemKeys.FallbackName(key), DesktopItemKind.File))];
    }

    /// <summary>Whether the cached order holds exactly the config's items.</summary>
    private bool SameItems()
        => _order is { } order && order.Count == _config.Items.Count && order.ToHashSet(DesktopItemKeys.Comparer).SetEquals(_config.Items);

    /// <summary>One cell per item shown: kept for an item still there, made for a new one, taken down for one gone.</summary>
    private void SyncCells(IReadOnlyList<DesktopItem> items)
    {
        _loads.RemoveAll(task => task.IsCompleted);
        var keep = new HashSet<string>(items.Select(i => i.Key), DesktopItemKeys.Comparer);
        foreach (var gone in _cells.Keys.Where(k => !keep.Contains(k)).ToList())
        {
            DropCell(_cells[gone]);
            _cells.Remove(gone);
        }

        foreach (var item in items)
        {
            if (!_cells.TryGetValue(item.Key, out var cell))
            {
                cell = MakeCell(item.Key);
                _cells[item.Key] = cell;
                _canvas.Children.Add(cell.Root);
            }

            var renamed = cell.Item is not null && !string.Equals(cell.Item.Name, item.Name, StringComparison.Ordinal);
            var first = cell.Item is null;
            cell.Item = item;
            cell.Name.Text = item.Name;
            if (first || renamed || cell.IconPixels != IconPixels())
            {
                _loads.Add(LoadIconAsync(cell));
            }
        }

        _shown = [.. items.Select(i => i.Key)];
    }

    private Cell MakeCell(string key)
    {
        var icon = DesktopCell.Icon(DesktopCell.Current);
        var image = new Image { Stretch = Stretch.Uniform, Width = icon, Height = icon };
        var iconHost = new Grid { Width = icon, Height = icon, HorizontalAlignment = HorizontalAlignment.Center };
        iconHost.Children.Add(image);
        var name = Family.Text(FamilyType.Caption, ColourRole.TextPrimary);
        name.TextAlignment = TextAlignment.Center;
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.TextWrapping = TextWrapping.WrapWholeWords;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.MaxLines = 2;
        name.MaxWidth = DesktopCell.NameWidth(DesktopCell.Current, 0);
        var stack = new StackPanel
        {
            Spacing = DesktopCell.Gap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(DesktopCell.SideInset, DesktopCell.HighlightTop + DesktopCell.IconTop, DesktopCell.SideInset, 0),
        };
        stack.Children.Add(iconHost);
        stack.Children.Add(name);
        var chrome = SelectionChrome.Wrap(null, SelectionLook.Rows);
        var root = new Grid { Background = Fluent.Clear() };
        root.Children.Add(chrome);
        root.Children.Add(stack);
        var cell = new Cell { Key = key, Root = root, Chrome = chrome, IconHost = iconHost, Image = image, Name = name, Stack = stack };

        // [S-5564] The shell's bitmap is read for the ghost once it is decoded.
        image.ImageOpened += (_, _) => cell.Ghost = ItemDrag.IconOf(image.Source);
        root.PointerPressed += (_, e) => OnItemPressed(cell, e);
        root.PointerReleased += (_, e) => OnItemReleased(cell, e);
        root.DoubleTapped += (_, e) => OnItemDoubleTapped(cell, e);
        root.RightTapped += (_, e) => OnItemRightTapped(cell, e);
        cell.Hover = SelectionChrome.Hover(root, hovered =>
        {
            cell.Hovered = hovered;
            PaintCell(cell);
        });
        return cell;
    }

    private void DropCell(Cell cell)
    {
        cell.IconRequest++;
        cell.Hover?.Dispose();
        cell.Hover = null;
        if (ReferenceEquals(_renameCell, cell))
        {
            CloseItemBox();
        }

        _canvas.Children.Remove(cell.Root);
    }

    private void ClearCells()
    {
        foreach (var cell in _cells.Values)
        {
            DropCell(cell);
        }

        _cells.Clear();
        _shown = [];
        _press = null;
    }

    /// <summary>[S-5563] [S-5593] The icon's pixels on this display at the desktop's icon size.</summary>
    private int IconPixels() => DesktopIconPixels.For(DesktopCell.Icon(DesktopCell.Current), XamlRoot?.RasterizationScale ?? 1.0);

    /// <summary>[S-5512] The shell's icon with its overlay, the cached one at once, else asked.</summary>
    private async Task LoadIconAsync(Cell cell)
    {
        var request = ++cell.IconRequest;
        var pixels = IconPixels();
        cell.IconPixels = pixels;
        var icon = DesktopCell.Icon(DesktopCell.Current);
        cell.IconHost.Width = icon;
        cell.IconHost.Height = icon;
        cell.Image.Width = icon;
        cell.Image.Height = icon;
        try
        {
            if (_host.DesktopItems.TryIcon(cell.Item, pixels, out var cached) && cached is not null)
            {
                cell.Image.Source = cached;
                cell.Ghost = ItemDrag.IconOf(cached);
                return;
            }

            if (await _host.DesktopItems.IconAsync(cell.Item, pixels).ConfigureAwait(true) is { } source && request == cell.IconRequest)
            {
                cell.Image.Source = source;
                cell.Ghost = ItemDrag.IconOf(source);
            }
        }
        catch (Exception ex)
        {
            // An icon is decoration: the name still shows.
            Log.Warn(Scope, "could not load a fence item's icon", ex);
        }
    }

    /// <summary>[S-5602] Every cell's look from the desktop's one selection.</summary>
    private void PaintCells()
    {
        foreach (var cell in _cells.Values)
        {
            PaintCell(cell);
        }

        SyncItemBox();
    }

    private void PaintCell(Cell cell)
    {
        var desk = _host.DesktopItems;
        var key = cell.Key;
        var selected = _fixture.On ? _fixture.Selected.Contains(key) : desk.IsSelected(key);
        var focus = _fixture.On ? _fixture.Focus == key : desk.ShowsFocus(key);
        var active = _fixture.On ? !_fixture.Inactive : desk.Active;
        var target = DesktopItemKeys.Comparer.Equals(_dropKey, key);
        SelectionChrome.Paint(cell.Chrome, new SelectionCue(selected, cell.Hovered, focus, focus, active, target), SelectionLook.Rows);
        var faded = _fixture.On ? _fixture.Faded.Contains(key) : desk.IsCut(key) || cell.Item?.Hidden == true;
        cell.IconHost.Opacity = faded ? SelectionLook.Faded : 1;
    }

    // ---- gestures ([S-13011]) ----------------------------------------------------------------------

    /// <summary>A left press selects as the desktop does (Ctrl toggles, Shift the range) and may become the item's drag; never the card's move.</summary>
    private void OnItemPressed(Cell cell, PointerRoutedEventArgs e)
    {
        if (!Acts || cell.Box is not null)
        {
            return;
        }

        e.Handled = true;
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var keys = e.KeyModifiers;
        var ctrl = keys.HasFlag(Windows.System.VirtualKeyModifiers.Control);
        var shift = keys.HasFlag(Windows.System.VirtualKeyModifiers.Shift);
        _host.DesktopItems.Press(cell.Key, ctrl, shift);
        _press = (cell.Key, point.Position, ctrl || shift);
        PointerMoved -= OnPressMoved;
        PointerMoved += OnPressMoved;
    }

    /// <summary>[S-5549] Past the dead zone with the button held, the press is the shell's drag of the selection, from this fence.</summary>
    private void OnPressMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_press is not { } press)
        {
            PointerMoved -= OnPressMoved;
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _press = null;
            PointerMoved -= OnPressMoved;
            return;
        }

        if (!SelectionBand.PastThreshold(point.Position.X - press.At.X, point.Position.Y - press.At.Y))
        {
            return;
        }

        _press = null;
        PointerMoved -= OnPressMoved;
        e.Handled = true;
        if (_cells.TryGetValue(press.Key, out var cell))
        {
            _ = DragAsync(cell, point);
        }
    }

    private void OnItemReleased(Cell cell, PointerRoutedEventArgs e)
    {
        if (_press is { } press && DesktopItemKeys.Comparer.Equals(press.Key, cell.Key))
        {
            _host.DesktopItems.Release(cell.Key);
        }

        _press = null;
        PointerMoved -= OnPressMoved;
    }

    /// <summary>[S-5530] A double-click opens the selection (the item alone when it is not selected).</summary>
    private void OnItemDoubleTapped(Cell cell, DoubleTappedRoutedEventArgs e)
    {
        if (!Acts || cell.Box is not null)
        {
            return;
        }

        e.Handled = true;
        _host.DesktopItems.Open(cell.Key);
    }

    /// <summary>[S-5548] A right-click is the desktop's menu for the selection (Shift: the shell's classic one at once).</summary>
    private void OnItemRightTapped(Cell cell, RightTappedRoutedEventArgs e)
    {
        if (!Acts || XamlRoot is not { } root)
        {
            return;
        }

        e.Handled = true;
        var scale = root.RasterizationScale > 0 ? root.RasterizationScale : 1.0;
        var at = e.GetPosition(null);
        var window = Window();
        var (x, y) = _host.ExplorerMenus.ToScreen(window, (int)Math.Round(at.X * scale), (int)Math.Round(at.Y * scale));
        _host.DesktopItems.Context(cell.Key, x, y, window, classic: SelectionInput.ShiftHeld());
    }

    /// <summary>The window this view is drawn in, for the menus and the drag's source surface.</summary>
    private nint Window()
        => XamlRoot?.ContentIslandEnvironment is { } environment
            ? Microsoft.UI.Win32Interop.GetWindowFromWindowId(environment.AppWindowId)
            : 0;

    /// <summary>
    /// [S-5549] [S-13006] One drag of the selection from this fence, from the host's beginning through XAML's drag to its
    /// end: onto empty desktop the items leave the fence where they are let go, onto another fence they move there, onto a
    /// folder, Explorer, an app or the Recycle Bin the target does what it does. The ghost is Explorer's, of this fence's
    /// pictures. The host's state is cleared on every way out.
    /// </summary>
    private async Task DragAsync(Cell held, PointerPoint point)
    {
        var desk = _host.DesktopItems;
        ItemDragOffer? drag = null;
        try
        {
            drag = desk.BeginDrag(held.Key, Window());
            if (drag is null)
            {
                return;
            }

            var grab = point.Position;
            var square = held.IconHost.TransformToVisual(this).TransformBounds(new Rect(0, 0, held.IconHost.ActualWidth, held.IconHost.ActualHeight));
            var anchor = new Point(Math.Clamp(grab.X - square.X, 0, square.Width), Math.Clamp(grab.Y - square.Y, 0, square.Height));
            var offer = drag;
            var effect = await ItemDrag.StartAsync(this, point, offer, () => Ghost(held, square.Width, anchor, offer)).ConfigureAwait(true);
            Log.Debug(Scope, $"a drag from a fence ended: {effect}");
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a fence item's drag failed", ex);
        }
        finally
        {
            if (drag is not null)
            {
                desk.EndDrag(drag);
            }
        }
    }

    /// <summary>[S-5564] Explorer's ghost from this fence: the held icon in front, up to two more selected ones behind, the count in a badge.</summary>
    private DragPicture? Ghost(Cell held, double side, Point anchor, ItemDragOffer offer)
    {
        if (held.Ghost is not { } front || side <= 0)
        {
            return null;
        }

        var desk = _host.DesktopItems;
        var selected = _shown.Where(desk.IsSelected).ToList();
        var icons = new List<GhostIcon> { front };
        foreach (var key in selected)
        {
            if (icons.Count >= DragGhost.MaxShown)
            {
                break;
            }

            if (!DesktopItemKeys.Comparer.Equals(key, held.Key) && _cells.TryGetValue(key, out var other) && other.Ghost is { } picture)
            {
                icons.Add(picture);
            }
        }

        var count = Math.Max(Math.Max(1, selected.Count), offer.Paths.Count);
        return ItemDrag.Ghost(icons, count, side, anchor, XamlRoot?.RasterizationScale ?? 1.0);
    }

    // ---- F2 on an item ([S-5543]) ------------------------------------------------------------------

    /// <summary>The box shows on the item the host renames in this fence, and goes when it no longer does.</summary>
    private void SyncItemBox()
    {
        var key = _fixture.On ? _fixture.Renaming
            : WidgetId is { } id && string.Equals(_host.DesktopItems.RenamingWidget, id, StringComparison.Ordinal) ? _host.DesktopItems.RenamingKey : null;
        if (key is null || !_cells.TryGetValue(key, out var cell) || !DesktopRename.CanRename(cell.Item))
        {
            CloseItemBox();
            return;
        }

        if (ReferenceEquals(_renameCell, cell))
        {
            return;
        }

        CloseItemBox();
        var (text, select) = DesktopRename.Edit(cell.Item);
        var box = new TextBox
        {
            Text = text,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            IsSpellCheckEnabled = false,
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
            Margin = new Thickness(-DesktopCell.SideInset, 0, -DesktopCell.SideInset, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = FamilyType.Caption.FontSize,
        };
        cell.Box = box;
        _renameCell = cell;
        cell.Name.Visibility = Visibility.Collapsed;
        cell.Stack.Children.Add(box);
        if (_fixture.On)
        {
            box.Loaded += (_, _) => box.Select(0, select);
            return;
        }

        box.GotFocus += (_, _) => _surface?.RequestTextInput(true);
        box.LostFocus += (_, _) =>
        {
            _surface?.RequestTextInput(false);
            if (ReferenceEquals(cell.Box, box))
            {
                CommitItem(cell, box);
            }
        };
        box.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                CommitItem(cell, box);
            }
            else if (args.Key == Windows.System.VirtualKey.Escape)
            {
                args.Handled = true;
                _host.DesktopItems.CancelRename();
            }
        };
        box.Loaded += (_, _) =>
        {
            box.Focus(FocusState.Programmatic);
            box.Select(0, select);
        };
    }

    /// <summary>[S-5543] Enter or a click elsewhere: through the host's rename (the shell's own); a name it refuses keeps the box with the tip saying why.</summary>
    private void CommitItem(Cell cell, TextBox box)
    {
        if (_host.DesktopItems.CommitRename(cell.Key, box.Text) is { } why)
        {
            var tip = new ToolTip { Content = why };
            ToolTipService.SetToolTip(box, tip);
            tip.IsOpen = true;
            box.Focus(FocusState.Programmatic);
        }
    }

    private void CloseItemBox()
    {
        if (_renameCell is not { } cell || cell.Box is not { } box)
        {
            _renameCell = null;
            return;
        }

        cell.Box = null;
        _renameCell = null;
        if (ToolTipService.GetToolTip(box) is ToolTip tip)
        {
            tip.IsOpen = false;
        }

        cell.Stack.Children.Remove(box);
        cell.Name.Visibility = Visibility.Visible;
        _surface?.RequestTextInput(false);
    }
}
