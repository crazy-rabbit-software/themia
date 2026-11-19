using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Licensing;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;
using Windows.System;
using A = Themia.Sdk.Kit.ShellMetrics.AddPage;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>What the add page's tiles and keys do, given by the surface (or a render's stand-in).</summary>
/// <param name="Pick">[S-11702] A tile was picked: the page closes and the placement session starts.</param>
/// <param name="Close">[S-13617] Escape with nothing typed.</param>
/// <param name="OverLimit">[S-1065] Whether placing the tile would pass the free plan's widgets (its tile shows the Pro hint).</param>
internal sealed record AddPageCommands(Action<AddTile> Pick, Action Close, Func<AddTile, bool> OverLimit);

/// <summary>
/// [S-13610]..[S-13617] [S-13600]..[S-13604] The add page as drawn: on the left the rail (<see cref="AddRail"/>)
/// under the search box; beside it the chosen subject's content, scrolling (its name, then its sections: a
/// BodyStrong heading over a grid of tiles, a family a stack under its head, its first row and a "+N" tile that
/// opens it in place); beside that the preview pane (<see cref="AddPreview"/>) of the tile or family pointed at
/// or on the keyboard. Typing searches every subject, flat. Only the tiles in and near sight ask for their
/// pictures, so a subject or a search of hundreds of tiles costs what is seen. The page's model is Core's
/// (<see cref="AddPage"/>, <see cref="AddPageKeys"/>, <see cref="AddPageLayout"/>).
/// </summary>
internal sealed partial class AddPageView : Grid
{
    private const string Scope = "add-page";

    private readonly AddPage _page;
    private readonly AddPageCommands _commands;
    private readonly AddPictures? _pictures;

    private readonly SearchBox _search;
    private readonly AddRail _rail;
    private readonly Grid _railColumn = new();
    private readonly Grid _contentColumn = new();
    private readonly StackPanel _sections = new();
    private readonly ScrollViewer _body;
    private readonly StatusBlock _none = new() { Margin = default };
    private readonly AddPreview _preview = new();

    /// <summary>The runs shown now: a section's heading or a family's head, and its grid of tiles.</summary>
    private readonly List<Run> _runs = [];

    /// <summary>[S-13612] The families opened while the page is open (by key), whichever subject they are in.</summary>
    private readonly HashSet<string> _open = new(StringComparer.Ordinal);

    private AddFolderId _item = AddFolderId.Start;

    /// <summary>[S-13602] What the preview shows (a tile's key, a family's), or null.</summary>
    private string? _previewed;

    /// <summary>How many tiles stand across the list, as the page's width last shared it out.</summary>
    private int _columns = 3;

    private bool _compact;

    /// <summary>The tiles shown changed: those in sight ask for their pictures once laid out.</summary>
    private bool _wantPending;

    public AddPageView(AddPage page, AddPageCommands commands, AddPictures? pictures)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _pictures = pictures;

        Padding = new Thickness(ShellMetrics.Flyout.Gutter, ShellMetrics.Flyout.Gutter, ShellMetrics.Flyout.Gutter, 0);
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(A.Rail) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });

        // [S-13606] The one search box, as every search of Themia's; over the rail.
        _search = new SearchBox(Str.Shell_Picker_Search.Text()) { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, P.HeadingGap) };
        _search.Changed += _ => Draw();

        // [S-13610] The rail: Start, the subjects, More; scrolling on its own where it is taller than the page.
        _rail = new AddRail(page.Rail, ShowItem) { Padding = new Thickness(0, 0, 0, P.Bottom) };
        var railScroll = new ScrollViewer
        {
            Content = _rail,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsTabStop = false,
        };
        _railColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _railColumn.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        SetRow(railScroll, 1);
        _railColumn.Children.Add(_search);
        _railColumn.Children.Add(railScroll);
        Children.Add(_railColumn);

        // The content: 16 off the rail, a family head's fill reaching 8 of that.
        _sections.Padding = new Thickness(A.FamilyHeadInset, 0, 0, P.Bottom);
        _body = new ScrollViewer
        {
            Content = _sections,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsTabStop = false,
        };
        _body.ViewChanged += (_, _) => WantInSight();
        _body.LayoutUpdated += (_, _) =>
        {
            if (_wantPending)
            {
                WantInSight();
            }
        };
        _contentColumn.Margin = new Thickness(A.RailGap - A.FamilyHeadInset, 0, 0, 0);
        _contentColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _contentColumn.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        SetRow(_body, 1);
        _contentColumn.Children.Add(_body);
        SetColumn(_contentColumn, 1);
        Children.Add(_contentColumn);

        // [S-13603] The preview pane beside the content, at the page's top.
        _preview.Margin = new Thickness(A.PreviewGap, 0, 0, P.Bottom);
        SetColumn(_preview, 2);
        Children.Add(_preview);

        SizeChanged += (_, e) => Split(e.NewSize.Width);

        PreviewKeyDown += OnKey;
        CharacterReceived += OnCharacter;
        Draw();
    }

    /// <summary>The rail item shown (with nothing typed).</summary>
    public AddFolderId Item => _item;

    /// <summary>[S-13617] The caret in the search box: as the page opens, typing searches at once.</summary>
    public void FocusSearch() => _search.FocusEnd();

    /// <summary>
    /// Type into the search box (a render). Drawn at once: a box not yet loaded raises no TextChanged, and
    /// a render typing into a page that has just opened drew the subject.
    /// </summary>
    internal void SetQuery(string text)
    {
        _search.Text = text;
        Draw();
    }

    /// <summary>The pictures the page asked for are drawn (a render waits for it).</summary>
    internal Task PicturesDrawn => _pictures?.Idle ?? Task.CompletedTask;

    /// <summary>[S-13610] Show a rail item; a search typed is cleared, as choosing a subject is leaving it.</summary>
    internal void ShowItem(AddFolderId item)
    {
        _item = item;
        if (_search.Text.Length > 0)
        {
            _search.Text = string.Empty; // draws
        }
        else
        {
            Draw();
        }
    }

    /// <summary>[S-13612] Open the family of <paramref name="key"/> (<c>family:Id</c>) in place (a render's <c>--open</c>); false when none shown has it.</summary>
    internal bool OpenFamily(string key)
    {
        if (_runs.FirstOrDefault(r => r.Family?.Key == key) is not { } run)
        {
            return false;
        }

        if (!_open.Contains(key))
        {
            Toggle(run);
        }

        return true;
    }

    /// <summary>How tall the page wants to be at <paramref name="width"/>, before the room cuts it: its rail, its content or its preview, whichever is tallest.</summary>
    public double ContentHeight(double width)
    {
        var (rail, list, preview) = Split(width);
        _railColumn.Measure(new Size(rail, double.PositiveInfinity));
        _contentColumn.Measure(new Size(list + _contentColumn.Margin.Left + A.FamilyHeadInset, double.PositiveInfinity));
        var tall = Math.Max(_railColumn.DesiredSize.Height, _contentColumn.DesiredSize.Height);
        if (preview > 0)
        {
            _preview.Measure(new Size(preview, double.PositiveInfinity));
            tall = Math.Max(tall, _preview.DesiredSize.Height + _preview.Margin.Bottom);
        }

        return Padding.Top + tall;
    }

    /// <summary>[S-13616] [S-13603] Share the page's <paramref name="width"/> between the rail, the list and the preview pane.</summary>
    private (double Rail, double List, double Preview) Split(double width)
    {
        var split = AddPageLayout.Split(width - Padding.Left - Padding.Right);
        ColumnDefinitions[0].Width = new GridLength(split.Rail);
        ColumnDefinitions[2].Width = new GridLength(split.Preview > 0 ? split.Preview + A.PreviewGap : 0);
        _preview.Visibility = split.Preview > 0 ? Visibility.Visible : Visibility.Collapsed;

        // [S-13616] Compact: the rail its glyphs alone, the search box over the content instead.
        var compact = split.Rail < A.Rail;
        if (compact != _compact)
        {
            _compact = compact;
            _rail.Compact = compact;
            (compact ? _railColumn : _contentColumn).Children.Remove(_search);
            (compact ? _contentColumn : _railColumn).Children.Add(_search);
            _search.Margin = compact ? new Thickness(A.FamilyHeadInset, 0, 0, P.HeadingGap) : new Thickness(0, 0, 0, P.HeadingGap);
        }

        _search.Width = Math.Min(A.SearchWidth, compact ? split.List : split.Rail);

        // [S-13612] A collapsed stack shows one row: a new count across lays it again.
        var columns = AddPageLayout.Columns(split.List);
        if (columns != _columns)
        {
            _columns = columns;
            foreach (var run in _runs.Where(r => r.Family is not null))
            {
                Stack(run);
            }
        }

        return split;
    }

    // ---- [S-13602] [S-13614] the preview ---------------------------------------------------------

    /// <summary>
    /// [S-13602] [S-13614] Preview the tile or family of <paramref name="key"/> as if pointed at (a render's hover
    /// state); false when nothing shown has that key.
    /// </summary>
    internal bool PreviewKey(string key)
    {
        if (_runs.FirstOrDefault(r => r.Family?.Key == key) is { Family: { } family } run)
        {
            Preview(family);
            run.Head?.Pointed();
            return true;
        }

        if (_runs.SelectMany(r => r.Grid.Children.OfType<Tile>()).FirstOrDefault(t => t.Key == key) is not { } view)
        {
            return false;
        }

        Preview(view.Thing);
        view.Pointed();
        return true;
    }

    /// <summary>
    /// [S-13602] Show <paramref name="tile"/> in the preview pane: its picture at once where it is kept, else
    /// its glyph until the picture, drawn before every other asked for ([S-13604]), comes.
    /// </summary>
    private void Preview(AddTile tile)
    {
        if (string.Equals(_previewed, tile.Key, StringComparison.Ordinal))
        {
            return;
        }

        _previewed = tile.Key;
        _preview.Show(tile, _commands.OverLimit(tile));
        Picture(tile);
    }

    /// <summary>[S-13614] Show a family in the preview: its first widget's picture, its name, its count, its widgets.</summary>
    private void Preview(AddFamily family)
    {
        if (string.Equals(_previewed, family.Key, StringComparison.Ordinal))
        {
            return;
        }

        _previewed = family.Key;
        _preview.Show(family);
        Picture(family.Picture);
    }

    private void Picture(AddTile tile)
    {
        if (AddPictures.Cached(tile) is { } picture)
        {
            _preview.ShowPicture(picture);
        }
        else
        {
            _pictures?.Prefer(tile, drawn => Drew(tile, drawn));
        }
    }

    /// <summary>A picture came: the preview shows it while it still wants it.</summary>
    private void Drew(AddTile tile, AddPicture picture)
    {
        if (string.Equals(_preview.PictureKey, tile.Key, StringComparison.Ordinal))
        {
            _preview.ShowPicture(picture);
        }
    }

    // ---- [S-13611] [S-13615] the content ---------------------------------------------------------

    /// <summary>[S-13611] [S-13615] Draw the rail item's sections, or the search's, or the status row saying nothing matched.</summary>
    private void Draw()
    {
        var query = _search.Text;
        var searching = AddPage.Searching(query);
        IReadOnlyList<AddSection> sections = searching ? _page.Find(query) : _page.Sections(_item);
        _rail.Choose(searching ? null : _item);

        _sections.Children.Clear();
        _runs.Clear();
        _pictures?.Forget();
        _previewed = null;

        // [S-13611] The subject's name over its content; a search has none (its sections say what they are).
        if (!searching && _page.Rail.FirstOrDefault(r => r.Id == _item) is { } item)
        {
            var title = Fluent.Text(FamilyType.Title);
            title.Text = item.Title.Text();
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.TextWrapping = TextWrapping.NoWrap;
            title.Margin = new Thickness(0, 0, 0, Fluent.Space3);
            AutomationProperties.SetHeadingLevel(title, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
            _sections.Children.Add(title);
        }

        if (sections.Count == 0)
        {
            _preview.Clear();
            _none.Show(StatusKind.Empty, Glyphs.Search, searching ? Str.AddMenu_NoResults.Format(("query", query.Trim())) : string.Empty);
            _sections.Children.Add(_none);
        }

        var first = true;
        foreach (var section in sections)
        {
            var top = first ? 0 : P.SectionTop - A.TileGap;
            first = false;
            var grid = new TileGrid { Margin = new Thickness(0, 0, 0, A.TileGap) };
            Run run;
            if (section.Family is { } family)
            {
                var head = new FamilyHead(family, () => Preview(family)) { Margin = new Thickness(-A.FamilyHeadInset, top - A.FamilyHeadRise, 0, 0) };
                run = new Run(section, head, grid);
                head.Click += (_, _) => Toggle(run);
                _sections.Children.Add(head);
            }
            else
            {
                var heading = Fluent.Text(FamilyType.BodyStrong);
                heading.Text = section.Heading.Text();
                heading.Margin = new Thickness(0, top, 0, P.HeadingGap);
                _sections.Children.Add(heading);
                run = new Run(section, null, grid);
            }

            AutomationProperties.SetName(grid, section.Heading.Text());
            _sections.Children.Add(grid);
            _runs.Add(run);
            Stack(run);
        }

        // [S-13602] Until a tile is pointed at, the preview shows the first: the one Enter in the box picks.
        if (FirstTile() is { } shown)
        {
            Preview(shown);
        }

        _body.ChangeView(null, 0, null, disableAnimation: true);
        _wantPending = true;
        Drawn?.Invoke();
    }

    /// <summary>The first tile shown (every run shows its first), or null.</summary>
    private AddTile? FirstTile() => _runs.Count > 0 ? _runs[0].Section.Tiles[0] : null;

    /// <summary>
    /// [S-13612] Lay a run's tiles: a section's all; a family's as its stack (<see cref="AddPageLayout.Stack"/>):
    /// open, all; else its first row, the "+N" tile last.
    /// </summary>
    private void Stack(Run run)
    {
        run.Grid.Children.Clear();
        var tiles = run.Section.Tiles;
        var stack = run.Family is { } family
            ? AddPageLayout.Stack(tiles.Count, _columns, _open.Contains(family.Key))
            : new AddStack(tiles.Count, 0);
        for (var i = 0; i < stack.Shown; i++)
        {
            var tile = tiles[i];
            run.Grid.Children.Add(new Tile(tile, _commands.OverLimit(tile), () => Pick(tile), () => Preview(tile)));
        }

        if (stack.Hidden > 0 && run.Family is { } stacked)
        {
            var more = new MoreTile(stacked, stack.Hidden, () => Preview(stacked));
            more.Click += (_, _) => Toggle(run);
            run.Grid.Children.Add(more);
        }

        if (run.Head is { } head && run.Family is { } shown)
        {
            // A family that fits one row has nothing to open: its head says its name and count alone.
            head.Show(_open.Contains(shown.Key), AddPageLayout.Stack(tiles.Count, _columns, open: false).Hidden > 0);
        }
        _wantPending = true;
    }

    /// <summary>[S-13612] Open a family in place, or close it again; the keys stay where they were, or on the first tile opened.</summary>
    private void Toggle(Run run)
    {
        if (run.Family is not { } family)
        {
            return;
        }

        var fromMore = run.Grid.Children.OfType<MoreTile>().Any(m => m.HasFocus);
        var shown = run.Grid.Children.Count - (fromMore ? 1 : 0);
        if (!_open.Add(family.Key))
        {
            _open.Remove(family.Key);
        }

        Stack(run);
        if (fromMore && shown < run.Grid.Children.Count && run.Grid.Children[shown] is IStop stop && !stop.FocusStop())
        {
            Log.Debug(Scope, $"the first tile opened of {family.Key} took no focus");
        }

        Drawn?.Invoke();
    }

    /// <summary>
    /// [S-13613] Ask for the pictures of the tiles in sight and a screen below it ([S-13604]): a subject or a search
    /// of hundreds of tiles draws only what is looked at. Once laid out (until then it waits).
    /// </summary>
    private void WantInSight()
    {
        if (_pictures is null || _runs.Count == 0)
        {
            _wantPending = false;
            return;
        }

        var sight = _body.ViewportHeight > 0 ? _body.ViewportHeight : _body.ActualHeight;
        if (sight <= 0)
        {
            return;
        }

        _wantPending = false;
        var top = _body.VerticalOffset;
        var bottom = top + (2 * sight);
        foreach (var run in _runs)
        {
            var at = run.Grid.ActualOffset.Y;
            if (at > bottom)
            {
                break;
            }

            if (at + run.Grid.ActualHeight < top)
            {
                continue;
            }

            foreach (var tile in run.Grid.Children.OfType<Tile>())
            {
                var y = at + tile.ActualOffset.Y;
                if (y + tile.ActualHeight >= top && y <= bottom && AddPictures.Cached(tile.Thing) is null)
                {
                    var thing = tile.Thing;
                    _pictures.Want(thing, drawn => Drew(thing, drawn));
                }
            }
        }
    }

    /// <summary>The page drew other sections (a subject, a search, a family opened): its flyout may want more room.</summary>
    public event Action? Drawn;

    private void Pick(AddTile tile)
    {
        try
        {
            _commands.Pick(tile);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"picking {tile.Key} failed", ex);
        }
    }

    // ---- [S-13617] keys -----------------------------------------------------------------------

    private void OnKey(object sender, KeyRoutedEventArgs e)
    {
        try
        {
            e.Handled = Key(e.Key);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the add page's key {e.Key} failed", ex);
        }
    }

    private bool Key(VirtualKey key)
    {
        var ids = _rail.Ids;
        if (key == VirtualKey.Tab && Held(VirtualKey.Control))
        {
            var at = Math.Max(0, ids.ToList().IndexOf(_item));
            ShowItem(ids[AddPageKeys.Tab(ids.Count, at, back: Held(VirtualKey.Shift))]);
            return true;
        }

        // [S-13607] The one search key rule: Escape empties the box (from anywhere on the page), an empty box's
        // Escape closes the page; Enter in the box adds the best match (or the subject's first tile).
        var inBox = _search.HasKeys;
        var typed = _search.Text.Length > 0;
        var best = FirstTile();
        var search = key switch
        {
            VirtualKey.Escape => inBox ? SearchKeys.InBox(WidgetKey.Escape, typed, best is null ? 0 : 1) : SearchKeys.OnResult(WidgetKey.Escape, 1, typed),
            VirtualKey.Enter when inBox => SearchKeys.InBox(WidgetKey.Enter, typed, best is null ? 0 : 1),
            _ => SearchMove.None,
        };
        switch (search)
        {
            case SearchMove.Clear:
                _search.Clear();
                return true;
            case SearchMove.LetGo:
                _commands.Close();
                return true;
            case SearchMove.PickBest when best is not null:
                Pick(best);
                return true;
        }

        if (key == VirtualKey.Enter && inBox)
        {
            return true;
        }

        if (Map(key) is not { } move)
        {
            return false;
        }

        // [S-13617] On the rail: the arrows walk it, showing what they reach; Right or Enter into the content.
        if (_rail.Focused is { } on)
        {
            var step = AddPageKeys.Rail(ids.Count, on, move);
            switch (step.Move)
            {
                case AddRailMove.Item:
                    if (ids[step.Item] != _item || _search.Text.Length > 0)
                    {
                        ShowItem(ids[step.Item]);
                    }

                    _rail.FocusItem(step.Item);
                    return true;
                case AddRailMove.Box:
                    FocusSearch();
                    return true;
                case AddRailMove.Content:
                    if (ids[step.Item] != _item || _search.Text.Length > 0)
                    {
                        ShowItem(ids[step.Item]);
                    }

                    return FocusStop(new AddCursor(0, 0));
                default:
                    return false;
            }
        }

        if (move == WidgetKey.Enter || (inBox && move != WidgetKey.Down))
        {
            return false;
        }

        var cursor = inBox ? null : Focused();
        if (!inBox && cursor is null)
        {
            return false;
        }

        var stops = Stops();
        var moved = AddPageKeys.Move([.. stops.Select(s => s.Count)], _columns, cursor, move);
        if (!moved.Handled)
        {
            return false;
        }

        if (moved.ToRail)
        {
            _rail.FocusItem(Math.Max(0, ids.ToList().IndexOf(_item)));
            return true;
        }

        if (moved.Cursor is { } to)
        {
            FocusStop(to);
            return true;
        }

        FocusSearch();
        return true;
    }

    /// <summary>The content's stops as the keys walk them: per run, its head alone (a run of one), then its tiles.</summary>
    private List<IReadOnlyList<IStop>> Stops()
    {
        var stops = new List<IReadOnlyList<IStop>>();
        foreach (var run in _runs)
        {
            if (run.Head is { } head)
            {
                stops.Add([head]);
            }

            stops.Add([.. run.Grid.Children.OfType<IStop>()]);
        }

        return stops;
    }

    private bool FocusStop(AddCursor at)
    {
        var stops = Stops();
        if (at.Section < stops.Count && at.Tile < stops[at.Section].Count && stops[at.Section][at.Tile].FocusStop())
        {
            return true;
        }

        Log.Debug(Scope, $"stop {at.Tile} of run {at.Section} took no focus");
        return false;
    }

    /// <summary>The stop the keyboard is on, as the keys walk it.</summary>
    private AddCursor? Focused()
    {
        var stops = Stops();
        for (var s = 0; s < stops.Count; s++)
        {
            for (var t = 0; t < stops[s].Count; t++)
            {
                if (stops[s][t].HasFocus)
                {
                    return new AddCursor(s, t);
                }
            }
        }

        return null;
    }

    /// <summary>[S-13617] Typing anywhere on the page goes to the search box.</summary>
    private void OnCharacter(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        if (_search.HasKeys || char.IsControl(e.Character) || e.Character == ' ')
        {
            return;
        }

        _search.Type(e.Character);
        e.Handled = true;
    }

    /// <summary>The arrows, mirrored right to left; Home, End and Enter.</summary>
    private WidgetKey? Map(VirtualKey key)
    {
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        return key switch
        {
            VirtualKey.Up => WidgetKey.Up,
            VirtualKey.Down => WidgetKey.Down,
            VirtualKey.Left => rtl ? WidgetKey.Right : WidgetKey.Left,
            VirtualKey.Right => rtl ? WidgetKey.Left : WidgetKey.Right,
            VirtualKey.Home => WidgetKey.Home,
            VirtualKey.End => WidgetKey.End,
            VirtualKey.Enter => WidgetKey.Enter,
            _ => null,
        };
    }

    private static bool Held(VirtualKey key)
        => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    // ---- the parts ------------------------------------------------------------------------------

    /// <summary>A run of the content: a section's heading (or a family's head) and its grid of tiles.</summary>
    private sealed record Run(AddSection Section, FamilyHead? Head, TileGrid Grid)
    {
        public AddFamily? Family => Section.Family;
    }

    /// <summary>What the keys stop on: a tile, a "+N" tile, a family's head.</summary>
    private interface IStop
    {
        bool HasFocus { get; }

        bool FocusStop();
    }

    /// <summary>
    /// [S-13601] Tiles of at least 200 across the list, the row filling it evenly, 4 apart either way;
    /// a row as tall as its tallest tile.
    /// </summary>
    private sealed partial class TileGrid : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsFinite(availableSize.Width) ? availableSize.Width : A.TileLeast;
            var columns = AddPageLayout.Columns(width);
            var tile = AddPageLayout.TileWidth(width, columns);
            double height = 0, row = 0;
            for (var i = 0; i < Children.Count; i++)
            {
                Children[i].Measure(new Size(tile, double.PositiveInfinity));
                row = Math.Max(row, Children[i].DesiredSize.Height);
                if ((i + 1) % columns == 0 || i == Children.Count - 1)
                {
                    height += row + (height > 0 ? A.TileGap : 0);
                    row = 0;
                }
            }

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var columns = AddPageLayout.Columns(finalSize.Width);
            var tile = AddPageLayout.TileWidth(finalSize.Width, columns);
            double y = 0;
            for (var start = 0; start < Children.Count; start += columns)
            {
                var count = Math.Min(columns, Children.Count - start);
                var row = Enumerable.Range(start, count).Max(i => Children[i].DesiredSize.Height);
                for (var c = 0; c < count; c++)
                {
                    Children[start + c].Arrange(new Rect(c * (tile + A.TileGap), y, tile, row));
                }

                y += row + A.TileGap;
            }

            return finalSize;
        }
    }

    /// <summary>
    /// [S-13612] A family's head over its stack: a subtle row as wide as the list (its fill reaching 8 past the
    /// tiles' start, its words standing where a heading's do): the family's name in BodyStrong, its count in
    /// Caption and secondary and a chevron at its end (right while closed, down while open). Pointed at or on the
    /// keyboard it previews the family ([S-13614]); a click opens it in place or closes it again.
    /// </summary>
    private sealed partial class FamilyHead : Button, IStop
    {
        private readonly FontIcon _chevron;
        private readonly string _said;

        public FamilyHead(AddFamily family, Action point)
        {
            var name = Fluent.Text(FamilyType.BodyStrong);
            name.Text = family.Title.Text();
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.TextWrapping = TextWrapping.NoWrap;
            name.VerticalAlignment = VerticalAlignment.Center;

            var count = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
            count.Text = LocaleFormat.Integer(family.Tiles.Count);
            count.VerticalAlignment = VerticalAlignment.Center;

            _chevron = Fluent.Icon(Glyphs.ChevronRight, "TextFillColorSecondaryBrush", ShellMetrics.Chevron);
            _chevron.VerticalAlignment = VerticalAlignment.Center;
            _chevron.MirroredWhenRightToLeft = true;

            var row = new Grid
            {
                ColumnSpacing = P.HeadingGap,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Children = { name, count, _chevron },
            };
            Grid.SetColumn(count, 1);
            Grid.SetColumn(_chevron, 2);

            Content = row;
            Height = A.FamilyHead;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Center;
            Padding = new Thickness(A.FamilyHeadInset, 0, A.TilePad, 0);
            CornerRadius = new CornerRadius(P.CardRadius);
            PageTabButton.Subtle(this);
            Tag = family.Key;
            _said = $"{name.Text}, {Str.Groups_Add_Count.Plural(family.Tiles.Count)}";
            PointerEntered += (_, _) => point();
            GotFocus += (_, _) => point();
        }

        public bool HasFocus => FocusState != FocusState.Unfocused;

        public bool FocusStop()
        {
            StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            return Focus(FocusState.Keyboard);
        }

        /// <summary>Open or closed; <paramref name="opens"/>: whether a click does anything (a family that fits one row has nothing to open).</summary>
        public void Show(bool open, bool opens)
        {
            _chevron.Glyph = open ? Glyphs.ChevronDown : Glyphs.ChevronRight;
            _chevron.Visibility = opens ? Visibility.Visible : Visibility.Collapsed;
            var action = open ? Str.AddMenu_ShowFewer.Text() : Str.AddMenu_ShowAll.Text();
            AutomationProperties.SetName(this, opens ? $"{_said}. {action}" : _said);
            ToolTipService.SetToolTip(this, opens ? action : null);
        }

        /// <summary>A render's pointer resting on the head: the subtle fill it wears under the pointer.</summary>
        public void Pointed() => Background = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
    }

    /// <summary>
    /// [S-13601] A tile, Karpo's (Â§578): one compact button on a page card (its fill and rim, radius 4; the
    /// subtle fills under the pointer and pressed), 52 tall: the thing's icon 12 in, then its name in Body
    /// over one Caption line, each cut short; over the free plan's widgets, the Pro hint at its end. Pointed
    /// at or reached by the keys, it is the preview's ([S-13602]).
    /// </summary>
    private sealed partial class Tile : Grid, IStop
    {
        private readonly Button _button;

        public Tile(AddTile tile, bool overLimit, Action pick, Action point)
        {
            Thing = tile;
            Key = tile.Key;
            Height = A.Tile;
            Card(this);

            var words = AddTileWords.Of(tile);
            var glyph = Fluent.Icon(words.Glyph, "TextFillColorPrimaryBrush");
            glyph.VerticalAlignment = VerticalAlignment.Center;

            var title = Fluent.Text(FamilyType.Body);
            title.Text = words.Name;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.TextWrapping = TextWrapping.NoWrap;
            var line = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
            line.Text = words.Caption;
            line.TextTrimming = TextTrimming.CharacterEllipsis;
            line.TextWrapping = TextWrapping.NoWrap;
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, line } };

            var row = new Grid
            {
                ColumnSpacing = A.TilePad,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Children = { glyph, text },
            };
            SetColumn(text, 1);

            var limit = Str.AddMenu_OverLimit.Format(("count", LocaleFormat.Integer(LicenseRules.FreeWidgetLimit)));
            if (overLimit)
            {
                var pro = Fluent.Text(FamilyType.Caption);
                pro.Foreground = ShellBrush.Of(ShellRole.AccentText);
                pro.Text = "Pro";
                pro.VerticalAlignment = VerticalAlignment.Center;
                SetColumn(pro, 2);
                row.Children.Add(pro);
            }

            _button = Press(row, tile.Key);
            _button.Click += (_, _) => pick();

            // [S-13602] Pointing at a tile, or the keys reaching it, shows it in the preview.
            _button.PointerEntered += (_, _) => point();
            _button.GotFocus += (_, _) => point();

            var said = overLimit ? $"{words.Name}, {words.Caption}. {limit}" : $"{words.Name}, {words.Caption}";
            AutomationProperties.SetName(_button, said);
            ToolTipService.SetToolTip(_button, overLimit ? limit : words.Caption);
            Children.Add(_button);
        }

        /// <summary>What the tile places.</summary>
        public AddTile Thing { get; }

        public string Key { get; }

        public bool HasFocus => _button.FocusState != FocusState.Unfocused;

        /// <summary>A render's pointer resting on the tile: the subtle fill it wears under the pointer.</summary>
        public void Pointed() => _button.Background = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);

        public bool FocusStop()
        {
            _button.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            return _button.Focus(FocusState.Keyboard);
        }

        /// <summary>A tile's card: Windows' card fill and rim, radius 4.</summary>
        internal static void Card(Grid card)
        {
            card.Background = ShellBrush.Of(ShellRole.CardFill);
            card.BorderBrush = ShellBrush.Of(ShellRole.CardStroke);
            card.BorderThickness = new Thickness(FamilyShape.Hairline);
            card.CornerRadius = new CornerRadius(P.CardRadius);
        }

        /// <summary>A tile's button over its card: the whole card, the subtle fills under the pointer and pressed.</summary>
        internal static Button Press(UIElement content, string key)
        {
            var button = new Button
            {
                Content = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(A.TilePad, 0, A.TilePad, 0),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(P.CardRadius),
                Tag = key,
            };
            PageTabButton.Subtle(button);
            return button;
        }
    }

    /// <summary>
    /// [S-13612] The "+N" tile closing a family's first row: a tile's card and size, "+N" in BodyStrong over
    /// "Show all" in Caption; a click opens the family in place; pointed at or on the keyboard it previews the
    /// family ([S-13614]).
    /// </summary>
    private sealed partial class MoreTile : Grid, IStop
    {
        private readonly Button _button;

        public MoreTile(AddFamily family, int hidden, Action point)
        {
            Height = A.Tile;
            Tile.Card(this);

            var plus = Fluent.Text(FamilyType.BodyStrong);
            plus.Text = Str.AddMenu_MoreCount.Format(("count", LocaleFormat.Integer(hidden)));
            var all = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
            all.Text = Str.AddMenu_ShowAll.Text();
            all.TextTrimming = TextTrimming.CharacterEllipsis;
            all.TextWrapping = TextWrapping.NoWrap;
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { plus, all } };

            _button = Tile.Press(text, family.Key + "/more");
            _button.Click += (_, _) => Click?.Invoke(this, EventArgs.Empty);
            _button.PointerEntered += (_, _) => point();
            _button.GotFocus += (_, _) => point();
            AutomationProperties.SetName(_button, $"{family.Title.Text()}: {Str.AddMenu_ShowAll.Text()}, {Str.Groups_Add_Count.Plural(family.Tiles.Count)}");
            Children.Add(_button);
        }

        public event EventHandler? Click;

        public bool HasFocus => _button.FocusState != FocusState.Unfocused;

        public bool FocusStop()
        {
            _button.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            return _button.Focus(FocusState.Keyboard);
        }
    }
}
