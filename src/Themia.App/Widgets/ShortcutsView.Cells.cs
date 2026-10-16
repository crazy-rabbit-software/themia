using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
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

    /// <summary>Below this icon size the Recycle Bin's badge is a dot rather than a count.</summary>
    private const double DotBelow = 24;

    /// <summary>Decoded favicons by origin, per view: the bytes are shared app-wide by <see cref="FaviconCache"/>.</summary>
    private readonly Dictionary<string, Task<BitmapImage?>> _favicons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Rebuild every cell for the current shape.</summary>
    private void Render(List<ShortcutItem> visible)
    {
        ClearCells();

        _panel.RowDefinitions.Clear();
        _panel.ColumnDefinitions.Clear();
        _panel.Padding = Inset();

        if (_config.Style == ShortcutsStyle.Grid)
        {
            RenderGrid(visible, _shape.Tiles);
        }
        else
        {
            RenderRows(visible, _shape.Rows);
        }

        _selection.Prune(Order());
        PaintAll();
        _fade.Refresh();
    }

    private void RenderGrid(List<ShortcutItem> visible, ShortcutTilePlan plan)
    {
        _columnHeader.Visibility = Visibility.Collapsed;
        _panel.ColumnSpacing = ShortcutLayout.Gap;
        _panel.RowSpacing = ShortcutLayout.Gap;

        // [S-1382] The tiles fill from the top-left, row by row, in the columns the plan shared the
        // width into; a grid that does not fill the tile leaves the room at the right and bottom.
        _panel.HorizontalAlignment = HorizontalAlignment.Stretch;
        _panel.VerticalAlignment = VerticalAlignment.Top;

        for (var c = 0; c < plan.Columns; c++)
        {
            _panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var rows = (visible.Count + plan.Columns - 1) / plan.Columns;

        for (var r = 0; r < rows; r++)
        {
            _panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(plan.TileHeight) });
        }

        for (var i = 0; i < visible.Count; i++)
        {
            var cell = BuildTile(visible[i], plan);
            SetRow(cell.Root, i / plan.Columns);
            SetColumn(cell.Root, i % plan.Columns);
            Add(cell);
        }
    }

    private Cell BuildTile(ShortcutItem item, ShortcutTilePlan plan)
    {
        var target = LaunchTargets.Classify(item.Target);
        var (iconHost, image, fallback, badge, badgeText) = BuildIcon(item, target, plan.Icon);
        iconHost.HorizontalAlignment = HorizontalAlignment.Center;

        var name = Fluent.Text(FluentType.Caption);
        name.Text = NameOf(item);
        name.TextAlignment = TextAlignment.Center;
        name.HorizontalAlignment = HorizontalAlignment.Stretch;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.TextWrapping = TextWrapping.NoWrap;
        name.Margin = new Thickness(0, ShortcutLayout.LabelGap, 0, 0);
        name.Visibility = plan.Labels ? Visibility.Visible : Visibility.Collapsed;

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(iconHost);
        stack.Children.Add(name);

        if (plan.Labels && plan.Detail)
        {
            var detail = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            detail.Text = DetailOf(item, target);
            detail.TextAlignment = TextAlignment.Center;
            detail.TextTrimming = TextTrimming.CharacterEllipsis;
            detail.TextWrapping = TextWrapping.NoWrap;
            stack.Children.Add(detail);
        }

        var root = new Border
        {
            Child = stack,
            Padding = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
        };

        if (!plan.Labels)
        {
            // An icon-only tile still says what it is, on hover.
            ToolTipService.SetToolTip(root, NameOf(item));
        }

        return NewCell(item, target, root, stack, image, fallback, name, badge, badgeText, null, plan.Icon);
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

    private Cell BuildRow(ShortcutItem item, ShortcutRowPlan plan, bool details)
    {
        var target = LaunchTargets.Classify(item.Target);
        var (iconHost, image, fallback, badge, badgeText) = BuildIcon(item, target, plan.Icon);
        iconHost.Margin = new Thickness(0, 0, Fluent.Space3, 0);
        iconHost.VerticalAlignment = VerticalAlignment.Center;

        var name = Fluent.Text(FluentType.Body);
        name.Text = NameOf(item);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.TextWrapping = TextWrapping.NoWrap;

        var grid = new Grid();
        DetailColumns(grid, details, plan);

        SetColumn(iconHost, 0);
        grid.Children.Add(iconHost);

        FrameworkElement nameBlock = name;

        if (plan.SecondLine)
        {
            var line = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            line.Text = DetailOf(item, target);
            line.TextTrimming = TextTrimming.CharacterEllipsis;
            line.TextWrapping = TextWrapping.NoWrap;
            nameBlock = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name, line } };
        }

        SetColumn(nameBlock, 1);
        grid.Children.Add(nameBlock);

        TextBlock? kind = null;

        if (details && plan.TargetColumn)
        {
            var path = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            path.Text = item.IsLayout ? LayoutPlan.Summary(item.Layout) : item.Target;
            path.TextTrimming = TextTrimming.CharacterEllipsis;
            path.TextWrapping = TextWrapping.NoWrap;
            path.Margin = new Thickness(Fluent.Space3, 0, 0, 0);
            SetColumn(path, 2);
            grid.Children.Add(path);
        }

        if (details && plan.KindColumn)
        {
            kind = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            kind.Margin = new Thickness(Fluent.Space3, 0, 0, 0);
            kind.TextTrimming = TextTrimming.CharacterEllipsis;
            SetColumn(kind, 3);
            grid.Children.Add(kind);
        }

        var root = new Border
        {
            Child = grid,
            Padding = new Thickness(Fluent.Space4, 0, Fluent.Space4, 0),
            BorderThickness = new Thickness(Fluent.Hairline),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
        };

        grid.VerticalAlignment = VerticalAlignment.Center;

        return NewCell(item, target, root, grid, image, fallback, name, badge, badgeText, kind, plan.Icon);
    }

    /// <summary>The details table's columns: icon, name, then target and kind where the plan has them.</summary>
    private static void DetailColumns(Grid grid, bool details, ShortcutRowPlan plan)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (details && plan.TargetColumn)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        }

        if (details && plan.KindColumn)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        }
    }

    private void BuildColumnHeader(bool show, ShortcutRowPlan plan)
    {
        _columnHeader.Children.Clear();
        _columnHeader.ColumnDefinitions.Clear();
        _columnHeader.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        if (!show)
        {
            return;
        }

        DetailColumns(_columnHeader, details: true, plan);

        var spacer = new Border { Width = plan.Icon + Fluent.Space3 };
        _columnHeader.Children.Add(spacer);

        string[] titles = [Str.Folder_ColName.Text(), Str.WidgetsC_Shortcuts_Column_Target.Text(), Str.WidgetsC_Shortcuts_Column_Kind.Text()];

        for (var c = 1; c < _columnHeader.ColumnDefinitions.Count; c++)
        {
            var title = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            title.Text = titles[c - 1];
            title.Margin = new Thickness(c == 1 ? 0 : Fluent.Space3, 0, 0, 0);
            SetColumn(title, c);
            _columnHeader.Children.Add(title);
        }
    }

    /// <summary>
    /// An item's icon slot: the glyph for its kind straight away, replaced by the real icon when it
    /// arrives; the Recycle Bin's count badge pinned to the slot's top-right corner.
    /// </summary>
    private (Grid Host, Image Image, FontIcon Fallback, Border Badge, TextBlock BadgeText) BuildIcon(ShortcutItem item, LaunchTarget target, double size)
    {
        var fallback = item.IsLayout
            ? Fluent.Icon(Glyphs.AppLayout, "TextFillColorSecondaryBrush", Math.Round(size * 0.75))
            : Fluent.Icon(GlyphFor(target.Kind), ColorKeyFor(target.Kind), Math.Round(size * 0.75));
        fallback.HorizontalAlignment = HorizontalAlignment.Center;

        var image = new Image
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Visibility = Visibility.Collapsed,
        };

        var badgeText = new TextBlock();
        var badge = Fluent.Badge(badgeText);

        // 42% of the icon, held between 12 and 20 DIPs (original S-75).
        var badgeHeight = Math.Clamp(size * 0.42, 12, 20);

        if (size < DotBelow)
        {
            // Too small an icon for a legible count: a dot says "not empty", the tooltip says how much.
            badgeHeight = Fluent.Space2;
            badge.CornerRadius = new CornerRadius(badgeHeight / 2);
            badgeText.Visibility = Visibility.Collapsed;
        }

        badge.MinHeight = badgeHeight;
        badge.MinWidth = badgeHeight;
        badge.Padding = size < DotBelow ? new Thickness(0) : new Thickness(Fluent.Space1, 0, Fluent.Space1, 0);
        badgeText.FontSize = Math.Max(9, badgeHeight * 0.62);
        badgeText.LineHeight = badgeHeight;
        badge.HorizontalAlignment = HorizontalAlignment.Right;
        badge.VerticalAlignment = VerticalAlignment.Top;
        badge.Margin = new Thickness(0, -badgeHeight / 3, -badgeHeight / 2, 0);
        badge.Visibility = Visibility.Collapsed;

        var host = new Grid { Width = size, Height = size };
        host.Children.Add(fallback);
        host.Children.Add(image);
        host.Children.Add(badge);

        return (host, image, fallback, badge, badgeText);
    }

    private Cell NewCell(
        ShortcutItem item,
        LaunchTarget target,
        Border root,
        FrameworkElement content,
        Image image,
        FontIcon fallback,
        TextBlock name,
        Border badge,
        TextBlock badgeText,
        TextBlock? kind,
        double iconSize)
    {
        var cell = new Cell
        {
            Item = item,
            Target = target,
            Root = root,
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
        foreach (var cell in _cells)
        {
            Paint(cell);
        }
    }

    /// <summary>
    /// Hover, selection and the keyboard cursor. Selected is the subtle fill plus an accent edge, so
    /// it reads as selected and not merely hovered; the cursor alone (no selection) is the edge only.
    /// </summary>
    private void Paint(Cell cell)
    {
        var selected = _selection.Contains(cell.Item.Id);
        var cursor = _selection.Cursor == cell.Item.Id;

        // [S-6513] The item layer's row, in the cell's state.
        ItemFill.Paint(cell.Root, ItemRole.Row, selected ? (cell.Hovered ? ItemState.SelectedHover : ItemState.Selected)
            : cell.Hovered ? ItemState.Hover
            : ItemState.Rest);

        cell.Root.BorderBrush = selected || cursor
            ? FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue)
            : FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
    }

    /// <summary>[S-1386] A missing local target: dimmed, its name struck through, and the tooltip says so.</summary>
    private void PaintHealth(Cell cell)
    {
        if (cell.Target.Kind == LaunchKind.LocalPath && _health.TryGetValue(cell.Target.Value, out var health))
        {
            cell.Health = health;
        }

        var missing = cell.Health == TargetHealth.Missing;
        cell.Content.Opacity = missing || (!cell.Target.IsValid && !cell.Item.IsLayout) ? 0.5 : 1;
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
                ? Str.WidgetsC_Shortcuts_Tooltip_Missing.Format(("name", name), ("target", cell.Item.Target))
                : $"{name}\n{cell.Item.Target}");
        }
    }

    /// <summary>[S-1389] The bin's badge, tooltip, and its empty or full icon.</summary>
    private void PaintBin(Cell cell)
    {
        var state = RecycleBinStatus.Current;
        var badge = ShortcutRules.BadgeText(state);

        cell.BadgeText.Text = badge ?? string.Empty;
        cell.Badge.Visibility = badge is null ? Visibility.Collapsed : Visibility.Visible;
        ToolTipService.SetToolTip(cell.Root, ShortcutRules.BinTooltip(NameOf(cell.Item), state));

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
                ? await AppIcons.GetAsync(iconTarget.Value, (int)Math.Ceiling(cell.IconSize * 2), AppIcons.ThemeOf(this)).ConfigureAwait(true)
                : null;

            source ??= cell.Target.Kind switch
            {
                LaunchKind.Web => await FaviconAsync(cell.Target.Value).ConfigureAwait(true),
                LaunchKind.LocalPath or LaunchKind.ShellNamespace when LaunchTargets.MayProbe(cell.Target)
                    => await AppIcons.GetAsync(cell.Target.Value, (int)Math.Ceiling(cell.IconSize * 2), AppIcons.ThemeOf(this), variant).ConfigureAwait(true),
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

    private async Task<BitmapImage?> FaviconAsync(string url)
    {
        if (FeedAggregate.FaviconOf(url) is not { } icon)
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

    private static async Task<BitmapImage?> DecodeFaviconAsync(string url)
    {
        var bytes = await FaviconCache.GetAsync(url).ConfigureAwait(true);

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
                        => await AppIcons.GetAsync(target.Value, (int)Math.Ceiling(cell.IconSize * 2), AppIcons.ThemeOf(this)).ConfigureAwait(true),
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
                Margin = new Thickness(step * i, step * i, 0, 0),
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
        LaunchKind.ShellNamespace => Str.WidgetsC_Shortcuts_Kind_Location.Text(),
        _ => Str.WidgetsC_Shortcuts_Kind_Refused.Text(),
    };

    /// <summary>The glyph that stands for an item until its real icon arrives, or for good when it has none.</summary>
    private static string GlyphFor(LaunchKind kind) => kind switch
    {
        LaunchKind.Web => Glyphs.Globe,
        LaunchKind.LocalPath => Glyphs.File,
        LaunchKind.ShellNamespace => Glyphs.Folder,
        _ => Glyphs.Warning,
    };

    private static string ColorKeyFor(LaunchKind kind)
        => kind == LaunchKind.Refused ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush";
}
