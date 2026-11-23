using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>What a card needs from the surface it lives on.</summary>
internal interface IWidgetCardHost
{
    ScreenMetrics Metrics { get; }

    DesignState Design { get; }

    Ticker Ticker { get; }

    bool EditMode { get; }

    /// <summary>The card moved or resized and is still being dragged: reposition it, do not re-layout everything.</summary>
    void OnGeometryPreview(WidgetCard card);

    /// <summary>The drag ended: persist.</summary>
    void OnGeometryCommitted(WidgetCard card);

    /// <summary>
    /// [S-1862] The card's Set up button: go where what the widget waits for is set (its properties
    /// menu in edit mode, or settings › Accounts when an account is all it lacks).
    /// </summary>
    void RequestSetup(WidgetCard card);

    /// <summary>
    /// [S-3558] A widget's way to its accounts (its "Open Accounts settings", a status's sign-in
    /// problem): settings › Accounts on this display, edit mode entered. False where there is none (a render).
    /// </summary>
    bool RequestAccounts();

    void RequestDelete(WidgetCard card);

    void RequestEditMode(bool enabled);

    /// <summary>[S-12950] Whether Lock widgets is on (the card menu's line is checked while it is).</summary>
    bool WidgetsLocked => false;

    /// <summary>[S-12951] The card menu's Lock widgets line: the one setting on or off.</summary>
    void RequestLockWidgets(bool on)
    {
        // ignore: a debug render's cards have no setting to flip
    }

    /// <summary>[S-12950] Whether Lock widgets keeps this card where it is on the locked desktop (<see cref="Core.Desktop.WidgetLock.Pins"/>).</summary>
    bool Pins(WidgetCard card) => false;

    bool IsSelected(WidgetCard card);

    /// <summary>
    /// [S-14310] How many widgets the selection's frame this card is one of goes round (<see cref="SelectionRules.Framed{T}"/>:
    /// two or more), 0 when no frame holds it: a member of a frame of several shows no marks of its own.
    /// </summary>
    int FramedWith(WidgetCard card) => 0;

    void RequestSelect(WidgetCard card);

    void RequestCopy(WidgetCard card);

    void RequestCut(WidgetCard card);

    void RequestPaste();

    /// <summary>The view these cards belong to, so a card can ask where it sits in the paint order.</summary>
    ViewState? ViewForCards { get; }

    bool CanPaste { get; }

    /// <summary>A widget changed its own config: persist it without re-running layout.</summary>
    void RequestSaveConfig();

    /// <summary>
    /// [S-801] A text box in the widget gained or lost focus. The surface is normally unfocusable, so
    /// this — the keyboard lease, held for typing — is what makes typing into a widget reach it at all.
    /// </summary>
    void RequestTextInput(WidgetCard card, bool wanted);

    /// <summary>
    /// [S-800] The user pressed somewhere in this card outside edit mode: the keyboard lease moves to
    /// it when its view takes keys (<see cref="IWidgetKeys"/>), and ends for whichever widget had it
    /// otherwise.
    /// </summary>
    void KeyboardPress(WidgetCard card);

    /// <summary>
    /// [S-11879] A Ctrl or Shift press on this card outside edit mode: it goes into the desktop's one selection by the
    /// icons' rules (Ctrl toggles, Shift adds the range). Returns whether the selection took it (a preview host never does).
    /// </summary>
    bool DeskPick(WidgetCard card, bool ctrl, bool shift) => false;

    /// <summary>[S-1251] True for a debug render's host: its cards are previews and must not act on the machine.</summary>
    bool IsPreview => false;

    /// <summary>
    /// [S-11851] What this host's cards read in place of this PC (the add page's pictures: the sample PC's
    /// hub, their own gallery service), handed to them explicitly; null for every desktop surface, whose
    /// cards read the app's own. Only a preview host has one.
    /// </summary>
    SampleWorld? Samples => null;

    /// <summary>
    /// [S-2118] The blurred wallpaper of the display these cards are on, shared by all of them, or null
    /// where there is none (most debug renders): the cards then draw without it ([S-2111]).
    /// </summary>
    Backdrop? Backdrop => null;

    /// <summary>[S-3300] The links of the view these cards belong to: one board per view id, whichever display shows it.</summary>
    Themia.Core.Links.LinkBoard? Links => ViewForCards is { } view ? AppServices.Links?.For(view.Id) : null;

    /// <summary>
    /// [S-873] The card's drawn frame changed — hidden or shown — so it is positioned and joined
    /// again. Nothing is persisted.
    /// </summary>
    void OnCardFrameChanged(WidgetCard card)
    {
        // ignore: a host with no layout of its own (a debug render) has nothing to redo
    }

    /// <summary>[S-874] Bring the widget to the front of the view, or send it to the back.</summary>
    void RequestZOrder(WidgetCard card, bool front)
    {
        // ignore: a host with no view (a debug render) has no order to change
    }

    /// <summary>[S-1580] Ctrl+click: add the card to the selection, or take it out.</summary>
    void RequestToggleSelect(WidgetCard card) => RequestSelect(card);

    /// <summary>How many widgets are selected, so the card menu can say what its commands act on.</summary>
    int SelectionCount => 1;

    /// <summary>
    /// [S-15833] Outside edit mode: how many things the card menu acts on, for its row: the desktop's one selection's count
    /// (icons and cards) when it holds the card, else 1 (a card Lock widgets holds is in none, [S-12953]).
    /// </summary>
    int DeskSelectionCount(WidgetCard card) => SelectionCount;

    /// <summary>
    /// [S-15832] The card menu's Widget settings (outside edit mode): edit mode entered and the card's page opened, or with
    /// the card one of several selected on the desktop the page of several.
    /// </summary>
    void RequestSettings(WidgetCard card)
    {
        // ignore: a debug render's cards have no page to open
    }

    /// <summary>[S-5524] Whether this card's Delete takes desktop items' widgets off the view only ("Remove from this view").</summary>
    bool DeleteRemovesFromView(WidgetCard card) => Themia.Core.Desktop.DesktopBackground.RemovesFromView([card.Model.Config]);

    /// <summary>
    /// [S-14321] Open this card's menu at <paramref name="at"/> (a point of the card; null: from the keys) the way the
    /// desktop's own menus open (the surface's menu anchor, at the pointer). False where there is no surface (a render).
    /// </summary>
    bool OpenCardMenu(WidgetCard card, Point? at) => false;

    /// <summary>[S-14321] Outside edit mode: how the card menu's Delete line reads (what Del does to the desktop's one selection); None: no line.</summary>
    (Themia.Core.Desktop.DeleteWords Words, int Count) DeskDeleteLine(WidgetCard card) => (Themia.Core.Desktop.DeleteWords.None, 0);

    /// <summary>[S-14321] Outside edit mode: the desktop's background menu's lines, for a card menu at <paramref name="at"/> (surface DIPs).</summary>
    IReadOnlyList<ItemMenuEntry> DesktopLines(WidgetCard card, Point at) => [];

    /// <summary>
    /// [S-2343] The pointer that pressed this card let go (a click, the end of a move or of a resize), or
    /// its press could not be held after all: nothing moves under the pointer any more, and in edit mode the
    /// host opens the selection's page.
    /// </summary>
    void PressEnded(WidgetCard card)
    {
        // ignore: a host with no page (a debug render) has nothing to open
    }

    /// <summary>[S-1584] A move of this card starts: it and every other selected card move together.</summary>
    void BeginMove(WidgetCard card)
    {
        // ignore: a debug render's cards do not move
    }

    /// <summary>[S-1584] The pointer moved by a fraction of the screen since the move began; the block lands on the grid ([S-06]).</summary>
    void PreviewMove(WidgetCard card, double dxFrac, double dyFrac)
    {
        // ignore: as above
    }

    /// <summary>
    /// [S-11878] Whether the move this card holds (a desktop icon's, outside edit mode) is to become the
    /// shell's drag now: the pointer left the desktop or is over a drop target on it.
    /// </summary>
    bool DragsOut(WidgetCard card) => false;

    /// <summary>[S-11878] The move ends without landing (it became the shell's drag): every moving card back where it started, nothing stored.</summary>
    void CancelMove(WidgetCard card)
    {
        // ignore: a debug render's cards do not move
    }

    /// <summary>[S-1584]/[S-1591] The move ended: persist it, or hand the widgets to another display.</summary>
    void CommitMove(WidgetCard card)
    {
        // ignore: as above
    }

    /// <summary>
    /// [S-1593] A press on the card in edit mode: the card alone, or a selection that holds it kept so the drag
    /// carries all of it. Returns whether the selection changed; one that kept it lets the release decide
    /// (<see cref="RequestClick"/>).
    /// </summary>
    bool RequestPress(WidgetCard card, bool onHandle)
    {
        RequestSelect(card);
        return true;
    }

    /// <summary>[S-1593] Released without moving after a press that kept the selection: narrow to the card.</summary>
    void RequestClick(WidgetCard card) => RequestSelect(card);
}

/// <summary>
/// One widget on the desktop: the Fluent card chrome, the drag and resize affordances, and the
/// right-click menu. The widget's own view is dropped inside and knows nothing about input.
/// </summary>
internal sealed partial class WidgetCard : Grid, IWidgetSurface, IItemPaletteOwner, IGridCellHost
{
    private const string Scope = "card";

    private readonly IWidgetCardHost _host;

    /// <summary>[S-6913] What the view may ask of the host, handed to its module when it is built.</summary>
    private readonly IWidgetHost _widgetHost;

    /// <summary>[S-2000] The last press reached the card unhandled, outside edit mode: its tap may open.</summary>
    private bool _openArmed;

    /// <summary>[S-2008] A left press on the locked desktop that may become the card's move, and where it was (the surface's DIPs).</summary>
    private bool _moveArmed;
    private Point _moveStart;

    /// <summary>[S-6512] The item fills of this widget's view, from its own item layer or the app-wide one.</summary>
    private readonly ItemPalette _items = new();

    /// <summary>[S-2100] The card's one fill, across the whole card.</summary>
    private readonly Border _fill;
    private readonly Border _surface;

    /// <summary>
    /// [S-824] The border, drawn as a path because a <see cref="Border"/> cannot dash; a path, not a
    /// rectangle, because a card joined to its neighbours leaves sides out ([S-2243]).
    /// </summary>
    private readonly Microsoft.UI.Xaml.Shapes.Path _stroke;

    /// <summary>
    /// [S-826] Carries the design's font to the content: text inherits a font family from the
    /// nearest presenter or control above it, and a <see cref="Border"/> has none to give.
    /// </summary>
    private readonly ContentPresenter _fontScope;

    /// <summary>[S-821] The text size the text already on this card was laid out at.</summary>
    private double _textScale = FamilyType.TextScale;

    /// <summary>[S-873] The view asked to paint nothing (outside edit mode).</summary>
    private bool _hidden;

    /// <summary>The widget's view, or its error card ([S-891]) when the view failed. Swapped by a retry.</summary>
    private FrameworkElement _view;

    /// <summary>The view and what the card lays over it: what [S-850]'s content scale magnifies.</summary>
    private readonly Grid _content;

    private double _contentScale = 1.0;

    private FracRect _dragStartRect;
    private Point _dragStartPoint;

    /// <summary>[S-78] [S-13046] The move's look: going to another display, a ghost where it lands.</summary>
    private bool _leaving;
    private bool _ghost;
    private ResizeHandle? _activeHandle;

    /// <summary>[S-1593] The press landed on a card of a multi-selection: a click without a drag narrows the selection to it.</summary>
    private bool _narrowOnRelease;

    /// <summary>Whether the pointer has travelled far enough since the press to count as a drag ([S-1745]).</summary>
    private bool _moved;

    private bool _dragging;

    public WidgetCard(IWidgetCardHost host, WidgetInstance model)
    {
        _host = host;
        _widgetHost = new CardWidgetHost(host);
        Model = model;
#if DEBUG
        Gallery.LiveCensus.CardBuilt(this);
#endif

        // [S-6512] The item fills are right before the view paints its first item, and what the view
        // paints while the card is built (it attaches in here too) is drawn from them.
        _items.Apply(ItemLook.Effective(host.Design, model), ItemLook.TextColour(host.Design, model), DesignApplier.IsDark, host.Design.Tokens ?? new LookTokens());
        using var building = ItemPalette.Building(_items);

        // [S-891] Guarded: a view that throws while being built becomes an error card, not a hole.
        _view = CreateView(model.Kind);

        // [S-12480] No card has a header: a widget is its content, and a title is a Header widget of its
        // own ([S-9600]). [S-891] A view that throws here, or on the first tick it asks for, is swapped for its
        // error card before the card is assembled, so the card is built around whichever it is.
        // [S-6583] Aligned first, so the first layout it does sits where the widget asks.
        ApplyAlign(building: true);
        AttachView();

        // Fill and content are separate borders so the surface opacity setting fades the card
        // without fading the text on it.
        _fill = new Border
        {
            // [S-27c] The opaque base runs under the stroke, not just inside it. Fluent's card stroke
            // is a ~10% overlay meant to sit on something; WinUI's default paints a border's
            // background only inside the stroke, so the outer pixel of every card was a nearly clear
            // ring over the wallpaper. Two neighbours at a 0px gap read as a visible gap between them.
            BackgroundSizing = BackgroundSizing.OuterBorderEdge,
        };

        // No padding: the content area goes edge to edge and each view insets its own parts. A card
        // that padded everything pushed every list row away from the edge and stopped its hover
        // highlight short of it, so a row read as a box inside a box rather than as a row ([S-6711]:
        // a row carries the side inset itself). The card gives the view's top its inset ([S-5700]),
        // joined or alone ([S-12483]).
        var content = new Grid();

        // [S-2103] The widget's fill is one surface across the whole card.
        // [S-6590] The background image under everything, across the whole card.
        // [S-2140] The grain over the material, under the content.
        // [S-16640] A view's own grounds in place of the card's, under the content.
        // [S-11963] The card lit as one item (view links), over the material and under the view.
        foreach (var layer in new FrameworkElement[] { _image, _fill, _glass, _lens, _grain, _grounds, _linkFill })
        {
            content.Children.Add(layer);
        }

        content.Children.Add(_view);

        // The card's content never draws past its cell over a neighbour.
        content.SizeChanged += (_, args) => content.Clip = new RectangleGeometry { Rect = new Rect(0, 0, args.NewSize.Width, args.NewSize.Height) };

        _content = content;

        // [S-1862] Over the view, once the content grid exists to hold it.
        AttachSetup();

        // [S-13975] What the view draws past the card stands on its own plate, over every card.
        BuildOverhang();

        _fontScope = new ContentPresenter
        {
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };

        // [S-2114] The blurred wallpaper under everything the card paints, outside the content's scale
        // ([S-850]) and inside the surface's rounded clip, so the picture, the fill over it and the
        // border all meet the same corner.
        _surface = new Border
        {
            Child = new Grid { Children = { _frost, _fontScope } },
            Padding = new Thickness(0),
        };
        AttachBackdrop();

        // Over the content, as a CSS border sits round it: a full-bleed chart would otherwise cover
        // the line along its edges. Never hit-testable, so it cannot take a click from the widget.
        _stroke = new Microsoft.UI.Xaml.Shapes.Path { IsHitTestVisible = false };

        _surface.SizeChanged += (_, _) =>
        {
            FitContent();
            DrawStroke();
        };

        // [S-1113] The card's content (the view, its menus) flows with the language; the resize
        // grips are physical corners and keep their places, so a drag on the left grip still moves
        // the left edge.
        UiLanguage.ApplyFlow(this);
        _markers = UiLanguage.KeepLeftToRight(BuildMarkers());

        Children.Add(_glassLift);
        Children.Add(_surface);
        Children.Add(_dividers);
        Children.Add(_stroke);
        Children.Add(_markers);

        Background = Family.Brush(ColourRole.Clear); // makes the whole card hit-testable for drags

        // Focusable so that clicking a widget gives the surface keyboard focus. Without something
        // focusable in the tree no KeyDown is raised at all, and the edit-mode shortcuts never fire.
        IsTabStop = true;
        UseSystemFocusVisuals = false;
        ApplyEdges();
        ApplyDesign();
        ApplyEditMode(host.EditMode);

        // [S-663] A view that discloses by size is told its slot - the card less its insets - once
        // per layout pass that changed it. The view's own SizeChanged is that signal exactly: it is
        // raised after arrange, only when the slot really changed (a resize, a join), and the slot is
        // the cell of a fixed-size card, so nothing the view shows in response can feed back into it.
        HookViewSize();

        PointerPressed += OnPointerPressed;
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), handledEventsToo: HearsHandledMoves);
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        RightTapped += OnRightTapped;
        Tapped += OnTapped;

        // [S-800] Handled presses too: a click on a row's button or a list item is still a click in
        // this widget, and the controls inside mark their presses handled.
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnLeasePointerPressed), handledEventsToo: true);
        GettingFocus += OnGettingFocus;
    }

    public WidgetInstance Model { get; }

    /// <inheritdoc />
    public ItemPalette Items => _items;

    /// <summary>The widget's own view, so the surface can ask what it takes (a drop, keys).</summary>
    public FrameworkElement View => _view;

    /// <summary>The rect the card is currently showing, which during a drag leads the persisted model.</summary>
    public FracRect LiveRect { get; private set; }

    // -----------------------------------------------------------------------------------------
    // Appearance
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-5700] [S-12483] Lay the view under the card's top: at the family's inset, none where the view owns the
    /// edge (<see cref="CardContent.TopInset"/>). Never the joins: a card joined to another lays its content out
    /// exactly as alone. Re-run on every config and view swap.
    /// </summary>
    public void ApplyEdges()
    {
        // [S-12835] Under the card's compact Set up line the view keeps its face: it stands under the line's whole cells.
        // [S-15814] A plot with nothing written at its top keeps the whole card: the line stands over it.
        var edge = _view as IWidgetTopEdge;
        var under = edge is { OwnsTopEdge: true, SetupOver: true } ? 0 : SetupBand;
        SetupUnder = under;
        _view.Margin = _view.Margin with { Top = CardContent.TopInset(edge?.OwnsTopEdge == true) + under };

        // [S-1862] The Set up and waiting rows never own the edge.
        if (_setup is not null)
        {
            _setup.Margin = OverlayTop;
        }

        if (_waiting is not null)
        {
            _waiting.Margin = OverlayTop;
        }
    }

    /// <summary>[S-15814] The band the view stands under: the Set up line's, none where the line stands over a plot.</summary>
    internal double SetupUnder { get; private set; }

    /// <summary>[S-12835] [S-6754] The band the card's compact Set up line takes over the view (its whole cells); none without it.</summary>
    internal double SetupBand => _setup is not null ? LineRules.EdgeRow(new ItemGrid(CellPitch, XamlRoot?.RasterizationScale ?? 1)) : 0;

    /// <summary>[S-1862] [S-5700] Where a row the card lays over the view starts: at the inset.</summary>
    private static Thickness OverlayTop => new(0, CardContent.TopInset(ownsTopEdge: false), 0, 0);

    /// <summary>[S-873] Whether the card draws anything: a hidden one draws only in edit mode.</summary>
    public bool IsShown(bool editMode) => !_hidden || editMode;

    /// <summary>
    /// Paint the card from the design: corner radius, fill colour and opacity, border and
    /// font. The one per-card apply path ([S-820]..[S-829]); the app-wide half (theme, accent, text
    /// size) is <see cref="DesignApplier"/>. Runs on every state change, and only sets properties, so
    /// a slider dragged across the Appearance page re-paints cards without rebuilding any.
    /// </summary>
    public void ApplyDesign()
    {
        var design = _host.Design;
        // [S-11880] A desktop icon on no card is not clipped to a card's corners: its highlight keeps its own four.
        var radiusPx = CardJoins.ClipRadius(ModuleCatalog.Of(Model.Kind).Traits, DrawsSurface, DesignLook.CornerRadius(design.CornerRadiusPx));

        // [S-2241] Round only where no joined neighbour covers the corner.
        var radius = Corners(radiusPx);

        // [S-6510] The second layer: the view's items, on their own colour whatever the card's fill.
        var items = _preview?.Items is { } previewed ? FillChoice.Effective(design.ItemBackground ?? new FillChoice(), previewed) : ItemLook.Effective(design, Model);
        _items.Apply(items, ItemLook.TextColour(design, Model), DesignApplier.IsDark, design.Tokens ?? new LookTokens());

        // The content border is rounded too, so anything drawn edge to edge inside it - a meter's
        // graph reaches the bottom and both sides - is clipped to the card's corners instead of
        // squaring them off.
        _surface.CornerRadius = radius;

        // [S-2250] The layers inside that clip take the radius all round or not at all, never one a corner.
        var inside = InsideCorners(radiusPx);
        _insideRadius = inside.TopLeft;
        ApplyViewCorners();

        // [S-823] [S-2100] The card paints its own fill, one colour at one opacity: Themia's neutral
        // card for the theme by default, the user's colour exactly otherwise. The surface is genuinely
        // transparent, so what shows through it is the desktop.
        // [S-6592] Over a background image only a colour of the widget's own is painted, as a tint.
        ApplyImage(inside);
        BindBackdrop();
        var cardFill = CardFillNow();
        var fill = PaintedFillNow();
        ApplyFrost(fill);

        _fill.CornerRadius = inside;
        _fill.Background = fill.Painted && fill.Colour is { } colour ? DesignApplier.UserBrush(HexColor.From(colour.R, colour.G, colour.B)) : null;
        _fill.Opacity = fill.Painted ? fill.Opacity : 1;

        // [S-824] The border: style, width, colour and opacity, over the content.
        var border = DesignLook.Border(cardFill, design.BorderStyle, design.BorderWidthPx, design.BorderOpacity);
        if (border.Visible && (!_hidden || _host.EditMode))
        {
            _stroke.Visibility = Visibility.Visible;
            // [S-6481] The user's colour exactly, else the theme stroke for this weight: the card
            // hairline only for a solid 1px line, the strong stroke for anything heavier or broken.
            // [S-2102] The theme's hairline is Themia's own pair, a colour at its alpha.
            var ink = DesignLook.Ink(border, design.BorderColor, DesignApplier.IsDark);
            _stroke.Stroke = (ink.Colour is { } line ? DesignApplier.UserBrush(HexColor.From(line.R, line.G, line.B), ink.Alpha) : null)
                ?? ThemeBrush.Of(ink.ThemeKey ?? DesignLook.StrongStrokeKey, Colors.Gray);
            _stroke.StrokeThickness = border.Thickness;
            _stroke.Opacity = border.Opacity;
            _strokeLook = (border.Thickness, radiusPx);
            DrawStroke();

            var dashes = new DoubleCollection();
            foreach (var dash in border.Dashes)
            {
                dashes.Add(dash);
            }

            _stroke.StrokeDashArray = dashes;
            _stroke.StrokeDashCap = border.RoundCaps ? PenLineCap.Round : PenLineCap.Flat;
            _stroke.StrokeStartLineCap = _stroke.StrokeEndLineCap = _stroke.StrokeDashCap;
        }
        else
        {
            _stroke.Visibility = Visibility.Collapsed;
        }

        // [S-2123] Liquid glass: its tint, sheen, lens, rim and lift over what the design painted.
        ApplyGlass(inside, radiusPx, fill);

        // [S-2133] [S-2140] The accent in a default card, the grain.
        ApplyLook(inside, fill);

        // [S-16640] A view drawn as several cards: its grounds in place of the card's own.
        ApplyGrounds(radiusPx);

        // [S-826] The design's font for everything in the card that does not pick its own; icons
        // name Segoe Fluent Icons themselves, so their glyphs are untouched.
        if (DesignApplier.ContentFont is { } font)
        {
            _fontScope.FontFamily = font;
        }
        else
        {
            _fontScope.ClearValue(ContentPresenter.FontFamilyProperty);
        }
    }

    /// <summary>[S-6563] What this card's background paints: its own choice (or one previewed, [S-3731]), or the app-wide one for Default.</summary>
    private CardFill CardFillNow()
        => DesignLook.Card(FillChoice.Effective(_host.Design.Background, _preview?.Background ?? Model.Background), DesignApplier.IsDark, Frosted);

    /// <summary>[S-3731] A fill the selection's page's slider shows while it is dragged; null: the card's own.</summary>
    private CardPreview? _preview;

    /// <summary>
    /// [S-3731] Show a background or item fill not yet stored while a slider of the selection's page
    /// is dragged, or the card's own again (null). Paints only: nothing is stored or rebuilt. The
    /// content scale is the surface's (<see cref="ApplyContentScale"/>).
    /// </summary>
    public void Preview(CardPreview? preview)
    {
        _preview = preview;
        ApplyDesign();
    }

    /// <summary>
    /// [S-873] Draw the card or nothing. Hidden, the fill, content and border collapse (the view
    /// stays attached and keeps receiving its updates, so it can show itself again); edit mode
    /// always draws it, as it is when it shows (owner, 2026-11-21: "in edit mode they should appear normally"), so it
    /// can be found, placed and set.
    /// </summary>
    private void ApplyHidden()
    {
        var editing = _host.EditMode;
        var visibility = !_hidden || editing ? Visibility.Visible : Visibility.Collapsed;

        _image.Visibility = visibility;
        _fill.Visibility = visibility;
        _surface.Visibility = visibility;
        _grounds.Visibility = _groundsOn ? visibility : Visibility.Collapsed;
    }

    /// <summary>
    /// [S-821] Bring text already on the card to the current text size, before the view lays itself
    /// out again from the (already scaled) type ramp. Text a view builds from the ramp is right by
    /// construction; this catches what it built once — labels — so the card follows
    /// the setting without being rebuilt. Only sizes set on the element itself are scaled; text
    /// that inherits its size follows its parent.
    /// </summary>
    private void ApplyTextScale()
    {
        var scale = FamilyType.TextScale;
        if (Math.Abs(scale - _textScale) < 0.0001)
        {
            return;
        }

        var ratio = scale / _textScale;
        _textScale = scale;
        Rescale(_content, ratio);
    }

    private static void Rescale(DependencyObject node, double ratio)
    {
        switch (node)
        {
            case TextBlock text when text.ReadLocalValue(TextBlock.FontSizeProperty) != DependencyProperty.UnsetValue:
                text.FontSize *= ratio;
                if (text.LineHeight > 0)
                {
                    text.LineHeight *= ratio;
                }

                break;

            case RichTextBlock rich when rich.ReadLocalValue(RichTextBlock.FontSizeProperty) != DependencyProperty.UnsetValue:
                rich.FontSize *= ratio;
                break;

            case Control control when control.ReadLocalValue(Control.FontSizeProperty) != DependencyProperty.UnsetValue:
                control.FontSize *= ratio;
                break;
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            Rescale(VisualTreeHelper.GetChild(node, i), ratio);
        }
    }

    /// <summary>
    /// Show whether this card is the one the keyboard commands will act on. Without it, Ctrl+C in a
    /// view with several widgets is a guess.
    /// </summary>
    public void ApplySelection()
    {
        var editing = _host.EditMode;
        var selected = _host.IsSelected(this);

        // [S-11877] On the locked desktop a selected card shows its four corners alone, in the accent: light,
        // no handles, no grips. [S-11881] A locked move shows the corners of the moving cards and of the ones
        // near where they land. [S-11889] A desktop icon too, round its grid box, over its highlight, but only in edit mode and in a move:
        // selected on the locked desktop it shows its highlight alone. [S-14310] One of a selection's frame of several shows
        // none and its grips take no press: the frame marks and resizes the block.
        _markersShown = SelectionRules.MarksShown(editing, selected, editing ? null : _moveMarks, icon: _view is IDesktopIconView, framed: _host.FramedWith(this));
        _markers.Visibility = editing && _markersShown ? Visibility.Visible : Visibility.Collapsed;

        // [S-1597] A selected card's grips and move area stand above every card, so one under another keeps all of them.
        Lift(editing && selected);
        ApplyResizable();
        FollowPlates();
        PaintMarkers(selected);
    }

    public void ApplyEditMode(bool editing)
    {
        ApplySelection();
        ApplyHidden();
        PaintLinkFill();

        // Outside edit mode the card must not eat pointer input at all: a clock is something you
        // look at, and the desktop underneath stays usable.
        IsHitTestVisible = true;

        // [S-1751] In edit mode the view is inert, all of it, for every widget: presses, right-clicks,
        // long presses and hovers fall through to the card (its transparent background) and select,
        // drag or open the card menu. Each view guarding itself left Mail's message menu reachable.
        _surface.IsHitTestVisible = CardGestures.ContentTakesInput(editing);

        // [S-2363] A cursor made only when edit mode changed for the card.
        if (_cursorEditing != editing)
        {
            _cursorEditing = editing;
            ProtectedCursor = InputSystemCursor.Create(editing ? InputSystemCursorShape.SizeAll : InputSystemCursorShape.Arrow);
        }
    }

    /// <summary>[S-2363] The edit mode the card's cursor was made for; null before the first.</summary>
    private bool? _cursorEditing;

    public void ApplyConfig()
    {
        // Before the view re-lays itself out from the ramp, or its fresh sizes would be scaled twice.
        ApplyTextScale();

        // [S-6583] The alignment lives on the instance; a style change can also make it apply or not.
        ApplyAlign(building: false);

        // [S-891] Guarded like Attach: a view that throws on its new settings becomes its error card.
        ApplyViewConfig();

        // [S-1862] The config may have supplied what was missing, or taken it away.
        ApplySetup();

        // [S-5700] A style may own the top edge, or stop owning it.
        ApplyEdges();
        ApplyDesign();
    }

    /// <summary>
    /// [S-15660] The app-wide design moved and this card's config did not: its text brought to the text size, then painted
    /// again (<see cref="ApplyDesign"/>). The type ramp's and the look tokens' own followers restyle what a view built.
    /// </summary>
    public void ApplyDesignChange()
    {
        ApplyTextScale();
        ApplyDesign();
    }

    public void Detach()
    {
        DetachBackdrop();
        DetachSetup();
        StopView();
        DropGrounds();
        DropPlates();
        DropOverhang();
    }

    public void SetLiveRect(FracRect rect) => LiveRect = rect;

    // -----------------------------------------------------------------------------------------
    // Sizing ([S-2444]..[S-2447])
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-2444] This card's sizing on its display, as fractions of the screen; null before the display
    /// is measured. [S-11807] Whole grid cells, every widget (a desktop icon too, [S-11875]).
    /// </summary>
    public SizeLimits? Limits
        => _host.Metrics is { IsUsable: true } metrics
            ? SizeFit.Limits(SizeFit.Of(Model.Config), metrics, _contentScale)
            : null;

    /// <summary>
    /// [S-12830] The desktop grid's pitch on this card's display, in the card's content units (the content scale
    /// divides it): what the view's items are whole numbers of. The target before the display is measured.
    /// </summary>
    public double CellPitch
        => GridGeometry.CellPitch(_host.Metrics) / (_contentScale > 0 && double.IsFinite(_contentScale) ? _contentScale : 1);

    /// <summary>
    /// [S-2460] A step of the selection's resize as a block: the card shows <paramref name="rect"/>, nothing stored until the
    /// surface lands it ([S-15662]). [S-14502] Its view shows what that box holds: counts follow the card live; a resize
    /// stores the box alone, never a config.
    /// </summary>
    public void PreviewResize(FracRect rect) => LiveRect = rect;

    /// <summary>
    /// [S-850] Magnify the card's content — text, icons, padding — without touching its cell:
    /// the card stays exactly where it is and the same size, and the view lays out in the smaller
    /// (or larger) logical space the scale leaves it, so it discloses less (or more) per [S-663].
    /// </summary>
    public void ApplyContentScale(double scale)
    {
        var next = double.IsFinite(scale) && scale > 0 ? scale : 1.0;
        if (Math.Abs(next - _contentScale) < 0.0001)
        {
            return;
        }

        _contentScale = next;
        FitContent();
        ApplyViewCorners();
    }

    /// <summary>[S-2250] The radius of the layers inside the surface's clip as the last design set it (0: square).</summary>
    private double _insideRadius;

    /// <summary>
    /// [S-11883] A view whose fill reaches the card's edge takes the card's corners, in its own DIPs (the content is
    /// scaled by the content scale): [S-2250] the card's radius all round on a card whose four corners are round, square
    /// on a joined one, where the surface's clip rounds the fill at the corners the card keeps.
    /// </summary>
    private void ApplyViewCorners()
    {
        // The view is built (and attached) before the card's surface exists; the first design brings the corners then.
        if (_view is not IWidgetCorners corners || _surface is null)
        {
            return;
        }

        var s = _contentScale > 0 ? _contentScale : 1;
        corners.ApplyCorners(new CornerRadius(_insideRadius / s));
    }

    /// <summary>
    /// WinUI has no layout transform, so the content is laid out at the painted size divided by the
    /// scale and then render-scaled back up to fill it exactly.
    /// </summary>
    private void FitContent()
    {
        if (Math.Abs(_contentScale - 1.0) < 0.0001)
        {
            _content.Width = double.NaN;
            _content.Height = double.NaN;
            _content.HorizontalAlignment = HorizontalAlignment.Stretch;
            _content.VerticalAlignment = VerticalAlignment.Stretch;
            _content.RenderTransform = null;
            return;
        }

        var width = _surface.ActualWidth;
        var height = _surface.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return; // not laid out yet; SizeChanged brings it back here
        }

        _content.HorizontalAlignment = HorizontalAlignment.Left;
        _content.VerticalAlignment = VerticalAlignment.Top;
        _content.Width = width / _contentScale;
        _content.Height = height / _contentScale;
        _content.RenderTransform = new ScaleTransform { ScaleX = _contentScale, ScaleY = _contentScale };

        // [S-2123] The glass's gradients run in the content's DIPs: drawn again at the new scale.
        if (Glass)
        {
            ApplyDesign();
        }
    }

    /// <summary>[S-1007] The intro's cover over the painted surface, while one plays; otherwise null.</summary>
    private FrameworkElement? _motionCover;

    /// <summary>
    /// [S-1007] Lay an intro's cover (the hacker tile) over the painted surface, under the edit
    /// markers, or take it away with null. Never hit-testable: the widget under it is
    /// live and takes clicks from the first frame ([S-1013]).
    /// </summary>
    public void SetMotionCover(FrameworkElement? cover)
    {
        if (_motionCover is not null)
        {
            Children.Remove(_motionCover);
        }

        _motionCover = cover;
        if (cover is null)
        {
            return;
        }

        cover.IsHitTestVisible = false;
        Children.Insert(Children.IndexOf(_markers), cover);
    }

    // -----------------------------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------------------------

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_host.EditMode)
        {
            LockedPress(
                e.GetCurrentPoint(ParentSurface()).Position,
                e.GetCurrentPoint(this).Properties.IsLeftButtonPressed,
                e.KeyModifiers != Windows.System.VirtualKeyModifiers.None);
            return;
        }

        _openArmed = false;
        _moveArmed = false;

        // [S-1745] What the press starts.
        var handle = HitHandle(e.OriginalSource);
        var press = CardGestures.Press(
            editMode: true,
            leftButton: e.GetCurrentPoint(this).Properties.IsLeftButtonPressed,
            ctrl: IsKeyDown(Windows.System.VirtualKey.Control),
            onHandle: handle is not null);

        switch (press)
        {
            case CardPress.None:
                return;

            case CardPress.ToggleSelect:
                // [S-1580] Ctrl+click picks the widget in or out of the selection and does nothing else.
                _host.RequestToggleSelect(this);
                Focus(FocusState.Pointer);
                e.Handled = true;
                return;
        }

        _activeHandle = handle;

        // [S-1593] A press on a widget already in a multi-selection keeps the selection, so the drag
        // that may follow carries all of it. A resize is about this one widget only.
        _narrowOnRelease = !_host.RequestPress(this, onHandle: _activeHandle is not null) && _activeHandle is null;

        Focus(FocusState.Pointer);
        _dragStartRect = LiveRect;
        _dragStartPoint = e.GetCurrentPoint(ParentSurface()).Position;
        _moved = false;
        _dragging = CapturePointer(e.Pointer);

        if (_dragging && _activeHandle is null)
        {
            _host.BeginMove(this);
        }
        else if (!_dragging)
        {
            // The press could not be held: no release will end it.
            _host.PressEnded(this);
        }

        e.Handled = _dragging;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(ParentSurface());
        if (!_dragging && !LockedMove(point.Position, point.Properties.IsLeftButtonPressed, () => TakePointer(e)))
        {
            return;
        }

        if (Follow(point.Position) && _view is IDesktopIconView icon)
        {
            DragOut(e, icon);
        }

        e.Handled = true;
    }

    /// <summary>
    /// The held press's step to <paramref name="now"/> (the surface's DIPs): a resize or the move of what is selected. True
    /// when a desktop icon's move is to become the shell's drag from here on ([S-11878]).
    /// </summary>
    private bool Follow(Point now)
    {
        var metrics = _host.Metrics;
        if (!metrics.IsUsable)
        {
            return false;
        }

        _moved |= CardGestures.Travelled(now.X - _dragStartPoint.X, now.Y - _dragStartPoint.Y);

        var dxFrac = (now.X - _dragStartPoint.X) / metrics.Width;
        var dyFrac = (now.Y - _dragStartPoint.Y) / metrics.Height;

        if (_activeHandle is { } handle)
        {
            // [S-2445] The moved edge stops at the sizing's Min and Max; [S-06] [S-14500] on every whole cell of the grid.
            LiveRect = GridGeometry.Settle(GridGeometry.Resize(_dragStartRect, handle, dxFrac, dyFrac, metrics), metrics, handle, Limits);
            _host.OnGeometryPreview(this);
        }
        else if (_moved)
        {
            // [S-1584] The host moves the whole selection as one block, this card included, on the grid.
            _host.PreviewMove(this, dxFrac, dyFrac);

            // [S-11878] A desktop icon's move that leaves the desktop (or reaches a drop target) is the shell's drag from here on.
            return !_host.EditMode && _view is IDesktopIconView && _host.DragsOut(this);
        }

        return false;
    }

    /// <summary>
    /// [S-11878] The move becomes the shell's drag of the icons: the cards go back where they were, the card
    /// lets the pointer go, and the icon starts the drag with this event's pointer, still pressed.
    /// </summary>
    private void DragOut(PointerRoutedEventArgs e, IDesktopIconView icon)
    {
        _host.CancelMove(this);
        _dragging = false;
        _activeHandle = null;
        _narrowOnRelease = false;
        ReleasePointerCapture(e.Pointer);
        _host.PressEnded(this);
        icon.DragOut(e.GetCurrentPoint(_view));
    }

    /// <summary>
    /// [S-2008] The pointer for the card's move: taken from every element inside that holds it (a button holds it from its
    /// press: the card's capture would fail and the press end as the button's click, a toggle flipped by a drag), then
    /// captured by the card. False when the card could not capture it.
    /// </summary>
    private bool TakePointer(PointerRoutedEventArgs e)
    {
        TakePointerFromInside(e);
        return CapturePointer(e.Pointer);
    }

    /// <summary>
    /// [S-2008] Release the pointer from every element between the one pressed and this card, so the card
    /// can capture it and the element (a button) forgets its press and does not click on the release.
    /// </summary>
    private void TakePointerFromInside(PointerRoutedEventArgs e)
    {
        for (var element = e.OriginalSource as DependencyObject; element is not null && !ReferenceEquals(element, this); element = VisualTreeHelper.GetParent(element))
        {
            if (element is UIElement inner && inner.PointerCaptures is { Count: > 0 })
            {
                inner.ReleasePointerCapture(e.Pointer);
            }
        }
    }

    private static bool IsKeyDown(Windows.System.VirtualKey key)
        => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        ReleasePointerCapture(e.Pointer);
        EndDrag();
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        // Capture can be lost without a release (a display change, another window grabbing input).
        // Committing here rather than dropping the gesture avoids a widget stuck mid-drag.
        if (_dragging)
        {
            EndDrag();
        }
    }

    private void EndDrag()
    {
        var resizing = _activeHandle is not null;
        var narrow = _narrowOnRelease && !_moved;
        _dragging = false;
        _activeHandle = null;
        _narrowOnRelease = false;

        // Only a move may hand widgets to another display: a resize that happens to end with the
        // pointer over another screen is still a resize.
        // [S-15662] A resize lands where the card shows it: the surface lands its live rect.
        if (resizing)
        {
            _host.OnGeometryCommitted(this);
            _host.PressEnded(this);
            return;
        }

        _host.CommitMove(this);

        // [S-1593] A click, not a drag, on a card of a multi-selection selects just that card.
        if (narrow)
        {
            _host.RequestClick(this);
        }

        // [S-2343] Last: the selection is what it will be, and nothing moves under the pointer any more.
        _host.PressEnded(this);
    }

    /// <summary>
    /// [S-78] Fade the card while its drop would land on another display, so the screen it is
    /// leaving says so as clearly as the ghost on the screen it is going to.
    /// </summary>
    public void SetLeaving(bool leaving)
    {
        _leaving = leaving;
        ShowMoveOpacity();
    }

    /// <summary>
    /// [S-13046] While a move held by a desktop icon carries this icon: it stands where the block lands as the icon's ghost
    /// (the whole icon, faint), the picture under the pointer being the icon the hand holds.
    /// </summary>
    public void SetGhost(bool ghost)
    {
        _ghost = ghost;
        ShowMoveOpacity();
    }

    /// <summary>[S-78] [S-13046] Faded going to another display, a ghost where it lands, else whole.</summary>
    private void ShowMoveOpacity() => Opacity = _leaving ? 0.35 : _ghost ? SelectionLook.Landing : 1.0;

    /// <summary>[S-13046] Where the pointer pressed the card, in its parent's (the surface's widget layer's) DIPs.</summary>
    internal Point PressPoint => _dragStartPoint;

    private ResizeHandle? HitHandle(object? source)
        => source is FrameworkElement { Tag: ResizeHandle handle } ? handle : null;

    private UIElement ParentSurface() => Parent as UIElement ?? this;

    /// <summary>
    /// [S-2000] A click the view's own buttons, rows, links and controls did not take opens what the
    /// widget is about, at once ([S-2001]: the view says what, <see cref="IWidgetOpens"/>). The press
    /// must have reached the card unhandled too: a control that takes the press but not the tap
    /// keeps its click. Outside edit mode only; in edit mode the press selected.
    /// </summary>
    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        var armed = _openArmed;
        _openArmed = false;

        if (!armed || _view is not IWidgetOpens opens)
        {
            return;
        }

        try
        {
            if (opens.Opens is not { } target)
            {
                return;
            }

            e.Handled = true;
            var outcome = _widgetHost.Launcher.OpenCard(target);
            if (outcome != LaunchOutcome.Opened)
            {
                Log.Warn(Scope, $"{Model.Kind}: a click opened nothing ({outcome})");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{Model.Kind} threw saying what a click opens", ex);
        }
    }

    /// <summary>
    /// [S-1743] [S-14321] The card's menu, Windows 11 style, opened where the right-click was through the surface, as the
    /// desktop's own menus (<see cref="IWidgetCardHost.OpenCardMenu"/>). Which commands apply is
    /// <see cref="ShellMenus.ForCard"/>'s; <see cref="CardMenu"/> only words them and wires them up.
    /// </summary>
    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        try
        {
            // [S-1592] In edit mode a right-click on a selected card keeps the selection: the commands act on all of it. On
            // any other card it selects just that one first. [S-14321] Outside edit mode the surface puts it in the desktop's
            // one selection, as a right-click on an icon.
            if (_host.EditMode && !_host.IsSelected(this))
            {
                // ignore: whether it changed; the menu acts on what is selected now
                _ = _host.RequestPress(this, onHandle: false);

                // [S-2343] A right-click holds nothing: the press ends here, or the selection's page
                // would stay held shut until some later press let go.
                _host.PressEnded(this);
            }

            if (!_host.OpenCardMenu(this, e.GetPosition(this)))
            {
                Log.Debug(Scope, $"{Model.Kind}: no card menu here");
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not open the widget context menu", ex);
        }
    }

    /// <summary>
    /// [S-1743] [S-14321] The card menu's entries, read when it opens at <paramref name="at"/> (surface DIPs: where what the
    /// desktop's lines bring lands, outside edit mode).
    /// </summary>
    internal List<ItemMenuEntry> CardMenu(Point at)
    {
        // The view's own commands — pause a camera, refresh a page — read fresh each time so their text can follow the state
        // they toggle. [S-1746] None of them changes how the widget looks: that is its settings' job.
        IReadOnlyList<WidgetAction> actions = _running && _view is IWidgetContextActions { ContextActions: { } own } ? own : [];
        var refresh = actions.FirstOrDefault(action => action.Quick == QuickCommand.Refresh);

        var cards = _host.ViewForCards;
        var facts = new CardMenuFacts(
            EditMode: _host.EditMode,
            CanBringToFront: cards?.CanBringToFront(Model.Id) == true,
            CanSendToBack: cards?.CanSendToBack(Model.Id) == true,
            // [S-15833] Outside edit mode what the menu acts on is the desktop's one selection, not edit mode's.
            SelectionCount: _host.EditMode ? _host.SelectionCount : _host.DeskSelectionCount(this),
            CanPaste: _host.CanPaste,
            WidgetsLocked: _host.WidgetsLocked,
            CanRefresh: refresh.Invoke is not null);

        // [S-14321] Beside the desktop's lines the widget's Refresh says it is the widget's.
        string Words(WidgetAction action)
            => facts.EditMode ? action.Text : ShellMenus.WidgetLine(action.Text, Str.Desktop_Menu_Refresh.Text(), Str.Shell_Card_RefreshWidget.Text());

        var entries = new List<ItemMenuEntry>();
        var refreshInRow = false;
        foreach (var item in ShellMenus.ForCard(facts))
        {
            switch (item.Command)
            {
                case ShellCommand.Refresh:
                    // [S-15830] The widget's Refresh in the icon row, its words the tooltip; no key does it (F5 is the desktop's).
                    entries.Add(new ItemMenuEntry(refresh.Glyph, Words(refresh), refresh.Invoke, Quick: item.Quick));
                    refreshInRow = true;
                    continue;

                case ShellCommand.WidgetActions:
                    entries.AddRange(actions
                        .Where(action => !(refreshInRow && action == refresh))
                        .Select(action => new ItemMenuEntry(action.Glyph, Words(action), action.Invoke)));
                    continue;

                case ShellCommand.WidgetSettings:
                    // [S-15832] Its page, edit mode entered for it; Alt+Enter on a focused card does the same (Windows' Properties).
                    entries.Add(new ItemMenuEntry(Glyphs.Settings, Str.Shell_Card_WidgetSettings.Text(), () => _host.RequestSettings(this),
                        Quick: item.Quick, Shortcut: KeyChord.Alt(ChordKey.Enter)));
                    continue;

                case ShellCommand.DeleteOnDesktop:
                    if (DeskDeleteEntry() is { } delete)
                    {
                        entries.Add(delete);
                    }

                    continue;

                case ShellCommand.DesktopMenu:
                    // [S-14321] The desktop's background menu whole, as a right-click on empty desktop opens it. [S-15831] Its lines
                    // stay the list: the row is the widget's, so the desktop's Paste (of files) is not pulled up beside its Delete.
                    entries.AddRange(_host.DesktopLines(this, at).Select(line => line with { Quick = null }));
                    continue;
            }

            entries.Add(CardEntry(item, facts.SelectionCount));
        }

        return entries;
    }

    /// <summary>
    /// [S-14320] [S-14321] The Delete line outside edit mode: what Del does to the desktop's one selection, worded by what it
    /// does ("Delete widget", "Delete 3 widgets", or Explorer's "Delete" while a file goes to the shell's delete), its key
    /// right-aligned; none while it would do nothing.
    /// </summary>
    private ItemMenuEntry? DeskDeleteEntry()
    {
        var (words, count) = _host.DeskDeleteLine(this);
        var text = words switch
        {
            Themia.Core.Desktop.DeleteWords.Delete => Str.Common_Delete.Text(),
            Themia.Core.Desktop.DeleteWords.OneWidget => Str.Shell_Card_DeleteWidget.Text(),
            Themia.Core.Desktop.DeleteWords.Widgets => Str.Shell_Card_DeleteMany.Plural(count),
            _ => null,
        };
        return text is null ? null : new(Glyphs.Delete, text, () => _host.RequestDelete(this), Danger: true, Quick: QuickCommand.Delete, Shortcut: KeyChord.Plain(ChordKey.Delete));
    }

    /// <summary>One of the card's own commands, worded and wired.</summary>
    private ItemMenuEntry CardEntry(ShellMenuItem item, int count)
    {
        // [S-1592] One widget reads "Copy"; a selection reads "Copy 3 widgets", one sentence per
        // language rather than a verb with a count glued on.
        string Verb(Str one, Str many) => count > 1 ? many.Plural(count) : one.Text();

        // [S-1753] Shortcuts are chords; the menu writes them as Windows does ("Strg+C" in German).
        var disabled = !item.Enabled;
        return item.Command switch
        {
            ShellCommand.Cut => new(Glyphs.Cut, Verb(Str.CtxMenu_Cut, Str.Shell_Card_CutMany), () => _host.RequestCut(this), Quick: item.Quick, Shortcut: KeyChord.Ctrl(ChordKey.X)),
            ShellCommand.Copy => new(Glyphs.Copy, Verb(Str.CtxMenu_Copy, Str.Shell_Card_CopyMany), () => _host.RequestCopy(this), Quick: item.Quick, Shortcut: KeyChord.Ctrl(ChordKey.C)),
            ShellCommand.Paste => new(Glyphs.Paste, Str.CtxMenu_Paste.Text(), _host.RequestPaste, Disabled: disabled, Quick: item.Quick, Shortcut: KeyChord.Ctrl(ChordKey.V)),
            ShellCommand.Delete => new(Glyphs.Delete, _host.DeleteRemovesFromView(this) ? Str.Desktop_Delete_FromView.Text() : Verb(Str.Common_Delete, Str.Shell_Card_DeleteMany), () => _host.RequestDelete(this), Danger: true, Quick: item.Quick, Shortcut: KeyChord.Plain(ChordKey.Delete)),
            ShellCommand.BringToFront => new(Glyphs.MoveUp, Str.Shell_Card_BringToFront.Text(), () => _host.RequestZOrder(this, front: true), Disabled: disabled),
            ShellCommand.SendToBack => new(Glyphs.MoveDown, Str.Shell_Card_SendToBack.Text(), () => _host.RequestZOrder(this, front: false), Disabled: disabled),
            // [S-65] One line, Edit mode, checked while on (Windows' View toggles); a click flips it.
            ShellCommand.ToggleEditMode => new(Glyphs.Edit, Str.Shell_Menu_EditMode.Text(), () => _host.RequestEditMode(!_host.EditMode), Checked: _host.EditMode),
            // [S-12951] Lock widgets under it, checked while on; a click flips the one setting.
            ShellCommand.ToggleLockWidgets => new(Glyphs.Lock, Str.Shell_Menu_LockWidgets.Text(), () => _host.RequestLockWidgets(item.Checked != true), Checked: item.Checked),
            _ => ItemMenuEntry.Separator,
        };
    }

    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// A resize grip.
    /// <para>
    /// A subclass only because <c>UIElement.ProtectedCursor</c> is protected: a resize handle whose
    /// pointer does not change shape reads as decoration rather than something you can grab, and
    /// the shape is the whole affordance. It derives from <see cref="Grid"/> rather than
    /// <c>Rectangle</c> because WinUI seals its shape primitives.
    /// </para>
    /// </summary>
    private sealed partial class ResizeHandleDot : Grid
    {
        private InputSystemCursorShape? _shape;

        /// <summary>
        /// [S-2363] The pointer's shape over the grip, a cursor made only when it changes: every layout of the surface asks
        /// each grip again (eight a card), and making a cursor each time was a third of a state change's cost.
        /// </summary>
        public void SetCursorShape(InputSystemCursorShape shape)
        {
            if (_shape == shape)
            {
                return;
            }

            _shape = shape;
            ProtectedCursor = InputSystemCursor.Create(shape);
        }
    }


    // ---- the keyboard lease ([S-800]) --------------------------------------------------------------

    /// <summary>[S-800] Whether a click here takes the keyboard: the view navigates with keys, and this is a real desktop card.</summary>
    public bool TakesKeys => _running && _view is IWidgetKeys { WantsKeys: true } && !_host.IsPreview;

    /// <summary>
    /// [S-803] Hand the view a key from the lease. A view that throws is logged, not rethrown: the key
    /// arrives from the surface's input handler, and one widget's bug must not take the surface down.
    /// </summary>
    public bool SendKey(WidgetKey key)
    {
        if (!_running || _view is not IWidgetKeys keys)
        {
            return false;
        }

        try
        {
            return keys.HandleKey(key);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{Model.Kind} threw handling {key}", ex);
            return false;
        }
    }

    /// <summary>[S-1997] Hand the view a typed character from the lease (type-ahead), guarded like <see cref="SendKey"/>.</summary>
    public bool SendText(char character)
    {
        if (!_running || _view is not IWidgetKeys keys)
        {
            return false;
        }

        try
        {
            return keys.HandleText(character);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{Model.Kind} threw handling typed text", ex);
            return false;
        }
    }

    private void OnLeasePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (LeasePress(
            e.OriginalSource,
            e.GetCurrentPoint(ParentSurface()).Position,
            e.GetCurrentPoint(this).Properties.IsLeftButtonPressed,
            ctrl: (e.KeyModifiers & Windows.System.VirtualKeyModifiers.Control) != 0,
            shift: (e.KeyModifiers & Windows.System.VirtualKeyModifiers.Shift) != 0,
            handled: e.Handled))
        {
            e.Handled = true;
        }
    }

    // ---- IWidgetSurface ------------------------------------------------------------------------

    /// <inheritdoc />
    void IWidgetSurface.RequestSave()
    {
        // [S-1862] A view that changed its own config (a shortcut dropped on it) may now be set up.
        ApplySetup();
        _host.RequestSaveConfig();
    }

    /// <inheritdoc />
    void IWidgetSurface.RequestTextInput(bool wanted) => _host.RequestTextInput(this, wanted);

    bool IWidgetSurface.IsPreview => _host.IsPreview;

    bool IWidgetSurface.EditMode => _host.EditMode;

    string? IWidgetSurface.WidgetId => _host.IsPreview ? null : Model.Id;

    Themia.Core.Links.LinkBoard? IWidgetSurface.Links => _host.Links;

    /// <summary>
    /// [S-11903] [S-11963] The card lit as one item (a drive's card under the pointer, or its drive pointed at
    /// elsewhere): one layer over the card's material and under the view, the whole content area edge to edge,
    /// clipped to the card's corners by the surface, so the band is the card and never the view's box (which stops
    /// at the inset the card gives its top). Not in edit mode, where the view takes no pointer ([S-1751]).
    /// </summary>
    void IWidgetSurface.LightCard(ItemState state)
    {
        _linkState = state;
        PaintLinkFill();
    }

    private readonly Border _linkFill = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private ItemState _linkState = ItemState.Rest;

    private void PaintLinkFill()
    {
        var lit = _linkState != ItemState.Rest && !_host.EditMode;
        if (lit)
        {
            ItemFill.Paint(_linkFill, ItemRole.Row, _linkState);
        }

        _linkFill.Visibility = lit ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    void IWidgetSurface.SetHidden(bool hidden)
    {
        if (_hidden == hidden)
        {
            return;
        }

        _hidden = hidden;
        ApplyHidden();
        ApplyDesign();
        _host.OnCardFrameChanged(this);
    }
}
