using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// The Apps widget's cells: a tile (icon over name) or a row (icon, name, folder), the icon that
/// replaces the initials when it arrives, hover and cursor paint, and the app's own menu.
/// </summary>
internal sealed partial class AppsView
{
    /// <summary>One app's visual and the state it is painted from.</summary>
    private sealed class Cell
    {
        public required InstalledApp App { get; init; }

        public required Border Root { get; init; }

        /// <summary>The real icon, collapsed until it arrives (null in compact rows, which have none).</summary>
        public Image? Image { get; init; }

        /// <summary>The hue-and-initials tile shown until then, or for good when the shortcut has no icon.</summary>
        public Border? Fallback { get; init; }

        public double IconSize { get; init; }

        public bool Hovered { get; set; }
    }

    private readonly List<Cell> _cells = [];

    /// <summary>How many cells each section holds, in order: what the arrows walk ([S-1526]).</summary>
    private readonly List<int> _sectionSizes = [];

    /// <summary>The cell under the keyboard cursor, by index, or null.</summary>
    private int? _cursor;

    /// <summary>
    /// Rebuild every cell for the current shape: one grid for everything, so each tile and row is a
    /// direct child the scroll fade can dim, with a full-width heading row before each section.
    /// </summary>
    private void Render(List<AppSection> sections, IReadOnlyList<string>? chips)
    {
        var cursorApp = CursorCell()?.App;

        ClearCells();
        BuildChips(chips);

        _panel.RowDefinitions.Clear();
        _panel.ColumnDefinitions.Clear();
        _panel.Padding = Inset(_shape.View);

        var grid = _shape.View == AppsViewMode.Grid;
        var columns = Columns();
        var rowHeight = grid ? _shape.Tiles.TileHeight : _shape.Rows.RowHeight;

        _panel.ColumnSpacing = grid ? ShortcutLayout.Gap : 0;
        _panel.RowSpacing = 0;

        for (var c = 0; c < columns; c++)
        {
            _panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var row = 0;

        foreach (var section in sections)
        {
            if (_shape.Headers)
            {
                _panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var heading = Heading(section.Title, first: row == 0, inset: grid ? Fluent.Space2 : Fluent.Space4);
                SetRow(heading, row++);
                SetColumnSpan(heading, columns);
                _panel.Children.Add(heading);
            }

            var lines = (section.Apps.Count + columns - 1) / columns;

            for (var r = 0; r < lines; r++)
            {
                // Tiles keep a gap between their rows; rows sit flush, as every Windows 11 list does.
                _panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowHeight + (grid ? ShortcutLayout.Gap : 0)) });
            }

            for (var i = 0; i < section.Apps.Count; i++)
            {
                var cell = grid ? BuildTile(section.Apps[i]) : BuildRow(section.Apps[i]);
                SetRow(cell.Root, row + (i / columns));
                SetColumn(cell.Root, i % columns);
                Add(cell);
            }

            _sectionSizes.Add(section.Apps.Count);
            row += lines;
        }

        // The cursor follows its app across a redraw; an app the redraw dropped lets it go.
        _cursor = cursorApp is { } app && _cells.FindIndex(cell => cell.App == app) is var index and >= 0 ? index : null;

        PaintAll();
        _fade.Refresh();
    }

    /// <summary>A section heading, lined up with the text under it (tiles keep a margin, rows pad inside).</summary>
    private static TextBlock Heading(string title, bool first, double inset)
    {
        var heading = Fluent.Text(FluentType.Strong(FluentType.Caption), "TextFillColorSecondaryBrush");
        heading.Text = title;
        heading.TextTrimming = TextTrimming.CharacterEllipsis;
        heading.Margin = new Thickness(inset, first ? 0 : Fluent.Space2, inset, Fluent.Space1);
        return heading;
    }

    /// <summary>[S-1520] A tile: the icon centred over the name (the name only where the plan has room for it).</summary>
    private Cell BuildTile(InstalledApp app)
    {
        var plan = _shape.Tiles;
        var (host, image, fallback) = BuildIcon(app, plan.Icon);
        host.HorizontalAlignment = HorizontalAlignment.Center;

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(host);

        if (plan.Labels)
        {
            var name = Fluent.Text(FluentType.Caption);
            name.Text = app.Name;
            name.TextAlignment = TextAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.TextWrapping = TextWrapping.NoWrap;
            name.Margin = new Thickness(0, ShortcutLayout.LabelGap, 0, 0);
            stack.Children.Add(name);
        }

        var root = new Border
        {
            Child = stack,
            Height = plan.TileHeight,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
        };

        return new Cell { App = app, Root = root, Image = image, Fallback = fallback, IconSize = plan.Icon };
    }

    /// <summary>
    /// [S-1520] A row: the icon (none in compact rows), the name, and the folder — as a caption line
    /// under the name on a large list, or as a right-hand column where the plan has one.
    /// </summary>
    private Cell BuildRow(InstalledApp app)
    {
        var plan = _shape.Rows;
        var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Image? image = null;
        Border? fallback = null;

        if (plan.Icon > 0)
        {
            (var host, image, fallback) = BuildIcon(app, plan.Icon);
            host.Margin = new Thickness(0, 0, Fluent.Space3, 0);
            host.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(host);
        }

        var name = Fluent.Text(FluentType.Body);
        name.Text = app.Name;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.TextWrapping = TextWrapping.NoWrap;

        FrameworkElement nameBlock = name;

        if (plan.SecondLine)
        {
            var folder = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            folder.Text = InstalledAppRules.CategoryTitle(app.Category, Strings.Current);
            folder.TextTrimming = TextTrimming.CharacterEllipsis;
            folder.TextWrapping = TextWrapping.NoWrap;
            nameBlock = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name, folder } };
        }

        SetColumn(nameBlock, 1);
        grid.Children.Add(nameBlock);

        if (_shape.CategoryColumn && app.Category.Length > 0)
        {
            var category = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            category.Text = app.Category;
            category.TextAlignment = TextAlignment.Right;
            category.TextTrimming = TextTrimming.CharacterEllipsis;
            category.MaxWidth = 120;
            category.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
            SetColumn(category, 2);
            grid.Children.Add(category);
        }

        var root = new Border
        {
            Child = grid,
            Padding = new Thickness(Fluent.Space4, 0, Fluent.Space4, 0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
        };

        return new Cell { App = app, Root = root, Image = image, Fallback = fallback, IconSize = plan.Icon };
    }

    /// <summary>
    /// [S-1522] An app's icon slot: its initials on a colour hashed from its name straight away
    /// (original S-07), replaced by the real icon when it arrives.
    /// </summary>
    private static (Grid Host, Image Image, Border Fallback) BuildIcon(InstalledApp app, double size)
    {
        var initials = InstalledAppRules.Initials(app.Name);

        var text = Fluent.Text(size > 32 ? FluentType.BodyStrong : FluentType.Strong(FluentType.Caption));

        // The fill is always dark (34% lightness), so the initials are always white, in either theme;
        // a theme brush would put black on it in the dark theme.
        text.Foreground = new SolidColorBrush(Colors.White);
        text.Text = size < 24 ? initials[..System.Globalization.StringInfo.GetNextTextElement(initials, 0).Length] : initials;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextTrimming = TextTrimming.None;

        var rgb = ColorFormats.FromHsl(new HslColor(InstalledAppRules.Hue(app.Name), 0.38, 0.34));

        var fallback = new Border
        {
            Child = text,
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(Math.Round(size * 0.22)),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, rgb.R, rgb.G, rgb.B)),
        };

        var image = new Image
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Visibility = Visibility.Collapsed,
        };

        var host = new Grid { Width = size, Height = size };
        host.Children.Add(fallback);
        host.Children.Add(image);

        return (host, image, fallback);
    }

    private void Add(Cell cell)
    {
        var root = cell.Root;
        root.Tag = cell;
        root.Background = Fluent.RowRest();
        ToolTipService.SetToolTip(root, cell.App.Category.Length > 0 ? $"{cell.App.Name} — {cell.App.Category}" : cell.App.Name);

        root.PointerEntered += OnCellEntered;
        root.PointerExited += OnCellExited;
        root.PointerCaptureLost += OnCellExited;
        root.PointerCanceled += OnCellExited;
        root.Tapped += OnCellTapped;
        root.DoubleTapped += OnCellDoubleTapped;
        root.RightTapped += OnCellRightTapped;

        _cells.Add(cell);
        _panel.Children.Add(root);

        if (cell.Image is not null)
        {
            _ = LoadIconAsync(cell);
        }
    }

    private void ClearCells()
    {
        foreach (var cell in _cells)
        {
            var root = cell.Root;
            root.PointerEntered -= OnCellEntered;
            root.PointerExited -= OnCellExited;
            root.PointerCaptureLost -= OnCellExited;
            root.PointerCanceled -= OnCellExited;
            root.Tapped -= OnCellTapped;
            root.DoubleTapped -= OnCellDoubleTapped;
            root.RightTapped -= OnCellRightTapped;
        }

        _cells.Clear();
        _sectionSizes.Clear();
        _panel.Children.Clear();
    }

    private Cell? CursorCell() => _cursor is { } at && at >= 0 && at < _cells.Count ? _cells[at] : null;

    private List<int> SectionSizes() => _sectionSizes;

    /// <summary>
    /// [S-1522] The icon Explorer shows for the shortcut, asked at the drawn size (twice the DIPs, so
    /// it is sharp at 200%). <see cref="AppIcons"/> extracts one at a time and caches each answer, so
    /// a redraw asks again for free. A shortcut on a share is never probed; its initials stay.
    /// </summary>
    private async Task LoadIconAsync(Cell cell)
    {
        try
        {
            var target = LaunchTargets.Classify(cell.App.Target);

            if (!target.IsValid || target.Kind != LaunchKind.LocalPath || !LaunchTargets.MayProbe(target))
            {
                return;
            }

            var source = await AppIcons.GetAsync(target.Value, (int)Math.Ceiling(cell.IconSize * 2)).ConfigureAwait(true);

            if (_detached || source is null || cell.Image is null || !_cells.Contains(cell))
            {
                return;
            }

            cell.Image.Source = source;
            cell.Image.Visibility = Visibility.Visible;

            if (cell.Fallback is not null)
            {
                cell.Fallback.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            // The initials stay; an icon is decoration.
            Log.Warn(Scope, "could not load an app icon", ex);
        }
    }

    // ---- paint ------------------------------------------------------------------------------------

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

    /// <summary>Hover is the subtle fill; the keyboard cursor is an accent edge, so the two never read alike.</summary>
    private void Paint(Cell cell)
    {
        var cursor = ReferenceEquals(CursorCell(), cell);

        cell.Root.Background = cell.Hovered ? Fluent.RowHover() : Fluent.RowRest();
        cell.Root.BorderBrush = cursor
            ? FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue)
            : FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
    }

    // ---- clicks -----------------------------------------------------------------------------------

    private void OnCellTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is not Border { Tag: Cell cell })
        {
            return;
        }

        args.Handled = true;

        // A click puts the keyboard cursor there, so the arrows go on from what was clicked.
        _cursor = _cells.IndexOf(cell);
        PaintAll();

        if (_config.ClickMode == ItemClickMode.Single)
        {
            Open(cell);
        }
    }

    private void OnCellDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    {
        if (sender is not Border { Tag: Cell cell })
        {
            return;
        }

        // Handled either way: a double-click on an app is never the card's "enter edit mode".
        args.Handled = true;

        if (_config.ClickMode == ItemClickMode.Double)
        {
            Open(cell);
        }
    }

    private void OnCellRightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        if (sender is Border { Tag: Cell cell })
        {
            _cursor = _cells.IndexOf(cell);
            PaintAll();
            ItemMenu.Show(cell.Root, args, MenuFor(cell));
        }
    }

    /// <summary>
    /// The one launch path: the shortcut is classified again by <see cref="ShellLauncher"/> at the
    /// moment it opens. Inert in a preview card.
    /// </summary>
    private void Open(Cell cell)
    {
        if (_detached || _surface?.IsPreview == true)
        {
            return;
        }

        ShellLauncher.Open(cell.App.Target);
    }

    // ---- the item menu ([S-1528]) -----------------------------------------------------------------

    private void ShowMenuAt(Cell cell)
    {
        try
        {
            ItemMenu.Open(cell.Root, MenuFor(cell));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not open an app's menu", ex);
        }
    }

    /// <summary>
    /// [S-1528] Open, Open file location, Copy path, then Hide from list and — for an app in a
    /// folder — hiding the whole folder (original S-15 / S-24). Hiding is undone on the settings page.
    /// </summary>
    private IEnumerable<ItemMenuEntry> MenuFor(Cell cell)
    {
        var app = cell.App;

        yield return new ItemMenuEntry(Glyphs.Open, Str.Common_Open.Text(), () => Open(cell));
        yield return new ItemMenuEntry(Glyphs.Folder, Str.Shortcut_OpenFileLocation.Text(), () => Reveal(app));
        yield return new ItemMenuEntry(Glyphs.Copy, Str.Apps_CopyPath.Text(), () => Copy(app.Target));
        yield return ItemMenuEntry.Separator;
        yield return new ItemMenuEntry(Glyphs.Hide, Str.Apps_HideFromList.Text(), () => Hide(app, folder: false));
        yield return new ItemMenuEntry(Glyphs.Hide, Str.WidgetsA_Apps_HideFolder.Format(("folder", InstalledAppRules.CategoryTitle(app.Category, Strings.Current))),
            () => Hide(app, folder: true));
    }

    private void Reveal(InstalledApp app)
    {
        if (_surface?.IsPreview != true)
        {
            ShellLauncher.OpenContainingFolder(app.Target);
        }
    }

    private static void Copy(string text)
    {
        if (!ClipboardWatcher.SetText(text))
        {
            Log.Warn(Scope, "could not copy an app's path to the clipboard");
        }
    }

    /// <summary>[S-1523] Add the app's path, or its folder, to the widget's hide list and save.</summary>
    private void Hide(InstalledApp app, bool folder)
    {
        SetConfig(config =>
        {
            var list = folder ? config.HiddenCategories : config.HiddenApps;
            var value = folder ? app.Category : app.Target;

            if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(value);
            }
        });
    }
}
