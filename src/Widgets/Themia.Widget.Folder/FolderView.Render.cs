using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Folder;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1323] Drawing, on the family's blocks ([S-6711], [S-6713]): the header's parts, the path, the
/// ladder, the rows (Row blocks of 32 with the file's own 16 icon) and the cells (Row.Cell, 32 or 48),
/// icons, the Status row.
/// </summary>
internal sealed partial class FolderView
{
    private readonly List<ItemVisual> _visuals = [];
    private readonly List<IDisposable> _hovers = [];

    /// <summary>[S-1326] [S-12481] The tool line: the folder's name, the ring, the git badge, at most two buttons.</summary>
    private ToolLine _line = null!;
    private ProgressRing _busy = null!;
    private TextBlock _gitText = null!;
    private Border _backButton = null!;
    private Border _upButton = null!;
    private Border _searchButton = null!;
    private Border _sortButton = null!;
    private Border _crumbHost = null!;
    private StackPanel _crumbs = null!;
    private SearchBox _searchBox = null!;

    /// <summary>[S-13950] The search the tool line's Search opens: the one search box at the line's size, in the line's label while open.</summary>
    private SearchBox _lineSearch = null!;
    private Grid _columnHeader = null!;
    private ScrollViewer _scroll = null!;
    private Grid _scrollContent = null!;
    private Panel _items = null!;
    private Canvas _overlay = null!;
    private SelectionLabel _fullName = null!;
    private StatusBlock _status = null!;
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

        /// <summary>[S-6627] The selection's mark: behind the row or the cell, over all of it and over its item fill.</summary>
        public required Border Chrome { get; init; }

        /// <summary>Dimmed while the item is cut; the row itself carries the scroll fade's opacity.</summary>
        public required FrameworkElement Content { get; init; }

        /// <summary>[S-6631] The icon slot: the kind's glyph until the shell's icon (or a thumbnail) comes.</summary>
        public required IconSlot Icon { get; init; }

        /// <summary>The name: what a rename's box stands in for ([S-1331], [S-13950]).</summary>
        public required TextBlock Name { get; init; }

        /// <summary>A cell, not a row: its name may be shown in full when selected ([S-6629]).</summary>
        public bool Tile { get; init; }

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
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // [S-1326] [S-6710] [S-12481] The tool line, the content's first line: the folder's name as its label,
        // the busy ring, the git badge as its informing label and at most two buttons, wanted by
        // FolderLayout.Header; the line folds them by its own rule.
        _line = new ToolLine { Margin = Family.SideInset };
        var parts = _line.Parts;
        _busy = parts.Add(LinePartKind.Busy, ToolLine.Busy());
        _gitText = parts.Add(LinePartKind.Label, ToolLine.Note(string.Empty));
        _backButton = parts.Add(LinePartKind.Button, ToolLine.Button(Glyphs.Back, Str.Common_Back.Text(), GoBack));
        _upButton = parts.Add(LinePartKind.Button, ToolLine.Button(Glyphs.MoveUp, FolderStr.WidgetsB_Folder_UpTip.Text(), GoUp));
        _searchButton = parts.Add(LinePartKind.Button, ToolLine.Button(Glyphs.Search, FolderStr.WidgetsB_Folder_SearchTip.Text(), ToggleSearch));
        _sortButton = parts.Add(LinePartKind.Button, ToolLine.Button(Glyphs.Sort, FolderStr.WidgetsB_Folder_ReverseTip.Text(), () => Edit(c => c.Descending = !c.Descending)));
        foreach (var part in new FrameworkElement[] { _busy, _gitText, _backButton, _upButton, _searchButton, _sortButton })
        {
            parts.Want(part, false);
        }

        SetRow(_line, 0);

        // [S-1323] The path: a row of 28 at the side inset.
        _crumbs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = FamilySpace.S1, VerticalAlignment = VerticalAlignment.Center };
        _crumbHost = new Border
        {
            Child = _crumbs,
            Height = FolderLayout.BreadcrumbHeight,
            Padding = Family.SideInset,
            Visibility = Visibility.Collapsed,
        };
        SetRow(_crumbHost, 1);

        // [S-13606] The one search box (the Search box setting's field over the items); [S-13607] its keys: Enter opens the
        // first item shown, Escape empties it and, empty, folds it away.
        _searchBox = new SearchBox(FolderStr.WidgetsB_Folder_SearchPlaceholder.Text())
        {
            Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, 0),
            Visibility = Visibility.Collapsed,
        };
        SetRow(_searchBox, 2);

        // [S-13950] The same box at the line's size: the tool line's Search opens it in the line's label, so nothing moves.
        _lineSearch = new SearchBox(FolderStr.WidgetsB_Folder_SearchPlaceholder.Text(), SearchSize.Line);
        foreach (var box in new[] { _searchBox, _lineSearch })
        {
            box.Count = () => _shown.Count;
            box.PickBest = () => OpenEntry(_shown[0]);
            box.LetGo = CloseSearch;
            box.Changed += OnSearchTyped;
            box.GotFocus += (_, _) => _surface?.RequestTextInput(true);
            box.LostFocus += (_, _) => _surface?.RequestTextInput(false);
        }

        // [S-7034] The details table's titles: a Caption line over the columns, as the kit's table.
        _columnHeader = new Grid
        {
            Height = FolderLayout.ColumnHeaderHeight,
            Padding = new Thickness(FamilySpace.TextEdge, 0, FamilySpace.Inset, 0),
            ColumnSpacing = FolderColumns.Gap,
            Visibility = Visibility.Collapsed,
            Background = Family.Brush(ColourRole.Clear),
        };
        SetRow(_columnHeader, 3);

        _items = new StackPanel();
        // [S-6629] The layer a selected tile's full name is laid on, over the items.
        _overlay = new Canvas { IsHitTestVisible = false };
        _fullName = new SelectionLabel(_overlay);

        // Clear, not null: the empty area below the items must still take presses (the
        // background menu, a click that clears the selection) and drops.
        _scrollContent = new Grid { Background = Family.Brush(ColourRole.Clear), Children = { _items, _overlay } };

        _scroll = new ScrollViewer
        {
            Content = _scrollContent,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        SetRow(_scroll, 4);

        // [S-1887] Rows are built as the list scrolls toward them, and when the tile grows.
        _scroll.ViewChanged += (_, _) => RealizeMore();
        _scroll.SizeChanged += (_, _) =>
        {
            FillViewport();
            RealizeMore();
        };

        // [S-1334] [S-6714] A folder with nothing to show says which state it is in the Status row, at the top.
        _status = new StatusBlock { VerticalAlignment = VerticalAlignment.Top };
        _status.Hide();
        SetRow(_status, 4);

        Children.Add(_line);
        Children.Add(_crumbHost);
        Children.Add(_searchBox);
        Children.Add(_columnHeader);
        Children.Add(_scroll);
        Children.Add(_status);

        _fade = new ScrollFade(_scroll, _items);
        AttachInput();
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
    /// items only when what they look like changed (a resize that keeps the same columns and cells
    /// moves nothing).
    /// </summary>
    private void Lay()
    {
        if (!_area.IsMeasured)
        {
            return;
        }

        var plan = FolderLayout.Plan(_area, _config, _shown.Count, _shown.Any(e => !e.IsDirectory), Navigates, _git is not null);
        _plan = plan;
        ApplyChrome(plan);

        if (ItemsKey(plan) != _itemsKey)
        {
            Render();
        }
    }

    private string ItemsKey(FolderPlan plan)
    {
        var grid = plan.Grid is { } g ? $"{g.Columns}|{Math.Round(g.Width)}|{g.Icon}|{g.Named}" : "-";
        return $"{_config.Style}|{plan.Modified}|{string.Join(',', plan.Columns)}|{grid}";
    }

    private void ApplyChrome(FolderPlan plan)
    {
        _crumbHost.Visibility = plan.Breadcrumb ? Visibility.Visible : Visibility.Collapsed;
        _searchBox.Visibility = plan.SearchBox ? Visibility.Visible : Visibility.Collapsed;
        var cells = _area.Grid(XamlRoot?.RasterizationScale ?? 1);
        _searchBox.Fill(FolderLayout.SearchBand(cells, plan.Breadcrumb));
        _columnHeader.Visibility = plan.ColumnHeader ? Visibility.Visible : Visibility.Collapsed;

        // [S-14102] The path's and the titles' bands end on grid lines, so the rows under them start on one.
        _crumbHost.Height = FolderLayout.CrumbBand(cells);
        _columnHeader.Height = FolderLayout.HeaderBand(cells, FolderLayout.Over(cells, plan.SearchBox, header: false, plan.Breadcrumb));

        // [S-6712] [S-6713] [S-14816] The items from their top to the card's foot: rows, or cells from the card's left edge,
        // touching, the last line in sight cut by the foot.
        var height = FolderLayout.ItemsHeight(_area, plan.SearchBox, plan.ColumnHeader, plan.Breadcrumb, rows: true);
        if (plan.Grid is not null)
        {
            _scroll.Padding = new Thickness(0);
            _scroll.Height = height;
        }
        else
        {
            // [S-12807] [S-14810] The list's rows at their one fixed size from the items' top, the viewport the card's room,
            // the last row in sight cut by its foot; each on whole device pixels.
            var reach = FolderLayout.Reach(height, _area.Grid(XamlRoot?.RasterizationScale ?? 1));
            if (reach != _rowReach)
            {
                _rowReach = reach;
                _rowPitch = reach.Pitch;
                foreach (var row in _items.Children.OfType<Border>().Where(b => b.Tag is int))
                {
                    row.Height = reach.Height((int)row.Tag);
                }
            }

            _scroll.Padding = new Thickness(0);
            _scroll.Height = reach.Room;
        }

        FillViewport();
        ApplyHeader();
    }

    /// <summary>
    /// [S-6412] The content is at least the viewport tall, so the empty space under a short listing
    /// takes presses (the background menu, a click that clears) down to the bottom.
    /// </summary>
    private void FillViewport()
        => _scrollContent.MinHeight = Math.Max(0, _scroll.ActualHeight - _scroll.Padding.Top - _scroll.Padding.Bottom);

    /// <summary>[S-1326] The tool line's buttons and git label, by the plan and by what history allows.</summary>
    private void ApplyHeader()
    {
        // Re-planned here rather than read from the last pass: the git badge's rung depends on a status
        // that arrives after the listing, and must not wait for the next resize to appear.
        var header = _area.IsMeasured ? FolderLayout.Header(_config, Navigates, _git is not null) : FolderHeaderPlan.None;

        _line.Parts.Want(_backButton, header.Back);
        Enable(_backButton, _history.CanGoBack);
        _line.Parts.Want(_upButton, header.Up);
        Enable(_upButton, _history.CanGoUp);
        _line.Parts.Want(_searchButton, header.Search);
        _line.Parts.Want(_sortButton, header.Sort);
        ToolTipService.SetToolTip(_sortButton, _config.Descending ? FolderStr.WidgetsB_Folder_OrderDescTip.Text() : FolderStr.WidgetsB_Folder_OrderAscTip.Text());

        if (header.Git && _git is { } git)
        {
            var counts = git.Summary.Counts();
            _gitText.Text = counts.Length == 0 ? git.Summary.Branch : $"{git.Summary.Branch} {counts}";
            ToolTipService.SetToolTip(_gitText, GitTip(git.Summary));
            _line.Parts.Want(_gitText, true);
        }
        else
        {
            _line.Parts.Want(_gitText, false);
        }
    }

    /// <summary>A line button that cannot act now (no way back, at the root): its glyph in the disabled role ([S-1940]).</summary>
    private static void Enable(Border button, bool can)
    {
        if (button.Child is FontIcon glyph)
        {
            glyph.Foreground = Family.Brush(can ? ColourRole.TextSecondary : ColourRole.TextDisabled);
        }

        button.IsHitTestVisible = can;
    }

    private static string GitTip(GitSummary s) => s.Clean
        ? FolderStr.WidgetsB_Folder_GitClean.Format(("branch", s.Branch))
        : FolderStr.WidgetsB_Folder_GitCounts.Format(("branch", s.Branch), ("staged", s.Staged), ("modified", s.Modified), ("deleted", s.Deleted), ("untracked", s.Untracked), ("conflicted", s.Conflicted));

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

        LinkDrive();

        var keywords = FolderFilter.Apply(_snapshot.Entries, _search);
        _total = keywords.Count(e => _config.ShowHidden || !e.Hidden);
        _shown = FolderRules.Arrange(keywords, _config.Sort, _config.Descending, _config.ShowHidden, RenderCap);
        _selection.SetOrder([.. _shown.Select(e => e.FullPath)]);

        if (_area.IsMeasured)
        {
            _plan = FolderLayout.Plan(_area, _config, _shown.Count, _shown.Any(e => !e.IsDirectory), Navigates, _git is not null);
            ApplyChrome(_plan);
        }

        ClearItems();
        BuildColumnHeader();
        UpdateCrumbs();

        var status = _snapshot.Status;

        if (status == FolderListingStatus.Ok && _shown.Count == 0)
        {
            ShowMessage(StatusKind.Empty, _search.Length > 0
                ? FolderStr.WidgetsB_Folder_NoMatches.Text()
                : Str.WidgetsB_Folder_Status_Empty.Text());
            return;
        }

        if (status != FolderListingStatus.Ok)
        {
            ShowMessage(status is FolderListingStatus.Loading or FolderListingStatus.Empty ? StatusKind.Empty : StatusKind.Problem,
                FolderFormatting.StatusMessage(status, _resolved.Problem));
            return;
        }

        _status.Hide();
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

    /// <summary>[S-12807] [S-14810] A list row's height: 28 at its optimal, its one fixed size.</summary>
    private double _rowPitch = FolderLayout.RowHeight;

    /// <summary>[S-14810] The list's rows as laid: each at its size on whole device pixels.</summary>
    private RowReach _rowReach = new(FolderLayout.RowHeight);


    /// <summary>When the items on screen were drawn: the clock their "modified" cells read.</summary>
    private DateTimeOffset _renderedAt;

    /// <summary>[S-1887] The spacer standing for the items not built yet, or the "+N more" row; always last.</summary>
    private FrameworkElement? _tail;

    /// <summary>[S-1887] One item's pitch and how many sit side by side, as the plan lays them.</summary>
    private (double Extent, int Columns) ItemExtent()
        => _plan?.Grid is { } grid
            ? (grid.Height, Math.Max(1, grid.Columns))
            : (_rowPitch, 1);

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

    /// <summary>[S-1334] [S-6714] The Status row where the first item would be: the folder's glyph for a plain state, the warning for a problem.</summary>
    private void ShowMessage(StatusKind kind, string text)
    {
        _status.Show(kind, Glyphs.Folder, text);
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
                tiles.Set(g);
            }

            return;
        }

        _fade?.Dispose();
        _scrollContent.Children.Remove(_items);

        _items = wantTiles && _plan?.Grid is { } grid
            ? new TilePanel(grid)
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
        _fullName.Hide();
        _visuals.Clear();
        _items.Children.Clear();
        _tail = null;
        _renaming = null;
    }

    // ---- rows -----------------------------------------------------------------------------------

    /// <summary>
    /// [S-6711] A row: the Row block of 32, the file's own icon (16) as its lead, the name primary, and
    /// the list's date modified as its value or the details table's columns in the name's place
    /// ([S-7034]). The selection's mark lies behind the whole row, so marking it moves nothing.
    /// </summary>
    private ItemVisual BuildRow(FolderEntry entry, int index, FolderPlan? plan, DateTimeOffset now)
    {
        var columns = plan?.Columns ?? [];
        var details = _config.Style == FolderStyle.Details;
        var icon = IconBox(entry, FolderLayout.Lead);

        var block = new RowBlock(RowKind.List, FolderLayout.Lead) { Lines = 1 };
        block.Lead(icon);

        TextBlock name;
        Grid? cells = null;

        if (details)
        {
            block.Show(string.Empty);
            cells = new Grid { ColumnSpacing = FolderColumns.Gap };
            cells.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            name = Family.Text(FamilyType.Body, ColourRole.TextPrimary, FileNamesDisplay(entry));
            name.VerticalAlignment = VerticalAlignment.Center;
            cells.Children.Add(name);
            block.Cells(cells);
        }
        else
        {
            block.Show(FileNamesDisplay(entry), plan?.Modified == true ? FolderFormatting.Modified(entry.Modified, now) : null);
            name = block.NameText;
        }

        // [S-6412] As wide as its text: a press on the rest of the row selects, never drags the item out.
        name.HorizontalAlignment = HorizontalAlignment.Left;

        // [S-6627] The whole row is the item's mark, as the desktop marks the whole icon and name.
        var chrome = SelectionChrome.Wrap(new Grid());

        var visual = new ItemVisual
        {
            Entry = entry,
            Index = index,
            Root = new Border { Height = _rowReach.Height(index), Child = new Grid { Children = { chrome, block } }, Tag = index },
            Chrome = chrome,
            Content = block,
            Icon = icon,
            Name = name,
        };

        if (cells is { } grid)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                var column = columns[c];
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(FolderColumns.Width(column)) });

                FrameworkElement shown;
                if (column == FolderColumn.Cloud)
                {
                    // [S-1324] The cloud state as a 16 glyph, secondary; none for a local item.
                    var state = CloudBadges.State(entry.Attributes);
                    shown = Family.Glyph(CloudGlyph(state), FamilyShape.Icon, ColourRole.TextSecondary);
                    shown.HorizontalAlignment = c == columns.Count - 1 ? HorizontalAlignment.Right : HorizontalAlignment.Center;
                    shown.Visibility = state == CloudState.None ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    var cell = Cell(column, last: c == columns.Count - 1);
                    cell.Text = CellText(entry, column, now);
                    visual.Cells[column] = cell;
                    shown = cell;
                }

                shown.VerticalAlignment = VerticalAlignment.Center;
                SetColumn(shown, c + 1);
                grid.Children.Add(shown);
            }
        }

        WidgetItems.Mark(visual.Root); // [S-814] a press here is the item's, not the background's
        ItemMark.Mark(visual.Root);
        ItemFill.Paint(visual.Root, ItemRole.Row); // [S-6513] the item layer at rest; the selection is laid over it
        Prime(visual);
        ToolTipService.SetToolTip(visual.Root, Tip(entry));
        return visual;
    }

    /// <summary>An item's tooltip: its path, and its cloud state where it has one ([S-1324]).</summary>
    private static string Tip(FolderEntry entry)
        => CloudBadges.State(entry.Attributes) is var cloud && cloud != CloudState.None
            ? $"{entry.FullPath}{Environment.NewLine}{CloudBadges.Describe(cloud)}"
            : entry.FullPath;

    /// <summary>[S-6711] A table cell: Body, secondary, as the Row block's value; a number column end-aligned.</summary>
    private static TextBlock Cell(FolderColumn column, bool last)
    {
        var cell = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
        cell.TextAlignment = FolderColumns.AlignsRight(column, last) ? TextAlignment.Right : TextAlignment.Left;
        cell.HorizontalAlignment = FolderColumns.AlignsRight(column, last) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        return cell;
    }

    private string CellText(FolderEntry entry, FolderColumn column, DateTimeOffset now) => column switch
    {
        FolderColumn.Size => FolderFormatting.Size(entry),
        FolderColumn.Modified => FolderFormatting.Modified(entry.Modified, now),
        FolderColumn.Type => _widgetHost.FileIcons.TypeName(entry),
        FolderColumn.Git => GitCode(entry),
        _ => string.Empty,
    };

    private static string CloudGlyph(CloudState state) => state switch
    {
        CloudState.CloudOnly => Glyphs.Cloudy,
        CloudState.Pinned => Glyphs.CheckCircleSolid,
        CloudState.Synced => Glyphs.CheckCircle,
        _ => Glyphs.Cloudy,
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
        => Themia.Core.Files.FileNames.Display(entry.Name, entry.IsDirectory, _config.Extensions);

    /// <summary>The "+N more" row past the render cap: a Row block of 32 that opens the folder in Explorer, where all of it is.</summary>
    private RowBlock MoreRow(int more)
    {
        var row = new RowBlock(RowKind.List, FolderLayout.Lead) { Interactive = true };
        row.LeadGlyph(Glyphs.OpenExternal);
        row.Show(FolderStr.WidgetsB_Folder_MoreInExplorer.Plural(more));
        row.NameText.Foreground = Family.Brush(ColourRole.TextSecondary);
        row.Tapped += (_, args) =>
        {
            args.Handled = true;
            _widgetHost.FileOps.Open(CurrentPath);
        };
        _itemHovers.Add(new Unhook(row.Detach));
        return row;
    }

    // ---- tiles ----------------------------------------------------------------------------------

    /// <summary>
    /// [S-6713] A cell (Row.Cell): the file's icon (or the picture's thumbnail) at the grid's 32 or 48
    /// over its name in Caption, one line (the whole name shows on selection, [S-6629]); Icons draws
    /// no name. Its mark stands 4 around it.
    /// </summary>
    private ItemVisual BuildTile(FolderEntry entry, int index, CellGrid grid)
    {
        var icon = IconBox(entry, grid.Icon);
        // [S-14816] The lead and name inside the cell's box, its padding round them: the box is the item, edge to edge.
        var cell = new RowCell(grid.Icon) { Width = grid.ContentWidth, Margin = new Thickness(FamilySpace.S2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        cell.Show(icon, grid.Named ? FileNamesDisplay(entry) : string.Empty);

        // [S-6627] The cell, icon and name, is the item's mark.
        var chrome = SelectionChrome.Wrap(new Grid());

        var visual = new ItemVisual
        {
            Entry = entry,
            Index = index,
            Root = new Border
            {
                Child = new Grid { Children = { chrome, cell } },
                CornerRadius = Family.Radius,
                Tag = index,
            },
            Chrome = chrome,
            Content = cell,
            Icon = icon,
            Name = cell.NameText,
            Tile = true,
        };

        WidgetItems.Mark(visual.Root); // [S-814] a press here is the item's, not the background's
        ItemMark.Mark(visual.Root);
        ItemFill.Paint(visual.Root, ItemRole.Row); // [S-6513] the item layer at rest; the selection is laid over it
        Prime(visual);
        ToolTipService.SetToolTip(visual.Root, grid.Named ? Tip(entry) : $"{entry.Name}\n{Tip(entry)}");
        return visual;
    }

    /// <summary>[S-6631] The icon slot: the kind's glyph at once (secondary, the lead's role), the shell's icon (or a thumbnail) over it when it arrives.</summary>
    private static IconSlot IconBox(FolderEntry entry, double size)
    {
        var glyph = Family.Glyph(entry.IsDirectory ? Glyphs.Folder : Glyphs.File, size, ColourRole.TextSecondary);
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        return new IconSlot(size, glyph) { VerticalAlignment = VerticalAlignment.Center };
    }

    /// <summary>Paint a cached icon at once, so a redraw does not flash glyphs.</summary>
    private void Prime(ItemVisual visual)
    {
        var pixels = IconPixels(visual.Icon.Side);

        if (_widgetHost.FileIcons.TryIcon(visual.Entry, pixels, out var image) && image is not null)
        {
            visual.Icon.Show(image);
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

                var pixels = (int)Math.Round(visual.Icon.Side * scale);

                if (visual.Icon.Source is null)
                {
                    var icon = await _widgetHost.FileIcons.IconAsync(visual.Entry, pixels).ConfigureAwait(true);

                    if (generation != _renderGeneration)
                    {
                        return;
                    }

                    if (icon is not null)
                    {
                        visual.Icon.Show(icon);
                    }
                }

                if (thumbnails && await _widgetHost.FileIcons.ThumbnailAsync(visual.Entry, pixels).ConfigureAwait(true) is { } thumb
                    && generation == _renderGeneration)
                {
                    visual.Icon.Show(thumb);
                }

                if (counts && visual.Entry.IsDirectory && visual.Cells.TryGetValue(FolderColumn.ItemCount, out var cell))
                {
                    var count = await _widgetHost.FileIcons.CountAsync(visual.Entry).ConfigureAwait(true);

                    if (generation == _renderGeneration)
                    {
                        cell.Text = FolderFormatting.ItemCount(count, _widgetHost.FileIcons.CountCap);
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

    // ---- the details header and the breadcrumb ----------------------------------------

    private void BuildColumnHeader()
    {
        _columnHeader.Children.Clear();
        _columnHeader.ColumnDefinitions.Clear();

        if (_plan is not { ColumnHeader: true } plan)
        {
            return;
        }

        _columnHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddHeaderCell(Str.Folder_ColName.Text(), 0, FolderSort.Name, TextAlignment.Left);

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

            AddHeaderCell(FolderColumns.Title(column), c + 1, sort, FolderColumns.AlignsRight(column, last: c == plan.Columns.Count - 1) ? TextAlignment.Right : TextAlignment.Left);
        }
    }

    /// <summary>A column title (Caption, secondary); a sortable one sorts on click and turns the order round on a second ([S-1322]).</summary>
    private void AddHeaderCell(string title, int column, FolderSort? sort, TextAlignment alignment)
    {
        var text = Family.Text(FamilyType.Caption, ColourRole.TextSecondary,
            sort is { } s && s == _config.Sort ? $"{title} {(_config.Descending ? "↓" : "↑")}" : title);
        text.TextAlignment = alignment;
        text.HorizontalAlignment = alignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        // [S-12815] The titles stand on their band's foot, right over the first row (WR-1: nothing between the first item
        // and what stands over it), as the kit's table titles do.
        text.VerticalAlignment = VerticalAlignment.Bottom;

        var cell = new Border { Child = text, Background = Family.Brush(ColourRole.Clear) };
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

    /// <summary>
    /// [S-1323] The path, tail first as Explorer's address bar keeps it: the folders above in Body
    /// secondary, the shown one primary, 16 chevrons (tertiary) between them, 4 apart.
    /// </summary>
    private void UpdateCrumbs()
    {
        _crumbs.Children.Clear();

        if (_plan is not { Breadcrumb: true } || CurrentPath.Length == 0)
        {
            return;
        }

        // [S-2056] A preview's folder named as its breadcrumb should read, its crumbs still opening the real folders.
        IReadOnlyList<(string Label, string Path)> crumbs = _crumbAlias is { } alias && CurrentPath.StartsWith(alias.Real, StringComparison.OrdinalIgnoreCase)
            ? [.. FolderHistory.Crumbs(alias.Shown + CurrentPath[alias.Real.Length..]).Select(c => (c.Label,
                c.Path.StartsWith(alias.Shown, StringComparison.OrdinalIgnoreCase) ? alias.Real + c.Path[alias.Shown.Length..] : c.Path))]
            : FolderHistory.Crumbs(CurrentPath);

        // The tail is what matters; leading crumbs give way first, as Explorer's address bar does.
        var budget = _area.Width - (2 * FamilySpace.Inset);
        var shown = new List<(string Label, string Path)>();
        var step = FamilyShape.Icon + (2 * FamilySpace.S1);

        for (var i = crumbs.Count - 1; i >= 0; i--)
        {
            var cost = (HeroFit.Ems(crumbs[i].Label) * FamilyType.Body.FontSize) + step;

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
                _crumbs.Children.Add(Family.Glyph(Glyphs.ChevronRight, FamilyShape.Icon, ColourRole.TextTertiary));
            }

            var (label, path) = shown[i];
            var last = i == shown.Count - 1;
            var crumb = new Border
            {
                Child = Family.Text(FamilyType.Body, last ? ColourRole.TextPrimary : ColourRole.TextSecondary, label),
                CornerRadius = Family.Radius,
                VerticalAlignment = VerticalAlignment.Center,
            };

            ItemFill.Paint(crumb, ItemRole.Row);

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
                        _widgetHost.FileOps.Open(path);
                    }
                };
                _itemHovers.Add(Fluent.Hover(crumb));
            }

            ToolTipService.SetToolTip(crumb, path);
            _crumbs.Children.Add(crumb);
        }
    }

    /// <summary>[S-1329] Cut items faded as the desktop fades them, while the cut is still on the clipboard.</summary>
    private void RefreshDims()
    {
        var pending = _widgetHost.FileClipboard.CutStillPending();

        foreach (var visual in _visuals)
        {
            visual.Content.Opacity = pending && _widgetHost.FileClipboard.IsCut(visual.Entry.FullPath) ? SelectionLook.Faded : 1;
        }
    }

    // ---- the tile panel -------------------------------------------------------------------------

    /// <summary>
    /// [S-6713] [S-14816] The grid views' layout: each cell's box at its one size, touching, from the card's left edge and
    /// the items' top, as many columns as the plan says, every edge on a whole device pixel.
    /// </summary>
    private sealed partial class TilePanel : Panel
    {
        private CellGrid _grid;

        public TilePanel(CellGrid grid) => Set(grid);

        public void Set(CellGrid grid)
        {
            _grid = grid;
            InvalidateMeasure();
        }

        private int Columns => Math.Max(1, _grid.Columns);

        protected override Size MeasureOverride(Size availableSize)
        {
            var tiles = 0;
            var extra = 0.0;

            foreach (var child in Children)
            {
                if (child is FrameworkElement { Tag: int })
                {
                    child.Measure(new Size(_grid.Width, _grid.Height));
                    tiles++;
                }
                else
                {
                    // The "+N more" row sits under the tiles at full width.
                    child.Measure(new Size(_grid.Left(Columns), double.PositiveInfinity));
                    extra += child.DesiredSize.Height;
                }
            }

            var rows = (tiles + Columns - 1) / Columns;
            return new Size(_grid.Left(Columns), _grid.Top(rows) + extra);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var i = 0;
            var rows = 0;

            foreach (var child in Children)
            {
                if (child is FrameworkElement { Tag: int })
                {
                    var row = i / Columns;
                    var column = i % Columns;
                    var (x, y0) = (_grid.Left(column), _grid.Top(row));
                    child.Arrange(new Rect(x, y0, _grid.Left(column + 1) - x, _grid.Top(row + 1) - y0));
                    rows = row + 1;
                    i++;
                }
            }

            var y = _grid.Top(rows);

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
