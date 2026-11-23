using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell.Motion;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;
using Windows.System;
using G = Themia.Sdk.Kit.ShellMetrics.WidgetGallery;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>[S-14916] What the gallery asks of its surface.</summary>
/// <param name="Drag">Widgets of the screen shown (<c>screen</c>, a view of new widgets on their cells) picked up at a point of the surface (DIPs): the placement starts.</param>
internal sealed record WidgetGalleryCommands(Action<ViewState, IReadOnlyList<string>, Point> Drag);

/// <summary>
/// [S-14910]..[S-14917] [S-16400]..[S-16410] The widget gallery (owner, 2026-11-21: "on the independent page it should
/// bascially be a dashboard that you show with the widgets already arranged / shown like it would be on a themia view ...
/// when you want to place a widget you basically just drag it onto the desktop from that preview"; 2026-11-23: "the
/// different categories should be moved to the top as tabs. there should not be a header like Time anymore over the grid";
/// "when you click on a widget it should show the other styles of the widget around it and should also show the
/// description of it somewhere ... like a widget"; "it should be like a little browsing experience"). A window flush under
/// the display's edit bar (<see cref="WidgetGallery.Frame"/>): the categories' tabs and the search box over a well, the
/// display's blurred wallpaper dimmed with the display's own grid on it, in which the category (or what the search finds)
/// stands as a bento of real cards on their samples (<see cref="SampleWorld"/>, <see cref="SampleCards"/>;
/// <see cref="WidgetGallery.Lay"/>), every card on the display's cells: a cell of the gallery is a cell of the desktop. A
/// click on a card puts its piece in focus (<see cref="WidgetGallery.Focus"/>): the others fade, and round it stand its
/// words as a card of their own, its other styles (or sizes) and its neighbours, every one a real card to drag; a click
/// away or Escape brings the category back. The edit mode's selection rules pick (a press, Ctrl or Shift, the lasso; a
/// group is picked whole), the arrows move the pick, Enter places it. Dragging the pick hands it to the surface
/// (<see cref="WidgetGalleryCommands.Drag"/>), which starts the one placement session; while it is aimed the gallery
/// steps aside and the cards picked up follow the ghost (<see cref="Follow"/>) from exactly where they stood.
/// </summary>
internal sealed partial class WidgetGalleryView : Grid, IDisposable
{
    private const string Scope = "widget-gallery";

    /// <summary>The view id of a screen's widgets: never a view of the state.</summary>
    internal const string ScreenViewId = "widget-gallery";

    private readonly WidgetGalleryCommands _commands;
    private readonly Backdrop? _backdrop;
    private readonly AddPage _page;
    private readonly IReadOnlyList<GalleryCategory> _categories;
    private readonly Services.Ticker _ticker;
    private readonly SampleWorld _samples;
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private readonly WidgetSelection _selection = new();

    private readonly Canvas _stage = UiLanguage.KeepLeftToRight(new Canvas());
    private readonly Grid _ground = new();
    private readonly Image _wallpaper = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private readonly Border _sheet = new() { IsHitTestVisible = false };
    private readonly SnapGridOverlay _grid = new() { IsHitTestVisible = false };
    private readonly Canvas _cardLayer = new() { IsHitTestVisible = false };
    private readonly EditMarkerLayer _marks = new();
    private readonly Canvas _markPlates = new() { IsHitTestVisible = false };
    private readonly Canvas _input = new() { Background = Fluent.Clear() };
    private readonly Rectangle _band = new()
    {
        Stroke = Family.Brush(ColourRole.Accent),
        StrokeThickness = FamilyShape.Hairline,
        Fill = HexBrush.Wash(ColourRole.Accent),
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed,
    };

    /// <summary>[S-14911] The window: Windows' sheet, its rim and its lift, under everything of the gallery.</summary>
    private readonly Border _window = new();

    /// <summary>[S-16401] [S-16402] The head: the categories' tabs, the search box at its end.</summary>
    private readonly Grid _head = new();
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = P.TabGap };
    private readonly SearchBox _search = new(Str.Shell_Picker_Search.Text()) { Width = G.Search, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>[S-16402] The words the bento shows the finds of (empty: a category).</summary>
    private string _searched = string.Empty;

    /// <summary>[S-16402] The search folded to its magnifier: the tabs have the line until it is clicked.</summary>
    private readonly Button _searchButton = new();

    /// <summary>[S-14911] The well's rounded box: the ground the cards stand on is clipped to it.</summary>
    private readonly Border _wellBox = new();

    /// <summary>[S-14911] The well's rim, over the cards.</summary>
    private readonly Border _wellRim = new() { IsHitTestVisible = false };

    /// <summary>[S-14913] The well's scroll bar: shown while the sheet is taller than the well.</summary>
    private readonly Microsoft.UI.Xaml.Controls.Primitives.ScrollBar _scrollBar = new()
    {
        Orientation = Orientation.Vertical,
        IndicatorMode = Microsoft.UI.Xaml.Controls.Primitives.ScrollingIndicatorMode.MouseIndicator,
        Visibility = Visibility.Collapsed,
    };

    private readonly List<WidgetCard> _cards = [];

    /// <summary>[S-16404] The words' cards of the sheet shown, by their tile.</summary>
    private readonly List<(int Tile, Border Card)> _abouts = [];
    private readonly Host _host;

    private ScreenMetrics _metrics;
    private double _raster = 1;
    private double _contentScale = 1;
    private DipRect _work;
    private GalleryFrame? _frame;

    /// <summary>[S-14912] The cards' room in the well, whole cells of the display's grid.</summary>
    private CellBox _view;

    /// <summary>[S-14913] The category chosen (its place in <see cref="_categories"/>).</summary>
    private int _at;

    /// <summary>[S-16402] What the bento shows when nothing is in focus: the category chosen, or what the search finds.</summary>
    private GalleryCategory? _base;

    /// <summary>[S-16404] The focus shown (<see cref="WidgetGallery.Focus"/>), or null: the base is shown.</summary>
    private GalleryCategory? _focus;

    private GallerySheet? _laid;
    private int _scroll;
    private ViewState _screen = Empty();

    /// <summary>[S-16407] The tile the keys stand on (the pick's), or null.</summary>
    private int? _cursor;

    /// <summary>[S-14912] Each widget of the sheet shown: the tile it belongs to and its box on the sheet (cells from its corner).</summary>
    private readonly Dictionary<string, (int Tile, CellBox Box)> _placed = new(StringComparer.Ordinal);

    /// <summary>A press on the cards' room: where (surface DIPs), on which card (null: empty room), and how.</summary>
    private (Point At, string? Card, bool Kept, bool Extending, List<string> Base)? _press;
    private bool _banding;
    private List<WidgetCard>? _following;
    private bool _disposed;

    public WidgetGalleryView(WidgetGalleryCommands commands, Backdrop? backdrop, DesignState design, AddPage page)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _categories = WidgetGallery.Of(page);
        ArgumentNullException.ThrowIfNull(design);
        _backdrop = backdrop;
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("the gallery is built on the UI thread");
        _ticker = new Services.Ticker(dispatcher);
        _samples = new SampleWorld(_ticker, dispatcher);
        _host = new Host(_ticker, this) { Design = design, Samples = _samples, Selected = _selected };

        // [S-14911] The window: the edit bar's own ground with a flyout's rim, lifted off the desktop as a flyout is. A press
        // on it is the gallery's: it never reaches the desktop under it.
        _window.Background = ShellBrush.Of(ShellRole.Acrylic);
        _window.BorderBrush = ShellBrush.Of(ShellRole.FlyoutStroke);
        _window.BorderThickness = new Thickness(FamilyShape.Hairline);
        _window.CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius);
        _window.Shadow = new ThemeShadow();
        _window.Translation = new System.Numerics.Vector3(0, 0, (float)ShellMetrics.Shadow.Elevation);
        _window.PointerPressed += (_, e) => e.Handled = true;

        _sheet.Background = ShellBrush.Of(ShellRole.CardBase);
        _ground.Children.Add(_wallpaper);
        _ground.Children.Add(_sheet);
        _ground.Background = ShellBrush.Of(ShellRole.CardBase);
        _wellBox.CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius);
        _wellBox.Child = _ground;
        _wellRim.CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius);
        _wellRim.BorderBrush = ShellBrush.Of(ShellRole.CardStroke);
        _wellRim.BorderThickness = new Thickness(FamilyShape.Hairline);

        foreach (var plate in new[] { _marks.Halos, _marks.Inks, _marks.Lifts, _marks.Overhangs })
        {
            _markPlates.Children.Add(plate);
        }

        // [S-16401] Every category a tab of the one tab component, in a line at the top; a line wider than the window scrolls.
        foreach (var category in _categories)
        {
            var id = category.Id;
            _tabs.Children.Add(new PageTabButton(id.ToString(), category.Title.Text(), () => ShowCategory(id)));
        }

        AutomationProperties.SetName(_tabs, Str.WidgetGallery_Categories.Text());
        var tabScroll = new ScrollViewer
        {
            Content = _tabs,
            HorizontalScrollMode = ScrollMode.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // [S-16402] The search at the line's end: its magnifier, the tabs having the line; clicked, the box opens there (the
        // tabs giving it room) and what is typed is looked for in every widget and group; emptied, it folds back.
        _search.Typed += text =>
        {
            // [S-16409] Words the box raises again (its template taking them up) are no new search.
            if (!string.Equals(text, _searched, StringComparison.Ordinal))
            {
                Searched();
            }
        };
        _search.Margin = new Thickness(G.Pad, 0, 0, 0);
        _search.Visibility = Visibility.Collapsed;
        _searchButton.Content = Fluent.Icon(Glyphs.Search, "TextFillColorPrimaryBrush", P.IconGlyph);
        _searchButton.Margin = new Thickness(G.Pad, 0, 0, 0);
        _searchButton.Height = P.Tabs;
        _searchButton.Width = P.Tabs;
        _searchButton.Padding = new Thickness(0);
        _searchButton.VerticalAlignment = VerticalAlignment.Center;
        PageTabButton.Subtle(_searchButton);
        ToolTipService.SetToolTip(_searchButton, Str.Shell_Picker_Search.Text());
        AutomationProperties.SetName(_searchButton, Str.Shell_Picker_Search.Text());
        _searchButton.Click += (_, _) =>
        {
            OpenSearch(true);
            _search.FocusEnd();
        };
        _head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        SetColumn(_search, 1);
        SetColumn(_searchButton, 1);
        _head.Children.Add(tabScroll);
        _head.Children.Add(_search);
        _head.Children.Add(_searchButton);
        UiLanguage.ApplyFlow(_head);

        _scrollBar.ValueChanged += (_, e) =>
        {
            // Its own range being set (a sheet shown) is no scroll of the user's.
            if (!_settingBar)
            {
                ScrollTo((int)Math.Round(e.NewValue));
            }
        };

        _stage.Children.Add(_window);
        _stage.Children.Add(_head);
        _stage.Children.Add(_wellBox);
        _stage.Children.Add(_grid);
        _stage.Children.Add(_cardLayer);
        _stage.Children.Add(_markPlates);
        _stage.Children.Add(_input);
        _stage.Children.Add(_band);
        _stage.Children.Add(_wellRim);
        _stage.Children.Add(_scrollBar);
        Children.Add(_stage);

        _input.PointerPressed += OnPressed;
        _input.PointerMoved += OnMoved;
        _input.PointerReleased += OnReleased;
        _input.PointerCaptureLost += (_, _) => EndPress();
        _input.PointerWheelChanged += OnWheel;
        if (_backdrop is not null)
        {
            _backdrop.Changed += OnBackdrop;
        }

        AutomationProperties.SetName(this, Str.WidgetGallery_Entry.Text());
    }

    /// <summary>The categories, in the tabs' order.</summary>
    internal IReadOnlyList<GalleryCategory> Categories => _categories;

    /// <summary>The category chosen.</summary>
    internal GalleryCategory Category => _categories[Math.Clamp(_at, 0, Math.Max(0, _categories.Count - 1))];

    /// <summary>[S-14912] The sheet shown: its tiles.</summary>
    internal GallerySheet? Sheet => _laid;

    /// <summary>[S-16404] Whether a piece is in focus, and which (its key).</summary>
    internal bool Focused => _focus is not null;

    internal string? FocusKey => _focus?.Pieces[0].Key;

    /// <summary>[S-16402] Whether what the search finds is shown in place of a category.</summary>
    internal bool Searching => AddPage.Searching(_search.Text);

    /// <summary>[S-14911] The window's parts on the display, and the cards' room in the well.</summary>
    internal GalleryFrame? Frame => _frame;

    /// <summary>[S-14913] How far the sheet shown is scrolled, and how far it scrolls, in rows of cells.</summary>
    internal int Scrolled => _scroll;

    internal int MostScroll => _laid is { } sheet ? WidgetGallery.MaxScroll(sheet.Rows, _view.H) : 0;

    /// <summary>The widgets of the sheet shown, on the cells they stand on now: what a drag picks up.</summary>
    internal ViewState Screen => _screen;

    /// <summary>[S-14912] The widgets of the piece <paramref name="id"/> belongs to: itself, or its ready-made group's every widget.</summary>
    internal IReadOnlyList<string> PieceOf(string id) => [.. Mates(id)];

    /// <summary>The piece a widget of the sheet shown belongs to.</summary>
    internal GalleryPiece? PieceAt(string id) => _placed.TryGetValue(id, out var at) && _laid is { } sheet ? sheet.Tiles[at.Tile].Piece : null;

    /// <summary>The widgets picked, in the order they were picked.</summary>
    internal IReadOnlyList<string> Picked => _selection.Ids;

    /// <summary>[S-14916] The cards picked up by the placement under way: they follow its ghost.</summary>
    internal bool Following => _following is not null;

    /// <summary>The card of a widget of the screen shown, for a check.</summary>
    internal WidgetCard? CardOf(string id) => _cards.Find(c => string.Equals(c.Model.Id, id, StringComparison.Ordinal));

    /// <summary>[S-16404] The words' cards shown, for a check.</summary>
    internal int Abouts => _abouts.Count;

    /// <summary>
    /// [S-14911] Stand over <paramref name="metrics"/>' display: its work area <paramref name="work"/> (DIPs), the bar's foot
    /// <paramref name="barBottom"/>, the part new geometry may use <paramref name="allowed"/> (DIPs), the raster scale and the
    /// content scale new widgets get there. The sheet is laid again only when one of them changed.
    /// </summary>
    public void Place(ScreenMetrics metrics, double rasterScale, DipRect work, double barBottom, DipRect allowed, double contentScale)
    {
        if (_disposed || !metrics.IsUsable)
        {
            return;
        }

        Width = metrics.Width;
        Height = metrics.Height;
        var same = metrics == _metrics && Math.Abs(rasterScale - _raster) < 1e-9 && work == _work && Math.Abs(contentScale - _contentScale) < 1e-9;
        (_metrics, _raster, _work, _contentScale) = (metrics, rasterScale, work, contentScale);

        // [S-16400] The window flush under the bar, its parts inside it.
        var (cols, rows) = GridGeometry.GridDims(metrics);
        var frame = WidgetGallery.Frame(work, barBottom, allowed, metrics.Width / cols, metrics.Height / rows);
        var moved = frame != _frame;
        _frame = frame;
        SetBox(_window, frame.Window);
        SetBox(_head, frame.Head);

        // The well: the display's blurred wallpaper under Windows' sheet, the display's own grid on it ([S-06c]): a cell
        // here is a cell of the desktop.
        var well = frame.Well;
        SetBox(_wellBox, well);
        SetBox(_wellRim, well);
        _wallpaper.Margin = new Thickness(-well.X, -well.Y, 0, 0);
        _wallpaper.Width = metrics.Width;
        _wallpaper.Height = metrics.Height;
        _wallpaper.HorizontalAlignment = HorizontalAlignment.Left;
        _wallpaper.VerticalAlignment = VerticalAlignment.Top;
        if (!same || moved)
        {
            OnBackdrop();
            _grid.Width = metrics.Width;
            _grid.Height = metrics.Height;
            _grid.Draw(metrics, rasterScale);
        }

        // What stands in the well is cut by it: the grid, the cards (a scrolled one runs under its rim), their marks, the lasso.
        _clip = new Rect(well.X, well.Y, well.Width, well.Height);
        ClipToWell(true);
        SetBox(_input, well);
        SetBox(_scrollBar, new DipRect(well.X + well.Width - G.ScrollBar - FamilyShape.Hairline, well.Y + G.WellPad, G.ScrollBar, Math.Max(0, well.Height - (2 * G.WellPad))));

        if (same && frame.View == _view && _laid is not null)
        {
            return;
        }

        _view = frame.View;
        _sizes = null;
        if (_base is null)
        {
            Show(_at);
        }
        else
        {
            Present(_focus ?? _base, _focus is not null, glide: null);
        }
    }

    private Rect _clip;

    /// <summary>The well cuts what stands in it; while a drag is aimed the cards picked up follow the ghost anywhere.</summary>
    private void ClipToWell(bool on)
    {
        foreach (var part in new UIElement[] { _grid, _cardLayer, _markPlates, _band })
        {
            part.Clip = on || ReferenceEquals(part, _grid) ? new RectangleGeometry { Rect = _clip } : null;
        }
    }

    // ---- [S-16403] [S-16404] what a piece asks of the bento ---------------------------------------------------------

    /// <summary>[S-14912] [S-16403] What a piece asks of the bento on this display (<see cref="GallerySizes"/>), made again when the display or the view changes.</summary>
    private GallerySizes? _sizes;

    private GallerySizes Sizes => _sizes ??= new GallerySizes(_metrics, _contentScale, _view.W);

    private PieceSize Size(GalleryPiece piece) => Sizes.Of(piece);

    /// <summary>What a piece asks of the bento here, for a check.</summary>
    internal PieceSize SizeOf(GalleryPiece piece) => Sizes.Of(piece);

    // ---- [S-14913] [S-16401] [S-16402] [S-16404] what is shown -------------------------------------------------------

    /// <summary>[S-14913] [S-16401] Show category <paramref name="at"/>: its bento from its top, nothing picked, nothing in focus; a search typed is cleared.</summary>
    public void Show(int at)
    {
        if (_disposed || _categories.Count == 0 || !_metrics.IsUsable)
        {
            return;
        }

        _at = Math.Clamp(at, 0, _categories.Count - 1);
        if (_search.Text.Length > 0)
        {
            _search.Text = string.Empty;
        }

        _searched = string.Empty;
        OpenSearch(false);
        _base = _categories[_at];
        _focus = null;
        Present(_base, focus: false, glide: null);
    }

    /// <summary>[S-16401] A category's bento (its tab).</summary>
    public void ShowCategory(AddFolderId category)
    {
        for (var i = 0; i < _categories.Count; i++)
        {
            if (_categories[i].Id == category)
            {
                Show(i);
                return;
            }
        }
    }

    /// <summary>[S-16402] Search for <paramref name="text"/> as typing into the box does (a render's, a check's).</summary>
    internal void Search(string text)
    {
        _search.Text = text;
        OpenSearch(text.Length > 0);
        Searched();
    }

    /// <summary>[S-16402] The search box open at the line's end, or folded back to its magnifier.</summary>
    private void OpenSearch(bool open)
    {
        _search.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        _searchButton.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>[S-16402] The search's words changed: what it finds as a bento, or the category back when it searches nothing.</summary>
    private void Searched()
    {
        if (_disposed || !_metrics.IsUsable)
        {
            return;
        }

        _focus = null;
        _searched = _search.Text;
        _base = WidgetGallery.Find(_page, _categories, _search.Text) ?? _categories[Math.Clamp(_at, 0, _categories.Count - 1)];
        Present(_base, focus: false, glide: null);
    }

    /// <summary>
    /// [S-16404] Put the piece of the card <paramref name="id"/> in focus: the others go, round it stand its words, its other
    /// styles (or sizes) and its neighbours; the card glides to its place in the middle. A neighbour clicked in focus is put in
    /// focus in its turn. Nothing for a card of no piece.
    /// </summary>
    internal void FocusOn(string id)
    {
        if (_disposed || _base is null || PieceAt(id) is not { } piece || CardOf(id) is not { } card)
        {
            return;
        }

        // The piece as its category (or search) has it: a neighbour shown in focus is that piece.
        var key = piece.Role == GalleryRole.Similar && piece.Key.StartsWith("similar:", StringComparison.Ordinal) ? piece.Key["similar:".Length..] : piece.Key;
        if (_base.Pieces.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal)) is not { } whole)
        {
            return;
        }

        IReadOnlyList<int> styles = [];
        var resizes = false;
        if (whole.Kind is { } kind && whole.Group is null)
        {
            var current = WidgetStyles.Current(card.Model.Config);
            styles = [.. WidgetStyles.For(kind).Select(o => o.Value).Where(v => v != current)];
            resizes = Sizes.Resizes(kind);
        }

        _focus = WidgetGallery.Focus(_base, whole, styles, resizes);
        Present(_focus, focus: true, glide: (whole.Key, Corner(card)));
    }

    /// <summary>[S-16406] Leave the focus: the category (or search) back, the piece gliding back to its place in it.</summary>
    internal void LeaveFocus()
    {
        if (_disposed || _focus is not { } focus || _base is null)
        {
            return;
        }

        var key = focus.Pieces[0].Key;
        var from = _placed.Where(p => p.Value.Tile == 0).Select(p => CardOf(p.Key)).OfType<WidgetCard>().FirstOrDefault();
        _focus = null;
        Present(_base, focus: false, glide: from is null ? null : (key, Corner(from)));
    }

    private static Point Corner(UIElement card) => new(Canvas.GetLeft(card), Canvas.GetTop(card));

    /// <summary>
    /// [S-14912] [S-16404] Lay <paramref name="content"/> (a category, a search's finds, or a focus) and stand its cards on
    /// the display's cells from its top, nothing picked but the piece in focus: the cards shown before fade out, the new ones
    /// fade in, and the card of the piece <paramref name="glide"/> names glides from where it stood. Every piece is built,
    /// the ones under the well's foot too: scrolling moves cards, it builds none.
    /// </summary>
    private void Present(GalleryCategory content, bool focus, (string Key, Point From)? glide)
    {
        if (_disposed || !_metrics.IsUsable)
        {
            return;
        }

        Settle();
        var leaving = _cards.Cast<FrameworkElement>().Concat(_abouts.Select(a => a.Card)).ToList();
        _cards.Clear();
        _abouts.Clear();
        _marks.Frame.Hide();
        _selection.Clear();
        _selected.Clear();
        _placed.Clear();
        _scroll = 0;
        _cursor = null;
        EndPress();

        var (cols, rows) = GridGeometry.GridDims(_metrics);
        var sheet = focus ? WidgetGallery.LayFocus(content, _view.W, Size) : WidgetGallery.Lay(content, _view.W, Size);
        _laid = sheet;

        var view = Empty();
        for (var t = 0; t < sheet.Tiles.Count; t++)
        {
            var tile = sheet.Tiles[t];
            if (tile.Piece.Role == GalleryRole.About)
            {
                _abouts.Add((t, AboutCard(tile.Piece)));
                continue;
            }

            if (tile.Piece.Group is { } group)
            {
                // [S-2471] A ready-made group as it is placed: its widgets with their own settings, laid as one block; [S-2460]
                // made as wide as its box as one block, every member in its own sizing and every edge on a cell.
                var laid = Sizes.Widen(group, tile.Box.W) ?? ReadyMadeGroups.Laid(group, _metrics, new PlacementSizes(_contentScale));
                if (ReadyMadeGroups.Build(group, _metrics) is not { } made || made.Count != laid.Count)
                {
                    continue;
                }

                for (var i = 0; i < made.Count; i++)
                {
                    var box = new CellBox(tile.Box.X + (int)Math.Round(laid[i].X * cols), tile.Box.Y + (int)Math.Round(laid[i].Y * rows),
                        Math.Max(1, (int)Math.Round(laid[i].W * cols)), Math.Max(1, (int)Math.Round(laid[i].H * rows)));
                    made[i].Rect = RectOf(box);
                    _placed[made[i].Id] = (t, box);
                    view.Widgets.Add(made[i]);
                }
            }
            else if (tile.Piece.Kind is { } kind)
            {
                var widget = WidgetInstance.Create(kind, RectOf(tile.Box));
                if (tile.Piece.Style is { } style)
                {
                    WidgetStyles.Set(widget.Config, style);
                }

                // Its box, held to what its sizing takes on this display: what placing it then keeps.
                var held = tile.Box;
                if (SizeFit.Limits(widget, _metrics, _contentScale) is { } limits)
                {
                    held = held with
                    {
                        W = Math.Max(1, (int)Math.Round(limits.Width.Range(tile.Box.W / (double)cols) * cols)),
                        H = Math.Max(1, (int)Math.Round(limits.Height.Range(tile.Box.H / (double)rows) * rows)),
                    };
                    widget.Rect = RectOf(held);
                }

                _placed[widget.Id] = (t, held);
                view.Widgets.Add(widget);
            }
        }

        _screen = view;
        foreach (var widget in view.Widgets)
        {
            try
            {
                var cell = GridGeometry.PaintedCell(widget.Rect, _metrics, _raster);
                var card = new WidgetCard(_host, widget) { Width = cell.Width, Height = cell.Height };
                card.SetLiveRect(widget.Rect);
                card.ApplyEditMode(false);
                card.ApplyContentScale(_contentScale);
                Canvas.SetLeft(card, cell.X);
                Canvas.SetTop(card, cell.Y);
                _cardLayer.Children.Add(card);
                _marks.Add(card);
                _cards.Add(card);
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, $"the gallery's {widget.Kind} card could not be built; the sheet shows the rest", ex);
            }
        }

        foreach (var (t, about) in _abouts)
        {
            PlaceAbout(about, sheet.Tiles[t].Box);
            _cardLayer.Children.Add(about);
        }

        // [S-2240] Cards that touch are one panel: a group's own; pieces stand a cell apart and never join.
        WidgetCard.Join(_cards, _metrics, _raster, editMode: false, _ => 0);
        for (var i = 0; i < _tabs.Children.Count; i++)
        {
            ((PageTabButton)_tabs.Children[i]).Chosen = !Searching && i == _at;
        }

        // [S-16404] In focus the piece is picked.
        if (focus && sheet.Tiles.Count > 0)
        {
            _cursor = 0;
            _selection.Replace(ScreenViewId, _placed.Where(p => p.Value.Tile == 0).Select(p => p.Key));
        }

        ShowScroll();
        Sync();
        Animate(leaving, glide);
        var cards = _cards.ToList();
        _ = SampleCards.ShowAsync(cards, () => _disposed || !_cards.SequenceEqual(cards), Scope);
    }

    /// <summary>
    /// [S-16404] A piece's words as a card of the bento, as a widget stands (Windows' card fill and rim, its radius): its
    /// glyph and name, what it is, its styles' count or how many widgets land together, what it needs (an account), and how
    /// to place it.
    /// </summary>
    private Border AboutCard(GalleryPiece piece)
    {
        var body = new StackPanel { Spacing = Fluent.Space2 };
        AddTileWords words;
        var lines = new List<string>();
        if (piece.Group is { } group)
        {
            words = AddTileWords.Of(new AddGroupTile(group));
            lines.Add(Str.WidgetGallery_Group.Plural(group.Widgets.Count));
        }
        else
        {
            var kind = piece.Kind!.Value;
            words = AddTileWords.Of(new AddWidgetTile(kind));
            var styles = WidgetStyles.For(kind).Count;
            if (styles > 1)
            {
                lines.Add(Str.WidgetGallery_Styles.Plural(styles));
            }

            var services = ModuleCatalog.Of(kind).Providers
                .Select(id => ProviderCatalog.Offered.FirstOrDefault(p => p.Id == id)?.Label.Text())
                .OfType<string>()
                .ToList();
            if (services.Count > 0)
            {
                lines.Add(Str.WidgetGallery_Needs.Format(("services", string.Join(", ", services))));
            }
        }

        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };
        head.Children.Add(Fluent.Icon(words.Glyph, "TextFillColorPrimaryBrush", P.IconGlyph));
        var name = Fluent.Text(FamilyType.BodyStrong);
        name.Text = words.Name;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        head.Children.Add(name);
        body.Children.Add(head);
        Add(Fluent.Text(FamilyType.Body, "TextFillColorSecondaryBrush"), words.About);
        foreach (var line in lines)
        {
            Add(Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush"), line);
        }

        var drag = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
        drag.Margin = new Thickness(0, Fluent.Space2, 0, 0);
        Add(drag, Str.WidgetGallery_Drag.Text());

        // A card standing on the well as the widgets do: Windows' card fill over its solid base, so the grid does not show through.
        var card = new Border
        {
            Background = ShellBrush.Of(ShellRole.CardBase),
            CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius),
            IsHitTestVisible = false,
            Child = new Border
            {
                Background = ShellBrush.Of(ShellRole.CardFill),
                BorderBrush = ShellBrush.Of(ShellRole.CardStroke),
                BorderThickness = new Thickness(FamilyShape.Hairline),
                CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius),
                Padding = new Thickness(G.AboutPad),
                Child = body,
            },
        };
        UiLanguage.ApplyFlow(card);
        AutomationProperties.SetName(card, words.Name);
        return card;

        void Add(TextBlock text, string said)
        {
            if (said.Length == 0)
            {
                return;
            }

            text.Text = said;
            text.TextWrapping = TextWrapping.Wrap;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            body.Children.Add(text);
        }
    }

    /// <summary>A words' card on its box of the sheet, as it stands scrolled.</summary>
    private void PlaceAbout(Border about, CellBox box)
    {
        var cell = GridGeometry.PaintedCell(RectOf(box), _metrics, _raster);
        about.Width = cell.Width;
        about.Height = cell.Height;
        Canvas.SetLeft(about, cell.X);
        Canvas.SetTop(about, cell.Y);
    }

    /// <summary>[S-14912] A box of the sheet as it stands on the display now: from the view's corner, scrolled.</summary>
    private FracRect RectOf(CellBox box)
    {
        var (cols, rows) = GridGeometry.GridDims(_metrics);
        return new FracRect((_view.X + box.X) / (double)cols, (_view.Y + box.Y - _scroll) / (double)rows, box.W / (double)cols, box.H / (double)rows);
    }

    // ---- [S-16406] the fade and the glide ---------------------------------------------------------------------------

    /// <summary>[S-16406] The cards on their way: what fades in or out, the one gliding, and when it began.</summary>
    private readonly List<(FrameworkElement Element, double From, double To, Point? Start, Point? End, bool Drop)> _tweens = [];
    private long _began;
    private bool _ticking;

    /// <summary>
    /// [S-16406] The shell's one glide over the change (<see cref="PageReveal.Ms"/>, eased out): <paramref name="leaving"/>
    /// fades out and goes, every new card fades in, and the card of the piece <paramref name="glide"/> names glides from where
    /// it stood instead. At once while Windows' animations are off.
    /// </summary>
    private void Animate(List<FrameworkElement> leaving, (string Key, Point From)? glide)
    {
        var gliding = glide is { } g && _laid is { } sheet
            ? _placed.Where(p => sheet.Tiles[p.Value.Tile].Piece is var piece && (piece.Key == g.Key || piece.Key == "similar:" + g.Key) && piece.Role is GalleryRole.Picked or GalleryRole.Piece)
                .Select(p => CardOf(p.Key)).OfType<WidgetCard>().OrderBy(c => Canvas.GetTop(c)).ThenBy(c => Canvas.GetLeft(c)).FirstOrDefault()
            : null;
        if (!SystemMotion.AnimationsEnabled)
        {
            foreach (var gone in leaving)
            {
                LetGo(gone);
            }

            return;
        }

        foreach (var gone in leaving)
        {
            _tweens.Add((gone, gone.Opacity, 0, null, null, true));
        }

        foreach (var card in _cards.Cast<FrameworkElement>().Concat(_abouts.Select(a => a.Card)))
        {
            if (ReferenceEquals(card, gliding) && glide is { } from)
            {
                _tweens.Add((card, 1, 1, from.From, Corner(card), false));
                Canvas.SetLeft(card, from.From.X);
                Canvas.SetTop(card, from.From.Y);
                continue;
            }

            card.Opacity = 0;
            _tweens.Add((card, 0, 1, null, null, false));
        }

        _began = Stopwatch.GetTimestamp();
        if (!_ticking && _tweens.Count > 0)
        {
            _ticking = true;
            CompositionTarget.Rendering += OnFrame;
        }
    }

    private void OnFrame(object? sender, object e)
    {
        var ms = Stopwatch.GetElapsedTime(_began).TotalMilliseconds;
        if (_disposed || PageReveal.Done(ms))
        {
            Settle();
            return;
        }

        foreach (var (element, from, to, start, end, _) in _tweens)
        {
            element.Opacity = PageReveal.Glide(from, to, ms);
            if (start is { } a && end is { } b)
            {
                Canvas.SetLeft(element, PageReveal.Glide(a.X, b.X, ms));
                Canvas.SetTop(element, PageReveal.Glide(a.Y, b.Y, ms));
            }
        }
    }

    /// <summary>[S-16406] Every card where it is going: the fades and the glide ended, the cards that went gone.</summary>
    private void Settle()
    {
        if (_ticking)
        {
            CompositionTarget.Rendering -= OnFrame;
            _ticking = false;
        }

        foreach (var (element, _, to, _, end, drop) in _tweens)
        {
            if (drop)
            {
                LetGo(element);
                continue;
            }

            element.Opacity = to;
            if (end is { } b)
            {
                Canvas.SetLeft(element, b.X);
                Canvas.SetTop(element, b.Y);
            }
        }

        _tweens.Clear();
    }

    private void LetGo(FrameworkElement element)
    {
        if (element is WidgetCard card)
        {
            card.Detach();
        }

        _cardLayer.Children.Remove(element);
    }

    // ---- [S-14913] scrolling --------------------------------------------------------------------------------------------

    /// <summary>
    /// [S-14913] Scroll the sheet shown to <paramref name="rows"/> rows of cells from its top (held to its ends): every
    /// card moves by whole cells, so it stands on the desktop's cells wherever it is scrolled to.
    /// </summary>
    public void ScrollTo(int rows)
    {
        if (_disposed || _laid is null || _following is not null)
        {
            return;
        }

        var to = WidgetGallery.Scroll(0, rows, MostScroll);
        if (to == _scroll)
        {
            return;
        }

        Settle();
        _scroll = to;
        foreach (var card in _cards)
        {
            if (!_placed.TryGetValue(card.Model.Id, out var at))
            {
                continue;
            }

            var rect = RectOf(at.Box);
            card.Model.Rect = rect;
            card.SetLiveRect(rect);
            card.FollowMove(GridGeometry.PaintedCell(rect, _metrics, _raster));
        }

        foreach (var (t, about) in _abouts)
        {
            PlaceAbout(about, _laid.Tiles[t].Box);
        }

        EndPress();
        ShowScroll();
        Sync();
    }

    /// <summary>[S-14913] The scroll bar: there while the sheet is taller than the well, its thumb the part in sight.</summary>
    private void ShowScroll()
    {
        var most = MostScroll;
        _settingBar = true;
        try
        {
            _scrollBar.Visibility = most > 0 ? Visibility.Visible : Visibility.Collapsed;
            _scrollBar.Minimum = 0;
            _scrollBar.Maximum = most;
            _scrollBar.ViewportSize = _view.H;
            _scrollBar.SmallChange = WidgetGallery.Step;
            _scrollBar.LargeChange = WidgetGallery.PageRows(_view.H);
            _scrollBar.Value = _scroll;
        }
        finally
        {
            _settingBar = false;
        }
    }

    private bool _settingBar;

    // ---- [S-16407] keys -------------------------------------------------------------------------------------------------

    /// <summary>
    /// [S-14913] [S-16407] The gallery's keys: the arrows move the pick from piece to piece (the sheet scrolled to show it),
    /// Enter places the pick (or the piece in focus), Page Up/Down scroll by a page, Home and End to the top and the foot,
    /// Ctrl+A picks every piece, Escape leaves the focus, then lets the pick go, then empties the search (with nothing of that
    /// it is not the gallery's: it closes). While the search box has the keys they are its own but Escape.
    /// </summary>
    public bool HandleKey(VirtualKey key, bool ctrl)
    {
        if (_disposed || _following is not null)
        {
            return false;
        }

        if (_search.HasKeys && key != VirtualKey.Escape)
        {
            return false;
        }

        switch (key)
        {
            case VirtualKey.Left:
                return Move(GalleryMove.Left);
            case VirtualKey.Right:
                return Move(GalleryMove.Right);
            case VirtualKey.Up:
                return Move(GalleryMove.Up);
            case VirtualKey.Down:
                return Move(GalleryMove.Down);
            case VirtualKey.Enter:
                return PlacePick();
            case VirtualKey.PageUp:
                ScrollTo(_scroll - WidgetGallery.PageRows(_view.H));
                return true;
            case VirtualKey.PageDown:
                ScrollTo(_scroll + WidgetGallery.PageRows(_view.H));
                return true;
            case VirtualKey.Home:
                ScrollTo(0);
                return true;
            case VirtualKey.End:
                ScrollTo(MostScroll);
                return true;
            case VirtualKey.A when ctrl:
                _selection.Replace(ScreenViewId, SelectionRules.All(_screen));
                Sync();
                return true;
            case VirtualKey.Escape when _focus is not null:
                LeaveFocus();
                return true;
            case VirtualKey.Escape when !_selection.IsEmpty:
                _selection.Clear();
                _cursor = null;
                Sync();
                return true;
            case VirtualKey.Escape when _search.Text.Length > 0 || _search.Visibility == Visibility.Visible:
                _search.Text = string.Empty;
                OpenSearch(false);
                Searched();
                return true;
            default:
                return false;
        }
    }

    /// <summary>[S-16407] An arrow: the pick to the next piece that way (<see cref="WidgetGallery.Next"/>), the sheet scrolled to show it.</summary>
    internal bool Move(GalleryMove move)
    {
        if (_laid is not { Tiles.Count: > 0 } sheet)
        {
            return false;
        }

        var to = WidgetGallery.Next(sheet, _cursor ?? CursorOfPick(), move);
        _cursor = to;
        _selection.Replace(ScreenViewId, _placed.Where(p => p.Value.Tile == to).Select(p => p.Key));
        ScrollTo(WidgetGallery.Reveal(_scroll, sheet.Tiles[to].Box, _view.H, MostScroll));
        Sync();
        return true;
    }

    private int? CursorOfPick() => _selection.Ids.Count > 0 && _placed.TryGetValue(_selection.Ids[^1], out var at) ? at.Tile : null;

    /// <summary>[S-16407] Enter: the pick (or the piece in focus) placed, held at its middle: the ghost follows the pointer from there.</summary>
    internal bool PlacePick()
    {
        var ids = SelectionRules.InView(_screen, _selection.Ids);
        if (ids.Count == 0)
        {
            return false;
        }

        var last = _screen.Find(ids[^1])!.Rect;
        var at = new Point((last.X + (last.W / 2)) * _metrics.Width, (last.Y + (last.H / 2)) * _metrics.Height);
        var screen = _screen;
        if (!DispatcherQueue.TryEnqueue(() => _commands.Drag(screen, ids, at)))
        {
            Log.Warn(Scope, "placing the gallery's pick could not be started");
        }

        return true;
    }

    /// <summary>[S-14914] Pick widgets of the screen shown, as a lasso or Ctrl+A would (a check's, and the keys').</summary>
    internal void Pick(IReadOnlyList<string> ids)
    {
        _selection.Replace(ScreenViewId, ids);
        Sync();
    }

    // ---- [S-14916] the drag ---------------------------------------------------------------------------------------------

    /// <summary>
    /// [S-14916] The placement of <paramref name="ids"/> is under way: the gallery steps aside (its ground, grid, tabs and
    /// every other card gone; nothing of it takes the pointer) and the cards picked up stay, to follow the ghost.
    /// </summary>
    public void BeginFollow(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        Settle();
        _following = [.. ids.Select(CardOf).OfType<WidgetCard>()];
        IsHitTestVisible = false;
        ClipToWell(false);
        foreach (var part in Chrome)
        {
            part.Opacity = 0;
        }

        foreach (var card in _cards)
        {
            card.Opacity = _following.Contains(card) ? 1 : 0;
        }

        foreach (var (_, about) in _abouts)
        {
            about.Opacity = 0;
        }
    }

    /// <summary>[S-14916] The ghost moved: the cards picked up stand on its outlines, in the payload's order (the screen's).</summary>
    public void Follow(IReadOnlyList<GhostOutline> outlines)
    {
        ArgumentNullException.ThrowIfNull(outlines);
        if (_following is not { } following)
        {
            return;
        }

        for (var i = 0; i < following.Count && i < outlines.Count; i++)
        {
            var box = GridGeometry.PaintedCell(outlines[i].Rect, _metrics, _raster);
            Canvas.SetLeft(following[i], box.X);
            Canvas.SetTop(following[i], box.Y);
        }
    }

    /// <summary>
    /// [S-14916] The placement is over: the gallery comes back as it was, every card on its cell (given up; [S-16408] one that
    /// landed closes the gallery right after, for the placed widgets' settings).
    /// </summary>
    public void EndFollow()
    {
        _following = null;
        IsHitTestVisible = true;
        ClipToWell(true);
        foreach (var part in Chrome)
        {
            part.Opacity = 1;
        }

        foreach (var card in _cards)
        {
            var cell = GridGeometry.PaintedCell(card.LiveRect, _metrics, _raster);
            Canvas.SetLeft(card, cell.X);
            Canvas.SetTop(card, cell.Y);
            card.Opacity = 1;
        }

        foreach (var (_, about) in _abouts)
        {
            about.Opacity = 1;
        }
    }

    /// <summary>[S-14916] Everything of the gallery but its cards: what steps aside while a drag off it is aimed.</summary>
    private UIElement[] Chrome => [_window, _head, _wellBox, _wellRim, _grid, _markPlates, _scrollBar, _band];

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Settle();
        if (_backdrop is not null)
        {
            _backdrop.Changed -= OnBackdrop;
        }

        foreach (var card in _cards)
        {
            card.Detach();
        }

        _cards.Clear();
        _samples.Dispose();
        _ticker.Dispose();
    }

    private void OnBackdrop()
    {
        var picture = _backdrop?.Picture;
        _wallpaper.Source = picture;

        // [S-14911] Over the wallpaper Windows' sheet dims it; with none (a render) the sheet alone is the ground.
        _sheet.Opacity = picture is null ? 1 : G.Sheet;
        foreach (var card in _cards)
        {
            card.ApplyDesign();
        }
    }

    private static ViewState Empty() => new() { Id = ScreenViewId, Name = string.Empty };

    private static void SetBox(FrameworkElement element, DipRect box)
    {
        Canvas.SetLeft(element, box.X);
        Canvas.SetTop(element, box.Y);
        element.Width = Math.Max(0, box.Width);
        element.Height = Math.Max(0, box.Height);
    }

    // ---- [S-14914] [S-16404] picking ------------------------------------------------------------------------------------

    private string? CardAt(Point at)
        => SelectionRules.PressTarget([.. _cards.Select(c => (c.Model.Id, new DipRect(Canvas.GetLeft(c), Canvas.GetTop(c), c.Width, c.Height)))], _selected, at.X, at.Y, 0);

    /// <summary>Whether <paramref name="at"/> is on a words' card.</summary>
    private bool OnAbout(Point at)
        => _abouts.Exists(a => at.X >= Canvas.GetLeft(a.Card) && at.X < Canvas.GetLeft(a.Card) + a.Card.Width && at.Y >= Canvas.GetTop(a.Card) && at.Y < Canvas.GetTop(a.Card) + a.Card.Height);

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_stage);
        if (!point.Properties.IsLeftButtonPressed || _following is not null)
        {
            return;
        }

        Settle();
        var extending = (e.KeyModifiers & (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)) != 0;
        var at = point.Position;
        var card = CardAt(at);
        var kept = false;
        List<string> start = extending ? [.. _selection.Ids] : [];
        if (card is not null)
        {
            // [S-1580] [S-1593] Ctrl or Shift toggles the card; a press keeps a pick that holds it (the drag carries all of it), else it is picked alone.
            if (extending)
            {
                // A group is toggled whole: one of its widgets let go lets all of it go.
                var had = _selection.Contains(card);
                _selection.Toggle(ScreenViewId, card);
                if (had)
                {
                    var mates = Mates(card).ToHashSet(StringComparer.Ordinal);
                    _selection.Replace(ScreenViewId, _selection.Ids.Where(id => !mates.Contains(id)));
                }
            }
            else
            {
                kept = _selection.Keeps(_screen, card);
                _selection.Press(_screen, card);
            }

            _cursor = _placed.TryGetValue(card, out var tile) ? tile.Tile : null;
        }
        else if (!extending && !OnAbout(at))
        {
            _selection.Clear();
        }

        _press = (at, card, kept, extending, start);
        _banding = false;
        _ = _input.CapturePointer(e.Pointer);
        Sync();
        e.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_press is not { } press || !e.GetCurrentPoint(_stage).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var now = e.GetCurrentPoint(_stage).Position;
        if (!_banding && !CardGestures.Travelled(now.X - press.At.X, now.Y - press.At.Y))
        {
            return;
        }

        if (press.Card is { } card)
        {
            // [S-14916] Past the dead zone a press on a picked card drags the pick onto the desktop.
            _press = null;
            _input.ReleasePointerCapture(e.Pointer);
            if (!_selection.Contains(card))
            {
                return;
            }

            var ids = SelectionRules.InView(_screen, _selection.Ids);
            var screen = _screen;
            e.Handled = true;

            // [S-2346] The placement starts after the pointer's own event, never inside it.
            if (!DispatcherQueue.TryEnqueue(() => _commands.Drag(screen, ids, press.At)))
            {
                Log.Warn(Scope, "the drag off the widget gallery could not be started");
            }

            return;
        }

        // [S-1581] The lasso: what it touches, added to what Ctrl or Shift kept.
        _banding = true;
        var left = Math.Min(press.At.X, now.X);
        var top = Math.Min(press.At.Y, now.Y);
        var width = Math.Abs(now.X - press.At.X);
        var height = Math.Abs(now.Y - press.At.Y);
        Canvas.SetLeft(_band, left);
        Canvas.SetTop(_band, top);
        _band.Width = width;
        _band.Height = height;
        _band.Visibility = Visibility.Visible;
        var band = new FracRect(left / _metrics.Width, top / _metrics.Height, width / _metrics.Width, height / _metrics.Height);
        _selection.Replace(ScreenViewId, press.Base.Concat(SelectionRules.Lasso(_screen, band)));
        Sync();
        e.Handled = true;
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        var press = _press;
        _input.ReleasePointerCapture(e.Pointer);
        EndPress();
        e.Handled = true;
        if (press is not { } p || _banding || p.Extending)
        {
            return;
        }

        // [S-16404] A click on a card puts its piece in focus (in focus, a neighbour's in its turn; another card is picked);
        // [S-16406] a click on empty room leaves the focus (on the words' card it stays).
        var piece = p.Card is { } card ? PieceAt(card) : null;
        if (piece is not null && (_focus is null || piece.Role == GalleryRole.Similar))
        {
            var id = p.Card!;
            if (!DispatcherQueue.TryEnqueue(() => FocusOn(id)))
            {
                Log.Warn(Scope, "a piece of the gallery could not be put in focus");
            }
        }
        else if (p.Card is { } narrowed && p.Kept)
        {
            // [S-1593] A click on a card of a pick of several narrows it to that card.
            _selection.Click(_screen, narrowed);
            Sync();
        }
        else if (p.Card is null && _focus is not null && !OnAbout(p.At))
        {
            if (!DispatcherQueue.TryEnqueue(LeaveFocus))
            {
                Log.Warn(Scope, "the gallery's focus could not be left");
            }
        }
    }

    private void EndPress()
    {
        _press = null;
        _banding = false;
        _band.Visibility = Visibility.Collapsed;
    }

    /// <summary>[S-14913] The wheel over the well scrolls the sheet shown, a step a notch; it never changes the category.</summary>
    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(_stage).Properties.MouseWheelDelta;
        if (delta != 0 && _following is null)
        {
            var notches = Math.Max(1, (int)Math.Round(Math.Abs(delta) / 120.0));
            ScrollTo(_scroll + (delta > 0 ? -notches : notches) * WidgetGallery.Step);
            e.Handled = true;
        }
    }

    /// <summary>[S-14912] The widgets picked with <paramref name="id"/>: itself, or every widget of the ready-made group it is of.</summary>
    private IEnumerable<string> Mates(string id)
    {
        if (!_placed.TryGetValue(id, out var at) || _laid is not { } sheet || sheet.Tiles[at.Tile].Piece.Group is null)
        {
            return [id];
        }

        return _placed.Where(p => p.Value.Tile == at.Tile).Select(p => p.Key);
    }

    /// <summary>[S-14914] A pick holds whole pieces: a group's every widget with any one of them, in the screen's order.</summary>
    private void Whole()
    {
        var ids = _selection.Ids;
        var all = new List<string>();
        foreach (var id in ids)
        {
            foreach (var mate in Mates(id))
            {
                if (!all.Contains(mate, StringComparer.Ordinal))
                {
                    all.Add(mate);
                }
            }
        }

        if (all.Count != ids.Count)
        {
            _selection.Replace(ScreenViewId, SelectionRules.InView(_screen, all));
        }
    }

    /// <summary>[S-14914] The pick drawn: a card's corners; several, the frame's round them.</summary>
    private void Sync()
    {
        Whole();
        _selected.Clear();
        _selected.UnionWith(_selection.Ids);
        var framed = SelectionRules.Framed(editing: false, _cards.Select(c => (c, _selected.Contains(c.Model.Id), false)));
        _host.Framed = new HashSet<string>(framed.Select(c => c.Model.Id), StringComparer.Ordinal);
        foreach (var card in _cards)
        {
            card.ApplySelection();
        }

        if (framed.Count > 0)
        {
            var box = GridGeometry.PaintedCell(BlockGeometry.Bounds([.. framed.Select(c => c.LiveRect)]), _metrics, _raster);
            _marks.Frame.Show(box, _raster, false, false, cornersOnly: true);
        }
        else
        {
            _marks.Frame.Hide();
        }
    }

    /// <summary>The gallery's cards' host: the display's screen and blurred wallpaper, the pick as the cards' selection, nothing persisted.</summary>
    private sealed class Host(Services.Ticker ticker, WidgetGalleryView gallery) : PreviewCardHost(ticker)
    {
        protected override ScreenMetrics? Screen => gallery._metrics.IsUsable ? gallery._metrics : null;

        public override Backdrop? Backdrop => gallery._backdrop;
    }
}
