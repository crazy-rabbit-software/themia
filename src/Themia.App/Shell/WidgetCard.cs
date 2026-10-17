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
using Themia.Sdk;
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

    void RequestConfigure(WidgetCard card);

    void RequestDelete(WidgetCard card);

    void RequestEditMode(bool enabled);

    bool IsSelected(WidgetCard card);

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

    /// <summary>[S-1251] True for a debug render's host: its cards are previews and must not act on the machine.</summary>
    bool IsPreview => false;

    /// <summary>[S-3300] The links of the view these cards belong to: one board per view id, whichever display shows it.</summary>
    Themia.Core.Links.LinkBoard? Links => ViewForCards is { } view ? AppServices.Links?.For(view.Id) : null;

    /// <summary>
    /// [S-873] The card's drawn frame changed — hidden or shown — so it is positioned again and the
    /// hit region redone. Nothing is persisted.
    /// </summary>
    void OnCardFrameChanged(WidgetCard card)
    {
        // ignore: a host with no hit region (a debug render) has nothing to redo
    }

    /// <summary>[S-874] Bring the widget to the front of the view, or send it to the back.</summary>
    void RequestZOrder(WidgetCard card, bool front)
    {
        // ignore: a host with no view (a debug render) has no order to change
    }

    /// <summary>[S-1580] Ctrl+click: add the card to the selection, or take it out.</summary>
    void RequestToggleSelect(WidgetCard card) => RequestSelect(card);

    /// <summary>[S-1745] Whether the settings window is open; a click on a card only ever follows an open one.</summary>
    bool SettingsOpen => false;

    /// <summary>[S-1745] A plain click on the card in edit mode: show its page in the open settings window.</summary>
    void RequestFollowInSettings(WidgetCard card)
    {
        // ignore: a host with no settings window (a debug render) has nothing to follow
    }

    /// <summary>How many widgets are selected, so the card menu can say what its commands act on.</summary>
    int SelectionCount => 1;

    /// <summary>[S-1584] A move of this card starts: it and every other selected card move together.</summary>
    void BeginMove(WidgetCard card)
    {
        // ignore: a debug render's cards do not move
    }

    /// <summary>[S-1584] The pointer moved by a fraction of the screen since the move began.</summary>
    void PreviewMove(WidgetCard card, double dxFrac, double dyFrac, bool snap)
    {
        // ignore: as above
    }

    /// <summary>[S-1584]/[S-1591] The move ended: persist it, or hand the widgets to another display.</summary>
    void CommitMove(WidgetCard card)
    {
        // ignore: as above
    }
}

/// <summary>
/// One widget on the desktop: the Fluent card chrome, the drag and resize affordances, and the
/// right-click menu. The widget's own view is dropped inside and knows nothing about input.
/// </summary>
internal sealed partial class WidgetCard : Grid, IWidgetSurface, IItemPaletteOwner
{
    private const string Scope = "card";

    private readonly IWidgetCardHost _host;

    /// <summary>[S-6512] The item fills of this widget's view, from its own item layer or the app-wide one.</summary>
    private readonly ItemPalette _items = new();

    private readonly Border _fill;
    private readonly Border _tint;
    private readonly Border _surface;

    /// <summary>[S-824] The border, drawn as a shape because a <see cref="Border"/> cannot dash.</summary>
    private readonly Rectangle _stroke;

    /// <summary>
    /// [S-826] Carries the design's font to the content: text inherits a font family from the
    /// nearest presenter or control above it, and a <see cref="Border"/> has none to give.
    /// </summary>
    private readonly ContentPresenter _fontScope;

    /// <summary>[S-821] The text size the text already on this card was laid out at.</summary>
    private double _textScale = FluentType.TextScale;
    private readonly Grid _headerHost;

    /// <summary>The header strip: its own fill ([S-1761]) under the header row.</summary>
    private readonly Grid _headerBand;

    /// <summary>[S-1761] The card's surface behind the header strip alone, faded by the header opacity.</summary>
    private readonly Border _headerBase = new() { IsHitTestVisible = false };

    private readonly Border _headerTint = new() { IsHitTestVisible = false };

    /// <summary>[S-405] Stands the header on end for a strip down a side edge.</summary>
    private readonly TurnedHeader _headerTurn;

    /// <summary>[S-873] The view asked to paint nothing (outside edit mode).</summary>
    private bool _hidden;

    private readonly Grid _header;
    private readonly TextBlock _headerTitle;

    /// <summary>A title the view worked out for itself, beaten only by one the user typed.</summary>
    private string? _viewTitle;

    /// <summary>The widget's view, or its error card ([S-891]) when the view failed. Swapped by a retry.</summary>
    private FrameworkElement _view;

    /// <summary>The header and the view together: what [S-850]'s content scale magnifies.</summary>
    private readonly Grid _content;

    private double _contentScale = 1.0;

    private FracRect _dragStartRect;
    private Point _dragStartPoint;
    private ResizeHandle? _activeHandle;

    /// <summary>[S-1593] The press landed on a card of a multi-selection: a click without a drag narrows the selection to it.</summary>
    private bool _narrowOnRelease;

    /// <summary>Whether the pointer has travelled far enough since the press to count as a drag ([S-1745]).</summary>
    private bool _moved;

    /// <summary>[S-1745] What the current press started; a plain click is told apart on release.</summary>
    private CardPress _press;

    private bool _dragging;

    public WidgetCard(IWidgetCardHost host, WidgetInstance model)
    {
        _host = host;
        Model = model;
#if DEBUG
        Gallery.LiveCensus.CardBuilt(this);
#endif

        // [S-6512] The item fills are right before the view paints its first item, and what the view
        // paints while the card is built (it attaches in here too) is drawn from them.
        _items.Apply(ItemLook.Effective(host.Design, model), ItemLook.TextColour(host.Design, model), DesignApplier.IsDark);
        using var building = ItemPalette.Building(_items);

        // [S-891] Guarded: a view that throws while being built becomes an error card, not a hole.
        _view = CreateView(model.Kind);

        // The header belongs to the card, not to the view. One builder means one look, and it is
        // the only way a "put it at the bottom" setting can exist without asking every widget to
        // honour it. The view contributes its trailing element and nothing else.
        _headerTitle = new TextBlock();
        _header = Fluent.Header(
            ModuleCatalog.Of(model.Kind).Glyph,
            _headerTitle,
            (_view as IWidgetHeaderContent)?.HeaderTrailing);

        // Padding rather than margin, so a header fill ([S-828]) reaches the card's edges.
        _headerHost = new Grid
        {
            Children = { _header },
            Padding = Fluent.HeaderMargin,
        };

        _headerTurn = new TurnedHeader(_headerHost);
        _headerBand = new Grid { Children = { _headerBase, _headerTint, _headerTurn } };

        // The view is bound and attached only once the header exists. A view that names itself
        // during Attach - a weather tile knows its place, a folder listing knows its folder -
        // calls straight back into SetHeaderTitle, and doing that before these fields were built
        // threw a NullReferenceException on the UI thread that took the whole surface down.
        // [S-891] A view that throws here, or on the first tick it asks for, is swapped for its
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

        // Windows 11 draws a card as a tint ON something, not as a flat colour. On an opaque
        // window that something is Mica; here the window is genuinely transparent, so the card
        // paints the opaque surface colour Fluent expects underneath and then lays the real
        // CardBackgroundFillColor overlay on top of it. That overlay is the ~5% lift that makes a
        // Windows card look raised rather than merely filled, and using it alone - as a first
        // attempt did - left the card almost invisible over the wallpaper.
        _tint = new Border
        {
            Background = FluentLayout.Themed("CardBackgroundFillColorDefaultBrush", Colors.Transparent),
            IsHitTestVisible = false,
        };

        // No padding: the content area goes edge to edge and each view insets its own parts. A card
        // that padded everything pushed every list row away from the edge and stopped its hover
        // highlight short of it, so a row read as a box inside a box rather than as a row. See
        // WidgetLayout.
        // Auto, star, auto both ways, the outer ones empty but for the header: it takes the whole
        // outer row or column its placement asks for ([S-404], HeaderStrip), and the view stays in
        // the middle cell, so switching it costs a cell rather than a rebuild.
        var content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        Place(_view, HeaderStrip.ViewCell);

        // [S-1761] The widget's fill paints the body only, under the view; the header strip has a
        // fill of its own (_headerBase/_headerTint), so the header opacity can fade it alone. Painted
        // across the whole card, the body fill sat under the header too, and a header that shares the
        // widget's fill could never look any different, whatever its opacity.
        content.Children.Add(_fill);
        content.Children.Add(_tint);
        content.Children.Add(_headerBand);
        content.Children.Add(_view);

        _content = content;

        // [S-1862] Over the view, once the content grid exists to hold it.
        AttachSetup();

        _fontScope = new ContentPresenter
        {
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };

        _surface = new Border
        {
            Child = _fontScope,
            Padding = new Thickness(0),
        };

        // Over the content, as a CSS border sits round it: a full-bleed chart would otherwise cover
        // the line along its edges. Never hit-testable, so it cannot take a click from the widget.
        _stroke = new Rectangle { IsHitTestVisible = false };

        _surface.SizeChanged += (_, _) => FitContent();

        // [S-1113] The card's content (header, view, its menus) flows with the language; the resize
        // grips are physical corners and keep their places, so a drag on the left grip still moves
        // the left edge.
        UiLanguage.ApplyFlow(this);
        _markers = UiLanguage.KeepLeftToRight(BuildMarkers());

        Children.Add(_surface);
        Children.Add(_stroke);
        Children.Add(_markers);

        Background = new SolidColorBrush(Colors.Transparent); // makes the whole card hit-testable for drags

        // Focusable so that clicking a widget gives the surface keyboard focus. Without something
        // focusable in the tree no KeyDown is raised at all, and the edit-mode shortcuts never fire.
        IsTabStop = true;
        UseSystemFocusVisuals = false;
        ApplyHeader();
        ApplyDesign();
        ApplyEditMode(host.EditMode);

        // The header sheds by height, so it has to be reconsidered when the height changes.
        SizeChanged += (_, _) => ApplyHeader();

        // [S-663] A view that discloses by size is told its slot - the card less the header - once
        // per layout pass that changed it. The view's own SizeChanged is that signal exactly: it is
        // raised after arrange, only when the slot really changed (a resize, the header shedding or
        // moving), and the slot is a star row of a fixed-size card, so nothing the view shows in
        // response can feed back into it.
        HookViewSize();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        RightTapped += OnRightTapped;
        DoubleTapped += OnDoubleTapped;

        // [S-800] Handled presses too: a click on a row's button or a list item is still a click in
        // this widget, and the controls inside mark their presses handled.
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnLeasePointerPressed), handledEventsToo: true);
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
    /// Put the header where the widget asks for it, and name it.
    /// <para>
    /// The title is the user's own when they set one, otherwise the catalogue's. A header on the far
    /// edge (the bottom) flips the padding as well, or the gap would end up on the wrong side of it.
    /// </para>
    /// </summary>
    public void ApplyHeader()
    {
        // The user's own label, then whatever the view knows about itself, then the catalogue.
        _headerTitle.Text = !string.IsNullOrWhiteSpace(Model.Label) ? Model.Label
            : !string.IsNullOrWhiteSpace(_viewTitle) ? _viewTitle
            : ModuleCatalog.Of(Model.Kind).Label;

        var placement = Model.ShownHeader;

        // Shed it on a tile too short to carry it, here rather than in twenty views: below this
        // the label is taking the room the content needs, and every widget drew the same
        // conclusion separately with a slightly different threshold. A size of zero is a card
        // that has not been measured yet, not a short one.
        // [S-404] [S-409] A strip down a side sheds on a card too narrow for it instead.
        var tooShort = HeaderStrip.Sheds(placement, ActualWidth, ActualHeight);
        var hidden = placement == HeaderPlacement.Hidden || tooShort;

        _headerBand.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        ApplyBodyFill();

        if (hidden)
        {
            return;
        }

        var far = placement == HeaderPlacement.Bottom;
        Place(_headerBand, HeaderStrip.StripCell(ShownEdge));

        // [S-406] Turned on a side, the header is laid out left to right (the turn is physical, and a
        // mirrored element's render transform turns about the mirrored origin); flat, it flows with the card.
        _headerTurn.FlowDirection = HeaderStrip.IsSide(placement) ? FlowDirection.LeftToRight : FlowDirection;
        _headerTurn.Turn = HeaderStrip.Turn(placement);

        // [S-408] A side strip is the icon and the title: the trailing column is closed, not the
        // view's element hidden, which views show and hide for their own reasons.
        _header.ColumnDefinitions[2].MaxWidth = HeaderStrip.ShowsTrailing(placement) ? double.PositiveInfinity : 0;

        _headerHost.Padding = far ? new Thickness(Fluent.Space4, Fluent.Space2, Fluent.Space4, Fluent.Space4)
            : Fluent.HeaderMargin;

        ApplyHeaderFill();
    }

    /// <summary>[S-872] Whether the card draws anything the window's hit region must take clicks for.</summary>
    public bool IsShown(bool editMode) => !_hidden || editMode;

    /// <summary>
    /// Paint the card from the design: corner radius, fill colour and opacity, border, header fill and
    /// font. The one per-card apply path ([S-820]..[S-829]); the app-wide half (theme, accent, text
    /// size) is <see cref="DesignApplier"/>. Runs on every state change, and only sets properties, so
    /// a slider dragged across the Appearance page re-paints cards without rebuilding any.
    /// </summary>
    public void ApplyDesign()
    {
        var design = _host.Design;
        var radiusPx = DesignLook.CornerRadius(design.CornerRadiusPx);
        var radius = new CornerRadius(radiusPx);

        // [S-6510] The second layer: the view's items, on their own colour whatever the card's fill.
        _items.Apply(ItemLook.Effective(design, Model), ItemLook.TextColour(design, Model), DesignApplier.IsDark);

        ApplyBodyFill();

        // The content border is rounded too, so anything drawn edge to edge inside it - a meter's
        // graph reaches the bottom and both sides - is clipped to the card's corners instead of
        // squaring them off.
        _surface.CornerRadius = radius;

        // [S-823] The card paints its OWN base, rather than relying on a window background.
        //
        // Fluent's CardBackgroundFillColor is a ~5% white overlay in dark theme: it is designed to
        // sit on top of an opaque window, not to be the only fill. The widget surface is genuinely
        // transparent, so using it alone made the card invisible and left the clock floating
        // unreadably over whatever was behind the window. SolidBackgroundFillColorBase is the
        // opaque surface colour that overlay is meant to sit on, so it is the correct base here.
        // A colour of the user's own replaces both: it is meant to be exactly that colour.
        var fill = CardFillNow();
        var opacity = fill.Painted ? fill.Opacity : 0;
        var custom = ColourBrush(fill);

        _fill.Background = opacity <= 0 ? null
            : custom ?? FluentLayout.Themed("SolidBackgroundFillColorBaseBrush", Colors.Black);
        _fill.BorderBrush = null;
        _fill.BorderThickness = new Thickness(0);
        _fill.Opacity = opacity <= 0 ? 1 : opacity;
        _tint.Opacity = custom is null ? opacity : 0;

        // [S-824] The border: style, width, colour and opacity, over the content.
        var border = DesignLook.Border(fill, design.BorderStyle, design.BorderWidthPx, design.BorderOpacity);
        if (border.Visible && (!_hidden || _host.EditMode))
        {
            _stroke.Visibility = Visibility.Visible;
            // [S-6481] The user's colour exactly, else the theme stroke for this weight: the card
            // hairline only for a solid 1px line, the strong stroke for anything heavier or broken.
            var ink = DesignLook.Ink(border, design.BorderColor);
            _stroke.Stroke = (ink.Colour is { } colour ? DesignApplier.UserBrush(HexColor.From(colour.R, colour.G, colour.B)) : null)
                ?? FluentLayout.Themed(ink.ThemeKey ?? DesignLook.HairlineStrokeKey, Colors.Gray);
            _stroke.StrokeThickness = border.Thickness;
            _stroke.Opacity = border.Opacity;

            // A shape strokes inside its bounds along a path inset by half the stroke, so the path's
            // radius is the card's less that half, or the corners would not meet the fill's.
            _stroke.RadiusX = _stroke.RadiusY = Math.Max(0, radiusPx - (border.Thickness / 2));

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

        ApplyHeaderFill();

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

    /// <summary>
    /// [S-828] [S-1761] The header strip's own fill: a colour of its own at the header opacity, or
    /// else the widget's fill at the widget's opacity times the header opacity. The corners on the
    /// card's edge follow the card's so the strip does not square them.
    /// </summary>
    private void ApplyHeaderFill()
    {
        var design = _host.Design;
        var r = DesignLook.CornerRadius(design.CornerRadiusPx);

        // [S-407] The strip's outer corners. The header inside a side strip is turned, its top
        // facing out, so its own fill rounds its top corners as a top header's does.
        var edge = ShownEdge;
        var radius = Round(HeaderStrip.StripCorners(edge), r);
        var hostRadius = HeaderStrip.IsSide(Model.ShownHeader) ? Round(HeaderStrip.StripCorners(HeaderEdge.Top), r) : radius;

        var own = DesignApplier.UserBrush(design.HeaderBackgroundColor);
        var fill = CardFillNow();
        var surface = fill.Painted ? fill.Opacity : 0;
        var opacity = DesignLook.HeaderFillOpacity(fill, design.HeaderOpacity, own is not null);
        var custom = ColourBrush(fill);

        _headerHost.Background = own is null ? null : DesignApplier.UserBrush(design.HeaderBackgroundColor, opacity);
        _headerHost.CornerRadius = hostRadius;

        // Shared with the widget: the same base and Fluent lift as the body, faded on their own.
        _headerBase.Background = own is null && surface > 0
            ? custom ?? FluentLayout.Themed("SolidBackgroundFillColorBaseBrush", Colors.Black)
            : null;
        _headerBase.Opacity = opacity;
        _headerTint.Background = own is null && custom is null && surface > 0
            ? FluentLayout.Themed("CardBackgroundFillColorDefaultBrush", Colors.Transparent)
            : null;
        _headerTint.Opacity = opacity;
        _headerBase.CornerRadius = radius;

        // [S-836] Inside a solid stroke on the card's three outer edges, as the body's tint is.
        var t = TintInset();
        var ri = Math.Max(0, r - t);
        (_headerTint.Margin, _headerTint.CornerRadius) = (Inset(HeaderStrip.StripSides(edge), t), Round(HeaderStrip.StripCorners(edge), ri));
    }

    /// <summary>[S-836] How far the Fluent tint stops short of the card's edge for the current border.</summary>
    private double TintInset()
    {
        var design = _host.Design;
        return DesignLook.TintInset(DesignLook.Border(CardFillNow(), design.BorderStyle, design.BorderWidthPx, design.BorderOpacity));
    }

    /// <summary>[S-6563] What this card's background paints: its own choice, or the app-wide one for Default.</summary>
    private CardFill CardFillNow() => DesignLook.Card(FillChoice.Effective(_host.Design.Background, Model.Background));

    /// <summary>[S-823] The user's colour of a colour fill, opaque (the fill's opacity is the element's), else null for the Fluent base.</summary>
    private static SolidColorBrush? ColourBrush(CardFill fill)
        => fill.Colour is { } c ? DesignApplier.UserBrush(HexColor.From(c.R, c.G, c.B)) : null;

    /// <summary>
    /// [S-1761] The body fill covers what the header leaves: the whole card without a header, else
    /// the rows beside it, with the card's corners only on the edges it reaches.
    /// </summary>
    private void ApplyBodyFill()
    {
        // A view that names itself while it is attached reaches here through ApplyHeader before the
        // constructor has built the fill; the constructor's own ApplyHeader places it after.
        if (_fill is null || _tint is null)
        {
            return;
        }

        var r = DesignLook.CornerRadius(_host.Design.CornerRadiusPx);
        var edge = _headerBand.Visibility == Visibility.Visible ? ShownEdge : HeaderEdge.None;
        var radius = Round(HeaderStrip.BodyCorners(edge), r);

        foreach (var layer in new[] { _fill, _tint })
        {
            Place(layer, HeaderStrip.BodyCell(edge));
            layer.CornerRadius = radius;
        }

        // [S-836] The Fluent tint stops at a solid stroke's inner edge, as a WinUI card's background
        // does, so the stroke composites over the base alone; its corners are the inner radius.
        var t = TintInset();
        var ri = Math.Max(0, r - t);
        (_tint.Margin, _tint.CornerRadius) = (Inset(HeaderStrip.BodySides(edge), t), Round(HeaderStrip.BodyCorners(edge), ri));

        // The base reaches one DIP under the header strip, which is painted over it: two fills that
        // only meet at an edge leave an anti-aliased seam of desktop between them wherever that edge
        // falls between pixels (a widget scale, an intro's scale, a fractional DPI).
        _fill.Margin = Inset(HeaderStrip.Seam(edge), -1);
    }

    /// <summary>
    /// [S-873] Draw the card or nothing. Hidden, the fill, content and border collapse (the view
    /// stays attached and keeps receiving its updates, so it can show itself again); edit mode
    /// always draws it, faded, so it can be found.
    /// </summary>
    private void ApplyHidden()
    {
        var editing = _host.EditMode;
        var visibility = !_hidden || editing ? Visibility.Visible : Visibility.Collapsed;

        _fill.Visibility = visibility;
        _tint.Visibility = visibility;
        _surface.Visibility = visibility;
        _surface.Opacity = _hidden && editing ? 0.5 : 1;
    }

    /// <summary>
    /// [S-821] Bring text already on the card to the current text size, before the view lays itself
    /// out again from the (already scaled) type ramp. Text a view builds from the ramp is right by
    /// construction; this catches what it built once — labels, the header — so the card follows
    /// the setting without being rebuilt. Only sizes set on the element itself are scaled; text
    /// that inherits its size follows its parent.
    /// </summary>
    private void ApplyTextScale()
    {
        var scale = FluentType.TextScale;
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
        PaintMarkers(_host.IsSelected(this));
    }

    public void ApplyEditMode(bool editing)
    {
        _markers.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        ApplySelection();
        ApplyHidden();

        // Outside edit mode the card must not eat pointer input at all: a clock is something you
        // look at, and the desktop underneath stays usable.
        IsHitTestVisible = true;

        // [S-1751] In edit mode the view is inert, all of it, for every widget: presses, right-clicks,
        // long presses and hovers fall through to the card (its transparent background) and select,
        // drag or open the card menu. Each view guarding itself left Mail's message menu reachable.
        _surface.IsHitTestVisible = CardGestures.ContentTakesInput(editing);
        ProtectedCursor = editing
            ? InputSystemCursor.Create(InputSystemCursorShape.SizeAll)
            : InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    }

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

        // The title and the placement live on the instance, not the config, so a rename or a
        // move to the bottom arrives here too.
        ApplyHeader();
        ApplyDesign();
    }

    public void Detach()
    {
        DetachSetup();
        StopView();
    }

    public void SetLiveRect(FracRect rect) => LiveRect = rect;

    /// <summary>
    /// [S-850] Magnify the card's content — header, text, icons, padding — without touching its cell:
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
    }

    /// <summary>
    /// Inset the painted surface inside the cell by half the gap.
    /// <para>
    /// Applied to the fill and the content only. The edit markers ([S-31b]) stay on the cell
    /// bounds, which is why they sit slightly outside the widget's visible border — they mark the
    /// space the widget actually owns, and that is the thing being dragged.
    /// </para>
    /// </summary>
    public void ApplyGap(double gapPx)
    {
        var half = double.IsFinite(gapPx) && gapPx > 0 ? gapPx / 2 : 0;
        var inset = new Thickness(half);

        // The fill and the tint live inside the surface ([S-1761]), so they take its inset with it.
        _surface.Margin = inset;
        _stroke.Margin = inset;

        if (_motionCover is not null)
        {
            _motionCover.Margin = inset;
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

        cover.Margin = _surface.Margin;
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
            return;
        }

        // [S-1745] What the press starts.
        var handle = HitHandle(e.OriginalSource);
        _press = CardGestures.Press(
            editMode: true,
            leftButton: e.GetCurrentPoint(this).Properties.IsLeftButtonPressed,
            ctrl: IsKeyDown(Windows.System.VirtualKey.Control),
            onHandle: handle is not null);

        switch (_press)
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
        _narrowOnRelease = _activeHandle is null && _host.IsSelected(this) && _host.SelectionCount > 1;
        if (!_narrowOnRelease)
        {
            _host.RequestSelect(this);
        }

        Focus(FocusState.Pointer);
        _dragStartRect = LiveRect;
        _dragStartPoint = e.GetCurrentPoint(ParentSurface()).Position;
        _moved = false;
        _dragging = CapturePointer(e.Pointer);

        if (_dragging && _activeHandle is null)
        {
            _host.BeginMove(this);
        }

        e.Handled = _dragging;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var metrics = _host.Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var now = e.GetCurrentPoint(ParentSurface()).Position;
        _moved |= CardGestures.Travelled(now.X - _dragStartPoint.X, now.Y - _dragStartPoint.Y);

        var dxFrac = (now.X - _dragStartPoint.X) / metrics.Width;
        var dyFrac = (now.Y - _dragStartPoint.Y) / metrics.Height;

        // [S-06] Alt temporarily suspends the snap aid for fine positioning.
        var snap = _host.Design.GridSnap && !IsKeyDown(Windows.System.VirtualKey.Menu);

        if (_activeHandle is { } handle)
        {
            LiveRect = GridGeometry.Settle(GridGeometry.Resize(_dragStartRect, handle, dxFrac, dyFrac, metrics), metrics, snap, handle);
            _host.OnGeometryPreview(this);
        }
        else if (_moved)
        {
            // [S-1584] The host moves the whole selection as one block, this card included.
            _host.PreviewMove(this, dxFrac, dyFrac, snap);
        }

        e.Handled = true;
    }

    private static bool IsKeyDown(Windows.System.VirtualKey key)
        => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        // [S-1745] Read before the drag ends: a plain click, released here rather than lost to
        // capture, shows this widget in a settings window that is already open.
        var follow = CardGestures.FollowsInSettings(_press, _moved, released: true, _host.SettingsOpen);

        ReleasePointerCapture(e.Pointer);
        EndDrag();
        e.Handled = true;

        if (follow)
        {
            _host.RequestFollowInSettings(this);
        }
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
        _press = CardPress.None;

        // Only a move may hand widgets to another display: a resize that happens to end with the
        // pointer over another screen is still a resize.
        if (resizing)
        {
            Model.Rect = LiveRect;
            _host.OnGeometryCommitted(this);
            return;
        }

        _host.CommitMove(this);

        // [S-1593] A click, not a drag, on a card of a multi-selection selects just that card.
        if (narrow)
        {
            _host.RequestSelect(this);
        }
    }

    /// <summary>
    /// [S-78] Fade the card while its drop would land on another display, so the screen it is
    /// leaving says so as clearly as the ghost on the screen it is going to.
    /// </summary>
    public void SetLeaving(bool leaving) => Opacity = leaving ? 0.35 : 1.0;

    private ResizeHandle? HitHandle(object? source)
        => source is FrameworkElement { Tag: ResizeHandle handle } ? handle : null;

    private UIElement ParentSurface() => Parent as UIElement ?? this;

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // Ctrl+Alt+W is the documented way in, but a double-click on a widget is the one people
        // actually try — and it is the fallback when another program already owns the hotkey.
        _host.RequestEditMode(!_host.EditMode);
        e.Handled = true;
    }

    /// <summary>
    /// [S-1743] The card's menu, Windows 11 style: Cut, Copy, Paste, Settings and Delete
    /// as a row of icons, the rest as a list. Which commands apply is <see cref="ShellMenus.ForCard"/>'s;
    /// this only words them and wires them up.
    /// </summary>
    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        try
        {
            // [S-1592] Right-click on a selected card keeps the selection: the commands below act on
            // all of it. On any other card it selects just that one first.
            if (!_host.IsSelected(this))
            {
                _host.RequestSelect(this);
            }

            ItemMenu.Show(this, e, CardMenu());
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not open the widget context menu", ex);
        }
    }

    /// <summary>[S-1743] The card menu's entries, read when it opens.</summary>
    internal List<ItemMenuEntry> CardMenu()
    {
        var cards = _host.ViewForCards;
        var facts = new CardMenuFacts(
            EditMode: _host.EditMode,
            CanBringToFront: cards?.CanBringToFront(Model.Id) == true,
            CanSendToBack: cards?.CanSendToBack(Model.Id) == true,
            SelectionCount: _host.SelectionCount,
            CanPaste: _host.CanPaste,
            HasSettings: Themia.App.Shell.Config.ConfigPages.HasWidgetSettings(Model));

        var entries = new List<ItemMenuEntry>();
        foreach (var item in ShellMenus.ForCard(facts))
        {
            if (item.Command == ShellCommand.WidgetActions)
            {
                // The view's own commands — pause a camera, refresh a page — read fresh each time so
                // their text can follow the state they toggle. [S-1746] None of them changes how the
                // widget looks: that is its settings' job.
                if (_running && _view is IWidgetContextActions { ContextActions: { Count: > 0 } actions })
                {
                    entries.AddRange(actions.Select(action => new ItemMenuEntry(action.Glyph, action.Text, action.Invoke)));
                }

                continue;
            }

            entries.Add(CardEntry(item, facts.SelectionCount));
        }

        return entries;
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
            ShellCommand.Configure => new(Glyphs.Settings, Str.CtxMenu_Configure.Text(), () => _host.RequestConfigure(this), Quick: item.Quick),
            ShellCommand.Delete => new(Glyphs.Delete, Verb(Str.Common_Delete, Str.Shell_Card_DeleteMany), () => _host.RequestDelete(this), Danger: true, Quick: item.Quick, Shortcut: KeyChord.Plain(ChordKey.Delete)),
            ShellCommand.BringToFront => new(Glyphs.MoveUp, Str.Shell_Card_BringToFront.Text(), () => _host.RequestZOrder(this, front: true), Disabled: disabled),
            ShellCommand.SendToBack => new(Glyphs.MoveDown, Str.Shell_Card_SendToBack.Text(), () => _host.RequestZOrder(this, front: false), Disabled: disabled),
            ShellCommand.ToggleEditMode => _host.EditMode
                ? new(Glyphs.Done, Str.Shell_Card_DoneEditing.Text(), () => _host.RequestEditMode(false))
                : new(Glyphs.Edit, Str.Shell_Card_EditLayout.Text(), () => _host.RequestEditMode(true)),
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
        public void SetCursorShape(InputSystemCursorShape shape)
            => ProtectedCursor = InputSystemCursor.Create(shape);
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
        if (_host.EditMode)
        {
            return;
        }

        _host.KeyboardPress(this);
    }

    // ---- IWidgetSurface ------------------------------------------------------------------------

    /// <inheritdoc />
    string? IWidgetSurface.Label => Model.Label;

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

    /// <inheritdoc />
    void IWidgetSurface.SetHeaderTitle(string? title)
    {
        if (string.Equals(_viewTitle, title, StringComparison.Ordinal))
        {
            return;
        }

        _viewTitle = title;
        ApplyHeader();
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
