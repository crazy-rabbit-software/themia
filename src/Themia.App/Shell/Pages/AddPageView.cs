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
/// [S-11700]..[S-11711] The add page as drawn, after Karpo's (karpo-app, docs/widgets.md §593, §578, §592):
/// a top row of the search box and the subjects' tab strip (40 tall, the chosen tab's words strong over
/// the accent's pill), then the chosen tab's sections, each a BodyStrong heading over its tiles (as many
/// of at least 200 as fit, the row filling the width, 8 apart). A tile is one button on a page card: a
/// picture of the thing itself (<see cref="AddPictures"/>; its glyph until it is drawn) over its name
/// and one caption line. Typing searches every tab; nothing matching says so in the family's status row.
/// The page's model is Core's (<see cref="AddPage"/>, <see cref="AddPageKeys"/>, <see cref="AddPageLayout"/>).
/// </summary>
internal sealed partial class AddPageView : Grid
{
    private const string Scope = "add-page";

    private readonly AddPage _page;
    private readonly AddPageCommands _commands;
    private readonly AddPictures? _pictures;

    private readonly TextBox _search;
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = ShellMetrics.Bar.EntryGap };
    private readonly ScrollViewer _tabScroll;
    private readonly StackPanel _sections = new();
    private readonly ScrollViewer _body;
    private readonly StatusBlock _none = new();

    /// <summary>The sections shown now, each its grid of tiles.</summary>
    private readonly List<(AddSection Section, TileGrid Grid)> _shown = [];

    /// <summary>The tiles shown now, by their key: a picture drawn later finds its tile here.</summary>
    private readonly Dictionary<string, Tile> _tiles = new(StringComparer.Ordinal);

    private AddFolderId _tab = AddFolderId.Start;

    public AddPageView(AddPage page, AddPageCommands commands, AddPictures? pictures)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _pictures = pictures;

        Padding = new Thickness(ShellMetrics.Flyout.Gutter, ShellMetrics.Flyout.Gutter, ShellMetrics.Flyout.Gutter, 0);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _search = new TextBox
        {
            PlaceholderText = Str.Shell_Picker_Search.Text(),
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
            Width = A.SearchWidth,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(_search, Str.Shell_Picker_Search.Text());
        _search.TextChanged += (_, _) => Draw();

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
            Height = A.TabHeight,
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
        SetRow(_body, 1);
        Children.Add(_body);

        foreach (var tab in page.Tabs)
        {
            _tabs.Children.Add(new TabButton(tab, () => ShowTab(tab.Id)));
        }

        PreviewKeyDown += OnKey;
        CharacterReceived += OnCharacter;
        Draw();
    }

    /// <summary>The tab shown (with nothing typed).</summary>
    public AddFolderId Tab => _tab;

    /// <summary>[S-11708] The caret in the search box: as the page opens, typing searches at once.</summary>
    public void FocusSearch() => _search.Focus(FocusState.Programmatic);

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

        if (_tabs.Children.OfType<TabButton>().FirstOrDefault(t => t.Id == tab) is { } chosen)
        {
            chosen.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
        }
    }

    /// <summary>How tall the page wants to be at <paramref name="width"/>, before the room cuts it.</summary>
    public double ContentHeight(double width)
    {
        _sections.Measure(new Size(Math.Max(0, width - Padding.Left - Padding.Right), double.PositiveInfinity));
        return Padding.Top + A.TabHeight + _body.Margin.Top + _sections.DesiredSize.Height;
    }

    /// <summary>[S-11701] [S-11706] Draw the tab's sections, or the search's, or the status row saying nothing matched.</summary>
    private void Draw()
    {
        var query = _search.Text;
        var searching = AddPage.Searching(query);
        IReadOnlyList<AddSection> sections = searching ? _page.Find(query) : _page.Sections(_tab);

        foreach (var tab in _tabs.Children.OfType<TabButton>())
        {
            tab.Chosen = !searching && tab.Id == _tab;
        }

        _sections.Children.Clear();
        _shown.Clear();
        _tiles.Clear();
        _pictures?.Forget();

        if (sections.Count == 0)
        {
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
                var view = new Tile(tile, _commands.OverLimit(tile), () => Pick(tile));
                _tiles.TryAdd(tile.Key, view);
                grid.Children.Add(view);
                if (AddPictures.Cached(tile) is { } picture)
                {
                    view.Show(picture);
                }
                else
                {
                    _pictures?.Want(tile, drawn => _tiles.GetValueOrDefault(tile.Key)?.Show(drawn));
                }
            }

            _sections.Children.Add(grid);
            _shown.Add((section, grid));
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

        var inBox = _search.FocusState != FocusState.Unfocused;
        switch (key)
        {
            case VirtualKey.Escape when _search.Text.Length > 0:
                _search.Text = string.Empty;
                FocusSearch();
                return true;
            case VirtualKey.Escape:
                _commands.Close();
                return true;
            case VirtualKey.Enter when inBox:
                // The best match (or the tab's first tile) is what Enter in the box adds.
                if (_shown.Count > 0 && _shown[0].Section.Tiles.Count > 0)
                {
                    Pick(_shown[0].Section.Tiles[0]);
                }

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
        if (_search.FocusState != FocusState.Unfocused || char.IsControl(e.Character) || e.Character == ' ')
        {
            return;
        }

        _search.Text += e.Character;
        _search.SelectionStart = _search.Text.Length;
        FocusSearch();
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
    /// [S-11707] Tiles of at least 200 across the width, the row filling it evenly, 8 apart either way;
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
    /// [S-11702] A tile: one button on a page card (its fill and rim, radius 4; the subtle fills under the
    /// pointer and pressed): its picture box (the picture fitted, never scaled up, centred; the kind's glyph
    /// until it is drawn), its name in Body and one Caption line; over the free plan's widgets, the Pro hint.
    /// </summary>
    private sealed partial class Tile : Grid
    {
        private readonly Button _button;
        private readonly Grid _box;
        private readonly FontIcon _glyph;

        public Tile(AddTile tile, bool overLimit, Action pick)
        {
            Background = ShellBrush.Of(ShellRole.CardFill);
            BorderBrush = ShellBrush.Of(ShellRole.CardStroke);
            BorderThickness = new Thickness(FamilyShape.Hairline);
            CornerRadius = new CornerRadius(P.CardRadius);

            var (glyph, name, caption) = Words(tile);
            _glyph = Fluent.Icon(glyph, "TextFillColorTertiaryBrush", ShellMetrics.Page.HeaderGlyph);
            _glyph.HorizontalAlignment = HorizontalAlignment.Center;
            _glyph.VerticalAlignment = VerticalAlignment.Center;
            _box = new Grid { Height = A.Picture, Children = { _glyph } };

            if (overLimit)
            {
                var pro = Fluent.Text(FamilyType.Caption);
                pro.Foreground = ShellBrush.Of(ShellRole.AccentText);
                pro.Text = "Pro";
                pro.HorizontalAlignment = HorizontalAlignment.Right;
                pro.VerticalAlignment = VerticalAlignment.Top;
                _box.Children.Add(pro);
            }

            var title = Fluent.Text(FamilyType.Body);
            title.Text = name;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.TextWrapping = TextWrapping.NoWrap;
            title.Margin = new Thickness(0, A.TilePad, 0, 0);
            var line = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
            line.Text = caption;
            line.TextTrimming = TextTrimming.CharacterEllipsis;
            line.TextWrapping = TextWrapping.NoWrap;

            _button = new Button
            {
                Content = new StackPanel { Children = { _box, title, line } },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                Padding = new Thickness(A.TilePad),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(P.CardRadius),
                Tag = tile.Key,
            };
            Subtle(_button);
            _button.Click += (_, _) => pick();

            var said = overLimit ? $"{name}, {caption}. {Str.AddMenu_OverLimit.Format(("count", LocaleFormat.Integer(LicenseRules.FreeWidgetLimit)))}" : $"{name}, {caption}";
            AutomationProperties.SetName(_button, said);
            ToolTipService.SetToolTip(_button, overLimit ? Str.AddMenu_OverLimit.Format(("count", LocaleFormat.Integer(LicenseRules.FreeWidgetLimit))) : caption);
            Children.Add(_button);
        }

        public bool HasFocus => _button.FocusState != FocusState.Unfocused;

        public bool FocusTile()
        {
            _button.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            return _button.Focus(FocusState.Keyboard);
        }

        /// <summary>[S-11710] The picture in its box: fitted 8 inside it, scaled down, never up, centred.</summary>
        public void Show(AddPicture picture)
        {
            var image = new Image
            {
                Source = picture.Image,
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            void Fit()
            {
                var width = _box.ActualWidth > 0 ? _box.ActualWidth : A.TileLeast - (2 * A.TilePad);
                var scale = AddPageLayout.Fit(picture.Width, picture.Height, width - (2 * A.TilePad), A.Picture - (2 * A.TilePad));
                image.Width = Math.Round(picture.Width * scale);
                image.Height = Math.Round(picture.Height * scale);
            }

            Fit();
            _box.SizeChanged += (_, _) => Fit();
            _box.Children.Remove(_glyph);
            _box.Children.Insert(0, image);
        }

        /// <summary>A tile's glyph, its name and its caption line.</summary>
        private static (string Glyph, string Name, string Caption) Words(AddTile tile) => tile switch
        {
            AddWidgetTile one => (ModuleCatalog.Of(one.Kind).Glyph, ModuleCatalog.Of(one.Kind).Name, ModuleCatalog.Of(one.Kind).Describe()),
            AddGroupTile group => (group.Group.Glyph, group.Group.Name.Text(), Str.Groups_Add_Count.Plural(group.Group.Widgets.Count)),
            AddItemTile item => (ItemGlyph(item.Item.Kind), item.Item.Name, Str.Desktop_Add_Folder.Text()),
            _ => (Glyphs.Apps, string.Empty, string.Empty),
        };

        /// <summary>[S-5523] A desktop item's glyph: what it is, as Explorer's own icons say it.</summary>
        private static string ItemGlyph(DesktopItemKind kind) => kind switch
        {
            DesktopItemKind.Folder => Glyphs.Folder,
            DesktopItemKind.Shortcut => Glyphs.Link,
            DesktopItemKind.ShellItem => Glyphs.Display,
            _ => Glyphs.File,
        };
    }

    /// <summary>
    /// [S-11700] A tab of the strip, as WinUI's SelectorBar draws its items (Karpo's §592): 40 tall, no
    /// fill until pointed at, its words in Body and secondary; the chosen one's in BodyStrong and primary
    /// over the accent's 16 × 3 pill.
    /// </summary>
    private sealed partial class TabButton : Button
    {
        private readonly TextBlock _words;
        private readonly Rectangle _pill = new()
        {
            Width = ShellMetrics.Bar.PillWidth,
            Height = ShellMetrics.Bar.PillHeight,
            RadiusX = ShellMetrics.Bar.PillRadius,
            RadiusY = ShellMetrics.Bar.PillRadius,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Fill = Family.Brush(ColourRole.Accent),
            IsHitTestVisible = false,
        };

        public TabButton(AddTab tab, Action choose)
        {
            Id = tab.Id;
            _words = Fluent.Text(FamilyType.Body);
            _words.Text = tab.Title.Text();
            _words.VerticalAlignment = VerticalAlignment.Center;
            _words.HorizontalAlignment = HorizontalAlignment.Center;
            Content = new Grid { Children = { _words, _pill } };
            Height = A.TabHeight;
            Padding = new Thickness(A.TabPad, 0, A.TabPad, 0);
            VerticalContentAlignment = VerticalAlignment.Stretch;
            Tag = "tab:" + tab.Id;
            Subtle(this);
            AutomationProperties.SetName(this, _words.Text);
            Click += (_, _) => choose();
            Chosen = false;
        }

        public AddFolderId Id { get; }

        public bool Chosen
        {
            get => _pill.Visibility == Visibility.Visible;
            set
            {
                _pill.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                _words.FontWeight = value ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
                _words.Foreground = ThemeBrush.Of(value ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush", Colors.Gray);
                AutomationProperties.SetItemStatus(this, value ? "selected" : string.Empty);
            }
        }
    }

    /// <summary>No fill and no rim at rest, the subtle fills under the pointer and pressed.</summary>
    private static void Subtle(Button button)
    {
        button.MinWidth = 0;
        button.MinHeight = 0;
        var clear = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
        button.Resources["ButtonBackground"] = clear;
        button.Resources["ButtonBackgroundPointerOver"] = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
        button.Resources["ButtonBackgroundPressed"] = ThemeBrush.Of("SubtleFillColorTertiaryBrush", Colors.DimGray);
        button.Resources["ButtonBackgroundDisabled"] = clear;
        var rim = "Button" + nameof(Control.BorderBrush);
        button.Resources[rim] = clear;
        button.Resources[rim + "PointerOver"] = clear;
        button.Resources[rim + "Pressed"] = clear;
        button.Resources[rim + "Disabled"] = clear;
        var text = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
        button.Resources["ButtonForeground"] = text;
        button.Resources["ButtonForegroundPointerOver"] = text;
        button.Resources["ButtonForegroundPressed"] = text;
    }
}
