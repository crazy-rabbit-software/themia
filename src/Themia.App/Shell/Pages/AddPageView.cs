using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
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
/// <param name="Close">[S-11708] Escape with nothing typed.</param>
/// <param name="OverLimit">[S-1065] Whether placing the tile would pass the free plan's widgets (its tile shows the Pro hint).</param>
internal sealed record AddPageCommands(Action<AddTile> Pick, Action Close, Func<AddTile, bool> OverLimit);

/// <summary>
/// [S-11700]..[S-11708] [S-13600]..[S-13604] The add page as drawn, after Karpo's (karpo-app, docs/widgets.md
/// §593, §578, §592): a top row of the search box and the subjects' tab strip (40 tall, the chosen tab's
/// words strong over the accent's pill), then the chosen tab's sections, each a BodyStrong heading over its
/// tiles (as many of at least 200 as fit, the row filling the list, 4 apart), and beside them the preview
/// pane (<see cref="AddPreview"/>). A tile is one compact button on a page card: the thing's icon, its name
/// and one caption line; the picture of the thing itself is the preview's, of the tile pointed at or on
/// the keyboard. Typing searches every tab; nothing matching says so in the family's status row.
/// The page's model is Core's (<see cref="AddPage"/>, <see cref="AddPageKeys"/>, <see cref="AddPageLayout"/>).
/// </summary>
internal sealed partial class AddPageView : Grid
{
    private const string Scope = "add-page";

    private readonly AddPage _page;
    private readonly AddPageCommands _commands;
    private readonly AddPictures? _pictures;

    private readonly SearchBox _search;
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = P.TabGap };
    private readonly ScrollViewer _tabScroll;
    private readonly StackPanel _sections = new();
    private readonly ScrollViewer _body;
    private readonly StatusBlock _none = new();
    private readonly Grid _main = new();
    private readonly AddPreview _preview = new();

    /// <summary>The sections shown now, each its grid of tiles.</summary>
    private readonly List<(AddSection Section, TileGrid Grid)> _shown = [];

    private AddFolderId _tab = AddFolderId.Start;

    /// <summary>[S-13602] The tile the preview shows, or null.</summary>
    private AddTile? _previewed;

    public AddPageView(AddPage page, AddPageCommands commands, AddPictures? pictures)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _pictures = pictures;

        Padding = new Thickness(ShellMetrics.Flyout.Gutter, ShellMetrics.Flyout.Gutter, ShellMetrics.Flyout.Gutter, 0);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // [S-13606] The one search box, as every search of Themia's.
        _search = new SearchBox(Str.Shell_Picker_Search.Text()) { Width = A.SearchWidth };
        _search.Changed += _ => Draw();

        _tabScroll = new ScrollViewer
        {
            Content = _tabs,
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsTabStop = false,
            Margin = new Thickness(A.TopGap, 0, 0, 0),
        };
        AutomationProperties.SetName(_tabs, Str.AddMenu_Tabs.Text());

        var top = new Grid
        {
            Height = P.Tabs,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Children = { _search, _tabScroll },
        };
        SetColumn(_tabScroll, 1);
        Children.Add(top);

        _body = new ScrollViewer
        {
            Content = _sections,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsTabStop = false,
            Margin = new Thickness(0, P.HeadingGap, 0, 0),
        };
        _sections.Padding = new Thickness(0, 0, 0, P.Bottom);

        // [S-13603] The tiles' list and, beside it, the preview pane: as the page's width shares them out.
        _main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        _preview.Margin = new Thickness(A.PreviewGap, P.HeadingGap, 0, P.Bottom);
        SetColumn(_preview, 1);
        _main.Children.Add(_body);
        _main.Children.Add(_preview);
        SetRow(_main, 1);
        Children.Add(_main);
        SizeChanged += (_, e) => Split(e.NewSize.Width);

        foreach (var tab in page.Tabs)
        {
            _tabs.Children.Add(new PageTabButton(tab.Id.ToString(), tab.Title.Text(), () => ShowTab(tab.Id)));
        }

        PreviewKeyDown += OnKey;
        CharacterReceived += OnCharacter;
        Draw();
    }

    /// <summary>The tab shown (with nothing typed).</summary>
    public AddFolderId Tab => _tab;

    /// <summary>[S-11708] The caret in the search box: as the page opens, typing searches at once.</summary>
    public void FocusSearch() => _search.FocusEnd();

    /// <summary>
    /// Type into the search box (a render). Drawn at once: a box not yet loaded raises no TextChanged, and
    /// a render typing into a page that has just opened drew the tab.
    /// </summary>
    internal void SetQuery(string text)
    {
        _search.Text = text;
        Draw();
    }

    /// <summary>The pictures the page asked for are drawn (a render waits for it).</summary>
    internal Task PicturesDrawn => _pictures?.Idle ?? Task.CompletedTask;

    /// <summary>[S-11700] Show a tab; a search typed is cleared, as choosing a subject is leaving it.</summary>
    internal void ShowTab(AddFolderId tab)
    {
        _tab = tab;
        if (_search.Text.Length > 0)
        {
            _search.Text = string.Empty; // draws
        }
        else
        {
            Draw();
        }

        if (_tabs.Children.OfType<PageTabButton>().FirstOrDefault(t => t.Key == tab.ToString()) is { } chosen)
        {
            chosen.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
        }
    }

    /// <summary>How tall the page wants to be at <paramref name="width"/>, before the room cuts it: its tiles, or its preview where that is taller.</summary>
    public double ContentHeight(double width)
    {
        var (list, preview) = Split(width);
        _sections.Measure(new Size(list, double.PositiveInfinity));
        var tall = _body.Margin.Top + _sections.DesiredSize.Height;
        if (preview > 0)
        {
            _preview.Measure(new Size(preview, double.PositiveInfinity));
            tall = Math.Max(tall, _preview.Margin.Top + _preview.DesiredSize.Height + _preview.Margin.Bottom);
        }

        return Padding.Top + P.Tabs + tall;
    }

    /// <summary>[S-13603] Share the page's <paramref name="width"/> between the list and the preview pane.</summary>
    private (double List, double Preview) Split(double width)
    {
        var split = AddPageLayout.Split(width - Padding.Left - Padding.Right);
        _main.ColumnDefinitions[1].Width = new GridLength(split.Preview > 0 ? split.Preview + A.PreviewGap : 0);
        _preview.Visibility = split.Preview > 0 ? Visibility.Visible : Visibility.Collapsed;
        return split;
    }

    /// <summary>
    /// [S-13602] Preview the tile of <paramref name="key"/> as if pointed at (a render's hover state); false
    /// when no tile shown has that key.
    /// </summary>
    internal bool PreviewKey(string key)
    {
        if (_shown.SelectMany(s => s.Section.Tiles).FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.Ordinal)) is not { } tile)
        {
            return false;
        }

        Preview(tile);
        if (_shown.SelectMany(s => s.Grid.Children.OfType<Tile>()).FirstOrDefault(t => t.Key == key) is { } view)
        {
            view.Pointed();
        }

        return true;
    }

    /// <summary>
    /// [S-13602] Show <paramref name="tile"/> in the preview pane: its picture at once where it is kept, else
    /// its glyph until the picture, drawn before every other asked for ([S-13604]), comes.
    /// </summary>
    private void Preview(AddTile tile)
    {
        if (string.Equals(_previewed?.Key, tile.Key, StringComparison.Ordinal))
        {
            return;
        }

        _previewed = tile;
        _preview.Show(tile, _commands.OverLimit(tile));
        if (AddPictures.Cached(tile) is { } picture)
        {
            _preview.ShowPicture(picture);
        }
        else
        {
            _pictures?.Prefer(tile, drawn => Drew(tile, drawn));
        }
    }

    /// <summary>A picture came: the preview shows it while it still shows its tile.</summary>
    private void Drew(AddTile tile, AddPicture picture)
    {
        if (string.Equals(_preview.Key, tile.Key, StringComparison.Ordinal))
        {
            _preview.ShowPicture(picture);
        }
    }

    /// <summary>[S-11701] [S-11706] Draw the tab's sections, or the search's, or the status row saying nothing matched.</summary>
    private void Draw()
    {
        var query = _search.Text;
        var searching = AddPage.Searching(query);
        IReadOnlyList<AddSection> sections = searching ? _page.Find(query) : _page.Sections(_tab);

        foreach (var tab in _tabs.Children.OfType<PageTabButton>())
        {
            tab.Chosen = !searching && tab.Key == _tab.ToString();
        }

        _sections.Children.Clear();
        _shown.Clear();
        _pictures?.Forget();
        _previewed = null;

        if (sections.Count == 0)
        {
            _preview.Clear();
            _none.Show(StatusKind.Empty, Glyphs.Search, searching ? Str.AddMenu_NoResults.Format(("query", query.Trim())) : string.Empty);
            _sections.Children.Add(_none);
        }

        var first = true;
        foreach (var section in sections)
        {
            var heading = Fluent.Text(FamilyType.BodyStrong);
            heading.Text = section.Heading.Text();
            heading.Margin = new Thickness(0, first ? 0 : P.SectionTop - A.TileGap, 0, P.HeadingGap);
            _sections.Children.Add(heading);
            first = false;

            var grid = new TileGrid { Margin = new Thickness(0, 0, 0, A.TileGap) };
            AutomationProperties.SetName(grid, heading.Text);
            foreach (var tile in section.Tiles)
            {
                grid.Children.Add(new Tile(tile, _commands.OverLimit(tile), () => Pick(tile), () => Preview(tile)));

                // [S-13604] Every tile shown gets its picture ready, after the preview's, so pointing at it shows it at once.
                if (AddPictures.Cached(tile) is null)
                {
                    _pictures?.Want(tile, drawn => Drew(tile, drawn));
                }
            }

            _sections.Children.Add(grid);
            _shown.Add((section, grid));
        }

        // [S-13602] Until a tile is pointed at, the preview shows the first: the one Enter in the box picks.
        if (sections.Count > 0 && sections[0].Tiles.Count > 0)
        {
            Preview(sections[0].Tiles[0]);
        }

        _body.ChangeView(null, 0, null, disableAnimation: true);
        Drawn?.Invoke();
    }

    /// <summary>The page drew other sections (a tab, a search): its flyout may want more room.</summary>
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

    // ---- [S-11708] keys -----------------------------------------------------------------------

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
        var ctrl = Held(VirtualKey.Control);
        if (key == VirtualKey.Tab && ctrl)
        {
            var tabs = _page.Tabs.ToList();
            var at = Math.Max(0, tabs.FindIndex(t => t.Id == _tab));
            ShowTab(tabs[AddPageKeys.Tab(tabs.Count, at, back: Held(VirtualKey.Shift))].Id);
            return true;
        }

        // [S-13607] The one search key rule: Escape empties the box (from a tile too), an empty box's Escape closes
        // the page; Enter in the box adds the best match (or the tab's first tile).
        var inBox = _search.HasKeys;
        var typed = _search.Text.Length > 0;
        var tiles = _shown.Count > 0 ? _shown[0].Section.Tiles.Count : 0;
        var search = key switch
        {
            VirtualKey.Escape => inBox ? SearchKeys.InBox(WidgetKey.Escape, typed, tiles) : SearchKeys.OnResult(WidgetKey.Escape, 1, typed),
            VirtualKey.Enter when inBox => SearchKeys.InBox(WidgetKey.Enter, typed, tiles),
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
            case SearchMove.PickBest:
                Pick(_shown[0].Section.Tiles[0]);
                return true;
        }

        if (key == VirtualKey.Enter && inBox)
        {
            return true;
        }

        if (Map(key) is not { } move || (inBox && move != WidgetKey.Down))
        {
            return false;
        }

        var cursor = inBox ? null : Focused();
        if (!inBox && cursor is null)
        {
            return false; // the tab strip's own keys
        }

        var columns = _shown.Count > 0 ? _shown[0].Grid.Columns : 1;
        var step = AddPageKeys.Move([.. _shown.Select(s => s.Section.Tiles.Count)], columns, cursor, move);
        if (!step.Handled)
        {
            return false;
        }

        if (step.Cursor is { } to)
        {
            if (_shown[to.Section].Grid.Children[to.Tile] is Tile tile && !tile.FocusTile())
            {
                Log.Debug(Scope, $"tile {to.Tile} of section {to.Section} took no focus");
            }
        }
        else
        {
            FocusSearch();
        }

        return true;
    }

    /// <summary>The tile the keyboard is on, as the keys walk it.</summary>
    private AddCursor? Focused()
    {
        for (var s = 0; s < _shown.Count; s++)
        {
            var children = _shown[s].Grid.Children;
            for (var t = 0; t < children.Count; t++)
            {
                if (children[t] is Tile { HasFocus: true })
                {
                    return new AddCursor(s, t);
                }
            }
        }

        return null;
    }

    /// <summary>[S-11708] Typing on a tile goes to the search box.</summary>
    private void OnCharacter(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        if (_search.HasKeys || char.IsControl(e.Character) || e.Character == ' ')
        {
            return;
        }

        _search.Type(e.Character);
        e.Handled = true;
    }

    /// <summary>The arrows, mirrored right to left; Home and End.</summary>
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
            _ => null,
        };
    }

    private static bool Held(VirtualKey key)
        => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    // ---- the parts ------------------------------------------------------------------------------

    /// <summary>
    /// [S-13601] Tiles of at least 200 across the list, the row filling it evenly, 4 apart either way;
    /// a row as tall as its tallest tile.
    /// </summary>
    private sealed partial class TileGrid : Panel
    {
        /// <summary>How many tiles stand across, as last measured.</summary>
        public int Columns { get; private set; } = 1;

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsFinite(availableSize.Width) ? availableSize.Width : A.TileLeast;
            Columns = AddPageLayout.Columns(width);
            var tile = AddPageLayout.TileWidth(width, Columns);
            double height = 0, row = 0;
            for (var i = 0; i < Children.Count; i++)
            {
                Children[i].Measure(new Size(tile, double.PositiveInfinity));
                row = Math.Max(row, Children[i].DesiredSize.Height);
                if ((i + 1) % Columns == 0 || i == Children.Count - 1)
                {
                    height += row + (height > 0 ? A.TileGap : 0);
                    row = 0;
                }
            }

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var tile = AddPageLayout.TileWidth(finalSize.Width, Columns);
            double y = 0;
            for (var start = 0; start < Children.Count; start += Columns)
            {
                var count = Math.Min(Columns, Children.Count - start);
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
    /// [S-13601] A tile, Karpo's (§578): one compact button on a page card (its fill and rim, radius 4; the
    /// subtle fills under the pointer and pressed), 52 tall: the thing's icon 12 in, then its name in Body
    /// over one Caption line, each cut short; over the free plan's widgets, the Pro hint at its end. Pointed
    /// at or reached by the keys, it is the preview's ([S-13602]).
    /// </summary>
    private sealed partial class Tile : Grid
    {
        private readonly Button _button;

        public Tile(AddTile tile, bool overLimit, Action pick, Action point)
        {
            Key = tile.Key;
            Height = A.Tile;
            Background = ShellBrush.Of(ShellRole.CardFill);
            BorderBrush = ShellBrush.Of(ShellRole.CardStroke);
            BorderThickness = new Thickness(FamilyShape.Hairline);
            CornerRadius = new CornerRadius(P.CardRadius);

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

            _button = new Button
            {
                Content = row,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(A.TilePad, 0, A.TilePad, 0),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(P.CardRadius),
                Tag = tile.Key,
            };
            PageTabButton.Subtle(_button);
            _button.Click += (_, _) => pick();

            // [S-13602] Pointing at a tile, or the keys reaching it, shows it in the preview.
            _button.PointerEntered += (_, _) => point();
            _button.GotFocus += (_, _) => point();

            var said = overLimit ? $"{words.Name}, {words.Caption}. {limit}" : $"{words.Name}, {words.Caption}";
            AutomationProperties.SetName(_button, said);
            ToolTipService.SetToolTip(_button, overLimit ? limit : words.Caption);
            Children.Add(_button);
        }

        public string Key { get; }

        public bool HasFocus => _button.FocusState != FocusState.Unfocused;

        /// <summary>A render's pointer resting on the tile: the subtle fill it wears under the pointer.</summary>
        public void Pointed() => _button.Background = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);

        public bool FocusTile()
        {
            _button.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            return _button.Focus(FocusState.Keyboard);
        }
    }
}
