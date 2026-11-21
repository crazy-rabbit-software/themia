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
/// that you show with the widgets already arranged / shown like it would be on a themia view, the widgets are grouped into
/// the categories like weather already there, when you want to place a widget you basically just drag it onto the desktop
/// from that preview."): over the display's work area, under its edit bar, a calm ground (the display's blurred wallpaper
/// under Windows' sheet) with the display's own grid on it, and on it a screen of real cards on their samples
/// (<see cref="SampleWorld"/>, <see cref="SampleCards"/>), each kind at its natural size on the display's cells, laid by
/// <see cref="WidgetGallery"/>: a cell of the gallery is a cell of the desktop. The categories' rail (the add page's) and
/// the arrows walk the screens; the edit mode's selection rules pick cards (<see cref="WidgetSelection"/>, <see cref="SelectionRules"/>: a press,
/// Ctrl or Shift, the lasso) marked as the locked desktop marks them (a card's corners, the frame's round several); the
/// panel on the right says what is picked and offers one widget's styles as small live cards. Dragging the pick hands it
/// to the surface (<see cref="WidgetGalleryCommands.Drag"/>), which starts the one placement session; while it is aimed
/// the gallery steps aside and the cards picked up follow the ghost (<see cref="Follow"/>) from exactly where they stood.
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
    private readonly TextBlock _pages = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
    private readonly StackPanel _tabRow;
    private readonly Border _panel = new();
    private readonly StackPanel _panelBody = new() { Spacing = Fluent.Space2 };
    private readonly List<WidgetCard> _cards = [];
    private readonly List<WidgetCard> _styleCards = [];
    private readonly Host _host;

    private ScreenMetrics _metrics;
    private double _raster = 1;
    private double _contentScale = 1;
    private DipRect _work;
    private CellBox _area;
    private IReadOnlyList<GalleryScreen> _screens = [];
    private int _at;
    private ViewState _screen = Empty();

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

        _sheet.Background = ShellBrush.Of(ShellRole.Flyout);
        _ground.Children.Add(_wallpaper);
        _ground.Children.Add(_sheet);
        // A press on the ground is the gallery's: it never reaches the desktop under it (no lasso of hidden widgets).
        _ground.Background = Fluent.Clear();
        _ground.PointerPressed += (_, e) => e.Handled = true;

        foreach (var plate in new[] { _marks.Halos, _marks.Inks, _marks.Lifts, _marks.Overhangs })
        {
            _markPlates.Children.Add(plate);
        }

        // Every category in sight at once, its name and how many widgets it holds: the add page's rail.
        _rail = new AddRail([.. _categories.Select(c => new AddRailItem(c.Id, c.Title, c.Glyph, c.Blocks.Sum(b => b.Kinds.Count)))], ShowCategory);
        _railScroll = new ScrollViewer
        {
            Content = _rail,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _pages.VerticalAlignment = VerticalAlignment.Center;
        _pages.Margin = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0);
        _tabRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Height = G.Tabs,
            Children = { Arrow(Glyphs.ChevronLeft, Str.WidgetGallery_Previous.Text(), back: true), _pages, Arrow(Glyphs.ChevronRight, Str.WidgetGallery_Next.Text(), back: false) },
        };
        UiLanguage.ApplyFlow(_railScroll);
        UiLanguage.ApplyFlow(_tabRow);

        _panel.Background = ShellBrush.Of(ShellRole.CardFill);
        _panel.BorderBrush = ShellBrush.Of(ShellRole.CardStroke);
        _panel.BorderThickness = new Thickness(FamilyShape.Hairline);
        _panel.CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius);
        _panel.Padding = new Thickness(G.PanelPad);
        _panel.Child = new ScrollViewer { Content = _panelBody, HorizontalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        UiLanguage.ApplyFlow(_panel);

        _stage.Children.Add(_ground);
        _stage.Children.Add(_grid);
        _stage.Children.Add(_titles);
        _stage.Children.Add(_cardLayer);
        _stage.Children.Add(_markPlates);
        _stage.Children.Add(_input);
        _stage.Children.Add(_band);
        _stage.Children.Add(_railScroll);
        _stage.Children.Add(_tabRow);
        _stage.Children.Add(_panel);
        Children.Add(_stage);

        _input.PointerPressed += OnPressed;
        _input.PointerMoved += OnMoved;
        _input.PointerReleased += OnReleased;
        _input.PointerCaptureLost += (_, _) => EndPress();
        _input.PointerWheelChanged += OnWheel;
        _ground.PointerWheelChanged += OnWheel;
        if (_backdrop is not null)
        {
            _backdrop.Changed += OnBackdrop;
        }

        AutomationProperties.SetName(this, Str.WidgetGallery_Entry.Text());
    }

    /// <summary>The screens laid for the display it stands on, in the tabs' order.</summary>
    internal IReadOnlyList<GalleryScreen> Screens => _screens;

    /// <summary>The screen shown.</summary>
    internal int At => _at;

    /// <summary>The widgets of the screen shown, on their cells: what a drag picks up.</summary>
    internal ViewState Screen => _screen;

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

        // The ground over the work area, the display's blurred wallpaper under Windows' sheet ([S-14911]).
        SetBox(_ground, work);
        _ground.Clip = new RectangleGeometry { Rect = new Rect(0, 0, work.Width, work.Height) };
        _wallpaper.Margin = new Thickness(-work.X, -work.Y, 0, 0);
        _wallpaper.Width = metrics.Width;
        _wallpaper.Height = metrics.Height;
        _wallpaper.HorizontalAlignment = HorizontalAlignment.Left;
        _wallpaper.VerticalAlignment = VerticalAlignment.Top;
        if (!same)
        {
            OnBackdrop();

            // The display's own grid, as edit mode draws it under the cards ([S-06c]): a cell here is a cell there.
            _grid.Width = metrics.Width;
            _grid.Height = metrics.Height;
            _grid.Draw(metrics, rasterScale);
            _grid.Clip = new RectangleGeometry { Rect = new Rect(work.X, work.Y, work.Width, work.Height) };
        }

        // [S-14911] The rail down the left, the pager over the panel on the right, the cards between.
        var top = barBottom + ShellMetrics.Flyout.Gap;
        var bottom = work.Y + work.Height - G.Margin;
        var railLeft = work.X + G.Margin;
        Canvas.SetLeft(_railScroll, railLeft);
        Canvas.SetTop(_railScroll, top);
        _railScroll.Width = G.Rail;
        _railScroll.Height = Math.Max(0, bottom - top);
        var left = railLeft + G.Rail + G.Margin;
        var right = work.X + work.Width - G.Margin;
        var panelLeft = right - G.Panel;

        _tabRow.Measure(new Size(double.PositiveInfinity, G.Tabs));
        var pager = Math.Ceiling(_tabRow.DesiredSize.Width);
        Canvas.SetLeft(_tabRow, panelLeft + G.Panel - pager);
        Canvas.SetTop(_tabRow, top);

        var panelTop = top + G.Tabs + ShellMetrics.Flyout.Gap;
        Canvas.SetLeft(_panel, panelLeft);
        Canvas.SetTop(_panel, panelTop);
        _panel.Width = G.Panel;
        _panel.Height = Math.Max(0, bottom - panelTop);

        // [S-14912] The cards' room in whole cells of the display's grid, inside the work area and the part new geometry may use.
        var (cols, rows) = GridGeometry.GridDims(metrics);
        var (pitchX, pitchY) = (metrics.Width / cols, metrics.Height / rows);
        var x0 = Math.Max(left, allowed.X + G.Margin);
        var y0 = Math.Max(top, allowed.Y + G.Margin);
        var x1 = Math.Min(panelLeft - G.Margin, allowed.X + allowed.Width - G.Margin);
        var y1 = Math.Min(bottom, allowed.Y + allowed.Height - G.Margin);
        var cx = (int)Math.Ceiling((x0 / pitchX) - 1e-9);
        var cy = (int)Math.Ceiling((y0 / pitchY) - 1e-9);
        var area = new CellBox(cx, cy, Math.Max(1, (int)Math.Floor((x1 / pitchX) + 1e-9) - cx), Math.Max(1, (int)Math.Floor((y1 / pitchY) + 1e-9) - cy));
        SetBox(_input, new DipRect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0)));

        if (same && area == _area && _screens.Count > 0)
        {
            return;
        }

        _area = area;
        var category = _screens.Count > 0 ? _screens[Math.Clamp(_at, 0, _screens.Count - 1)].Category : (AddFolderId?)null;
        _screens = WidgetGallery.Lay(_categories, area, Cells);
        Show(category is { } shown ? WidgetGallery.FirstOf(_screens, shown) : 0);
    }

    /// <summary>[S-14912] A kind's natural cells on this display: what placing it new makes (<see cref="PlacementPayload.NewSize"/>).</summary>
    private (int W, int H) Cells(WidgetId kind)
    {
        var natural = ModuleCatalog.Of(kind).Natural;
        var box = PlacementPayload.NewSize(kind, natural.Width, natural.Height, _metrics, new PlacementSizes(_contentScale));
        var (cols, rows) = GridGeometry.GridDims(_metrics);
        return ((int)Math.Round(box.W * cols), (int)Math.Round(box.H * rows));
    }

    /// <summary>[S-14913] Show screen <paramref name="at"/>: its title, its families' names and its cards, nothing picked.</summary>
    public void Show(int at)
    {
        if (_disposed || _screens.Count == 0)
        {
            return;
        }

        _at = Math.Clamp(at, 0, _screens.Count - 1);
        var screen = _screens[_at];
        DropCards();
        _selection.Clear();
        _selected.Clear();

        var (cols, rows) = GridGeometry.GridDims(_metrics);
        FracRect Rect(CellBox box) => new(box.X / (double)cols, box.Y / (double)rows, box.W / (double)cols, box.H / (double)rows);

        // The screen's widgets: new widgets on the cells they stand on, a style picked here kept on their kind.
        var view = Empty();
        foreach (var placed in screen.Cards)
        {
            var widget = WidgetInstance.Create(placed.Kind, Rect(placed.Box));
            if (_styles.TryGetValue(placed.Kind, out var style))
            {
                WidgetStyles.Set(widget.Config, style);
            }

            view.Widgets.Add(widget);
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
                Log.Warn(Scope, $"the gallery's {widget.Kind} card could not be built; the screen shows the rest", ex);
            }
        }

        WidgetCard.Join(_cards, _metrics, _raster, editMode: false, _ => 0);
        DrawTitles(screen, Rect);
        _rail.Choose(screen.Category);
        _pages.Text = Str.WidgetGallery_Pages.Format(("page", LocaleFormat.Integer(_at + 1)), ("count", LocaleFormat.Integer(_screens.Count)));
        Sync();
        var cards = _cards.ToList();
        _ = SampleCards.ShowAsync(cards, () => _disposed || !_cards.SequenceEqual(cards), Scope);
    }

    /// <summary>[S-14913] The next or the previous screen, round the ends.</summary>
    public void Step(bool back) => Show(WidgetGallery.Step(_at, _screens.Count, back));

    /// <summary>[S-14913] A category's first screen (its tab).</summary>
    public void ShowCategory(AddFolderId category) => Show(WidgetGallery.FirstOf(_screens, category));

    /// <summary>
    /// [S-14913] [S-14914] The gallery's keys: Left/Right and Page Up/Down walk the screens, Home and End the first and last,
    /// Ctrl+A picks the screen's every card, Escape lets the pick go (with nothing picked it is not the gallery's: it closes).
    /// </summary>
    public bool HandleKey(VirtualKey key, bool ctrl)
    {
        if (_disposed || _following is not null)
        {
            return false;
        }

        switch (key)
        {
            case VirtualKey.Left or VirtualKey.PageUp:
                Step(back: FlowDirection != FlowDirection.RightToLeft || key == VirtualKey.PageUp);
                return true;
            case VirtualKey.Right or VirtualKey.PageDown:
                Step(back: FlowDirection == FlowDirection.RightToLeft && key == VirtualKey.Right);
                return true;
            case VirtualKey.Home:
                Show(0);
                return true;
            case VirtualKey.End:
                Show(_screens.Count - 1);
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
        foreach (var part in new UIElement[] { _ground, _grid, _titles, _markPlates, _railScroll, _tabRow, _panel, _band })
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
        foreach (var part in new UIElement[] { _ground, _grid, _titles, _markPlates, _railScroll, _tabRow, _panel, _band })
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

    private Button Arrow(string glyph, string said, bool back)
    {
        var button = new Button
        {
            Content = UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = ShellMetrics.Bar.Icon }),
            Width = ShellMetrics.Bar.IconOnly,
            Height = G.Tabs,
            Padding = new Thickness(0),
        };
        PageTabButton.Subtle(button);
        ToolTipService.SetToolTip(button, said);
        AutomationProperties.SetName(button, said);
        button.Click += (_, _) => Step(back);
        return button;
    }

    /// <summary>[S-14912] The screen's title line (the category, its page of how many) and each family's name over its cards.</summary>
    private void DrawTitles(GalleryScreen screen, Func<CellBox, FracRect> rect)
    {
        _titles.Children.Clear();
        var title = GridGeometry.PaintedCell(rect(screen.Title), _metrics, _raster);
        var name = Fluent.Text(FamilyType.Title);
        name.Text = screen.Name.Text();
        var count = Fluent.Text(FamilyType.Body, "TextFillColorSecondaryBrush");
        count.Text = screen.Pages > 1 ? Str.WidgetGallery_Pages.Format(("page", LocaleFormat.Integer(screen.Page + 1)), ("count", LocaleFormat.Integer(screen.Pages))) : string.Empty;
        name.VerticalAlignment = VerticalAlignment.Center;
        count.VerticalAlignment = VerticalAlignment.Center;
        count.Margin = new Thickness(Fluent.Space3, 0, 0, 0);
        var line = new StackPanel { Orientation = Orientation.Horizontal, Children = { name, count }, Width = title.Width, Height = title.Height };
        UiLanguage.ApplyFlow(line);
        Canvas.SetLeft(line, title.X);
        Canvas.SetTop(line, title.Y);
        _titles.Children.Add(line);

        foreach (var head in screen.Heads)
        {
            var box = GridGeometry.PaintedCell(rect(head.Box), _metrics, _raster);
            var words = Fluent.Text(FamilyType.BodyStrong);
            words.Text = head.Text.Text();
            words.TextTrimming = TextTrimming.CharacterEllipsis;
            words.Width = box.Width;
            words.Height = box.Height;
            words.VerticalAlignment = VerticalAlignment.Top;
            UiLanguage.ApplyFlow(words);
            Canvas.SetLeft(words, box.X);
            Canvas.SetTop(words, box.Y);
            _titles.Children.Add(words);
        }
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
                _selection.Toggle(ScreenViewId, card);
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

    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(_stage).Properties.MouseWheelDelta;
        if (delta != 0 && _following is null)
        {
            Step(back: delta > 0);
            e.Handled = true;
        }
    }

    /// <summary>[S-14914] The pick drawn (a card's corners; several: the frame's round them) and said in the panel.</summary>
    private void Sync()
    {
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
        switch (picked.Count)
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
                Add(Fluent.Text(FamilyType.Body, "TextFillColorSecondaryBrush"), string.Join(Environment.NewLine, picked.Select(w => ModuleCatalog.Of(w.Kind).TileName)));
                break;
        }

        var drag = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
        drag.Margin = new Thickness(0, Fluent.Space4, 0, 0);
        Add(drag, Str.WidgetGallery_Drag.Text());
        var cards = _styleCards.ToList();
        _ = SampleCards.ShowAsync(cards, () => _disposed || !_styleCards.SequenceEqual(cards), Scope);

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
