using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
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
/// [S-10902] [S-10903] [S-13023] The cells a fence draws itself, for what is no desktop icon: the files a search fence finds
/// and a picture's items (the sample, a render's states). Each is drawn as a desktop icon is (the shell's icon with its
/// overlay and its name, the desktop icon's highlight, a cut item faded), in the body's cells of the desktop icon grid
/// (<see cref="FenceGrid"/>, the one layout), in the desktop's icon layout ([S-13976]: the name below, beside, or in icon
/// only its tip). A fence's desktop items are never drawn here: they are the desktop's icons.
/// </summary>
internal sealed partial class FenceView
{
    /// <summary>The cells by key, and the keys in the order they are shown.</summary>
    private readonly Dictionary<string, Cell> _cells = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _shown = [];

    /// <summary>A left press on a found file that may become its drag: the file and where it was pressed (the view's DIPs).</summary>
    private (string Key, Point At)? _press;

    /// <summary>The icon loads under way, for a picture that waits for them (the sample).</summary>
    private readonly List<Task> _loads = [];

    /// <summary>One file's cell: the one desktop-icon cell ([S-6632]) and its name.</summary>
    private sealed class Cell
    {
        public required string Key { get; init; }

        /// <summary>[S-6632] Its chrome, its icon slot and its stack, in its own box (<see cref="Root"/>).</summary>
        public required FileIconCell Tile { get; init; }

        public required TextBlock Name { get; init; }

        public Grid Root => (Grid)Tile.Root;

        public DesktopItem Item { get; set; } = null!;

        public IDisposable? Hover { get; set; }

        public bool Hovered { get; set; }

        public int IconPixels { get; set; }
    }

    /// <summary>What the fence draws itself: a picture's items, or what a search found (the newest first); none for a fence of desktop icons.</summary>
    private List<DesktopItem> ShownItems()
    {
        if (_fixture.On)
        {
            return _config.Sort is null ? [.. _fixture.Items] : [.. DesktopItemOrder.Sort(_fixture.Items)];
        }

        return Searches ? [.. _found.Select(AsItem)] : [];
    }

    /// <summary>One cell per file shown: kept for one still there, made for a new one, taken down for one gone.</summary>
    private void SyncCells(IReadOnlyList<DesktopItem> items)
    {
        _loads.RemoveAll(task => task.IsCompleted);
        var keep = new HashSet<string>(items.Select(i => i.Key), StringComparer.OrdinalIgnoreCase);
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
                _canvas.Children.Add(cell.Root);
                _cells[item.Key] = cell;
            }

            var renamed = cell.Item is not null && !string.Equals(cell.Item.Name, item.Name, StringComparison.Ordinal);
            var first = cell.Item is null;
            cell.Item = item;
            cell.Name.Text = item.Name;
            LayCell(cell);
            if (first || renamed || cell.IconPixels != IconPixels())
            {
                _loads.Add(LoadIconAsync(cell));
            }
        }

        _shown = [.. items.Select(i => i.Key)];
    }

    private Cell MakeCell(string key)
    {
        var name = Family.Text(FamilyType.Caption, ColourRole.TextPrimary);
        name.TextWrapping = TextWrapping.WrapWholeWords;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.MaxLines = 2;
        var root = new Grid { Background = Fluent.Clear() };

        // [S-14263] A cell of the body is one of the fence's items (its box on the icon grid), reported as one.
        ItemMark.Mark(root);
        var tile = new FileIconCell(root, Look, DesktopCell.Icon(DesktopCell.Current));
        tile.Stack.Children.Add(name);
        var cell = new Cell { Key = key, Tile = tile, Name = name };

        root.PointerPressed += (_, e) => OnItemPressed(cell, e);
        root.PointerReleased += (_, e) => OnItemReleased(cell, e);
        root.DoubleTapped += (_, e) => OnItemDoubleTapped(cell, e);
        root.RightTapped += (_, e) => OnItemRightTapped(cell, e);
        cell.Hover = tile.Hover(hovered =>
        {
            cell.Hovered = hovered;
            PaintCell(cell);
        });
        return cell;
    }

    /// <summary>
    /// [S-13976] A cell in the desktop's icon layout in force, as the desktop's own icons are: the name under the icon or
    /// beside it; in icon only no name in the cell, the tip says it (a fence's own cells have no name on hover).
    /// </summary>
    private static void LayCell(Cell cell)
    {
        var (size, layout) = (DesktopCell.Current, DesktopCell.CurrentLayout);
        DesktopCellLayout.Arrange(cell.Tile.Stack, layout);
        var beside = layout == DesktopIconLayout.Beside;
        cell.Name.TextAlignment = DesktopCellLayout.NameAlignment(layout);
        cell.Name.HorizontalAlignment = beside ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        cell.Name.VerticalAlignment = beside ? VerticalAlignment.Center : VerticalAlignment.Top;
        cell.Name.MaxWidth = DesktopCell.NameWidth(size, layout, 0);
        var overhangs = DesktopCell.NameOverhangs(layout);
        cell.Name.Visibility = overhangs ? Visibility.Collapsed : Visibility.Visible;
        ToolTipService.SetToolTip(cell.Root, overhangs ? cell.Item?.Name : null);
    }

    private void DropCell(Cell cell)
    {
        cell.Tile.Icon.Cancel();
        cell.Hover?.Dispose();
        cell.Hover = null;
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

    /// <summary>[S-5512] [S-6631] The shell's icon with its overlay, the cached one at once, else asked.</summary>
    private Task LoadIconAsync(Cell cell)
    {
        var pixels = IconPixels();
        cell.IconPixels = pixels;
        var icon = cell.Tile.Icon;
        icon.Side = DesktopCell.Icon(DesktopCell.Current);
        if (_host.DesktopItems.TryIcon(cell.Item, pixels, out var cached) && cached is not null)
        {
            icon.Cancel();
            icon.Show(cached);
            return Task.CompletedTask;
        }

        return icon.LoadAsync(() => _host.DesktopItems.IconAsync(cell.Item, pixels));
    }

    /// <summary>[S-5602] Every cell's look: a search's own selection, a picture's set one.</summary>
    private void PaintCells()
    {
        foreach (var cell in _cells.Values)
        {
            PaintCell(cell);
        }
    }

    /// <summary>
    /// [S-13019] The desktop icon's own look (its strengths, the whole cell, its corners): its white wash on a dark card, the
    /// same wash in black on a light one, where white would not show (<see cref="SelectionLook.DesktopIconOver"/>).
    /// </summary>
    private static SelectionPalette Look
        => SelectionLook.DesktopIconOver(Family.Brush(ColourRole.TextPrimary) is SolidColorBrush text
            && SelectionLook.LightGround(new RgbColor(text.Color.R, text.Color.G, text.Color.B)));

    private void PaintCell(Cell cell)
    {
        var key = cell.Key;
        var selected = _fixture.On ? _fixture.Selected.Contains(key) : _picks.IsSelected(key);
        var focus = _fixture.On ? _fixture.Focus == key : _picks.Cues && _picks.IsFocus(key);
        var active = !_fixture.On || !_fixture.Inactive;
        cell.Tile.Paint(new SelectionCue(selected, cell.Hovered, focus, focus, active, false), Look);
        cell.Tile.Icon.Opacity = _fixture.On && _fixture.Faded.Contains(key) ? SelectionLook.Faded : 1;
    }

    // ---- a search's files ([S-13023]) --------------------------------------------------------------

    /// <summary>A left press selects in the fence's own selection (Ctrl toggles, Shift the range) and may become the file's drag; never the card's move.</summary>
    private void OnItemPressed(Cell cell, PointerRoutedEventArgs e)
    {
        if (!Acts || !Searches)
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
        OnFoundPressed(cell, point, keys.HasFlag(Windows.System.VirtualKeyModifiers.Control), keys.HasFlag(Windows.System.VirtualKeyModifiers.Shift));
    }

    /// <summary>[S-5549] Past the dead zone with the button held, the press is the file's drag.</summary>
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
            _ = DragFoundAsync(cell, point);
        }
    }

    private void OnItemReleased(Cell cell, PointerRoutedEventArgs e)
    {
        if (_press is { } press && string.Equals(press.Key, cell.Key, StringComparison.OrdinalIgnoreCase))
        {
            OnFoundReleased(cell);
        }

        _press = null;
        PointerMoved -= OnPressMoved;
    }

    /// <summary>[S-5530] A double-click opens the selection (the file alone when it is not selected).</summary>
    private void OnItemDoubleTapped(Cell cell, DoubleTappedRoutedEventArgs e)
    {
        if (!Acts || !Searches)
        {
            return;
        }

        e.Handled = true;
        OpenFound(cell.Key);
    }

    /// <summary>[S-5548] A right-click is the shell's menu for the selection.</summary>
    private void OnItemRightTapped(Cell cell, RightTappedRoutedEventArgs e)
    {
        if (!Acts || !Searches || XamlRoot is not { } root)
        {
            return;
        }

        e.Handled = true;
        var scale = root.RasterizationScale > 0 ? root.RasterizationScale : 1.0;
        var at = e.GetPosition(null);
        var window = Window();
        var (x, y) = _host.ExplorerMenus.ToScreen(window, (int)Math.Round(at.X * scale), (int)Math.Round(at.Y * scale));
        FoundMenu(cell, x, y, window);
    }

    /// <summary>The window this view is drawn in, for the menus.</summary>
    private nint Window()
        => XamlRoot?.ContentIslandEnvironment is { } environment
            ? Microsoft.UI.Win32Interop.GetWindowFromWindowId(environment.AppWindowId)
            : 0;

    /// <summary>[S-5564] Explorer's ghost from this fence: the held icon in front, up to two more selected ones behind, the count in a badge.</summary>
    private DragPicture? Ghost(Cell held, double side, Point anchor, ItemDragOffer offer, Func<string, bool> isSelected)
    {
        if (held.Tile.Icon.Ghost is not { } front || side <= 0)
        {
            return null;
        }

        var selected = _shown.Where(isSelected).ToList();
        var icons = new List<GhostIcon> { front };
        foreach (var key in selected)
        {
            if (icons.Count >= DragGhost.MaxShown)
            {
                break;
            }

            if (!string.Equals(key, held.Key, StringComparison.OrdinalIgnoreCase) && _cells.TryGetValue(key, out var other) && other.Tile.Icon.Ghost is { } picture)
            {
                icons.Add(picture);
            }
        }

        var count = Math.Max(Math.Max(1, selected.Count), offer.Paths.Count);
        return ItemDrag.Ghost(icons, count, side, anchor, XamlRoot?.RasterizationScale ?? 1.0);
    }
}
