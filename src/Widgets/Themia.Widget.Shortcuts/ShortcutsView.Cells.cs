using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using Themia.App.Services.Web;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Shortcuts;
using Windows.Storage.Streams;

namespace Themia.App.Widgets;

/// <summary>
/// The Shortcuts widget's cells: a tile (grid) or a row (list, details), its icon, its badge, and
/// how it is painted for hover, selection and a missing target.
/// </summary>
internal sealed partial class ShortcutsView
{
    /// <summary>One item's visual and the state it is painted from.</summary>
    private sealed class Cell
    {
        public required ShortcutItem Item { get; init; }

        public required LaunchTarget Target { get; init; }

        public required Border Root { get; init; }

        /// <summary>[S-6627] The selection's mark: behind the row or the cell, over all of it and over its item fill.</summary>
        public required Border Chrome { get; init; }

        /// <summary>A labelled tile, whose name may be shown in full when selected ([S-6629]).</summary>
        public bool Tile { get; init; }

        /// <summary>Everything inside the root; dimmed for a missing target (the root's opacity is the scroll fade's).</summary>
        public required FrameworkElement Content { get; init; }

        public required Image Image { get; init; }

        public required FontIcon Fallback { get; init; }

        public required TextBlock Name { get; init; }

        public required Border Badge { get; init; }

        public required TextBlock BadgeText { get; init; }

        public TextBlock? Kind { get; init; }

        public required double IconSize { get; init; }

        public bool IsBin { get; init; }

        public bool Hovered { get; set; }

        public TargetHealth Health { get; set; }

        /// <summary>The icon variant last asked for (the Recycle Bin's empty / full), so it is not asked twice.</summary>
        public string? IconVariant { get; set; }
    }

    /// <summary>Decoded favicons by origin, per view: the bytes are shared app-wide by the host's cache (<see cref="IWidgetHost.Favicons"/>).</summary>
    private readonly Dictionary<string, Task<BitmapImage?>> _favicons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Rebuild every cell for the current shape.</summary>
    private void Render(List<ShortcutItem> visible)
    {
        ClearCells();

        _panel.RowDefinitions.Clear();
        _panel.ColumnDefinitions.Clear();

        if (_config.Style == ShortcutsStyle.Grid)
        {
            RenderGrid(visible, _shape.Tiles);
        }
        else
        {
            RenderRows(visible, _shape.Rows);
        }

        _selection.SetOrder(Order());
        PaintAll();
        _fade.Refresh();
    }

    /// <summary>
    /// [S-1382] [S-6713] The cells fill from the top-left, row by row, each mark 4 from the next (so the
    /// cells are 12 apart and the first stands at the inset); a grid that does not fill the tile leaves
    /// the room at the right and bottom.
    /// </summary>
    private void RenderGrid(List<ShortcutItem> visible, CellGrid plan)
    {
        _columnHeader.Visibility = Visibility.Collapsed;
        _panel.ColumnSpacing = FamilySpace.S1;
        _panel.RowSpacing = FamilySpace.S1;
        _panel.HorizontalAlignment = HorizontalAlignment.Left;
        _panel.VerticalAlignment = VerticalAlignment.Top;

        for (var c = 0; c < plan.Columns; c++)
        {
            _panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(plan.MarkWidth) });
        }

        var rows = (visible.Count + plan.Columns - 1) / plan.Columns;

        for (var r = 0; r < rows; r++)
        {
            _panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(plan.MarkHeight) });
        }

        for (var i = 0; i < visible.Count; i++)
        {
            var cell = BuildTile(visible[i], plan);
            SetRow(cell.Root, i / plan.Columns);
            SetColumn(cell.Root, i % plan.Columns);
            Add(cell);
        }
    }

    /// <summary>[S-6713] A cell (Row.Cell): the item's icon over its name in Caption (none on an icons-only grid), its mark 4 around them.</summary>
    private Cell BuildTile(ShortcutItem item, CellGrid plan)
    {
        var target = LaunchTargets.Classify(item.Target);
        var (iconHost, image, fallback, badge, badgeText) = BuildIcon(item, target, plan.Icon);

        var view = new RowCell(plan.Icon) { Width = plan.Width, Margin = new Thickness(FamilySpace.S1) };
        view.Show(iconHost, plan.Named ? NameOf(item) : string.Empty);

        // [S-6627] The whole cell, icon and name, is the item's mark.
        var chrome = SelectionChrome.Wrap(new Grid());
        var root = new Border
        {
            Child = new Grid { Children = { chrome, view } },
            CornerRadius = Family.Radius,
        };

        if (!plan.Named)
        {
            // An icon-only cell still says what it is, on hover.
            ToolTipService.SetToolTip(root, NameOf(item));
        }

        return NewCell(item, target, root, chrome, view, image, fallback, view.NameText, badge, badgeText, null, plan.Icon, tile: plan.Named);
    }

    private void RenderRows(List<ShortcutItem> visible, ShortcutRowPlan plan)
    {
        _panel.HorizontalAlignment = HorizontalAlignment.Stretch;
        _panel.VerticalAlignment = VerticalAlignment.Top;
        var details = _config.Style == ShortcutsStyle.Details;
        _panel.ColumnSpacing = 0;
        _panel.RowSpacing = 0;

        for (var c = 0; c < plan.Columns; c++)
        {
            _panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var rows = (visible.Count + plan.Columns - 1) / plan.Columns;

        for (var r = 0; r < rows; r++)
        {
            _panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(plan.RowHeight) });
        }

        BuildColumnHeader(details && plan.ColumnHeader, plan);

        for (var i = 0; i < visible.Count; i++)
        {
            var cell = BuildRow(visible[i], plan, details);
            SetRow(cell.Root, i / plan.Columns);
            SetColumn(cell.Root, i % plan.Columns);
            Add(cell);
        }
    }

    /// <summary>
    /// [S-6711] A row: the Row block — the item's icon (16, or 32 on a taller row) as its lead, the name
    /// primary, the target under it where the row has two lines (the list); the details table puts its
    /// columns in the name's place. The selection's mark lies behind the whole row.
    /// </summary>
    private Cell BuildRow(ShortcutItem item, ShortcutRowPlan plan, bool details)
    {
        var target = LaunchTargets.Classify(item.Target);
        var (iconHost, image, fallback, badge, badgeText) = BuildIcon(item, target, plan.Lead);

        var block = new RowBlock(RowKind.List, plan.Lead) { Lines = plan.Lines };
        block.Lead(iconHost);

        TextBlock name;
        TextBlock? kind = null;

        if (details)
        {
            block.Show(string.Empty);
            var grid = new Grid { ColumnSpacing = FamilySpace.ColumnGap };
            DetailColumns(grid, plan);
            name = Family.Text(FamilyType.Body, ColourRole.TextPrimary, NameOf(item));
            name.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(name);

            if (plan.TargetColumn)
            {
                var path = Family.Text(FamilyType.Body, ColourRole.TextSecondary, item.IsLayout ? LayoutPlan.Summary(item.Layout) : item.Target);
                path.VerticalAlignment = VerticalAlignment.Center;
                SetColumn(path, 1);
                grid.Children.Add(path);
            }

            if (plan.KindColumn)
            {
                kind = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
                kind.VerticalAlignment = VerticalAlignment.Center;
                SetColumn(kind, 2);
                grid.Children.Add(kind);
            }

            block.Cells(grid);
        }
        else
        {
            block.Show(NameOf(item), null, plan.Lines > 1 ? DetailOf(item, target) : null);
            name = block.NameText;
        }

        // [S-6627] The whole row is the item's mark, laid behind the row so marking it moves nothing.
        var chrome = SelectionChrome.Wrap(new Grid());
        var root = new Border { Child = new Grid { Children = { chrome, block } } };

        return NewCell(item, target, root, chrome, block, image, fallback, name, badge, badgeText, kind, plan.Lead, tile: false);
    }

    /// <summary>The details table's columns: name, then target and kind where the plan has them.</summary>
    private static void DetailColumns(Grid grid, ShortcutRowPlan plan)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (plan.TargetColumn)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        }

        if (plan.KindColumn)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ShortcutLayout.KindWidth) });
        }
    }

    /// <summary>[S-1384] [S-7034] The details table's titles: a Caption line (secondary) over the columns, at the text edge.</summary>
    private void BuildColumnHeader(bool show, ShortcutRowPlan plan)
    {
        _columnHeader.Children.Clear();
        _columnHeader.ColumnDefinitions.Clear();
        _columnHeader.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        if (!show)
        {
            return;
        }

        DetailColumns(_columnHeader, plan);
        _columnHeader.ColumnSpacing = FamilySpace.ColumnGap;
        _columnHeader.Height = KitLadder.ColumnHeaderHeight;
        _columnHeader.Padding = new Thickness(FamilySpace.Inset + RowRules.NameStart(plan.Lead), 0, FamilySpace.Inset, 0);

        string[] titles = [Str.Folder_ColName.Text(), ShortcutsStr.WidgetsC_Shortcuts_Column_Target.Text(), ShortcutsStr.WidgetsC_Shortcuts_Column_Kind.Text()];

        for (var c = 0; c < _columnHeader.ColumnDefinitions.Count; c++)
        {
            var title = Family.Text(FamilyType.Caption, ColourRole.TextSecondary, titles[c]);
            title.VerticalAlignment = VerticalAlignment.Top;
            SetColumn(title, c);
            _columnHeader.Children.Add(title);
        }
    }

    /// <summary>
    /// An item's icon slot: the glyph for its kind straight away at the slot's size, replaced by the real
    /// icon when it arrives; on a 32 or 48 icon the Recycle Bin's count, the neutral pill over a picture
    /// ([S-6706]), at the slot's top-right corner, moved out by half its height (a transform, never a margin).
    /// </summary>
    private (Grid Host, Image Image, FontIcon Fallback, Border Badge, TextBlock BadgeText) BuildIcon(ShortcutItem item, LaunchTarget target, double size)
    {
        var fallback = item.IsLayout
            ? Family.Glyph(Glyphs.AppLayout, size, ColourRole.TextSecondary)
            : Family.Glyph(GlyphFor(target.Kind), size, target.Kind == LaunchKind.Refused ? ColourRole.Critical : ColourRole.TextSecondary);
        fallback.HorizontalAlignment = HorizontalAlignment.Center;

        var image = new Image
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Visibility = Visibility.Collapsed,
        };

        var badgeText = new TextBlock();
        var badge = HeaderBlock.Count(badgeText, overImage: true);
        badge.HorizontalAlignment = HorizontalAlignment.Right;
        badge.VerticalAlignment = VerticalAlignment.Top;
        badge.RenderTransform = new TranslateTransform { X = FamilySpace.S2, Y = -FamilySpace.S1 };
        badge.Visibility = Visibility.Collapsed;

        var host = new Grid { Width = size, Height = size };
        host.Children.Add(fallback);

        // [S-1417] The user's emoji stands in the glyph's place at once; a picture loads over it the
        // way a real icon loads over the glyph (an item never holds both, [S-1419]).
        if (item.IconEmoji is { } emoji)
        {
            fallback.Visibility = Visibility.Collapsed;
            host.Children.Add(EmojiIcon(emoji, size));
        }
        host.Children.Add(image);
        host.Children.Add(badge);

        return (host, image, fallback, badge, badgeText);
    }

    private Cell NewCell(
        ShortcutItem item,
        LaunchTarget target,
        Border root,
        Border chrome,
        FrameworkElement content,
        Image image,
        FontIcon fallback,
        TextBlock name,
        Border badge,
        TextBlock badgeText,
        TextBlock? kind,
        double iconSize,
        bool tile)
    {
        var cell = new Cell
        {
            Item = item,
            Target = target,
            Root = root,
            Chrome = chrome,
            Tile = tile,
            Content = content,
            Image = image,
            Fallback = fallback,
            Name = name,
            Badge = badge,
            BadgeText = badgeText,
            Kind = kind,
            IconSize = iconSize,
            IsBin = ShortcutRules.IsRecycleBin(item.Target),
            Health = target.Kind == LaunchKind.LocalPath && _health.TryGetValue(target.Value, out var known) ? known : TargetHealth.Unknown,
        };

        root.Tag = cell;
        ItemFill.Paint(root, ItemRole.Row);

        if (!target.IsValid && !item.IsLayout)
        {
            // A refused target says what is wrong rather than disappearing: a shortcut that changed
            // under the user is the one worth seeing.
            ToolTipService.SetToolTip(root, $"{item.Target}\n{LaunchTargets.RefusalMessage(target.Refusal)}");
        }

        return cell;
    }

    private void Add(Cell cell)
    {
        var root = cell.Root;
        root.PointerEntered += OnCellEntered;
        root.PointerExited += OnCellExited;
        root.PointerCaptureLost += OnCellExited;
        root.PointerCanceled += OnCellExited;
        root.PointerPressed += OnCellPressed;
        WidgetItems.Mark(root); // [S-814] a press here is the cell's, not the background's
        root.Tapped += OnCellTapped;
        root.DoubleTapped += OnCellDoubleTapped;
        root.RightTapped += OnCellRightTapped;

        _cells.Add(cell);
        _panel.Children.Add(root);

        PaintHealth(cell);

        if (cell.IsBin)
        {
            PaintBin(cell);
        }
        else
        {
            _ = LoadIconAsync(cell, null);
        }
    }

    private void ClearCells()
    {
        EndRename(commit: false);

        foreach (var cell in _cells)
        {
            var root = cell.Root;
            root.PointerEntered -= OnCellEntered;
            root.PointerExited -= OnCellExited;
            root.PointerCaptureLost -= OnCellExited;
            root.PointerCanceled -= OnCellExited;
            root.PointerPressed -= OnCellPressed;
            root.Tapped -= OnCellTapped;
            root.DoubleTapped -= OnCellDoubleTapped;
            root.RightTapped -= OnCellRightTapped;
        }

        _fullName.Hide();
        _cells.Clear();
        _panel.Children.Clear();
    }

    private void OnCellEntered(object sender, PointerRoutedEventArgs args)
    {
        if (sender is Border { Tag: Cell cell })
        {
            cell.Hovered = true;
            Paint(cell);
        }
    }

    private void OnCellExited(object sender, PointerRoutedEventArgs args)
    {
        if (sender is Border { Tag: Cell cell })
        {
            cell.Hovered = false;
            Paint(cell);
        }
    }

    private void PaintAll()
    {
        // The focused tile places its full name in its own Paint; with no focused tile there is none.
        if (_selection.Focus is not { } focus || _cells.Find(cell => cell.Item.Id == focus) is not { Tile: true })
        {
            _fullName.Hide();
        }

        foreach (var cell in _cells)
        {
            Paint(cell);
        }
    }

    /// <summary>
    /// [S-6627] Hover, selection (inactive without the keyboard) and the focus after keyboard use, the
    /// desktop's look through the one selection chrome, laid over the item's fill ([S-6513], painted
    /// once at rest). [S-6629] A selected tile's cut-short name shows in full.
    /// </summary>
    private void Paint(Cell cell)
    {
        var id = cell.Item.Id;
        var cue = new SelectionCue(_selection.IsSelected(id), cell.Hovered, _selection.IsFocus(id), _selection.Cues, _active);
        SelectionChrome.Paint(cell.Chrome, cue);

        if (cell.Tile && cue.Focused)
        {
            _fullName.Update(cell.Chrome, cell.Name, cue, renaming: _renaming is not null);
        }
    }

    /// <summary>[S-1386] A missing local target: dimmed, its name struck through, and the tooltip says so.</summary>
    private void PaintHealth(Cell cell)
    {
        if (cell.Target.Kind == LaunchKind.LocalPath && _health.TryGetValue(cell.Target.Value, out var health))
        {
            cell.Health = health;
        }

        var missing = cell.Health == TargetHealth.Missing;
        cell.Content.Opacity = missing || (!cell.Target.IsValid && !cell.Item.IsLayout) ? SelectionLook.Faded : 1;
        cell.Name.TextDecorations = missing ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None;

        if (cell.Kind is { } kind)
        {
            kind.Text = cell.Item.IsLayout ? ShortcutRules.LayoutKindLabel() : ShortcutRules.KindLabel(cell.Target, cell.Health);
        }

        if (cell.Item.IsLayout)
        {
            ToolTipService.SetToolTip(cell.Root, $"{NameOf(cell.Item)}\n{LayoutPlan.Summary(cell.Item.Layout)}");
        }
        else if (cell.Target.IsValid && !cell.IsBin)
        {
            var name = NameOf(cell.Item);
            ToolTipService.SetToolTip(cell.Root, missing
                ? ShortcutsStr.WidgetsC_Shortcuts_Tooltip_Missing.Format(("name", name), ("target", cell.Item.Target))
                : $"{name}\n{cell.Item.Target}");
        }
    }

    /// <summary>[S-1389] The bin's badge, tooltip, and its empty or full icon.</summary>
    private void PaintBin(Cell cell)
    {
        var state = _widgetHost.RecycleBin.Current;
        var badge = ShortcutRules.BadgeText(state);

        cell.BadgeText.Text = badge ?? string.Empty;
        // [S-1389] The count only over a 32 or 48 icon; a 16 one has no room for it (the tooltip says how many).
        cell.Badge.Visibility = badge is null || cell.IconSize < FamilyShape.IconLarge ? Visibility.Collapsed : Visibility.Visible;
        ToolTipService.SetToolTip(cell.Root, RecycleBinState.Tooltip(NameOf(cell.Item), state));

        var variant = state is not { } bin ? "unknown" : bin.IsEmpty ? "empty" : "full";

        if (variant != cell.IconVariant)
        {
            cell.IconVariant = variant;
            _ = LoadIconAsync(cell, variant);
        }
    }

    /// <summary>
    /// [S-1383] The real icon: the site's own favicon for a web address (no third-party service),
    /// the shell's icon for a file, folder or Windows location. Never for a network path or a
    /// refused target, which keep their glyph.
    /// </summary>
    private async Task LoadIconAsync(Cell cell, string? variant)
    {
        // [S-1418] The user's own icon first: a picture while its file reads, else the emoji the cell
        // already shows; only then what Windows gives the target.
        if (cell.Item.IconPicture is { } picturePath)
        {
            var picture = await PictureAsync(picturePath, (int)Math.Ceiling(cell.IconSize * 2)).ConfigureAwait(true);

            if (picture is not null)
            {
                if (!_detached && _cells.Contains(cell))
                {
                    cell.Image.Source = picture;
                    cell.Image.Visibility = Visibility.Visible;
                    cell.Fallback.Visibility = Visibility.Collapsed;
                }

                return;
            }
        }

        if (ShortcutIcons.Choose(cell.Item, pictureReadable: false) == ShortcutIconSource.Emoji)
        {
            return;
        }

        if (cell.Item.IsLayout)
        {
            await LoadLayoutIconAsync(cell).ConfigureAwait(true);
            return;
        }

        try
        {
            ImageSource? source = cell.Item.IconPath is { } iconPath
                && LaunchTargets.Classify(iconPath) is { Kind: LaunchKind.LocalPath } iconTarget
                && LaunchTargets.MayProbe(iconTarget)
                ? await AppIconSource.GetAsync(iconTarget.Value, (int)Math.Ceiling(cell.IconSize * 2), AppIconSource.ThemeOf(this)).ConfigureAwait(true)
                : null;

            source ??= cell.Target.Kind switch
            {
                LaunchKind.Web => await FaviconAsync(cell.Target.Value).ConfigureAwait(true),
                LaunchKind.LocalPath or LaunchKind.ShellNamespace when LaunchTargets.MayProbe(cell.Target)
                    => await AppIconSource.GetAsync(cell.Target.Value, (int)Math.Ceiling(cell.IconSize * 2), AppIconSource.ThemeOf(this), variant).ConfigureAwait(true),
                _ => null,
            };

            if (_detached || source is null || !_cells.Contains(cell))
            {
                return;
            }

            cell.Image.Source = source;
            cell.Image.Visibility = Visibility.Visible;
            cell.Fallback.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            // The glyph stays; an icon is decoration.
            Log.Warn(Scope, "could not load a shortcut icon", ex);
        }
    }

    /// <summary>
    /// [S-1417] An emoji as an icon: colour glyphs from the system's emoji font (XAML falls back to it
    /// for emoji code points), its tight ink box scaled to fill the icon's square as the shell's icons do.
    /// </summary>
    private static Viewbox EmojiIcon(string emoji, double size) => new()
    {
        Width = size,
        Height = size,
        Stretch = Stretch.Uniform,
        Child = new TextBlock
        {
            Text = emoji,
            IsColorFontEnabled = true,
            TextLineBounds = TextLineBounds.Tight,
            IsTextScaleFactorEnabled = false,
        },
    };

    /// <summary>
    /// [S-1417] [S-1418] The user's picture at <paramref name="pixels"/>, or null when it cannot be drawn:
    /// a file that is gone, too big or will not decode (logged; the automatic icon is drawn instead,
    /// no notice: a moved picture is not an error the user must act on). An <c>.ico</c> goes through the
    /// shell, which picks its best size; an <c>.svg</c> is rasterised at the size; the rest are decoded.
    /// </summary>
    private async Task<ImageSource?> PictureAsync(string path, int pixels)
    {
        if (!ShortcutIcons.IsPicture(path))
        {
            return null; // repair refuses these on load; a picture set since is checked as it is set.
        }

        try
        {
            var bytes = await Task.Run(() =>
            {
                var file = new FileInfo(path);
                if (!file.Exists)
                {
                    Log.Info(Scope, $"a shortcut's picture is missing ({file.Name}); its automatic icon is drawn");
                    return null;
                }

                if (file.Length > ShortcutIcons.MaxPictureBytes)
                {
                    Log.Warn(Scope, $"a shortcut's picture is too big to draw ({file.Name}, {file.Length} bytes); its automatic icon is drawn");
                    return null;
                }

                return ShortcutIcons.IsIconFile(path) ? [] : File.ReadAllBytes(path);
            }).ConfigureAwait(true);

            if (bytes is null)
            {
                return null;
            }

            if (ShortcutIcons.IsIconFile(path))
            {
                return await AppIconSource.GetAsync(path, pixels, AppIconSource.ThemeOf(this)).ConfigureAwait(true);
            }

            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            if (ShortcutIcons.IsSvg(path))
            {
                var svg = new SvgImageSource { RasterizePixelWidth = pixels, RasterizePixelHeight = pixels };
                var status = await svg.SetSourceAsync(stream);
                if (status != SvgImageSourceLoadStatus.Success)
                {
                    Log.Warn(Scope, $"a shortcut's picture would not draw ({Path.GetFileName(path)}: {status}); its automatic icon is drawn");
                    return null;
                }

                return svg;
            }

            // An animated GIF holds still: a desktop icon that moves is a distraction and a cost.
            var bitmap = new BitmapImage { DecodePixelWidth = pixels, AutoPlay = false };
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or ArgumentException or InvalidOperationException)
        {
            Log.Warn(Scope, $"a shortcut's picture would not load ({Path.GetFileName(path)}); its automatic icon is drawn", ex);
            return null;
        }
    }

    private async Task<BitmapImage?> FaviconAsync(string url)
    {
        if (SiteIcons.FaviconOf(url) is not { } icon)
        {
            return null;
        }

        var origin = icon.GetLeftPart(UriPartial.Authority);

        if (!_favicons.TryGetValue(origin, out var decoding))
        {
            decoding = DecodeFaviconAsync(url);
            _favicons[origin] = decoding;
        }

        return await decoding.ConfigureAwait(true);
    }

    private async Task<BitmapImage?> DecodeFaviconAsync(string url)
    {
        var bytes = await _widgetHost.Favicons.GetAsync(url).ConfigureAwait(true);

        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            var bitmap = new BitmapImage { DecodePixelWidth = 96 };
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            // Bytes that passed the magic-number check and still would not decode: the globe stays.
            Log.Debug(Scope, $"a site icon would not decode: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// [S-1405] An app layout's icon previews what it opens (original S-72): up to four of its
    /// panes' icons, overlapping from the top left to the bottom right, in tree order. Web panes show
    /// their site's icon, the rest the shell's. With nothing assigned, or nothing loaded, the layout
    /// glyph stays.
    /// </summary>
    private async Task LoadLayoutIconAsync(Cell cell)
    {
        if (cell.Image.Parent is not Grid host)
        {
            return;
        }

        var sources = new List<ImageSource>();

        foreach (var assignment in LayoutPlan.Preview(cell.Item.Layout))
        {
            try
            {
                var target = LaunchTargets.Classify(assignment.Target);
                ImageSource? source = target.Kind switch
                {
                    LaunchKind.Web => await FaviconAsync(target.Value).ConfigureAwait(true),
                    LaunchKind.LocalPath when LaunchTargets.MayProbe(target)
                        => await AppIconSource.GetAsync(target.Value, (int)Math.Ceiling(cell.IconSize * 2), AppIconSource.ThemeOf(this)).ConfigureAwait(true),
                    _ => null,
                };

                if (source is not null)
                {
                    sources.Add(source);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, "could not load an app layout's pane icon", ex);
            }
        }

        if (_detached || sources.Count == 0 || !_cells.Contains(cell))
        {
            return;
        }

        var size = cell.IconSize;
        var each = sources.Count == 1 ? size : Math.Round(size * 0.66);
        var step = sources.Count == 1 ? 0 : (size - each) / (sources.Count - 1);

        for (var i = 0; i < sources.Count; i++)
        {
            // Inserted under the badge, which is the host's last child.
            host.Children.Insert(host.Children.Count - 1, new Image
            {
                Source = sources[i],
                Width = each,
                Height = each,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = new TranslateTransform { X = step * i, Y = step * i },
            });
        }

        cell.Fallback.Visibility = Visibility.Collapsed;
    }

    /// <summary>A tile's detail line or a row's second line, for an app layout too: how many apps it opens.</summary>
    private static string DetailOf(ShortcutItem item, LaunchTarget target)
        => item.IsLayout ? LayoutPlan.Summary(item.Layout) : DetailOf(target);

    /// <summary>A tile's detail line / a list row's second line: the host, the folder it is in, or the location.</summary>
    private static string DetailOf(LaunchTarget target) => target.Kind switch
    {
        LaunchKind.Web => Uri.TryCreate(target.Value, UriKind.Absolute, out var uri) ? uri.Host : target.Value,
        LaunchKind.LocalPath => Path.GetDirectoryName(target.Value.TrimEnd('\\')) ?? target.Value,
        LaunchKind.ShellNamespace => ShortcutsStr.WidgetsC_Shortcuts_Kind_Location.Text(),
        _ => ShortcutsStr.WidgetsC_Shortcuts_Kind_Refused.Text(),
    };

    /// <summary>The glyph that stands for an item until its real icon arrives, or for good when it has none.</summary>
    private static string GlyphFor(LaunchKind kind) => kind switch
    {
        LaunchKind.Web => Glyphs.Globe,
        LaunchKind.LocalPath => Glyphs.File,
        LaunchKind.ShellNamespace => Glyphs.Folder,
        _ => Glyphs.Warning,
    };

}
