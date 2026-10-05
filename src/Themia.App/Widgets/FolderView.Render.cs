using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>[S-1323] Drawing: the chrome, the ladder, rows, tiles, icons, badges.</summary>
internal sealed partial class FolderView
{
    private readonly List<ItemVisual> _visuals = [];
    private readonly List<IDisposable> _hovers = [];

    private ProgressRing _busy = null!;
    private StackPanel _trailing = null!;
    private Border _gitBadge = null!;
    private TextBlock _gitText = null!;
    private Border _backButton = null!;
    private Border _forwardButton = null!;
    private Border _upButton = null!;
    private Border _homeButton = null!;
    private Border _searchButton = null!;
    private Border _sortButton = null!;
    private Border _crumbHost = null!;
    private StackPanel _crumbs = null!;
    private TextBox _searchBox = null!;
    private Grid _columnHeader = null!;
    private ScrollViewer _scroll = null!;
    private Grid _scrollContent = null!;
    private Panel _items = null!;
    private Canvas _bandLayer = null!;
    private Border _band = null!;
    private Border _footerHost = null!;
    private TextBlock _footer = null!;
    private Border _messageHost = null!;
    private TextBlock _message = null!;
    private ScrollFade? _fade;

    private WidgetArea _area;
    private FolderPlan? _plan;
    private string _itemsKey = string.Empty;
    private IReadOnlyList<FolderEntry> _shown = [];
    private int _total;
    private int _renderGeneration;
    private string _search = string.Empty;
    private bool _searchOpen;

    /// <summary>One drawn item: what it shows, and the elements a selection or a late icon changes.</summary>
    private sealed class ItemVisual
    {
        public required FolderEntry Entry { get; init; }

        public required int Index { get; init; }

        public required Border Root { get; init; }

        /// <summary>Dimmed while the item is cut; the row itself carries the scroll fade's opacity.</summary>
        public required FrameworkElement Content { get; init; }

        public required Image Image { get; init; }

        public required FontIcon Glyph { get; init; }

        public required Grid IconBox { get; init; }

        public required TextBlock Name { get; init; }

        public required Grid NameCell { get; init; }

        public Border? Pill { get; init; }

        public Dictionary<FolderColumn, TextBlock> Cells { get; } = [];

        public bool Hovered { get; set; }

        public bool DropTarget { get; set; }
    }

    // ---- chrome ---------------------------------------------------------------------------------

    private void BuildChrome()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _busy = new ProgressRing { Width = 14, Height = 14, IsActive = false, Visibility = Visibility.Collapsed };

        _gitText = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        _gitText.TextTrimming = TextTrimming.CharacterEllipsis;
        _gitText.MaxWidth = FolderLayout.GitBadgeWidth - 22;
        _gitBadge = new Border
        {
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Fluent.Space1,
                Children = { Fluent.Icon(Glyphs.PullRequest, "TextFillColorSecondaryBrush", 12), _gitText },
            },
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        _backButton = HeaderButton(Glyphs.Back, Str.Common_Back.Text(), GoBack);
        _forwardButton = HeaderButton(Glyphs.Forward, Str.Folder_Forward.Text(), GoForward);
        _upButton = HeaderButton(Glyphs.MoveUp, Str.WidgetsB_Folder_UpTip.Text(), GoUp);
        _homeButton = HeaderButton(Glyphs.Home, Str.WidgetsB_Folder_HomeTip.Text(), GoHome);
        _searchButton = HeaderButton(Glyphs.Search, Str.WidgetsB_Folder_SearchTip.Text(), ToggleSearch);
        _sortButton = HeaderButton(Glyphs.Sort, Str.WidgetsB_Folder_ReverseTip.Text(), () => Edit(c => c.Descending = !c.Descending));

        // Read once by the card when it builds the header, so it exists from construction.
        _trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _busy, _gitBadge, _backButton, _forwardButton, _upButton, _homeButton, _searchButton, _sortButton },
        };

        _crumbs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        _crumbHost = new Border
        {
            Child = _crumbs,
            Height = FolderLayout.BreadcrumbHeight,
            Padding = new Thickness(Fluent.Space3, 0, Fluent.Space3, 0),
            Visibility = Visibility.Collapsed,
        };
        SetRow(_crumbHost, 0);

        _searchBox = new TextBox
        {
            PlaceholderText = Str.WidgetsB_Folder_SearchPlaceholder.Text(),
            Margin = new Thickness(Fluent.Space3, 0, Fluent.Space3, Fluent.Space1),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        _searchBox.TextChanged += (_, _) =>
        {
            if (_closingSearch)
            {
                return;
            }

            _search = _searchBox.Text;
            Render();
        };
        _searchBox.KeyDown += OnSearchKeyDown;
        _searchBox.GotFocus += (_, _) => _surface?.RequestTextInput(true);
        _searchBox.LostFocus += (_, _) => _surface?.RequestTextInput(false);
        SetRow(_searchBox, 1);

        _columnHeader = new Grid
        {
            Height = FolderLayout.ColumnHeaderHeight,
            Padding = new Thickness(FolderLayout.RowInset, 0, FolderLayout.RowInset, 0),
            ColumnSpacing = FolderColumns.Gap,
            Visibility = Visibility.Collapsed,
            Background = Fluent.RowRest(),
        };
        SetRow(_columnHeader, 2);

        _items = new StackPanel();
        _band = new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue),
            Child = new Border
            {
                Background = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue),
                Opacity = 0.15,
            },
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        _bandLayer = new Canvas { IsHitTestVisible = false, Children = { _band } };

        // Transparent, not null: the empty area below the items must still take presses (the band,
        // the background menu, a click that clears the selection) and drops.
        _scrollContent = new Grid { Background = Fluent.RowRest(), Children = { _items, _bandLayer } };

        _scroll = new ScrollViewer
        {
            Content = _scrollContent,
            Padding = WidgetLayout.ListInset,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        SetRow(_scroll, 3);

        // [S-1887] Rows are built as the list scrolls toward them, and when the tile grows.
        _scroll.ViewChanged += (_, _) => RealizeMore();
        _scroll.SizeChanged += (_, _) => RealizeMore();

        _footer = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        _footer.TextTrimming = TextTrimming.CharacterEllipsis;
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_footer, FontNumeralAlignment.Tabular);
        _footerHost = new Border
        {
            Child = _footer,
            Height = FolderLayout.FooterHeight,
            Padding = new Thickness(FolderLayout.RowInset, 0, FolderLayout.RowInset, 0),
            Visibility = Visibility.Collapsed,
        };
        _footer.VerticalAlignment = VerticalAlignment.Center;
        SetRow(_footerHost, 4);

        _message = new TextBlock();
        _messageHost = new Border
        {
            Child = Fluent.EmptyState(Glyphs.Folder, _message, out _),
            VerticalAlignment = VerticalAlignment.Stretch,
            Visibility = Visibility.Collapsed,
        };
        SetRow(_messageHost, 0);
        SetRowSpan(_messageHost, 5);

        Children.Add(_crumbHost);
        Children.Add(_searchBox);
        Children.Add(_columnHeader);
        Children.Add(_scroll);
        Children.Add(_footerHost);
        Children.Add(_messageHost);

        _fade = new ScrollFade(_scroll, _items);
        AttachInput();
    }

    private Border HeaderButton(string glyph, string tip, Action action)
    {
        var button = new Border
        {
            Child = Fluent.Icon(glyph, "TextFillColorSecondaryBrush", 14),
            Padding = new Thickness(7),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            Background = Fluent.RowRest(),
            Visibility = Visibility.Collapsed,
        };

        button.Tapped += (_, args) =>
        {
            args.Handled = true;
            action();
        };

        ToolTipService.SetToolTip(button, tip);
        _hovers.Add(Fluent.Hover(button));
        return button;
    }

    // ---- the ladder -----------------------------------------------------------------------------

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>
    /// [S-1323] One layout pass: the plan for this size, the chrome it shows, and a rebuild of the
    /// items only when what they look like changed (a resize that keeps the same columns and tiles
    /// moves nothing).
    /// </summary>
    private void Lay()
    {
        if (!_area.IsMeasured)
        {
            return;
        }

        var plan = FolderLayout.Plan(_area, _config, _shown.Count, _searchOpen, Navigates, _git is not null);
        _plan = plan;
        ApplyChrome(plan);

        if (ItemsKey(plan) != _itemsKey)
        {
            Render();
        }
    }

    private string ItemsKey(FolderPlan plan)
    {
        var grid = plan.Grid is { } g ? $"{g.Columns}|{Math.Round(g.CellWidth)}|{Math.Round(g.CellHeight)}|{g.Icon}" : "-";
        return $"{_config.View}|{plan.RowHeight}|{string.Join(',', plan.Columns)}|{grid}";
    }

    private void ApplyChrome(FolderPlan plan)
    {
        _crumbHost.Visibility = plan.Breadcrumb ? Visibility.Visible : Visibility.Collapsed;
        _searchBox.Visibility = plan.SearchBox ? Visibility.Visible : Visibility.Collapsed;
        _columnHeader.Visibility = plan.ColumnHeader ? Visibility.Visible : Visibility.Collapsed;
        _footerHost.Visibility = plan.Footer ? Visibility.Visible : Visibility.Collapsed;
        _scroll.Padding = plan.Grid is null ? WidgetLayout.ListInset : new Thickness(4, 2, 4, Fluent.Space2);
        ApplyHeader();
    }

    /// <summary>[S-1326] The header's buttons and git badge, by the plan and by what history allows.</summary>
    private void ApplyHeader()
    {
        // Re-planned here rather than read from the last pass: the git badge's rung depends on a status
        // that arrives after the listing, and must not wait for the next resize to appear.
        var header = _area.IsMeasured ? FolderLayout.Header(_area.Width, _config.View, Navigates, _git is not null) : FolderHeaderPlan.None;

        static Visibility Show(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;

        _backButton.Visibility = Show(header.Back);
        _backButton.Opacity = _history.CanGoBack ? 1 : 0.4;
        _forwardButton.Visibility = Show(header.Forward);
        _forwardButton.Opacity = _history.CanGoForward ? 1 : 0.4;
        _upButton.Visibility = Show(header.Up);
        _upButton.Opacity = _history.CanGoUp ? 1 : 0.4;
        _homeButton.Visibility = Show(header.Home && !_history.AtHome);
        _searchButton.Visibility = Show(header.Search || _searchOpen);
        _sortButton.Visibility = Show(header.Sort);
        ToolTipService.SetToolTip(_sortButton, _config.Descending ? Str.WidgetsB_Folder_OrderDescTip.Text() : Str.WidgetsB_Folder_OrderAscTip.Text());

        if (header.Git && _git is { } git)
        {
            var counts = git.Summary.Counts();
            _gitText.Text = counts.Length == 0 ? git.Summary.Branch : $"{git.Summary.Branch} {counts}";
            ToolTipService.SetToolTip(_gitBadge, GitTip(git.Summary));
            _gitBadge.Visibility = Visibility.Visible;
        }
        else
        {
            _gitBadge.Visibility = Visibility.Collapsed;
        }
    }

    private static string GitTip(GitSummary s) => s.Clean
        ? Str.WidgetsB_Folder_GitClean.Format(("branch", s.Branch))
        : Str.WidgetsB_Folder_GitCounts.Format(("branch", s.Branch), ("staged", s.Staged), ("modified", s.Modified), ("deleted", s.Deleted), ("untracked", s.Untracked), ("conflicted", s.Conflicted));

    // ---- drawing --------------------------------------------------------------------------------

    /// <summary>
    /// Draw what is in hand: filter, order, plan, items. Every state without items says which state it
    /// is; a blank tile is indistinguishable from a broken one.
    /// </summary>
    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var keywords = FolderFilter.Apply(_snapshot.Entries, _config.Filter, _search);
        _total = keywords.Count(e => _config.ShowHidden || !e.Hidden);
        _shown = FolderRules.Arrange(keywords, _config.Sort, _config.Descending, _config.FoldersFirst, _config.ShowHidden, RenderCap);
        _selection.SetOrder([.. _shown.Select(e => e.FullPath)]);

        if (_area.IsMeasured)
        {
            _plan = FolderLayout.Plan(_area, _config, _shown.Count, _searchOpen, Navigates, _git is not null);
            ApplyChrome(_plan);
        }

        ClearItems();
        BuildColumnHeader();
        UpdateCrumbs();

        var status = _snapshot.Status;

        if (status == FolderListingStatus.Ok && _shown.Count == 0)
        {
            ShowMessage(_search.Length > 0 || _config.Filter.Trim().Length > 0
                ? Str.WidgetsB_Folder_NoMatches.Text()
                : Str.WidgetsB_Folder_Status_Empty.Text());
            UpdateFooter();
            return;
        }

        if (status != FolderListingStatus.Ok)
        {
            ShowMessage(FolderFormatting.StatusMessage(status, _resolved.Problem));
            UpdateFooter();
            return;
        }

        _messageHost.Visibility = Visibility.Collapsed;
        _scroll.Visibility = Visibility.Visible;

        EnsureItemsPanel();

        var plan = _plan;
        var generation = ++_renderGeneration;
        _renderedAt = DateTimeOffset.Now;
        var scale = XamlRoot?.RasterizationScale ?? 1.0;

        // [S-1887] What is in view (or where the scroll is about to be put back) and one viewport
        // more; the rest is a spacer until the list scrolls toward it.
        var (extent, columns) = ItemExtent();
        RealizeTo(FolderRealize.Target(_shown.Count, 0, _restoreScroll ?? 0, Viewport(), extent, columns, _selection.FocusIndex));

        _itemsKey = plan is null ? string.Empty : ItemsKey(plan);
        RefreshSelectionVisuals();
        RefreshDims();
        UpdateFooter();
        _fade?.Refresh();
        LoadImages(generation, scale, 0);

        if (_restoreScroll is { } offset)
        {
            _restoreScroll = null;
            _scroll.UpdateLayout();
            _scroll.ChangeView(null, offset, null, disableAnimation: true);
        }

        if (_renameAfterLoad is { } renamed)
        {
            RealizeThrough(IndexOf(renamed));
        }

        if (_renameAfterLoad is { } renamedNow && _visuals.FirstOrDefault(v => string.Equals(v.Entry.FullPath, renamedNow, StringComparison.OrdinalIgnoreCase)) is { } target)
        {
            _renameAfterLoad = null;
            _selection.SelectOnly(renamedNow);
            RefreshSelectionVisuals();
            BeginRename(target);
        }
    }

    /// <summary>When the items on screen were drawn: the clock their "modified" cells read.</summary>
    private DateTimeOffset _renderedAt;

    /// <summary>[S-1887] The spacer standing for the items not built yet, or the "+N more" row; always last.</summary>
    private FrameworkElement? _tail;

    /// <summary>[S-1887] One item's height and how many sit side by side, as the plan lays them.</summary>
    private (double Extent, int Columns) ItemExtent()
        => _plan?.Grid is { } grid
            ? (grid.CellHeight, Math.Max(1, grid.Columns))
            : (_plan?.RowHeight ?? FolderLayout.RowHeight(_config.ItemSize), 1);

    /// <summary>The DIPs of list in view: the scroller's once it is measured, the tile's before.</summary>
    private double Viewport() => _scroll.ViewportHeight > 0 ? _scroll.ViewportHeight : _area.Height;

    /// <summary>
    /// [S-1887] Build items, in order, until <paramref name="count"/> are built, then put the tail back:
    /// the spacer for the rest, or the "+N more" row once everything is built. Returns how many were
    /// built before, so the caller can finish the new ones.
    /// </summary>
    private int RealizeTo(int count)
    {
        var from = _visuals.Count;
        var target = Math.Min(count, _shown.Count);

        if (target <= from && from > 0)
        {
            return from;
        }

        if (_tail is not null)
        {
            _items.Children.Remove(_tail);
            _tail = null;
        }

        var plan = _plan;
        for (var i = from; i < target; i++)
        {
            var visual = plan?.Grid is { } grid
                ? BuildTile(_shown[i], i, grid)
                : BuildRow(_shown[i], i, plan, _renderedAt);

            HookItem(visual);
            _visuals.Add(visual);
            _items.Children.Add(visual.Root);
        }

        var (extent, columns) = ItemExtent();
        if (_visuals.Count < _shown.Count)
        {
            // Not hit-testable, not an item (no int Tag): the tile panel lays it under the tiles.
            _tail = new Border { Height = FolderRealize.SpacerHeight(_shown.Count, _visuals.Count, extent, columns), IsHitTestVisible = false };
        }
        else if (_total > _shown.Count)
        {
            _tail = MoreRow(_total - _shown.Count);
        }

        if (_tail is not null)
        {
            _items.Children.Add(_tail);
        }

        return from;
    }

    /// <summary>[S-1887] The list scrolled or grew: build what has come within a viewport of the view.</summary>
    private void RealizeMore()
    {
        if (_detached || _visuals.Count >= _shown.Count || _scroll.Visibility != Visibility.Visible)
        {
            return;
        }

        var (extent, columns) = ItemExtent();
        Finish(RealizeTo(FolderRealize.Target(_shown.Count, _visuals.Count, _scroll.VerticalOffset, Viewport(), extent, columns, _selection.FocusIndex)));
    }

    /// <summary>[S-1887] Make sure the item at <paramref name="index"/> is built (the keyboard cursor, a rename).</summary>
    private void RealizeThrough(int index)
    {
        if (_detached || index < _visuals.Count || index >= _shown.Count)
        {
            return;
        }

        var (extent, columns) = ItemExtent();
        Finish(RealizeTo(FolderRealize.Target(_shown.Count, _visuals.Count, _scroll.VerticalOffset, Viewport(), extent, columns, index)));
    }

    /// <summary>Paint, dim and load the icons of the items built from <paramref name="from"/> on.</summary>
    private void Finish(int from)
    {
        if (from >= _visuals.Count)
        {
            return;
        }

        for (var i = from; i < _visuals.Count; i++)
        {
            Paint(_visuals[i]);
        }

        RefreshDims();
        _fade?.Refresh();
        LoadImages(_renderGeneration, XamlRoot?.RasterizationScale ?? 1.0, from);
    }

    private int IndexOf(string path)
    {
        for (var i = 0; i < _shown.Count; i++)
        {
            if (string.Equals(_shown[i].FullPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void ShowMessage(string text)
    {
        _message.Text = text;
        _messageHost.Visibility = Visibility.Visible;
        _scroll.Visibility = Visibility.Collapsed;
        _columnHeader.Visibility = Visibility.Collapsed;
        _itemsKey = string.Empty;
    }

    /// <summary>A list is a stack of rows; the grid views are a <see cref="TilePanel"/>. The fade follows the panel.</summary>
    private void EnsureItemsPanel()
    {
        var wantTiles = _plan?.Grid is not null;

        if (wantTiles == _items is TilePanel)
        {
            if (_items is TilePanel tiles && _plan?.Grid is { } g)
            {
                tiles.Set(g.Columns, g.CellWidth, g.CellHeight);
            }

            return;
        }

        _fade?.Dispose();
        _scrollContent.Children.Remove(_items);

        _items = wantTiles && _plan?.Grid is { } grid
            ? new TilePanel(grid.Columns, grid.CellWidth, grid.CellHeight)
            : new StackPanel();

        _scrollContent.Children.Insert(0, _items);
        _fade = new ScrollFade(_scroll, _items);
    }

    private void ClearItems()
    {
        foreach (var hover in _itemHovers)
        {
            hover.Dispose();
        }

        _itemHovers.Clear();
        _visuals.Clear();
        _items.Children.Clear();
        _tail = null;
        _renaming = null;
    }

    // ---- rows -----------------------------------------------------------------------------------

    private ItemVisual BuildRow(FolderEntry entry, int index, FolderPlan? plan, DateTimeOffset now)
    {
        var rowHeight = plan?.RowHeight ?? FolderLayout.RowHeight(_config.ItemSize);
        var iconSize = plan?.IconSize ?? FolderLayout.IconSize(_config.ItemSize);
        var columns = plan?.Columns ?? [];

        var (iconBox, image, glyph) = IconBox(entry, iconSize);

        var name = Fluent.Text(plan?.SmallText == true ? FluentType.Caption : FluentType.Body);
        name.Text = FileNamesDisplay(entry);
        name.VerticalAlignment = VerticalAlignment.Center;

        var nameCell = new Grid { Children = { name } };

        var content = new Grid { ColumnSpacing = FolderColumns.Gap, Margin = new Thickness(FolderLayout.RowInset, 0, FolderLayout.RowInset, 0) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(iconSize) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        SetColumn(iconBox, 0);
        SetColumn(nameCell, 1);
        content.Children.Add(iconBox);
        content.Children.Add(nameCell);

        var pill = new Border
        {
            Width = 3,
            Height = Math.Max(8, rowHeight / 2),
            CornerRadius = new CornerRadius(1.5),
            Background = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };

        var visual = new ItemVisual
        {
            Entry = entry,
            Index = index,
            Root = new Border { Height = rowHeight, Child = new Grid { Children = { content, pill } }, Tag = index },
            Content = content,
            Image = image,
            Glyph = glyph,
            IconBox = iconBox,
            Name = name,
            NameCell = nameCell,
            Pill = pill,
        };

        for (var c = 0; c < columns.Count; c++)
        {
            var column = columns[c];
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(FolderColumns.Width(column)) });

            var cell = Cell(column);
            cell.Text = CellText(entry, column, now);
            SetColumn(cell, c + 2);
            content.Children.Add(cell);
            visual.Cells[column] = cell;
        }

        RowFocus.Prepare(visual.Root);
        Prime(visual);
        ToolTipService.SetToolTip(visual.Root, entry.FullPath);
        return visual;
    }

    private static TextBlock Cell(FolderColumn column)
    {
        var cell = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        cell.VerticalAlignment = VerticalAlignment.Center;
        cell.TextTrimming = TextTrimming.CharacterEllipsis;
        cell.TextAlignment = FolderColumns.AlignsRight(column) ? TextAlignment.Right : TextAlignment.Left;

        if (column == FolderColumn.Cloud)
        {
            cell.FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets");
            cell.TextAlignment = TextAlignment.Center;
        }

        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(cell, FontNumeralAlignment.Tabular);
        return cell;
    }

    private string CellText(FolderEntry entry, FolderColumn column, DateTimeOffset now) => column switch
    {
        FolderColumn.Size => FolderFormatting.Size(entry),
        FolderColumn.Modified => FolderFormatting.Modified(entry.Modified, now),
        FolderColumn.Type => FileIcons.TypeName(entry),
        FolderColumn.Cloud => CloudGlyph(CloudBadges.State(entry.Attributes)),
        FolderColumn.Git => GitCode(entry),
        _ => string.Empty,
    };

    private static string CloudGlyph(CloudState state) => state switch
    {
        CloudState.CloudOnly => Glyphs.Cloudy,
        CloudState.Pinned => Glyphs.CheckCircleSolid,
        CloudState.Synced => Glyphs.CheckCircle,
        _ => string.Empty,
    };

    private string GitCode(FolderEntry entry)
    {
        if (_git is not { } git)
        {
            return string.Empty;
        }

        var relative = Path.GetRelativePath(git.Root, entry.FullPath);
        return relative.StartsWith("..", StringComparison.Ordinal) ? string.Empty : git.Summary.Code(relative, entry.IsDirectory);
    }

    private void RefreshGitCells()
    {
        foreach (var visual in _visuals)
        {
            if (visual.Cells.TryGetValue(FolderColumn.Git, out var cell))
            {
                cell.Text = GitCode(visual.Entry);
            }
        }
    }

    private string FileNamesDisplay(FolderEntry entry)
        => Themia.Core.Files.FileNames.Display(entry.Name, entry.IsDirectory, _config.ShowExtensions);

    /// <summary>The "+N more" row past the render cap: opens the folder in Explorer, where all of it is.</summary>
    private Border MoreRow(int more)
    {
        var text = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        text.Text = Str.WidgetsB_Folder_MoreInExplorer.Plural(more);
        var row = Fluent.Row(text, compact: true);
        row.Tapped += (_, args) =>
        {
            args.Handled = true;
            ShellFileOps.Open(CurrentPath);
        };
        _itemHovers.Add(Fluent.Hover(row));
        return row;
    }

    // ---- tiles ----------------------------------------------------------------------------------

    private ItemVisual BuildTile(FolderEntry entry, int index, GridPlan grid)
    {
        var labelled = _config.View == FolderViewMode.Grid;
        var (iconBox, image, glyph) = IconBox(entry, grid.Icon);
        iconBox.HorizontalAlignment = HorizontalAlignment.Center;

        var name = Fluent.Text(FluentType.Caption);
        name.Text = FileNamesDisplay(entry);
        name.TextWrapping = TextWrapping.Wrap;
        name.MaxLines = _config.TileSize == FolderTileSize.Large ? 3 : 2;
        name.TextAlignment = TextAlignment.Center;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.Visibility = labelled ? Visibility.Visible : Visibility.Collapsed;

        var nameCell = new Grid { Children = { name } };

        var content = new StackPanel
        {
            Spacing = Fluent.Space1,
            VerticalAlignment = labelled ? VerticalAlignment.Top : VerticalAlignment.Center,
            Padding = new Thickness(Fluent.Space1, labelled ? Fluent.Space2 : 0, Fluent.Space1, 0),
            Children = { iconBox, nameCell },
        };

        var visual = new ItemVisual
        {
            Entry = entry,
            Index = index,
            Root = new Border
            {
                Child = content,
                Margin = new Thickness(1),
                CornerRadius = new CornerRadius(Fluent.ControlRadius),
                Tag = index,
            },
            Content = content,
            Image = image,
            Glyph = glyph,
            IconBox = iconBox,
            Name = name,
            NameCell = nameCell,
        };

        RowFocus.Prepare(visual.Root);
        Prime(visual);
        ToolTipService.SetToolTip(visual.Root, labelled ? entry.FullPath : $"{entry.Name}\n{entry.FullPath}");
        return visual;
    }

    /// <summary>The icon slot: a glyph at once, the shell's icon (or a thumbnail) over it when it arrives, and the cloud badge.</summary>
    private (Grid Box, Image Image, FontIcon Glyph) IconBox(FolderEntry entry, double size)
    {
        var glyph = Fluent.Icon(
            entry.IsDirectory ? Glyphs.Folder : Glyphs.File,
            entry.IsDirectory ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush",
            size);
        glyph.HorizontalAlignment = HorizontalAlignment.Center;

        var image = new Image { Width = size, Height = size, Stretch = Stretch.Uniform };
        var box = new Grid { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, Children = { glyph, image } };

        var cloud = CloudBadges.State(entry.Attributes);

        if (cloud != CloudState.None)
        {
            // [S-1324] Over the icon's bottom-right corner, as Explorer draws its status overlay.
            var badge = Fluent.Icon(CloudGlyph(cloud),
                cloud == CloudState.CloudOnly ? "AccentTextFillColorPrimaryBrush" : "SystemFillColorSuccessBrush",
                Math.Max(8, Math.Round(size * 0.5)));
            badge.HorizontalAlignment = HorizontalAlignment.Right;
            badge.VerticalAlignment = VerticalAlignment.Bottom;
            badge.Margin = new Thickness(0, 0, -2, -2);
            ToolTipService.SetToolTip(badge, CloudBadges.Describe(cloud));
            box.Children.Add(badge);
        }

        return (box, image, glyph);
    }

    /// <summary>Paint a cached icon at once, so a redraw does not flash glyphs.</summary>
    private void Prime(ItemVisual visual)
    {
        var pixels = IconPixels(visual.IconBox.Width);

        if (FileIcons.TryIcon(visual.Entry, pixels, out var image) && image is not null)
        {
            visual.Image.Source = image;
            visual.Glyph.Visibility = Visibility.Collapsed;
        }
    }

    private int IconPixels(double size) => (int)Math.Round(size * (XamlRoot?.RasterizationScale ?? 1.0));

    /// <summary>
    /// Icons, thumbnails (grid views) and item counts, each set when it arrives — on the items of the
    /// render that asked for it, never on a later render's.
    /// </summary>
    private async void LoadImages(int generation, double scale, int from)
    {
        try
        {
            var visuals = _visuals.Skip(from).ToArray();
            var thumbnails = _plan?.Grid is not null;
            var counts = _plan?.Columns.Contains(FolderColumn.ItemCount) == true;

            foreach (var visual in visuals)
            {
                if (generation != _renderGeneration || _detached)
                {
                    return;
                }

                var pixels = (int)Math.Round(visual.IconBox.Width * scale);

                if (visual.Image.Source is null)
                {
                    var icon = await FileIcons.IconAsync(visual.Entry, pixels).ConfigureAwait(true);

                    if (generation != _renderGeneration)
                    {
                        return;
                    }

                    if (icon is not null)
                    {
                        visual.Image.Source = icon;
                        visual.Glyph.Visibility = Visibility.Collapsed;
                    }
                }

                if (thumbnails && await FileIcons.ThumbnailAsync(visual.Entry, pixels).ConfigureAwait(true) is { } thumb
                    && generation == _renderGeneration)
                {
                    visual.Image.Source = thumb;
                    visual.Glyph.Visibility = Visibility.Collapsed;
                }

                if (counts && visual.Entry.IsDirectory && visual.Cells.TryGetValue(FolderColumn.ItemCount, out var cell))
                {
                    var count = await FileIcons.CountAsync(visual.Entry).ConfigureAwait(true);

                    if (generation == _renderGeneration)
                    {
                        cell.Text = FolderFormatting.ItemCount(count, FileIcons.CountCap);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // async void: nothing may escape to the dispatcher.
            Themia.Core.Diagnostics.Log.Error(Scope, "icons could not be loaded", ex);
        }
    }

    // ---- the details header, the breadcrumb, the footer ----------------------------------------

    private void BuildColumnHeader()
    {
        _columnHeader.Children.Clear();
        _columnHeader.ColumnDefinitions.Clear();

        if (_plan is not { ColumnHeader: true } plan)
        {
            return;
        }

        var iconSize = plan.IconSize;
        _columnHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(iconSize) });
        _columnHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddHeaderCell(Str.Folder_ColName.Text(), 1, FolderSort.Name, TextAlignment.Left);

        for (var c = 0; c < plan.Columns.Count; c++)
        {
            var column = plan.Columns[c];
            _columnHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(FolderColumns.Width(column)) });

            FolderSort? sort = column switch
            {
                FolderColumn.Size => FolderSort.Size,
                FolderColumn.Modified => FolderSort.Modified,
                _ => null,
            };

            AddHeaderCell(FolderColumns.Title(column), c + 2, sort, FolderColumns.AlignsRight(column) ? TextAlignment.Right : TextAlignment.Left);
        }
    }

    /// <summary>A column title; a sortable one sorts on click and turns the order round on a second ([S-1322]).</summary>
    private void AddHeaderCell(string title, int column, FolderSort? sort, TextAlignment alignment)
    {
        var text = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        text.Text = sort is { } s && s == _config.Sort ? $"{title} {(_config.Descending ? "↓" : "↑")}" : title;
        text.TextAlignment = alignment;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.VerticalAlignment = VerticalAlignment.Center;

        var cell = new Border { Child = text, Background = Fluent.RowRest() };
        SetColumn(cell, column);

        if (sort is { } key)
        {
            cell.Tapped += (_, args) =>
            {
                args.Handled = true;
                Edit(c =>
                {
                    c.Descending = c.Sort == key && !c.Descending;
                    c.Sort = key;
                });
            };
        }

        _columnHeader.Children.Add(cell);
    }

    private void UpdateCrumbs()
    {
        _crumbs.Children.Clear();

        if (_plan is not { Breadcrumb: true } || CurrentPath.Length == 0)
        {
            return;
        }

        var crumbs = FolderHistory.Crumbs(CurrentPath);

        // The tail is what matters; leading crumbs give way first, as Explorer's address bar does.
        var budget = _area.Width - (2 * Fluent.Space3);
        var shown = new List<(string Label, string Path)>();

        for (var i = crumbs.Count - 1; i >= 0; i--)
        {
            var cost = FluentType.EstimateWidth(FluentType.Caption, crumbs[i].Label.Length) + 20;

            if (cost > budget && shown.Count > 0)
            {
                break;
            }

            budget -= cost;
            shown.Insert(0, crumbs[i]);
        }

        for (var i = 0; i < shown.Count; i++)
        {
            if (i > 0)
            {
                _crumbs.Children.Add(Fluent.Icon(Glyphs.ChevronRight, "TextFillColorTertiaryBrush", 10));
            }

            var (label, path) = shown[i];
            var last = i == shown.Count - 1;
            var text = Fluent.Text(FluentType.Caption, last ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
            text.Text = label;
            text.TextTrimming = TextTrimming.CharacterEllipsis;

            var crumb = new Border
            {
                Child = text,
                Padding = new Thickness(Fluent.Space1, 2, Fluent.Space1, 2),
                CornerRadius = new CornerRadius(Fluent.ControlRadius),
                Background = Fluent.RowRest(),
            };

            if (!last)
            {
                crumb.Tapped += (_, args) =>
                {
                    args.Handled = true;

                    if (Navigates)
                    {
                        NavigateTo(path);
                    }
                    else
                    {
                        ShellFileOps.Open(path);
                    }
                };
                _itemHovers.Add(Fluent.Hover(crumb));
            }

            ToolTipService.SetToolTip(crumb, path);
            _crumbs.Children.Add(crumb);
        }
    }

    /// <summary>[S-1323] "24 items · 2 selected · 3.1 MB".</summary>
    private void UpdateFooter()
    {
        if (_plan is not { Footer: true })
        {
            return;
        }

        var selected = _shown.Where(e => _selection.IsSelected(e.FullPath)).ToList();
        _footer.Text = _snapshot.Status == FolderListingStatus.Ok || _snapshot.Status == FolderListingStatus.Empty
            ? FolderFormatting.Status(_total, selected.Count, selected.Where(e => !e.IsDirectory).Sum(e => e.Size), selected.Any(e => !e.IsDirectory))
            : string.Empty;
    }

    /// <summary>[S-1329] Cut items dimmed while the cut is still on the clipboard.</summary>
    private void RefreshDims()
    {
        var pending = FileClipboard.CutStillPending();

        foreach (var visual in _visuals)
        {
            visual.Content.Opacity = pending && FileClipboard.IsCut(visual.Entry.FullPath) ? 0.45 : 1;
        }
    }

    // ---- the tile panel -------------------------------------------------------------------------

    /// <summary>
    /// [S-1323] The grid views' layout: fixed cells in rows, as many columns as the plan says, each
    /// cell stretched so a row fills the width (the original's <c>minmax(col, 1fr)</c>).
    /// </summary>
    private sealed partial class TilePanel : Panel
    {
        private int _columns;
        private double _cellWidth;
        private double _cellHeight;

        public TilePanel(int columns, double cellWidth, double cellHeight) => Set(columns, cellWidth, cellHeight);

        public void Set(int columns, double cellWidth, double cellHeight)
        {
            _columns = Math.Max(1, columns);
            _cellWidth = Math.Max(1, cellWidth);
            _cellHeight = Math.Max(1, cellHeight);
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var tiles = 0;
            var extra = 0.0;

            foreach (var child in Children)
            {
                if (child is FrameworkElement { Tag: int })
                {
                    child.Measure(new Size(_cellWidth, _cellHeight));
                    tiles++;
                }
                else
                {
                    // The "+N more" row sits under the tiles at full width.
                    child.Measure(new Size(_columns * _cellWidth, double.PositiveInfinity));
                    extra += child.DesiredSize.Height;
                }
            }

            var rows = (tiles + _columns - 1) / _columns;
            return new Size(_columns * _cellWidth, (rows * _cellHeight) + extra);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var i = 0;
            var rows = 0;

            foreach (var child in Children)
            {
                if (child is FrameworkElement { Tag: int })
                {
                    var row = i / _columns;
                    var column = i % _columns;
                    child.Arrange(new Rect(column * _cellWidth, row * _cellHeight, _cellWidth, _cellHeight));
                    rows = row + 1;
                    i++;
                }
            }

            var y = rows * _cellHeight;

            foreach (var child in Children)
            {
                if (child is not FrameworkElement { Tag: int })
                {
                    child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
                    y += child.DesiredSize.Height;
                }
            }

            return finalSize;
        }
    }
}
