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
/// <param name="TakeKeys">[S-16557] The keyboard taken out of the search box back to the surface (a press on the cards): the arrows, Enter and Escape are the gallery's again.</param>
internal sealed record WidgetGalleryCommands(Action<ViewState, IReadOnlyList<string>, Point> Drag, Action TakeKeys)
{
    /// <summary>[S-13018] Enter's pick put where the gallery was opened for (a lassoed area) at once; false: it is placed as a drag is.</summary>
    public Func<ViewState, IReadOnlyList<string>, bool>? PlaceHere { get; init; }

    /// <summary>[S-16727] The Not on view tab's page: the desktop's icons this display's view does not place (null: no such tab).</summary>
    public Func<UnplacedPageView>? Unplaced { get; init; }

    /// <summary>[S-16727] The Not on view tab's words and count.</summary>
    public Func<(string Title, int Count)>? UnplacedTab { get; init; }
}

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
/// <see cref="WidgetGallery.Lay"/>), every card on the display's cells: a cell of the gallery is a cell of the desktop, its
/// name a small title over it ([S-16721]). [S-16722] A click on a card picks it and does nothing more: nothing moves,
/// fades or appears. The edit mode's selection rules pick (a press, Ctrl or Shift, the lasso; a
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
    /// <summary>
    /// [S-16747] The lasso, in the SDK's one look as the desktop's (<see cref="SelectionChrome.PaintBand"/>), over the cards,
    /// held to the well (its box cut to the well's, never clipped by a geometry in another element's space).
    /// </summary>
    private readonly Border _band = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    /// <summary>
    /// [S-14911] [S-16558] The window: Windows' sheet, its rim and its lift, under everything of the gallery, hung from the
    /// gallery's button by a beak as every flyout of the bar is (one outline, <see cref="PageFlyout.Outline"/>).
    /// </summary>
    private readonly Grid _window = new();

    private readonly Microsoft.UI.Xaml.Shapes.Path _windowGround = new() { IsHitTestVisible = true };

    private readonly Microsoft.UI.Xaml.Shapes.Path _windowRim = new() { IsHitTestVisible = false };

    /// <summary>[S-16401] [S-16402] The head: the categories' tabs, the search box at its end.</summary>
    private readonly Grid _head = new();
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = P.TabGap };
    private readonly SearchBox _search = new(Str.Shell_Picker_Search.Text()) { Width = G.Search, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>[S-16566] The tabs' line, scrolled across only when the tabs alone do not fit.</summary>
    private ScrollViewer? _tabScroll;

    /// <summary>[S-16560] The hint beside the search: drag a widget to add it.</summary>
    private readonly TextBlock _hint = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");

    /// <summary>[S-16566] The hint's look, apart: what the whole and the short hint measure.</summary>
    private readonly TextBlock _hintMeasure = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");

    /// <summary>[S-16402] The words the bento shows the finds of (empty: a category).</summary>
    private string _searched = string.Empty;

    /// <summary>[S-16557] A search asked for and not yet run: typing coalesces into one search after the keys.</summary>
    private bool _searchQueued;

    /// <summary>[S-16563] The desktop's own wallpaper being read for the well.</summary>
    private CancellationTokenSource? _wallpaperLoad;

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

    /// <summary>[S-17161] The well's scroll bar across, at its foot: shown while the sheet is wider than the well.</summary>
    private readonly Microsoft.UI.Xaml.Controls.Primitives.ScrollBar _scrollBarX = new()
    {
        Orientation = Orientation.Horizontal,
        IndicatorMode = Microsoft.UI.Xaml.Controls.Primitives.ScrollingIndicatorMode.MouseIndicator,
        Visibility = Visibility.Collapsed,
    };

    private readonly List<WidgetCard> _cards = [];

    /// <summary>[S-16721] [S-16725] The titles and headings of the sheet shown, each on its box of the sheet.</summary>
    private readonly List<(FrameworkElement Label, CellBox Box)> _labels = [];

    /// <summary>[S-16727] The Not on view tab (last), its page while it is shown, and the room it stands in.</summary>
    private readonly PageTabButton? _unplacedTab;
    private readonly ScrollViewer _unplacedRoom = new()
    {
        HorizontalScrollMode = ScrollMode.Disabled,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Visibility = Visibility.Collapsed,
    };

    private UnplacedPageView? _unplaced;
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

    /// <summary>[S-16402] What the bento shows: the category chosen, or what the search finds.</summary>
    private GalleryCategory? _base;

    private GallerySheet? _laid;
    private int _scroll;

    /// <summary>[S-17161] How far the sheet is scrolled across, in columns of cells (a curated tab wider than the well).</summary>
    private int _scrollX;
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
        _windowGround.Fill = ShellBrush.Of(ShellRole.Acrylic);
        _windowRim.Stroke = ShellBrush.Of(ShellRole.FlyoutStroke);
        _windowRim.StrokeThickness = FamilyShape.Hairline;
        _window.Children.Add(_windowGround);
        _window.Children.Add(_windowRim);
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

        SelectionChrome.PaintBand(_band);
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

        // [S-16727] The desktop's icons this view does not place, the last tab (owner, 2026-11-23: "not on home should be a tab
        // in the widget gallery now").
        if (commands.Unplaced is not null && commands.UnplacedTab is not null)
        {
            _unplacedTab = new PageTabButton(UnplacedKey, string.Empty, ShowUnplaced);
            _tabs.Children.Add(_unplacedTab);
            SyncUnplacedTab();
        }

        AutomationProperties.SetName(_tabs, Str.WidgetGallery_Categories.Text());
        var tabScroll = _tabScroll = new ScrollViewer
        {
            Content = _tabs,
            HorizontalScrollMode = ScrollMode.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // [S-16557] The search at the line's end, always open (owner, 2026-11-23: "the search on the topright should always be
        // expanded"); what is typed is looked for in every widget and group, once the keys typed so far are handled.
        _search.Typed += text =>
        {
            // [S-16409] Words the box raises again (its template taking them up) are no new search.
            if (!string.Equals(text, _searched, StringComparison.Ordinal))
            {
                QueueSearch();
            }
        };
        _search.Margin = new Thickness(G.Pad, 0, 0, 0);

        // [S-16560] The hint before it (owner: "there should be a hint on the topright somewhere that says drag widget to add").
        _hint.Text = Str.WidgetGallery_Hint.Text();
        _hint.VerticalAlignment = VerticalAlignment.Center;
        _hint.TextWrapping = TextWrapping.NoWrap;
        _hint.Margin = new Thickness(G.Pad, 0, 0, 0);
        _head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        SetColumn(_hint, 1);
        SetColumn(_search, 2);
        _tabs.SizeChanged += (_, _) => ShareHead();
        _head.Children.Add(tabScroll);
        _head.Children.Add(_hint);
        _head.Children.Add(_search);
        UiLanguage.ApplyFlow(_head);

        _scrollBar.ValueChanged += (_, e) =>
        {
            // Its own range being set (a sheet shown) is no scroll of the user's.
            if (!_settingBar)
            {
                ScrollTo((int)Math.Round(e.NewValue));
            }
        };
        _scrollBarX.ValueChanged += (_, e) =>
        {
            if (!_settingBar)
            {
                ScrollAcross((int)Math.Round(e.NewValue));
            }
        };

        _stage.Children.Add(_window);
        _stage.Children.Add(_head);
        _stage.Children.Add(_wellBox);
        _stage.Children.Add(_grid);
        _stage.Children.Add(_cardLayer);
        _stage.Children.Add(_markPlates);
        _stage.Children.Add(_input);
        _stage.Children.Add(_unplacedRoom);
        _stage.Children.Add(_band);
        _stage.Children.Add(_wellRim);
        _stage.Children.Add(_scrollBar);
        _stage.Children.Add(_scrollBarX);
        Children.Add(_stage);

        _input.PointerPressed += OnPressed;
        _input.PointerMoved += OnMoved;
        _input.PointerReleased += OnReleased;
        _input.PointerCaptureLost += (_, _) =>
        {
            EndPress();
#if DEBUG
            EditLost();
#endif
        };
        _input.PointerWheelChanged += OnWheel;
        if (_backdrop is not null)
        {
            _backdrop.Changed += OnBackdrop;
        }

        AutomationProperties.SetName(this, Str.WidgetGallery_Entry.Text());
#if DEBUG
        BuildEditor();
#endif
    }

    /// <summary>The categories, in the tabs' order.</summary>
    internal IReadOnlyList<GalleryCategory> Categories => _categories;

    /// <summary>The category chosen.</summary>
    internal GalleryCategory Category => _categories[Math.Clamp(_at, 0, Math.Max(0, _categories.Count - 1))];

    /// <summary>[S-14912] The sheet shown: its tiles.</summary>
    internal GallerySheet? Sheet => _laid;

    /// <summary>[S-16727] The Not on view tab's key.</summary>
    internal const string UnplacedKey = "unplaced";

    /// <summary>[S-16727] Whether the Not on view tab is shown.</summary>
    internal bool UnplacedShown => _unplaced is not null;

    /// <summary>[S-16727] The Not on view tab's page while it is shown; null otherwise.</summary>
    internal UnplacedPageView? UnplacedPart => _unplaced;

    /// <summary>[S-16727] The room the Not on view tab's page stands in (surface DIPs); null while it is not shown.</summary>
    internal DipRect? UnplacedRoom => _unplaced is not null && _frame is { } frame ? frame.Well : null;

    /// <summary>[S-16402] Whether what the search finds is shown in place of a category.</summary>
    internal bool Searching => AddPage.Searching(_search.Text);

    /// <summary>[S-14911] The window's parts on the display, and the cards' room in the well.</summary>
    internal GalleryFrame? Frame => _frame;

    /// <summary>[S-14913] How far the sheet shown is scrolled, and how far it scrolls, in rows of cells.</summary>
    internal int Scrolled => _scroll;

    internal int MostScroll => _laid is { } sheet ? WidgetGallery.MaxScroll(sheet.Rows, _view.H) : 0;

    /// <summary>[S-17161] How far the sheet is scrolled across, for a check.</summary>
    internal int ScrolledAcross => _scrollX;

    /// <summary>[S-17161] How far the sheet shown scrolls across: its width past the view's (a curated tab's columns wider than the well).</summary>
    internal int MostScrollAcross => _laid is { Cols: > 0 } sheet ? WidgetGallery.MaxScroll(sheet.Cols, _view.W) : 0;

    /// <summary>The widgets of the sheet shown, on the cells they stand on now: what a drag picks up.</summary>
    internal ViewState Screen => _screen;

    /// <summary>[S-14912] The widgets of the piece <paramref name="id"/> belongs to: itself, or its ready-made group's every widget.</summary>
    internal IReadOnlyList<string> PieceOf(string id) => [.. Mates(id)];

    /// <summary>The piece a widget of the sheet shown belongs to.</summary>
    internal GalleryPiece? PieceAt(string id) => _placed.TryGetValue(id, out var at) && _laid is { } sheet ? sheet.Tiles[at.Tile].Piece : null;

    /// <summary>The tile of <see cref="Sheet"/> the card <paramref name="id"/> shows, for a check (-1: none).</summary>
    internal int SheetTileOf(string id) => _placed.TryGetValue(id, out var at) ? at.Tile : -1;

    /// <summary>The widgets picked, in the order they were picked.</summary>
    internal IReadOnlyList<string> Picked => _selection.Ids;

    /// <summary>[S-14916] The cards picked up by the placement under way: they follow its ghost.</summary>
    internal bool Following => _following is not null;

    /// <summary>The card of a widget of the screen shown, for a check.</summary>
    internal WidgetCard? CardOf(string id) => _cards.Find(c => string.Equals(c.Model.Id, id, StringComparison.Ordinal));

    /// <summary>[S-16721] [S-16725] The titles and headings shown, for a check: each one's words and its box (surface DIPs).</summary>
    internal IReadOnlyList<(string Text, DipRect Box)> Labels
        => [.. _labels.Select(l => (((l.Label as Border)?.Child as TextBlock)?.Text ?? string.Empty, new DipRect(Canvas.GetLeft(l.Label), Canvas.GetTop(l.Label), l.Label.Width, l.Label.Height)))];

    /// <summary>[S-16557] [S-16560] Where the search box, the hint and the tabs' line stand now (surface DIPs; null: not shown), for a check.</summary>
    internal DipRect? SearchBoxShown => Shown(_search);

    /// <summary>[S-16557] Whether the keyboard is in the search box, for a check.</summary>
    internal bool SearchHasKeys => _search.HasKeys;

    internal DipRect? HintBox => Shown(_hint);

    internal DipRect? TabsShown => Shown((FrameworkElement)_head.Children[0]);

    /// <summary>[S-16563] Whether the well shows the desktop's own wallpaper, for a check.</summary>
    internal bool WallpaperShown => _wallpaper.Source is not null && _sheet.Opacity == 0;

    private DipRect? Shown(FrameworkElement element)
    {
        if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return null;
        }

        var box = element.TransformToVisual(_stage).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        return new DipRect(box.X, box.Y, box.Width, box.Height);
    }

    /// <summary>
    /// [S-14911] Stand over <paramref name="metrics"/>' display: its work area <paramref name="work"/> (DIPs), the bar's foot
    /// <paramref name="barBottom"/>, the part new geometry may use <paramref name="allowed"/> (DIPs), the raster scale and the
    /// content scale new widgets get there. The sheet is laid again only when one of them changed. [S-16558] The window's
    /// beak points at <paramref name="button"/>'s middle (the gallery's button on the bar, surface DIPs; null: the window's middle).
    /// </summary>
    public void Place(ScreenMetrics metrics, double rasterScale, DipRect work, double barBottom, DipRect allowed, double contentScale, DipRect? button)
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

        // [S-16558] The window and its beak as one outline, the beak's 8 over its top in the flyouts' gap under the bar.
        var beak = ShellMetrics.Flyout.BeakHeight;
        SetBox(_window, new DipRect(frame.Window.X, frame.Window.Y - beak, frame.Window.Width, frame.Window.Height + beak));
        var tip = FlyoutPlacement.Tip((button is { } b ? b.X + (b.Width / 2) : frame.Window.X + (frame.Window.Width / 2)) - frame.Window.X, frame.Window.Width);
        _windowGround.Data = PageFlyout.Outline(frame.Window.Width, frame.Window.Height, tip);
        _windowRim.Data = PageFlyout.Outline(frame.Window.Width, frame.Window.Height, tip);
        Tip = frame.Window.X + tip;
        SetBox(_head, frame.Head);

        // [S-16557] [S-16566] The search box a quarter of the line at the most on a narrow window; the tabs take what they need
        // of the rest, and the hint only what is left (its whole sentence, its short one, or none).
        _line = frame.Head.Width;
        ShareHead();

        // [S-16563] The well: the desktop's own wallpaper as the desktop shows it (owner, 2026-11-23: "the widget gallery
        // should have the same background as the desktop has"), the display's own grid on it ([S-06c]): a cell here is a
        // cell of the desktop, and a card looks as it will there.
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
        SetBox(_scrollBarX, new DipRect(well.X + G.WellPad, well.Y + well.Height - G.ScrollBar - FamilyShape.Hairline, Math.Max(0, well.Width - (2 * G.WellPad) - G.ScrollBar), G.ScrollBar));

        if (same && frame.View == _view && _laid is not null)
        {
            return;
        }

        _view = frame.View;
        _sizes = null;
        SetBox(_unplacedRoom, frame.Well);
        if (_unplaced is not null)
        {
            _unplaced.Width = Math.Max(0, frame.Well.Width);
            _unplaced.FillHeight = frame.Well.Height;
        }
        else if (_base is null)
        {
            Show(_at);
        }
        else
        {
            Present(_base, Lay(_base));
        }
    }

    /// <summary>[S-16566] The head's line, DIPs across.</summary>
    private double _line;

    /// <summary>
    /// [S-16566] The tabs first, the search and the hint in what they leave (<see cref="WidgetGallery.Head"/>): again whenever
    /// the tabs are laid out at their width (their buttons' own width is only known then).
    /// </summary>
    private void ShareHead()
    {
        if (_line <= 0)
        {
            return;
        }

        // Measured apart (a text box of the hint's look, never in the tree), so nothing laid out is touched to measure: the
        // tabs' size change that asks again is never set off by the asking.
        var whole = Str.WidgetGallery_Hint.Text();
        var brief = Str.WidgetGallery_HintShort.Text();
        double Wide(string text)
        {
            _hintMeasure.Text = text;
            _hintMeasure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return Math.Ceiling(_hintMeasure.DesiredSize.Width);
        }

        double tabs;
        if (_tabs.ActualWidth > 0)
        {
            tabs = Math.Ceiling(_tabs.ActualWidth);
        }
        else
        {
            _tabs.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            tabs = Math.Ceiling(_tabs.DesiredSize.Width);
        }

#if DEBUG
        var line = _line - EditorRoom();
#else
        var line = _line;
#endif
        (HintShown, var search) = WidgetGallery.Head(line, tabs, Math.Min(G.Search, line / 4), G.SearchLeast, Wide(whole), Wide(brief), G.Pad);
        var text = HintShown == GalleryHint.Long ? whole : brief;
        var shown = HintShown == GalleryHint.None || Editing ? Visibility.Collapsed : Visibility.Visible;
        if (Math.Abs(_search.Width - search) > 0.5)
        {
            _search.Width = search;
        }

        if (!string.Equals(_hint.Text, text, StringComparison.Ordinal))
        {
            _hint.Text = text;
            ToolTipService.SetToolTip(_search, HintShown == GalleryHint.Long ? null : whole);
        }

        if (_hint.Visibility != shown)
        {
            _hint.Visibility = shown;
        }
    }

    /// <summary>[S-16566] Which hint the head shows, for a check.</summary>
    internal GalleryHint HintShown { get; private set; }

    /// <summary>[S-16566] Whether every tab is in sight (the line does not scroll), for a check.</summary>
    internal bool AllTabsShown => _tabScroll is { } scroll && scroll.ScrollableWidth <= 0.5;

    /// <summary>[S-16558] Where the window's beak points, surface DIPs across: the gallery's button's middle.</summary>
    internal double Tip { get; private set; }

    private Rect _clip;

    /// <summary>The well cuts what stands in it; while a drag is aimed the cards picked up follow the ghost anywhere.</summary>
    private void ClipToWell(bool on)
    {
        foreach (var part in new UIElement[] { _grid, _cardLayer, _markPlates })
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

        // Emptied for the tab: no search of its own follows (the words it raises are the ones shown).
        _searched = string.Empty;
        if (_search.Text.Length > 0)
        {
            _search.Text = string.Empty;
            _searchQueued = false;
        }

        _base = _categories[_at];
        _scroll = 0;
        _scrollX = 0;
        LeaveUnplaced();
        Present(_base, Lay(_base));
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

    /// <summary>
    /// [S-16727] The Not on view tab (owner, 2026-11-23: "not on home should be a tab in the widget gallery now. it should be
    /// removed from the topbar root"): the well holds the desktop's icons this display's view does not place, as the page under
    /// the bar held them ([S-13700]..[S-13713]: dragged onto the desktop they are placed, desktop icons moved onto it leave the
    /// view); the cards, their grid and the search's finds go.
    /// </summary>
    internal void ShowUnplaced()
    {
        if (_disposed || _commands.Unplaced is not { } make || _frame is not { } frame)
        {
            return;
        }

        if (_unplaced is null)
        {
            Settle();
            foreach (var card in _cards)
            {
                LetGo(card);
            }

            foreach (var (label, _) in _labels)
            {
                _cardLayer.Children.Remove(label);
            }

            _cards.Clear();
            _labels.Clear();
            _placed.Clear();
            _selection.Clear();
            _selected.Clear();
            _screen = Empty();
            _laid = null;
            _marks.Frame.Hide();
            _unplaced = make();
            _unplaced.Width = Math.Max(0, frame.Well.Width);
            _unplaced.FillHeight = frame.Well.Height;
            _unplacedRoom.Content = _unplaced;
        }

        _unplacedRoom.Visibility = Visibility.Visible;
        _input.Visibility = Visibility.Collapsed;
        _grid.Visibility = Visibility.Collapsed;
        _scrollBar.Visibility = Visibility.Collapsed;
        _scrollBarX.Visibility = Visibility.Collapsed;
        foreach (PageTabButton tab in _tabs.Children)
        {
            tab.Chosen = ReferenceEquals(tab, _unplacedTab);
        }

        // [S-16566] On a line that scrolls the chosen tab is scrolled into sight.
        _unplacedTab?.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });

        SyncUnplacedTab();
#if DEBUG
        PlaceEditor();
#endif
    }

    /// <summary>[S-16727] Off the Not on view tab: its page goes, the cards' room is back.</summary>
    private void LeaveUnplaced()
    {
        if (_unplaced is not { } page)
        {
            return;
        }

        _unplacedRoom.Content = null;
        _unplacedRoom.Visibility = Visibility.Collapsed;
        _unplaced = null;
        page.Dispose();
        _input.Visibility = Visibility.Visible;
        _grid.Visibility = Visibility.Visible;
    }

    /// <summary>[S-13706] [S-16727] The state or the desktop's listing changed: the tab's count, and its page when shown, follow.</summary>
    internal void SyncUnplaced()
    {
        if (_disposed)
        {
            return;
        }

        SyncUnplacedTab();
        _unplaced?.Refresh();
    }

    /// <summary>[S-16727] The tab's words, "Not on" the view's name, and its count (none while every icon is placed).</summary>
    private void SyncUnplacedTab()
    {
        if (_unplacedTab is not { } tab || _commands.UnplacedTab is not { } read)
        {
            return;
        }

        var (title, count) = read();
        tab.Title = title;
        tab.Count(Themia.Core.Desktop.UnplacedItems.Badge(count), count > 0 ? Str.Unplaced_Badge.Plural(count) : null);
    }

    /// <summary>[S-16402] Search for <paramref name="text"/> as typing into the box does (a render's, a check's): at once.</summary>
    internal void Search(string text)
    {
        _searched = text;
        _search.Text = text;
        _searchQueued = false;
        Searched();
    }

    /// <summary>
    /// [S-16557] Typing asks for a search after the keys already typed: many keys in a row are one search, run once the
    /// box has taken them (the dispatcher's low priority comes after input), so typing never waits on laying cards.
    /// </summary>
    private void QueueSearch()
    {
        if (_searchQueued)
        {
            return;
        }

        _searchQueued = true;
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (_searchQueued)
            {
                _searchQueued = false;
                Searched();
            }
        }))
        {
            _searchQueued = false;
            Searched();
        }
    }

    /// <summary>
    /// [S-16402] [S-16557] The search's words changed: the first <see cref="WidgetGallery.MostFound"/> it finds as a bento,
    /// or the category back when it searches nothing.
    /// </summary>
    private void Searched()
    {
        if (_disposed || !_metrics.IsUsable)
        {
            return;
        }

        _searched = _search.Text;
        _base = WidgetGallery.Find(_page, _categories, _search.Text) ?? _categories[Math.Clamp(_at, 0, _categories.Count - 1)];
        _scroll = 0;
        _scrollX = 0;
        LeaveUnplaced();
        Present(_base, Lay(_base));
    }

    /// <summary>
    /// [S-16550] [S-16743] A category laid as the owner laid its tab (its curated layout, <see cref="CuratedBento"/>), or, with
    /// none yet, and a search always, as the column bento in the view's width.
    /// </summary>
    private GallerySheet Lay(GalleryCategory content)
    {
        var clock = Stopwatch.StartNew();
        var sheet = CuratedOf(content) is { } layout
            ? CuratedBento.Lay(content.Id, content.Title, layout, Sizes.Curated, Sizes.TitleRows, Sizes.HeadingRows, Editing)
            : WidgetGallery.Lay(content, _view.W, Size, Sizes.TitleRows, Sizes.HeadingRows);
        LastLay = clock.Elapsed;
        return sheet;
    }

    /// <summary>[S-16740] Each tab's curated layout as the build ships it (null: none yet), read once a gallery.</summary>
    private readonly Dictionary<AddFolderId, CuratedLayout?> _shipped = [];

    /// <summary>[S-16740] [S-16746] The curated layout <paramref name="content"/> shows: a tab's own (never a search's); null: laid automatically.</summary>
    private CuratedLayout? CuratedOf(GalleryCategory content)
    {
        if (!_categories.Any(c => ReferenceEquals(c, content)))
        {
            return null;
        }

#if DEBUG
        // [S-16745] What the owner's editor changed this session stands in for the build's file.
        if (EditedLayouts.TryGetValue(content.Id, out var edited))
        {
            return edited;
        }
#endif

        if (!_shipped.TryGetValue(content.Id, out var layout))
        {
            _shipped[content.Id] = layout = CuratedLayouts.Load(content.Id, Sizes.TitleRows);
        }

        return layout;
    }

    /// <summary>[S-16740] Whether the tab shown is a curated one (its sheet the owner's layout).</summary>
    internal bool ShowsCurated => _base is { } shown && !Searching && CuratedOf(shown) is not null;

#if !DEBUG
    /// <summary>[S-16745] The owner's editor is Debug's alone: never on in this build.</summary>
    private static bool Editing => false;
#endif

    /// <summary>[S-16557] How long the last bento took to lay (a search's share of a keystroke), for a check.</summary>
    internal TimeSpan LastLay { get; private set; }

    /// <summary>
    /// [S-14912] [S-16721] [S-16725] Stand <paramref name="content"/>, laid as <paramref name="sheet"/>, on the display's
    /// cells, scrolled as it stands, nothing picked: the cards shown before fade out and the new ones fade in (a tab or a
    /// search changed), each under its title, or a section's under its heading. Every piece is built, the ones under the
    /// well's foot too: scrolling moves cards, it builds none.
    /// </summary>
    private void Present(GalleryCategory content, GallerySheet sheet)
    {
        if (_disposed || !_metrics.IsUsable)
        {
            return;
        }

        Settle();
        var leaving = _cards.Cast<FrameworkElement>().Concat(_labels.Select(l => l.Label)).ToList();
        _cards.Clear();
        _labels.Clear();
        _marks.Frame.Hide();
        _selection.Clear();
        _selected.Clear();
        _placed.Clear();
        _cursor = null;
        EndPress();

        var (cols, rows) = GridGeometry.GridDims(_metrics);
        _laid = sheet;
        _scroll = Math.Clamp(_scroll, 0, MostScroll);
        _scrollX = Math.Clamp(_scrollX, 0, MostScrollAcross);

        var view = Empty();
        var made = new List<WidgetInstance>();
        _labels.AddRange(Titles(sheet));
        for (var t = 0; t < sheet.Tiles.Count; t++)
        {
            var tile = sheet.Tiles[t];
            var room = sheet.CardOf(tile);

            if (tile.Piece.Group is { } group)
            {
                // [S-2471] A ready-made group as it is placed: its widgets with their own settings, laid as one block; [S-2460]
                // made as wide as its box as one block, every member in its own sizing and every edge on a cell.
                var laid = Sizes.Widen(group, room.W) ?? ReadyMadeGroups.Laid(group, _metrics, new PlacementSizes(_contentScale));
                if (ReadyMadeGroups.Build(group, _metrics) is not { } members || members.Count != laid.Count)
                {
                    continue;
                }

                for (var i = 0; i < members.Count; i++)
                {
                    var box = new CellBox(room.X + (int)Math.Round(laid[i].X * cols), room.Y + (int)Math.Round(laid[i].Y * rows),
                        Math.Max(1, (int)Math.Round(laid[i].W * cols)), Math.Max(1, (int)Math.Round(laid[i].H * rows)));
                    members[i].Rect = RectOf(box);
                    _placed[members[i].Id] = (t, box);
                    view.Widgets.Add(members[i]);
                    made.Add(members[i]);
                }
            }
            else if (tile.Piece.Pick is { } kind)
            {
                // [S-16685] A preset's card is its kind with the preset applied; [S-16742] a curated one's fragment over it.
                var widget = WidgetInstance.Create(kind, RectOf(room));
                if (tile.Piece.Config is { } fragment)
                {
                    try
                    {
                        widget.Config = Themia.Core.Storage.StateJson.Merged(widget.Config, fragment.ToJsonString());
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.Text.Json.JsonException)
                    {
                        Log.Warn(Scope, $"the gallery's {kind} card's config fragment does not fit its kind; it shows its default", ex);
                    }
                }

                if (tile.Piece.Style is { } style)
                {
                    WidgetStyles.Set(widget.Config, style);
                }

                // Its box, held to what its sizing takes on this display: what placing it then keeps.
                var held = Held(widget, room);
                widget.Rect = RectOf(held);

                _placed[widget.Id] = (t, held);
                view.Widgets.Add(widget);
                made.Add(widget);
            }
        }

        // [S-16745] While the owner edits, a section's heading is its title box (the editor's), never a label.
        foreach (var heading in Editing ? [] : sheet.Headings)
        {
            _labels.Add((Label(heading.Text.Text(), FamilyType.BodyStrong, "TextFillColorPrimaryBrush"), heading.Box));
        }

        _screen = view;
        var fresh = new List<WidgetCard>();
        foreach (var widget in made)
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
                fresh.Add(card);
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, $"the gallery's {widget.Kind} card could not be built; the sheet shows the rest", ex);
            }
        }

        foreach (var (label, box) in _labels)
        {
            PlaceLabel(label, box);
            _cardLayer.Children.Add(label);
        }

        // [S-2240] [S-17223] Cards that touch are one panel: a ready-made group's own, a curated group of touching widgets; pieces
        // a cell apart never join.
        WidgetCard.Join(_cards, _metrics, _raster, editMode: false, _ => 0);
        for (var i = 0; i < _tabs.Children.Count; i++)
        {
            var tab = (PageTabButton)_tabs.Children[i];
            tab.Chosen = !Searching && i == _at && _unplaced is null;

            // [S-16566] On a line that scrolls (a small display) the chosen tab is scrolled into sight.
            if (tab.Chosen && !AllTabsShown)
            {
                tab.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            }
        }

        ShowScroll();
        Sync();
        if (Editing)
        {
            // [S-16745] An edit shows at once: nothing fades while the owner works.
            Animate(leaving, [], fade: false);
        }
        else
        {
            Animate(leaving, [.. fresh.Cast<FrameworkElement>().Concat(_labels.Select(l => l.Label))]);
        }

#if DEBUG
        PlaceEditor();
#endif

        // [S-16724] A quick toggle shows this PC's own state, as a placed card does: no sample over it.
        var samples = fresh.Where(c => !Live(c)).ToList();
        _ = SampleCards.ShowAsync(samples, () => _disposed || !samples.TrueForAll(_cards.Contains), Scope);
    }

    /// <summary>A widget's box <paramref name="room"/> held to what its sizing takes on this display (whole cells).</summary>
    private CellBox Held(WidgetInstance widget, CellBox room)
    {
        if (SizeFit.Limits(widget, _metrics, _contentScale) is not { } limits)
        {
            return room;
        }

        var (cols, rows) = GridGeometry.GridDims(_metrics);
        var w = limits.Width.Range(room.W / (double)cols);
        return room with
        {
            W = Math.Max(1, (int)Math.Round(w * cols)),
            // [S-17100] A face's height is held to what its width takes.
            H = Math.Max(1, (int)Math.Round(limits.At(w).Height.Range(room.H / (double)rows) * rows)),
        };
    }

    /// <summary>
    /// [S-16721] [S-17223] The small titles over the cards where the sheet has them: one over each card, and one over each group
    /// of touching cards (its first card's: the top-most, then left-most), across the group.
    /// </summary>
    private static List<(FrameworkElement Label, CellBox Box)> Titles(GallerySheet sheet)
    {
        var titles = new List<(FrameworkElement, CellBox)>();
        if (sheet.Title <= 0)
        {
            return titles;
        }

        foreach (var tile in sheet.Tiles.Where(t => t.Group is null))
        {
            titles.Add((Label(Title(tile.Piece), FamilyType.Caption, "TextFillColorSecondaryBrush"), tile.Box with { H = sheet.Title }));
        }

        foreach (var group in sheet.Groups)
        {
            titles.Add((Label(Title(sheet.Tiles[group.Tiles[0]].Piece), FamilyType.Caption, "TextFillColorSecondaryBrush"), group.Title));
        }

        return titles;
    }

    /// <summary>[S-16724] Whether the card reads what it shows live (a quick toggle: Windows' own state), never a sample.</summary>
    private static bool Live(WidgetCard card) => card.Model.Config is Themia.Core.Model.QuickToggleConfig;

    /// <summary>[S-16721] A widget's (its preset's, [S-16685]; or a ready-made group's) name, its title in the gallery.</summary>
    private static string Title(GalleryPiece piece)
        => piece.Group is { } group ? group.Name.Text() : piece.Pick is { } pick ? pick.Name : string.Empty;

    /// <summary>
    /// [S-16721] [S-16725] A title over a card (Caption, secondary: subtle and small, owner, 2026-11-23: "give the widgets a
    /// header above them that is their title. it should be subtile and small") or a section's heading (BodyStrong): one line
    /// at the foot of its box, cut with an ellipsis, a step in from the card's corner.
    /// </summary>
    private static Border Label(string text, TypeStep type, string brush)
    {
        var words = Fluent.Text(type, brush);
        words.Text = text;
        words.TextWrapping = TextWrapping.NoWrap;
        words.TextTrimming = TextTrimming.CharacterEllipsis;
        words.VerticalAlignment = VerticalAlignment.Bottom;
        words.Margin = new Thickness(Fluent.Space1, 0, Fluent.Space1, Fluent.Space1);
        var label = new Border { Child = words, IsHitTestVisible = false };
        UiLanguage.ApplyFlow(label);
        return label;
    }

    /// <summary>A title or heading on its box of the sheet, as it stands scrolled.</summary>
    private void PlaceLabel(FrameworkElement label, CellBox box)
    {
        var cell = GridGeometry.PaintedCell(RectOf(box), _metrics, _raster);
        label.Width = cell.Width;
        label.Height = cell.Height;
        Canvas.SetLeft(label, cell.X);
        Canvas.SetTop(label, cell.Y);
    }

    /// <summary>[S-14912] A box of the sheet as it stands on the display now: from the view's corner, scrolled.</summary>
    private FracRect RectOf(CellBox box)
    {
        var (cols, rows) = GridGeometry.GridDims(_metrics);
        return new FracRect((_view.X + box.X - _scrollX) / (double)cols, (_view.Y + box.Y - _scroll) / (double)rows, box.W / (double)cols, box.H / (double)rows);
    }

    // ---- [S-16406] the fade -------------------------------------------------------------------------------------------

    /// <summary>[S-16406] The cards on their way: what fades in or out, and when it began.</summary>
    private readonly List<(FrameworkElement Element, double From, double To, bool Drop)> _tweens = [];
    private long _began;
    private bool _ticking;

    /// <summary>
    /// [S-16406] [S-16554] The shell's one glide over the change (<see cref="PageReveal.Ms"/>, eased out): <paramref name="leaving"/>
    /// fades out and goes, <paramref name="coming"/> fades in; a card that stays (the piece put in focus, or left) does
    /// neither and never moves. At once while Windows' animations are off.
    /// </summary>
    private void Animate(List<FrameworkElement> leaving, List<FrameworkElement> coming, bool fade = true)
    {
        if (!fade || !SystemMotion.AnimationsEnabled)
        {
            foreach (var gone in leaving)
            {
                LetGo(gone);
            }

            return;
        }

        foreach (var gone in leaving)
        {
            _tweens.Add((gone, gone.Opacity, 0, true));
        }

        foreach (var card in coming)
        {
            card.Opacity = 0;
            _tweens.Add((card, 0, 1, false));
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

        foreach (var (element, from, to, _) in _tweens)
        {
            element.Opacity = PageReveal.Glide(from, to, ms);
        }
    }

    /// <summary>[S-16406] Every card as it is going to stand: the fades ended, the cards that went gone.</summary>
    private void Settle()
    {
        if (_ticking)
        {
            CompositionTarget.Rendering -= OnFrame;
            _ticking = false;
        }

        foreach (var (element, _, to, drop) in _tweens)
        {
            if (drop)
            {
                LetGo(element);
                continue;
            }

            element.Opacity = to;
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
    public void ScrollTo(int rows) => ScrollTo(rows, _scrollX);

    /// <summary>[S-17161] Scroll the sheet shown <paramref name="cols"/> columns of cells across from its left (held to its ends), by whole cells.</summary>
    public void ScrollAcross(int cols) => ScrollTo(_scroll, cols);

    private void ScrollTo(int rows, int cols)
    {
        if (_disposed || _laid is null || _following is not null)
        {
            return;
        }

        var to = WidgetGallery.Scroll(0, rows, MostScroll);
        var across = WidgetGallery.Scroll(0, cols, MostScrollAcross);
        if (to == _scroll && across == _scrollX)
        {
            return;
        }

        Settle();
        _scroll = to;
        _scrollX = across;
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

        foreach (var (label, box) in _labels)
        {
            PlaceLabel(label, box);
        }

        EndPress();
        ShowScroll();
        Sync();
#if DEBUG
        PlaceEditor();
#endif
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
            var wide = MostScrollAcross;
            _scrollBarX.Visibility = wide > 0 ? Visibility.Visible : Visibility.Collapsed;
            _scrollBarX.Minimum = 0;
            _scrollBarX.Maximum = wide;
            _scrollBarX.ViewportSize = _view.W;
            _scrollBarX.SmallChange = WidgetGallery.Step;
            _scrollBarX.LargeChange = WidgetGallery.PageRows(_view.W);
            _scrollBarX.Value = _scrollX;
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
    /// Enter places the pick, Page Up/Down scroll by a page, Home and End to the top and the foot, Ctrl+A picks every piece,
    /// Escape lets the pick go, then empties the search (with nothing of that it is not the gallery's: it closes). While the
    /// search box has the keys they are its own but Escape. [S-16727] On the Not on view tab the keys are its page's.
    /// </summary>
    public bool HandleKey(VirtualKey key, bool ctrl)
    {
        if (_disposed || _following is not null || _unplaced is not null)
        {
            return false;
        }

#if DEBUG
        if (EditKey(key, ctrl) is { } edited)
        {
            return edited;
        }
#endif

        // [S-16557] The search box always open keeps the keys it edits with (Left, Right, Home, End, Enter, Ctrl+A); Up and
        // Down, the pages and Escape are the gallery's.
        if (_search.HasKeys && key is not (VirtualKey.Escape or VirtualKey.Up or VirtualKey.Down or VirtualKey.PageUp or VirtualKey.PageDown))
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
            case VirtualKey.Escape when !_selection.IsEmpty:
                _selection.Clear();
                _cursor = null;
                Sync();
                return true;
            case VirtualKey.Escape when _search.Text.Length > 0:
                Search(string.Empty);
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
        ScrollTo(WidgetGallery.Reveal(_scroll, sheet.Tiles[to].Box, _view.H, MostScroll), WidgetGallery.RevealAcross(_scrollX, sheet.Tiles[to].Box, _view.W, MostScrollAcross));
        Sync();
        return true;
    }

    private int? CursorOfPick() => _selection.Ids.Count > 0 && _placed.TryGetValue(_selection.Ids[^1], out var at) ? at.Tile : null;

    /// <summary>
    /// [S-16407] Enter: the pick placed, held at its middle: the ghost follows the pointer from there; [S-13018] opened for a
    /// lassoed area, it goes into it at once where it can.
    /// </summary>
    internal bool PlacePick()
    {
        var ids = SelectionRules.InView(_screen, _selection.Ids);
        if (ids.Count == 0)
        {
            return false;
        }

        if (_commands.PlaceHere is { } here && here(_screen, ids))
        {
            return true;
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

        foreach (var (label, _) in _labels)
        {
            label.Opacity = 0;
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

        foreach (var (label, _) in _labels)
        {
            label.Opacity = 1;
        }
    }

    /// <summary>[S-14916] Everything of the gallery but its cards: what steps aside while a drag off it is aimed.</summary>
    private UIElement[] Chrome => [_window, _head, _wellBox, _wellRim, _grid, _markPlates, _scrollBar, _scrollBarX, _band, _unplacedRoom];

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Settle();
        _wallpaperLoad?.Cancel();
        _wallpaperLoad?.Dispose();
        _wallpaperLoad = null;
        if (_backdrop is not null)
        {
            _backdrop.Changed -= OnBackdrop;
        }

        foreach (var card in _cards)
        {
            card.Detach();
        }

        _cards.Clear();
        LeaveUnplaced();
        _samples.Dispose();
        _ticker.Dispose();
    }

    /// <summary>
    /// [S-16563] The wallpaper changed (or the gallery stood on its display): the desktop's own picture read again for the
    /// well, the cards' frost following the blurred one.
    /// </summary>
    private void OnBackdrop()
    {
        foreach (var card in _cards)
        {
            card.ApplyDesign();
        }

        _wallpaperLoad?.Cancel();
        _wallpaperLoad?.Dispose();
        _wallpaperLoad = null;
        if (_backdrop is null)
        {
            ShowWallpaper(null);
            return;
        }

        var cancel = new CancellationTokenSource();
        _wallpaperLoad = cancel;
        _ = Services.LoadWatch.Of(LoadWallpaperAsync(_backdrop, cancel.Token));
    }

    private async Task LoadWallpaperAsync(Backdrop backdrop, CancellationToken cancel)
    {
        try
        {
            var picture = await backdrop.PlainAsync(cancel).ConfigureAwait(true);
            if (!cancel.IsCancellationRequested && !_disposed)
            {
                ShowWallpaper(picture);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore: a newer read (or the gallery closing) replaced this one.
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "the desktop's wallpaper could not be read for the gallery; its well shows Windows' sheet", ex);
            ShowWallpaper(null);
        }
    }

    /// <summary>[S-16563] The well's ground: the desktop's picture as the desktop shows it; with none, Windows' sheet alone.</summary>
    private void ShowWallpaper(ImageSource? picture)
    {
        _wallpaper.Source = picture;
        _sheet.Opacity = picture is null ? 1 : 0;
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

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_stage);
        if (!point.Properties.IsLeftButtonPressed || _following is not null)
        {
            return;
        }

        Settle();
        TakeKeys();
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
        else if (!extending)
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
#if DEBUG
        if (EditMoved(e))
        {
            return;
        }
#endif

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
        Lasso(press.At, now, press.Base);
        e.Handled = true;
    }

    /// <summary>
    /// [S-1581] [S-16747] The lasso from <paramref name="from"/> to <paramref name="to"/> (surface DIPs): drawn over the cards as
    /// the desktop draws its own, its box cut to the well's (it never runs over the head or the window's rim, scrolled or
    /// not), and the cards it touches there picked, added to <paramref name="kept"/>.
    /// </summary>
    private void Lasso(Point from, Point to, IReadOnlyList<string> kept)
    {
        var well = _clip;
        var left = Math.Clamp(Math.Min(from.X, to.X), well.Left, well.Right);
        var top = Math.Clamp(Math.Min(from.Y, to.Y), well.Top, well.Bottom);
        var right = Math.Clamp(Math.Max(from.X, to.X), well.Left, well.Right);
        var bottom = Math.Clamp(Math.Max(from.Y, to.Y), well.Top, well.Bottom);
        Canvas.SetLeft(_band, left);
        Canvas.SetTop(_band, top);
        _band.Width = right - left;
        _band.Height = bottom - top;
        _band.Visibility = Visibility.Visible;
        var band = new FracRect(left / _metrics.Width, top / _metrics.Height, (right - left) / _metrics.Width, (bottom - top) / _metrics.Height);
        _selection.Replace(ScreenViewId, kept.Concat(SelectionRules.Lasso(_screen, band)));
        Sync();
    }

    /// <summary>[S-16747] A lasso drawn from <paramref name="from"/> to <paramref name="to"/> (surface DIPs) and left standing, for a check: its box shown (null: none).</summary>
    internal DipRect? LassoForCheck(Point from, Point to)
    {
        Lasso(from, to, []);
        return _band.Visibility == Visibility.Visible && _band.Width > 0 && _band.Height > 0
            ? new DipRect(Canvas.GetLeft(_band), Canvas.GetTop(_band), _band.Width, _band.Height)
            : null;
    }

    /// <summary>[S-16747] The lasso put away after a check drew it.</summary>
    internal void EndLassoForCheck() => EndPress();

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
#if DEBUG
        if (EditReleased(e))
        {
            return;
        }
#endif

        var press = _press;
        var banded = _banding;
        _input.ReleasePointerCapture(e.Pointer);
        EndPress();
        e.Handled = true;
        if (press is not { } p || banded || p.Extending)
        {
            return;
        }

        Clicked(p.Card, p.Kept, p.At, now: false);
    }

    /// <summary>
    /// [S-16722] A click (a press let go where it was) on the card <paramref name="card"/> (null: empty room at
    /// <paramref name="at"/>): the press picked it, and that is all (owner, 2026-11-23: "when i click a widget it should
    /// bascially not do anything to it"): nothing moves, fades or appears. A click of a pick of several narrows it
    /// (<paramref name="kept"/>, [S-1593]). <paramref name="now"/>: kept for a check's click, which is the pointer's.
    /// </summary>
    private void Clicked(string? card, bool kept, Point at, bool now)
    {
        _ = at;
        _ = now;
        if (card is { } narrowed && kept)
        {
            _selection.Click(_screen, narrowed);
            Sync();
        }
    }

    /// <summary>[S-16556] A click on the card <paramref name="id"/> as the pointer makes one (pressed and let go at its middle), at once: a check's.</summary>
    internal void ClickForCheck(string id)
    {
        if (CardOf(id) is not { } card)
        {
            return;
        }

        TakeKeys();
        var kept = _selection.Keeps(_screen, id);
        _selection.Press(_screen, id);
        _cursor = _placed.TryGetValue(id, out var tile) ? tile.Tile : null;
        Sync();
        Clicked(id, kept, new Point(Canvas.GetLeft(card) + (card.Width / 2), Canvas.GetTop(card) + (card.Height / 2)), now: true);
    }

    /// <summary>[S-16557] A press on the cards takes the keyboard out of the search box: the keys are the gallery's again.</summary>
    private void TakeKeys()
    {
        if (_search.HasKeys)
        {
            _commands.TakeKeys();
        }
    }

    /// <summary>[S-16722] A click on empty room of the well, at once: a check's.</summary>
    internal void ClickEmptyForCheck() => Clicked(null, kept: false, new Point(-1, -1), now: true);

    private void EndPress()
    {
        _press = null;
        _banding = false;
        _band.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// [S-14913] [S-17161] The wheel over the well scrolls the sheet shown, a step a notch; it never changes the category. Tilted
    /// (a horizontal wheel or a touchpad's swipe), with Shift, or over a sheet that only scrolls across, it scrolls across.
    /// </summary>
    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        var props = e.GetCurrentPoint(_stage).Properties;
        var delta = props.MouseWheelDelta;
        if (delta != 0 && _following is null)
        {
            var notches = Math.Max(1, (int)Math.Round(Math.Abs(delta) / 120.0)) * WidgetGallery.Step;
            if (props.IsHorizontalMouseWheel)
            {
                ScrollAcross(_scrollX + (delta > 0 ? notches : -notches));
            }
            else if (e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift) || (MostScroll == 0 && MostScrollAcross > 0))
            {
                ScrollAcross(_scrollX + (delta > 0 ? -notches : notches));
            }
            else
            {
                ScrollTo(_scroll + (delta > 0 ? -notches : notches));
            }

            e.Handled = true;
        }
    }

    /// <summary>
    /// [S-14912] [S-17223] The widgets picked with <paramref name="id"/>: itself, every widget of the ready-made group it is of,
    /// or every widget of the curated group its card touches (owner, 2026-11-24: groups "by connecting the widgets in the editor
    /// together without space"): picked, dragged and placed as one block. While the owner edits, each widget alone.
    /// </summary>
    private IEnumerable<string> Mates(string id)
    {
        if (!_placed.TryGetValue(id, out var at) || _laid is not { } sheet)
        {
            return [id];
        }

        if (sheet.Tiles[at.Tile].Group is { } group && !Editing)
        {
            return _placed.Where(p => sheet.Tiles[p.Value.Tile].Group == group).OrderBy(p => p.Value.Tile).Select(p => p.Key);
        }

        return sheet.Tiles[at.Tile].Piece.Group is null ? [id] : _placed.Where(p => p.Value.Tile == at.Tile).Select(p => p.Key);
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

#if DEBUG
        PlacePickGrip();
#endif
    }

    /// <summary>The gallery's cards' host: the display's screen and blurred wallpaper, the pick as the cards' selection, nothing persisted.</summary>
    private sealed class Host(Services.Ticker ticker, WidgetGalleryView gallery) : PreviewCardHost(ticker), IWidgetCardHost
    {
        protected override ScreenMetrics? Screen => gallery._metrics.IsUsable ? gallery._metrics : null;

        /// <summary>[S-16724] The quick toggles show this PC's own state.</summary>
        public bool ReadsLive => true;

        public override Backdrop? Backdrop => gallery._backdrop;
    }
}
