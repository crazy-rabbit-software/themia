using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-15000]..[S-15005] The one face of every icon widget (owner, 2026-11-21: "there should be a shared widget backend for
/// widgets that show just an icon"): one glyph, the Tile block's face without words (<see cref="TileBlock"/>, the quick
/// toggles' and power buttons' one face, [S-11882]), in the family's inset (giving way in a small card,
/// <see cref="IconRules.Inset"/>), planned on the step its box holds (<see cref="IconRules.Step"/>) and scaled as one until
/// its ink fills the card's height or the width stops it, centred on the other axis (owner, 2026-11-19, of the tiles:
/// "Centred is fine"). The card edge to edge is its one item (the tile, filled through the item layer): the item layer's tile at rest, the accent while it is on
/// (play while playing), the disabled role while it cannot be read or pressed; its glyph in the item's text role, so the
/// widget's Item text colour colours it ([S-6540]). Where it acts it lights under the pointer and pressed (WR-13; WR-12:
/// only the fill changes): a kind that opens something (<see cref="IconKind.Opens"/>) opens it on a click (the card's,
/// [S-2000]), Enter and its card menu's first line; a family that presses something (<see cref="Presses"/>: a transport
/// button) is a button edge to edge that takes the press itself (<see cref="IWidgetButton"/>) and acts on the release,
/// Space or Enter. In edit mode the press is the card's, never the icon's ([S-1911]). A kind that does neither lights
/// nothing. A family subclasses it for its data and hands each read to <see cref="Present"/>.
/// <para>
/// [S-15710] [S-15711] [S-15722] WR-10: while it cannot be read the glyph stays, drawn unavailable, and a card with the room
/// says why in one compact line beside or under it (<see cref="IconRules.Beside"/>, <see cref="IconRules.Under"/>); a failure
/// is the status mark's too, at every size, as a stale reading's.
/// </para>
/// </summary>
public abstract partial class IconView : Grid, IWidgetView, IWidgetOpens, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys,
    IWidgetCorners, IWidgetTopEdge, IWidgetSurfaceAware
{
    private const string Scope = "icons";

    private readonly IWidgetHost _host;
    private readonly IconKind _kind;
    private readonly KitStatus _status;
    private readonly TileBlock _tile = new(TileLook.Tile);

    /// <summary>[S-15716] Why it cannot be read, beside the glyph in a card too short for it under the glyph.</summary>
    private readonly TextBlock _beside = Family.Text(FamilyType.Caption, TileRules.Roles(TileLook.Tile, TileState.Unavailable).State);

    /// <summary>[S-15005] The face: a button edge to edge where the family presses, else the item alone.</summary>
    private readonly Face _face;

    private IconShown _shown;
    private WidgetConfig? _config;

    /// <summary>[S-15712] The config a click opens for in place of the card's: a preview's sample place; null for the card's.</summary>
    private WidgetConfig? _openFor;
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private bool _hover;
    private bool _pressed;
    private bool _hidden;
    private bool _detached = true;

    /// <param name="host">The card's host: the status glyph's account link, the launcher a click opens through.</param>
    /// <param name="kind">The kind: its glyph and what it opens.</param>
    /// <param name="presses">[S-15005] Whether a click presses something of the family's (<see cref="Press"/>).</param>
    protected IconView(IWidgetHost host, IconKind kind, bool presses = false)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Presses = presses;
        _shown = new IconShown(kind.Glyph) { Name = kind.Label.Text() };
        _status = new KitStatus(host);
        _face = presses ? new ButtonFace() : new Face();

        // The whole card is hit-testable, edge to edge: the tile is the item.
        _face.Background = Family.Brush(ColourRole.Clear);
        _face.Children.Add(_tile);
        _beside.TextWrapping = TextWrapping.NoWrap;
        _beside.TextTrimming = TextTrimming.CharacterEllipsis;
        _beside.HorizontalAlignment = HorizontalAlignment.Left;
        _beside.VerticalAlignment = VerticalAlignment.Center;
        _beside.IsHitTestVisible = false;
        _beside.Visibility = Visibility.Collapsed;
        _face.Children.Add(_beside);
        Children.Add(_face);
        _status.Place(this);

        // [S-15160] The status glyph stands in the card's top end corner, on the inset round the glyph: it takes none of the
        // glyph's room, so the icon stays centred at its size whatever the status says.
        _status.StandAt(FamilySpace.S1, FamilySpace.S1);
    }

    /// <summary>[S-15005] Whether a click presses something of the family's rather than opening a source.</summary>
    protected bool Presses { get; }

    /// <summary>[S-5700] It owns its top edge: the icon is centred in its own inset, its item the card edge to edge.</summary>
    public bool OwnsTopEdge => true;

    /// <summary>[S-15004] What a click opens: the kind's source for the card's config and what it shows; null for none now.</summary>
    public CardOpen? Opens => !Presses && (_openFor ?? _config) is { } config && _kind.Opens is { } open && !_hidden ? open.Target(config, _shown) : null;

    /// <summary>[S-15004] The card menu's own lines: the line that opens the source first, then the family's.</summary>
    public IReadOnlyList<WidgetAction> ContextActions
        => _kind.Opens is { } open && Opens is not null
            ? [new WidgetAction(Glyphs.Open, open.Label.Text(), () => Open()), .. OwnActions]
            : OwnActions;

    /// <summary>The family's own lines on the card menu (Refresh, where the source has one).</summary>
    protected virtual IReadOnlyList<WidgetAction> OwnActions => [];

    /// <summary>[S-15004] [S-15005] A click takes the keyboard only where Enter acts.</summary>
    public bool WantsKeys => Acts;

    /// <summary>Whether the card is a preview (the add page's picture, a render): it reads and presses nothing of the machine.</summary>
    protected bool IsPreview => _surface?.IsPreview == true;

    /// <summary>[S-15005] Whether the press can do something now (a player that can skip); true unless the family says.</summary>
    protected virtual bool CanPress => _shown.State != TileState.Unavailable;

    /// <summary>Whether a click does something now: opens the source, or presses what can be pressed.</summary>
    private bool Acts => !_hidden && (Presses ? CanPress : Opens is not null);

    /// <summary>[S-1911] In edit mode the card owns the press: the widget is selected, never run.</summary>
    private bool Inert => _detached || _surface?.EditMode == true || !Acts;

    public void Bind(IWidgetSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        OnBind(surface);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _face.PointerEntered += OnPointerEntered;
        _face.PointerExited += OnPointerExited;
        _face.PointerCaptureLost += OnPointerGone;
        _face.PointerCanceled += OnPointerGone;
        _face.AddHandler(PointerPressedEvent, (PointerEventHandler)OnPointerPressed, handledEventsToo: true);
        _face.AddHandler(PointerReleasedEvent, (PointerEventHandler)OnPointerReleased, handledEventsToo: true);
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        // [S-12831] What the icon shows under the pointer and pressed, for the renders' --hover and --pressed.
        PointerPreview.Register(_face, pressed =>
        {
            _hover = !Inert;
            _pressed = pressed && !Inert;
            Paint();
        });
        _config = config;
        Lay();
        OnAttach(ticker, config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config;
        _openFor = null;
        OnApply(config);
        Paint();
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _face.PointerEntered -= OnPointerEntered;
        _face.PointerExited -= OnPointerExited;
        _face.PointerCaptureLost -= OnPointerGone;
        _face.PointerCanceled -= OnPointerGone;
        _face.RemoveHandler(PointerPressedEvent, (PointerEventHandler)OnPointerPressed);
        _face.RemoveHandler(PointerReleasedEvent, (PointerEventHandler)OnPointerReleased);
        Strings.Changed -= OnLanguage;
        PointerPreview.Forget(_face);
        OnDetach();
    }

    /// <summary>[S-15002] The card's box: the glyph's step and the inset in it.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
        Paint();
    }

    /// <summary>[S-11883] The item's fill reaches the card's edges: it takes the card's corners.</summary>
    public void ApplyCorners(CornerRadius corners) => _tile.Corners(corners);

    /// <summary>[S-15004] [S-15005] Enter opens or presses, Space presses, while the card has the keyboard.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (!Acts)
        {
            return false;
        }

        if (Presses && key is WidgetKey.Space or WidgetKey.Enter)
        {
            Press();
            return true;
        }

        return key == WidgetKey.Enter && Open();
    }

    /// <summary>The card's surface is known: a family that presses or reads the machine asks it whether this is a preview.</summary>
    protected virtual void OnBind(IWidgetSurface surface)
    {
    }

    /// <summary>Hold the family's data for <paramref name="config"/>, presenting what it holds at once.</summary>
    protected abstract void OnAttach(Ticker ticker, WidgetConfig config);

    /// <summary>A changed config: what the data is about may differ (another place, another icon).</summary>
    protected abstract void OnApply(WidgetConfig config);

    /// <summary>Let go of the data. Safe twice.</summary>
    protected abstract void OnDetach();

    /// <summary>[S-15005] Press what the icon stands for (a family's with <see cref="Presses"/>): never optimistic, its data draws the result.</summary>
    protected virtual void Press()
    {
    }

    /// <summary>
    /// [S-15001] Put one read on the face. [S-15711] Its problems are the status mark's whatever its state: a reading kept
    /// through a failed refresh, or the failure that left it unreadable, so a failure is never quieter than a stale reading.
    /// </summary>
    protected void Present(IconShown shown)
    {
        _shown = shown ?? throw new ArgumentNullException(nameof(shown));
        _status.Show(_shown.Problems, busy: false);
        Lay();
        Paint();
    }

    /// <summary>
    /// [S-15712] A preview's sample stands somewhere the card's config does not say (the sample place): a click opens, and the
    /// item lights, for <paramref name="config"/> until the card's config changes.
    /// </summary>
    protected void OpenFor(WidgetConfig config)
    {
        _openFor = config ?? throw new ArgumentNullException(nameof(config));
        Paint();
    }

    /// <summary>
    /// [S-1464] Paint nothing (a family's hide-when-idle), or paint again. Never in a preview card: it always draws; edit mode
    /// always shows it (the card's rule).
    /// </summary>
    protected void Hide(bool hide)
    {
        hide &= !IsPreview;
        if (hide == _hidden)
        {
            return;
        }

        _hidden = hide;
        _surface?.SetHidden(hide);
    }

    /// <summary>Another language: the family writes its words again.</summary>
    private void OnLanguage(Locale locale)
    {
        if (_config is { } config)
        {
            OnApply(config);
        }

        Paint();
    }

    /// <summary>
    /// [S-15002] The glyph on the step its box holds in the inset that box leaves, alike on every side; the fit centres and
    /// scales it. [S-15160] The status glyph takes none of that room: it stands in the card's corner, over the inset.
    /// </summary>
    private void Lay()
    {
        var natural = IconRules.NaturalSide;
        var (width, height) = _area.IsMeasured ? (_area.Width, _area.Height) : (natural, natural);
        var inset = IconRules.Inset(width, height);
        var why = Why;
        var marked = _shown.Problems.Count > 0;

        // [S-15722] The why in one compact line: beside the glyph where it stands whole there (a wide card), else under it,
        // Caption down to the floor, else beside it cut; with no room for either the glyph alone (the mark, the tooltip).
        IconBeside? beside = null;
        IconWhy? under = null;
        if (why is not null && _area.IsMeasured)
        {
            // A collapsed text measures nothing: it is shown to be measured, and collapsed again where it has no room.
            _beside.Text = why;
            _beside.Width = double.NaN;
            _beside.MaxWidth = double.PositiveInfinity;
            _beside.Margin = default;
            _beside.FontSize = FamilyType.Caption.FontSize;
            _beside.LineHeight = FamilyType.Caption.LineHeight;
            _beside.Visibility = Visibility.Visible;
            _beside.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var sentence = _beside.DesiredSize.Width;
            beside = IconRules.Beside(width, height, sentence, marked, Aspect(_shown.Glyph is { Length: > 0 } g ? g : _kind.Glyph));
            if (beside is not { } whole || whole.TextWidth < sentence)
            {
                under = IconRules.Under(width, height, sentence, Aspect(_shown.Glyph is { Length: > 0 } u ? u : _kind.Glyph));
                if (under is not null)
                {
                    beside = null;
                }
            }
        }

        // [S-15717] Where the inset has given way the glyph stands clear of the status mark.
        var box = IconRules.Box(width, height, marked);
        _status.StandAt(box.Mark, box.Mark);
        if (beside is { } at)
        {
            _tile.Padding = new Thickness(at.GlyphLeft, inset, width - at.GlyphLeft - at.Glyph, inset);
            _tile.Use(IconRules.Step(at.GlyphHeight, at.GlyphHeight), at.Glyph, label: false, state: false);
            _beside.HorizontalAlignment = HorizontalAlignment.Left;
            _beside.VerticalAlignment = VerticalAlignment.Center;
            _beside.Margin = new Thickness(at.TextLeft, 0, 0, 0);
            _beside.Width = at.TextWidth;
            _beside.Visibility = Visibility.Visible;
            return;
        }

        var edges = box.Glyph;
        var band = under?.Band ?? 0;
        if (under is { } line)
        {
            // [S-15722] Under the glyph: one line at its foot, its size its own (never scaled with the glyph).
            _beside.FontSize = line.FontSize;
            _beside.LineHeight = line.FontSize * FamilyType.Caption.LineHeight / FamilyType.Caption.FontSize;
            _beside.HorizontalAlignment = HorizontalAlignment.Center;
            _beside.VerticalAlignment = VerticalAlignment.Bottom;
            _beside.Margin = new Thickness(inset, 0, inset, line.GlyphTop);
            _beside.MaxWidth = double.PositiveInfinity;
            _beside.Visibility = Visibility.Visible;
        }
        else
        {
            _beside.Visibility = Visibility.Collapsed;
        }

        _tile.Padding = under is { } block
            ? new Thickness(inset, block.GlyphTop, inset, block.GlyphBottom)
            : new Thickness(edges.Left, edges.Top, edges.Right, edges.Bottom);
        _tile.Use(IconRules.Step(width, height - band), width - edges.Left - edges.Right, label: false, state: false);
    }

    /// <summary>[S-15716] A glyph's ink, its width over its height, as the meter measures it (1 where it cannot).</summary>
    private static double Aspect(string glyph)
    {
        if (Aspects.TryGetValue(glyph, out var known))
        {
            return known;
        }

        var probe = Family.Glyph(glyph, 100, ColourRole.TextPrimary);
        var room = InkMeter.Room(new InkText(glyph, Ink.FontOf(probe.FontFamily), 100, probe.FontWeight.Weight, 0));
        var aspect = room is { } r && 100 - r.Over - r.Under > 0 && 100 - r.Left - r.Right > 0 ? (100 - r.Left - r.Right) / (100 - r.Over - r.Under) : 1;
        Aspects[glyph] = aspect;
        return aspect;
    }

    private static readonly Dictionary<string, double> Aspects = [];

    /// <summary>[S-15710] Why it cannot be read, where it says: null while it can be, or with no reason given.</summary>
    private string? Why => _shown.State == TileState.Unavailable && _shown.Why is { Length: > 0 } why ? why : null;


    /// <summary>The whole look from the read: the glyph, its state, hover and pressed, its tooltip and its name.</summary>
    private void Paint()
    {
        var name = _shown.Name ?? _kind.Label.Text();
        _tile.Show(_shown.Glyph is { Length: > 0 } glyph ? glyph : _kind.Glyph, name, string.Empty, _shown.State);
        _tile.Face(_shown.State == TileState.Unavailable || Inert ? ItemState.Rest : _pressed ? ItemState.Pressed : _hover ? ItemState.Hover : ItemState.Rest);
        ToolTipService.SetToolTip(_face, _shown.Tooltip);
        AutomationProperties.SetName(_face, name);
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hover = !Inert;
        Paint();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hover = false;
        _pressed = false;
        Paint();
    }

    private void OnPointerGone(object sender, PointerRoutedEventArgs e)
    {
        _pressed = false;
        Paint();
    }

    /// <summary>
    /// [S-15005] A press lights the item; a family's button takes it (so its card never opens on it) and acts on the release,
    /// as any button: a press that slid off before release is no click. An opening icon leaves the press to the card ([S-2000]).
    /// </summary>
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Inert || !e.GetCurrentPoint(_face).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (Presses)
        {
            _pressed = _face.CapturePointer(e.Pointer);
            e.Handled = true;
        }
        else
        {
            _pressed = true;
        }

        Paint();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        if (Presses)
        {
            _face.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
            var at = e.GetCurrentPoint(_face).Position;
            if (at.X >= 0 && at.Y >= 0 && at.X <= _face.ActualWidth && at.Y <= _face.ActualHeight && !Inert)
            {
                Press();
            }
        }

        Paint();
    }

    /// <summary>[S-15004] Open the source through the host's launcher; said in the log where nothing opened.</summary>
    private bool Open()
    {
        if (Opens is not { } target)
        {
            return false;
        }

        var outcome = _host.Launcher.OpenCard(target);
        if (outcome != LaunchOutcome.Opened)
        {
            Log.Warn(Scope, $"{_kind.Id}: opening its source opened nothing ({outcome})");
        }

        return true;
    }

    /// <summary>
    /// [S-15001] The face, the card edge to edge. [S-15160] It leaves the status glyph no room (no <see cref="IStatusRoom"/>):
    /// the glyph stays centred at its size, the status standing in the card's corner over the inset.
    /// </summary>
    private partial class Face : Grid;

    /// <summary>[S-2007] [S-15005] A pressing icon's face: one button edge to edge that takes the press itself.</summary>
    private sealed partial class ButtonFace : Face, IWidgetButton;
}
