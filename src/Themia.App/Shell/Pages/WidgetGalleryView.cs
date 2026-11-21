using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
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

/// <summary>[S-14915] [S-14916] What the gallery asks of its surface.</summary>
/// <param name="Drag">Widgets of the screen shown (<c>screen</c>, a view of new widgets on their cells) picked up at a point of the surface (DIPs): the placement starts.</param>
internal sealed record WidgetGalleryCommands(Action<ViewState, IReadOnlyList<string>, Point> Drag);

/// <summary>
/// [S-14910]..[S-14917] The widget gallery (owner, 2026-11-21: "on the independent page it should bascially be a dashboard
/// that you show with the widgets already arranged / shown like it would be on a themia view ... when you want to place a
/// widget you basically just drag it onto the desktop from that preview."; the same day: "lets just show complete widgets
/// / groups, not the small atoms. scrolling should not scroll the tabs but a tabs contents should be scrollable ... they
/// should be shown as a bento"; "it should also just be inside a settings window"; "the tabs and the widget explanation
/// should be more differnt from the page and not just merge into it"). A window in the middle of the display's work area
/// under its edit bar (<see cref="WidgetGallery.Frame"/>): the categories' rail on a pane of its own, the panel of what
/// is picked a card of its own, and between them a well, the display's blurred wallpaper dimmed with the display's own
/// grid on it, in which the category shown stands as a bento of real cards on their samples (<see cref="SampleWorld"/>,
/// <see cref="SampleCards"/>; <see cref="WidgetGallery.Lay"/>): whole widgets sized to their tiles and ready-made groups
/// as the panels they place, every card on the display's cells: a cell of the gallery is a cell of the desktop. A
/// category taller than the well is scrolled, by whole cells (the wheel, the keys, the scroll bar); only the rail changes
/// the category. The edit mode's selection rules pick pieces (<see cref="WidgetSelection"/>, <see cref="SelectionRules"/>:
/// a press, Ctrl or Shift, the lasso; a group is picked whole) marked as the locked desktop marks them; the panel says
/// what is picked and offers one widget's styles as small live cards. Dragging the pick hands it to the surface
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
    private readonly IReadOnlyList<GalleryCategory> _categories;
    private readonly Services.Ticker _ticker;
    private readonly SampleWorld _samples;
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private readonly WidgetSelection _selection = new();
    private readonly Dictionary<WidgetId, int> _styles = [];

    private readonly Canvas _stage = UiLanguage.KeepLeftToRight(new Canvas());
    private readonly Grid _ground = new();
    private readonly Image _wallpaper = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private readonly Border _sheet = new() { IsHitTestVisible = false };
    private readonly SnapGridOverlay _grid = new() { IsHitTestVisible = false };
    private readonly Canvas _titles = new() { IsHitTestVisible = false };
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

    /// <summary>[S-14913] The categories as the add page's rail (its one component, [S-13610]), down the gallery's left.</summary>
    private readonly AddRail _rail;
    private readonly ScrollViewer _railScroll;

    /// <summary>[S-14911] The window: Windows' sheet, its rim and its lift, under everything of the gallery.</summary>
    private readonly Border _window = new();

    /// <summary>[S-14911] The rail's own pane, apart from the window's ground.</summary>
    private readonly Border _railPane = new();

    /// <summary>[S-14911] The well's rounded box: the ground the cards stand on is clipped to it.</summary>
    private readonly Border _wellBox = new();

    /// <summary>[S-14911] The well's rim, over the cards.</summary>
    private readonly Border _wellRim = new() { IsHitTestVisible = false };
    private readonly TextBlock _title = Fluent.Text(FamilyType.Title);

    /// <summary>[S-14913] The well's scroll bar: shown while the category is taller than the well.</summary>
    private readonly Microsoft.UI.Xaml.Controls.Primitives.ScrollBar _scrollBar = new()
    {
        Orientation = Orientation.Vertical,
        IndicatorMode = Microsoft.UI.Xaml.Controls.Primitives.ScrollingIndicatorMode.MouseIndicator,
        Visibility = Visibility.Collapsed,
    };
    private readonly Border _panel = new();
    private readonly StackPanel _panelBody = new() { Spacing = Fluent.Space2 };
    private readonly List<WidgetCard> _cards = [];
    private readonly List<WidgetCard> _styleCards = [];
    private readonly Host _host;

    private ScreenMetrics _metrics;
    private double _raster = 1;
    private double _contentScale = 1;
    private DipRect _work;
    private GalleryFrame? _frame;

    /// <summary>[S-14912] The cards' room in the well, whole cells of the display's grid.</summary>
    private CellBox _view;

    /// <summary>[S-14913] The category shown (its place in <see cref="_categories"/>), its bento and how far it is scrolled, in rows of cells.</summary>
    private int _at;
    private GallerySheet? _laid;
    private int _scroll;
    private ViewState _screen = Empty();

    /// <summary>[S-14912] Each widget of the sheet shown: the tile it belongs to and its box on the sheet (cells from its corner).</summary>
    private readonly Dictionary<string, (int Tile, CellBox Box)> _placed = new(StringComparer.Ordinal);

    /// <summary>A press on the cards' room: where (surface DIPs), on which card (null: empty room), and how.</summary>
    private (Point At, string? Card, bool Kept, bool Extending, List<string> Base)? _press;
    private bool _banding;
    private List<WidgetCard>? _following;
    private bool _disposed;

    public WidgetGalleryView(WidgetGalleryCommands commands, Backdrop? backdrop, DesignState design, IReadOnlyList<GalleryCategory> categories)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _categories = categories ?? throw new ArgumentNullException(nameof(categories));
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

        // [S-14915] The rail and the panel are sheets of their own on that ground, the well a ground of its own with a rim:
        // none merges into the window (owner, 2026-11-21: "the tabs and the widget explanation should be more differnt
        // from the page and not just merge into it").
        _railPane.Background = ShellBrush.Of(ShellRole.CardFill);
        _railPane.BorderBrush = ShellBrush.Of(ShellRole.CardStroke);
        _railPane.BorderThickness = new Thickness(FamilyShape.Hairline);
        _railPane.CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius);
        _railPane.Padding = new Thickness(Fluent.Space1);

        _sheet.Background = ShellBrush.Of(ShellRole.CardBase);
        _ground.Children.Add(_wallpaper);
        _ground.Children.Add(_sheet);
        _ground.Background = ShellBrush.Of(ShellRole.CardBase);
        _wellBox.CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius);
        _wellBox.Child = _ground;
        _wellRim.CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius);
        _wellRim.BorderBrush = ShellBrush.Of(ShellRole.CardStroke);
        _wellRim.BorderThickness = new Thickness(FamilyShape.Hairline);
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        UiLanguage.ApplyFlow(_title);

        foreach (var plate in new[] { _marks.Halos, _marks.Inks, _marks.Lifts, _marks.Overhangs })
        {
            _markPlates.Children.Add(plate);
        }

        // Every category in sight at once, its name and how many widgets it holds: the add page's rail.
        _rail = new AddRail([.. _categories.Select(c => new AddRailItem(c.Id, c.Title, c.Glyph, c.Pieces.Count))], ShowCategory);
        _railScroll = new ScrollViewer
        {
            Content = _rail,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _railPane.Child = _railScroll;
        UiLanguage.ApplyFlow(_railPane);
        _scrollBar.ValueChanged += (_, e) =>
        {
            // Its own range being set (a category shown) is no scroll of the user's.
            if (!_settingBar)
            {
                ScrollTo((int)Math.Round(e.NewValue));
            }
        };

        _panel.Background = ShellBrush.Of(ShellRole.CardFill);
        _panel.BorderBrush = ShellBrush.Of(ShellRole.CardStroke);
        _panel.BorderThickness = new Thickness(FamilyShape.Hairline);
        _panel.CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius);
        _panel.Padding = new Thickness(G.PanelPad);
        _panel.Child = new ScrollViewer { Content = _panelBody, HorizontalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        UiLanguage.ApplyFlow(_panel);

        _stage.Children.Add(_window);
        _stage.Children.Add(_railPane);
        _stage.Children.Add(_titles);
        _stage.Children.Add(_wellBox);
        _stage.Children.Add(_grid);
        _stage.Children.Add(_cardLayer);
        _stage.Children.Add(_markPlates);
        _stage.Children.Add(_input);
        _stage.Children.Add(_band);
        _stage.Children.Add(_wellRim);
        _stage.Children.Add(_scrollBar);
        _stage.Children.Add(_panel);
        _titles.Children.Add(_title);
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

    /// <summary>The categories, in the rail's order.</summary>
    internal IReadOnlyList<GalleryCategory> Categories => _categories;

    /// <summary>The category shown.</summary>
    internal GalleryCategory Category => _categories[Math.Clamp(_at, 0, Math.Max(0, _categories.Count - 1))];

    /// <summary>[S-14912] The category shown as it is laid: its tiles on the sheet.</summary>
    internal GallerySheet? Sheet => _laid;

    /// <summary>[S-14911] The window's parts on the display, and the cards' room in the well.</summary>
    internal GalleryFrame? Frame => _frame;

    /// <summary>[S-14913] How far the category shown is scrolled, and how far it scrolls, in rows of cells.</summary>
    internal int Scrolled => _scroll;

    internal int MostScroll => _laid is { } sheet ? WidgetGallery.MaxScroll(sheet.Rows, _view.H) : 0;

    /// <summary>The widgets of the category shown, on the cells they stand on now: what a drag picks up.</summary>
    internal ViewState Screen => _screen;

    /// <summary>[S-14912] The widgets of the piece <paramref name="id"/> belongs to: itself, or its ready-made group's every widget.</summary>
    internal IReadOnlyList<string> PieceOf(string id) => [.. Mates(id)];

    /// <summary>The widgets picked, in the order they were picked.</summary>
    internal IReadOnlyList<string> Picked => _selection.Ids;

    /// <summary>[S-14916] The cards picked up by the placement under way: they follow its ghost.</summary>
    internal bool Following => _following is not null;

    /// <summary>The card of a widget of the screen shown, for a check.</summary>
    internal WidgetCard? CardOf(string id) => _cards.Find(c => string.Equals(c.Model.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// [S-14911] Stand over <paramref name="metrics"/>' display: its work area <paramref name="work"/> (DIPs), the bar's foot
    /// <paramref name="barBottom"/>, the part new geometry may use <paramref name="allowed"/> (DIPs), the raster scale and the
    /// content scale new widgets get there. The screens are laid again only when one of them changed.
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

        // [S-14911] The window in the middle of the work area's room under the bar, its parts inside it.
        var (cols, rows) = GridGeometry.GridDims(metrics);
        var frame = WidgetGallery.Frame(work, barBottom, allowed, metrics.Width / cols, metrics.Height / rows);
        var moved = frame != _frame;
        _frame = frame;
        SetBox(_window, frame.Window);
        SetBox(_railPane, frame.Rail);
        SetBox(_titles, frame.Head);
        _title.Width = frame.Head.Width;
        _title.Height = frame.Head.Height;
        SetBox(_panel, frame.Panel);

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
        Show(_at);
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

    /// <summary>
    /// [S-14912] What a piece asks of the bento on this display: a widget its natural cells, the least its sizing takes
    /// and the axes it may be another length on; a ready-made group the cells it is laid at, wider but never smaller or
    /// taller (its rows would grow unevenly, and its lines of text with them).
    /// </summary>
    private PieceSize Size(GalleryPiece piece)
    {
        var (cols, rows) = GridGeometry.GridDims(_metrics);
        if (piece.Group is { } group)
        {
            var laid = ReadyMadeGroups.Laid(group, _metrics, new PlacementSizes(_contentScale));
            var w = Math.Max(1, (int)Math.Round(laid.Max(r => r.Right) * cols));
            var h = Math.Max(1, (int)Math.Round(laid.Max(r => r.Bottom) * rows));
            return new PieceSize(w, h, w, h, Wider: true, Taller: false);
        }

        var kind = piece.Kind!.Value;
        var (nw, nh) = Cells(kind);
        var limits = SizeFit.Limits(SizeFit.Of(kind, _styles.TryGetValue(kind, out var style) ? style : null), _metrics, _contentScale);
        if (limits is null)
        {
            return new PieceSize(nw, nh, nw, nh, false, false);
        }

        return new PieceSize(nw, nh, (int)Math.Ceiling((limits.Width.Min * cols) - 1e-6), (int)Math.Ceiling((limits.Height.Min * rows) - 1e-6), !limits.Width.Fixed, !limits.Height.Fixed);
    }

    /// <summary>[S-14912] A kind's natural cells on this display: what placing it new makes (<see cref="PlacementPayload.NewSize"/>).</summary>
    private (int W, int H) Cells(WidgetId kind)
    {
        var natural = ModuleCatalog.Of(kind).Natural;
        var box = PlacementPayload.NewSize(kind, natural.Width, natural.Height, _metrics, new PlacementSizes(_contentScale));
        var grid = GridGeometry.GridDims(_metrics);
        return (grid.Col(box.W), grid.Row(box.H));
    }

    /// <summary>
    /// [S-14912] [S-14913] Show category <paramref name="at"/>: its name over the well and its bento in it from its top,
    /// nothing picked. Every piece is built, the ones under the well's foot too: scrolling moves cards, it builds none.
    /// </summary>
    public void Show(int at)
    {
        if (_disposed || _categories.Count == 0 || !_metrics.IsUsable)
        {
            return;
        }

        _at = Math.Clamp(at, 0, _categories.Count - 1);
        var category = _categories[_at];
        DropCards();
        _selection.Clear();
        _selected.Clear();
        _placed.Clear();
        _scroll = 0;

        var (cols, rows) = GridGeometry.GridDims(_metrics);
        var sheet = WidgetGallery.Lay(category, _view.W, Size);
        _laid = sheet;

        // The category's widgets: new widgets on the cells of their tiles, a style picked here kept on their kind.
        var view = Empty();
        for (var t = 0; t < sheet.Tiles.Count; t++)
        {
            var tile = sheet.Tiles[t];
            if (tile.Piece.Group is { } group)
            {
                // [S-2471] A ready-made group as it is placed: its widgets with their own settings, laid as one block.
                var laid = ReadyMadeGroups.Laid(group, _metrics, new PlacementSizes(_contentScale));
                if (ReadyMadeGroups.Build(group, _metrics) is not { } made || made.Count != laid.Count)
                {
                    continue;
                }

                // [S-2460] Made as wide as its tile as one block, every member in its own sizing and every edge on a cell;
                // where its common step stops it short, it stands in its tile's middle. Its height is its own.
                var (lw, lh) = (laid.Max(r => r.Right), laid.Max(r => r.Bottom));
                SizeLimits?[] limits = [.. made.Select(m => SizeFit.Limits(m, _metrics, _contentScale))];
                var grown = BlockResize.Resize(laid, limits, ResizeHandle.Right, (tile.Tile.W / (double)cols) - lw, 0, _metrics);
                var (gw, gh) = ((int)Math.Round(grown.Max(r => r.Right) * cols), (int)Math.Round(grown.Max(r => r.Bottom) * rows));
                if (grown.Count == laid.Count && gw <= tile.Tile.W && gh <= tile.Tile.H && grown.Min(r => r.X) > -1e-9 && grown.Min(r => r.Y) > -1e-9)
                {
                    laid = grown;
                }
                else
                {
                    (gw, gh) = ((int)Math.Round(lw * cols), (int)Math.Round(lh * rows));
                }

                var (ox, oy) = (tile.Tile.X + Math.Max(0, (tile.Tile.W - gw) / 2), tile.Tile.Y + Math.Max(0, (tile.Tile.H - gh) / 2));
                for (var i = 0; i < made.Count; i++)
                {
                    var box = new CellBox(ox + (int)Math.Round(laid[i].X * cols), oy + (int)Math.Round(laid[i].Y * rows),
                        Math.Max(1, (int)Math.Round(laid[i].W * cols)), Math.Max(1, (int)Math.Round(laid[i].H * rows)));
                    made[i].Rect = RectOf(box);
                    _placed[made[i].Id] = (t, box);
                    view.Widgets.Add(made[i]);
                }
            }
            else if (tile.Piece.Kind is { } kind)
            {
                var widget = WidgetInstance.Create(kind, RectOf(tile.Box));
                if (_styles.TryGetValue(kind, out var style))
                {
                    WidgetStyles.Set(widget.Config, style);
                }

                // Its tile's size, held to what its sizing takes on this display: what placing it then keeps.
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
                Log.Warn(Scope, $"the gallery's {widget.Kind} card could not be built; the category shows the rest", ex);
            }
        }

        // [S-2240] Cards that touch are one panel: a group's own; pieces stand a cell apart and never join.
        WidgetCard.Join(_cards, _metrics, _raster, editMode: false, _ => 0);
        _title.Text = category.Title.Text();
        _rail.Choose(category.Id);
        ShowScroll();
        Sync();
        var cards = _cards.ToList();
        _ = SampleCards.ShowAsync(cards, () => _disposed || !_cards.SequenceEqual(cards), Scope);
    }

    /// <summary>[S-14912] A box of the sheet as it stands on the display now: from the view's corner, scrolled.</summary>
    private FracRect RectOf(CellBox box)
    {
        var (cols, rows) = GridGeometry.GridDims(_metrics);
        return new FracRect((_view.X + box.X) / (double)cols, (_view.Y + box.Y - _scroll) / (double)rows, box.W / (double)cols, box.H / (double)rows);
    }

    /// <summary>
    /// [S-14913] Scroll the category shown to <paramref name="rows"/> rows of cells from its top (held to its ends): every
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

        EndPress();
        ShowScroll();
        Sync();
    }

    /// <summary>[S-14913] The scroll bar: there while the category is taller than the well, its thumb the part in sight.</summary>
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

    /// <summary>[S-14913] A category's bento (its tab on the rail).</summary>
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
    /// [S-14913] [S-14914] The gallery's keys: Up/Down scroll the category by a tile, Page Up/Down by a page, Home and End
    /// to its top and its foot (none changes the category: that is the rail's), Ctrl+A picks its every piece, Escape lets
    /// the pick go (with nothing picked it is not the gallery's: it closes).
    /// </summary>
    public bool HandleKey(VirtualKey key, bool ctrl)
    {
        if (_disposed || _following is not null)
        {
            return false;
        }

        switch (key)
        {
            case VirtualKey.Up:
                ScrollTo(_scroll - WidgetGallery.Step);
                return true;
            case VirtualKey.Down:
                ScrollTo(_scroll + WidgetGallery.Step);
                return true;
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
                Sync();
                return true;
            default:
                return false;
        }
    }

    /// <summary>[S-14914] Pick widgets of the screen shown, as a lasso or Ctrl+A would (a check's, and the keys').</summary>
    internal void Pick(IReadOnlyList<string> ids)
    {
        _selection.Replace(ScreenViewId, ids);
        Sync();
    }

    /// <summary>[S-14915] Choose a style for the one widget picked, as its preview's click does.</summary>
    internal void ChooseStyle(WidgetId kind, int style)
    {
        _styles[kind] = style;
        foreach (var card in _cards.Where(c => c.Model.Kind == kind))
        {
            if (WidgetStyles.Set(card.Model.Config, style))
            {
                card.ApplyConfig();
            }
        }

        Sync();
    }

    /// <summary>
    /// [S-14916] The placement of <paramref name="ids"/> is under way: the gallery steps aside (its ground, grid, words,
    /// panel and every other card gone; nothing of it takes the pointer) and the cards picked up stay, to follow the ghost.
    /// </summary>
    public void BeginFollow(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
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
    /// [S-14916] The placement is over (landed or given up): the gallery comes back as it was, every card on its cell
    /// (replenished: what was placed is still here, owner: "the settings menu should obviously replenish the widgets").
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
    }


    /// <summary>[S-14916] Everything of the gallery but its cards: what steps aside while a drag off it is aimed.</summary>
    private UIElement[] Chrome => [_window, _railPane, _titles, _wellBox, _wellRim, _grid, _markPlates, _scrollBar, _panel, _band];

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_backdrop is not null)
        {
            _backdrop.Changed -= OnBackdrop;
        }

        DropCards();
        DropStyleCards();
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

    // ---- [S-14914] picking -----------------------------------------------------------------------

    private string? CardAt(Point at)
        => SelectionRules.PressTarget([.. _cards.Select(c => (c.Model.Id, new DipRect(Canvas.GetLeft(c), Canvas.GetTop(c), c.Width, c.Height)))], _selected, at.X, at.Y, 0);

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_stage);
        if (!point.Properties.IsLeftButtonPressed || _following is not null)
        {
            return;
        }

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
        if (_press is { Card: { } card, Kept: true, Extending: false } && !_banding)
        {
            // [S-1593] A click on a card of a pick of several narrows it to that card.
            _selection.Click(_screen, card);
            Sync();
        }

        _input.ReleasePointerCapture(e.Pointer);
        EndPress();
        e.Handled = true;
    }

    private void EndPress()
    {
        _press = null;
        _banding = false;
        _band.Visibility = Visibility.Collapsed;
    }

    /// <summary>[S-14913] The wheel over the well scrolls the category shown, a tile a notch; it never changes the category.</summary>
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

    /// <summary>[S-14914] The pick drawn (a card's corners; several: the frame's round them) and said in the panel.</summary>
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

        FillPanel();
    }

    // ---- [S-14915] the panel ---------------------------------------------------------------------

    private string? _panelFor;

    /// <summary>[S-14915] What is picked: one widget's name, what it is and its styles; several, how many and their names; none, how to pick.</summary>
    private void FillPanel()
    {
        var key = string.Join(",", _selection.Ids) + "|" + string.Join(",", _styles.Select(s => $"{s.Key.Value}={s.Value}"));
        if (key == _panelFor)
        {
            return;
        }

        _panelFor = key;
        DropStyleCards();
        _panelBody.Children.Clear();
        var picked = SelectionRules.InView(_screen, _selection.Ids).Select(id => _screen.Find(id)!).ToList();
        var pieces = picked.Where(w => _placed.ContainsKey(w.Id)).Select(w => _placed[w.Id].Tile).Distinct().Select(t => _laid!.Tiles[t].Piece).ToList();
        if (pieces is [{ Group: { } group }])
        {
            // [S-14915] A ready-made group: its name, what it is and how many widgets land together.
            var said = AddTileWords.Of(new AddGroupTile(group));
            var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };
            top.Children.Add(Fluent.Icon(said.Glyph, "TextFillColorPrimaryBrush", P.IconGlyph));
            var title = Fluent.Text(FamilyType.BodyStrong);
            title.Text = said.Name;
            top.Children.Add(title);
            _panelBody.Children.Add(top);
            Add(Fluent.Text(FamilyType.Body, "TextFillColorSecondaryBrush"), said.About);
            Add(Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush"), Str.WidgetGallery_Group.Plural(group.Widgets.Count));
            Foot();
            return;
        }

        switch (pieces.Count == 1 ? 1 : picked.Count)
        {
            case 0:
                Add(Fluent.Text(FamilyType.BodyStrong), Str.WidgetGallery_None.Text());
                Add(Fluent.Text(FamilyType.Body, "TextFillColorSecondaryBrush"), Str.WidgetGallery_NoneAbout.Text());
                break;
            case 1:
                var widget = picked[0];
                var words = AddTileWords.Of(new AddWidgetTile(widget.Kind));
                var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };
                head.Children.Add(Fluent.Icon(words.Glyph, "TextFillColorPrimaryBrush", P.IconGlyph));
                var name = Fluent.Text(FamilyType.BodyStrong);
                name.Text = words.Name;
                head.Children.Add(name);
                _panelBody.Children.Add(head);
                Add(Fluent.Text(FamilyType.Body, "TextFillColorSecondaryBrush"), words.About);
                if (WidgetStyles.Offers(widget.Kind))
                {
                    var styles = Fluent.Text(FamilyType.BodyStrong);
                    styles.Margin = new Thickness(0, Fluent.Space4, 0, 0);
                    Add(styles, Str.WidgetGallery_Styles.Text());
                    _panelBody.Children.Add(Styles(widget));
                }

                break;
            default:
                Add(Fluent.Text(FamilyType.BodyStrong), Str.WidgetGallery_Several.Plural(picked.Count));
                Add(Fluent.Text(FamilyType.Body, "TextFillColorSecondaryBrush"), string.Join(Environment.NewLine,
                    pieces.Select(p => p.Group is { } g ? g.Name.Text() : ModuleCatalog.Of(p.Kind!.Value).TileName)));
                break;
        }

        Foot();

        void Foot()
        {
            var drag = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
            drag.Margin = new Thickness(0, Fluent.Space4, 0, 0);
            Add(drag, Str.WidgetGallery_Drag.Text());
            var cards = _styleCards.ToList();
            _ = SampleCards.ShowAsync(cards, () => _disposed || !_styleCards.SequenceEqual(cards), Scope);
        }

        void Add(TextBlock text, string words)
        {
            text.Text = words;
            text.TextWrapping = TextWrapping.Wrap;
            _panelBody.Children.Add(text);
        }
    }

    /// <summary>[S-14915] One widget's styles, each a small live card of it in that style over its name; the one it has marked.</summary>
    private Grid Styles(WidgetInstance widget)
    {
        var grid = new Grid { ColumnSpacing = G.StyleGap, RowSpacing = G.StyleGap };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var current = WidgetStyles.Current(widget.Config);
        var options = WidgetStyles.For(widget.Kind);
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            if (i % 2 == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var tile = StyleTile(widget.Kind, option, option.Value == current);
            Grid.SetRow(tile, i / 2);
            Grid.SetColumn(tile, i % 2);
            grid.Children.Add(tile);
        }

        return grid;
    }

    private Button StyleTile(WidgetId kind, StyleOption option, bool chosen)
    {
        var box = new Grid { Height = G.StyleHeight, HorizontalAlignment = HorizontalAlignment.Stretch };
        try
        {
            var sizing = SizeFit.Of(kind, option.Value).Natural;
            var model = WidgetInstance.Create(kind, FracRect.FromDips(0, 0, sizing.Width, sizing.Height, _metrics));
            WidgetStyles.Set(model.Config, option.Value);
            var card = new WidgetCard(_host, model) { Width = sizing.Width, Height = sizing.Height };
            card.SetLiveRect(model.Rect);
            card.ApplyEditMode(false);
            _styleCards.Add(card);

            // A picture of the style: the tile takes the press, nothing in the card does.
            box.Children.Add(new Viewbox
            {
                IsHitTestVisible = false,
                Child = card,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                MaxWidth = G.StyleWidth,
                MaxHeight = G.StyleHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"the {kind} card in its {option.Name} style could not be built; its tile shows its glyph", ex);
            box.Children.Add(Fluent.Icon(char.ConvertFromUtf32(option.Glyph), "TextFillColorSecondaryBrush", ShellMetrics.AddPage.PreviewGlyph));
        }

        var label = Fluent.Text(FamilyType.Caption, chosen ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        label.Text = option.Label.Text();
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        // Its rim on a border of its own: the button's rim is a subtle button's (none), the chosen style's the accent.
        var rim = new Border
        {
            Child = new StackPanel { Spacing = Fluent.Space1, Children = { box, label } },
            Padding = new Thickness(Fluent.Space2),
            CornerRadius = new CornerRadius(P.CardRadius),
            BorderThickness = new Thickness(chosen ? FamilyShape.Stroke : FamilyShape.Hairline),
            BorderBrush = chosen ? Family.Brush(ColourRole.Accent) : ShellBrush.Of(ShellRole.CardStroke),
        };
        var tile = new Button
        {
            Content = rim,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(P.CardRadius),
            Tag = "style:" + option.Name,
        };
        PageTabButton.Subtle(tile);
        AutomationProperties.SetName(tile, option.Label.Text());
        AutomationProperties.SetItemStatus(tile, chosen ? "selected" : string.Empty);
        tile.Click += (_, _) => ChooseStyle(kind, option.Value);
        return tile;
    }

    private void DropCards()
    {
        foreach (var card in _cards)
        {
            card.Detach();
            _cardLayer.Children.Remove(card);
        }

        _cards.Clear();
        _marks.Frame.Hide();
    }

    private void DropStyleCards()
    {
        foreach (var card in _styleCards)
        {
            card.Detach();
        }

        _styleCards.Clear();
    }

    /// <summary>The gallery's cards' host: the display's screen and blurred wallpaper, the pick as the cards' selection, nothing persisted.</summary>
    private sealed class Host(Services.Ticker ticker, WidgetGalleryView gallery) : PreviewCardHost(ticker)
    {
        protected override ScreenMetrics? Screen => gallery._metrics.IsUsable ? gallery._metrics : null;

        public override Backdrop? Backdrop => gallery._backdrop;

    }
}
