using Themia.Core.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6715] [S-11882] The Tile block, a one-readout widget's whole body: the glyph centred on the step
/// its box holds (<see cref="TileRules.Face"/>), its label 8 under it and its state line under that, the
/// words growing with the glyph (Body and Caption, Title and Body past 68). On, <see cref="TileLook.Tile"/> fills the whole tile with the accent and
/// draws on it in the on-accent roles; <see cref="TileLook.Round"/> puts the glyph in a circle (the
/// accent when on); <see cref="TileLook.Switch"/> is a Row with a switch as its value. Not on, the
/// tile (or the circle) is the item layer's ([S-6513]). Missing on this PC, every part is the disabled
/// role ([S-1940]).
/// <para>
/// [S-14201] The face (the glyph or its circle or ring, the label and the state line) stands centred on its ink, not its
/// box (<see cref="ReadingFit"/>: the glyph's room in its em box, the last line's room under its baseline or descender), so
/// the bands over and under it are alike (owner, 2026-11-19: "Centred is fine"). [S-14306] It is one block that scales up
/// as one (WR-7: "A reading and its caption stay together and scale up") until its ink fills the tile's height or the
/// width stops it, then stands centred on the other axis: the tile exemption relaxes only the bands over and under it
/// being alike, never its reach ("nothing else of WR-1..WR-4 is relaxed"). Its words wrap at the inset's width as
/// planned; the block is as wide as its widest part.
/// </para>
/// <para>
/// [S-6736] It only draws: its owner keeps the whole tile as the hit target and tells it the
/// pointer's state (<see cref="Face"/>): the item layer's hover and press, or over the accent the
/// subtle fills laid on it. A ringed tile (a power button's) draws its arm ring (<see cref="Ring"/>, the Chart's ring,
/// drained on the compositor) round the glyph, past the mark's box (<see cref="TileRules.RingAround"/>, [S-15740]); while it
/// shows it is the face's ink, so the armed face stands centred too.
/// </para>
/// </summary>
public sealed partial class TileBlock : Grid
{
    private readonly Border _overlay = new() { IsHitTestVisible = false };
    private readonly StackPanel _stack;

    /// <summary>[S-14201] [S-14306] The face centred on its ink in the tile's inset, scaled up as one to fill it.</summary>
    private readonly ReadingFit _fit = new() { Margin = default };

    /// <summary>[S-17153] The planned words' box, handed to the fit whole (its ink the box), so tiles of one size scale alike.</summary>
    private readonly PlannedFace _plannedFace = new();
    private readonly Grid _mark;
    private readonly Border _circle;
    private readonly Border _circleOverlay = new() { IsHitTestVisible = false };
    private readonly FontIcon _glyph;

    /// <summary>[S-15740] The arm ring's box round the glyph, past the mark's on every side.</summary>
    private readonly RingRoom _ringRoom;

    /// <summary>
    /// [S-15764] The words' planned width (the widest of what the tile may say: its name and every sentence of its state line)
    /// and the state line's planned lines: the block keeps that box in every state, so a change of state (armed, failed,
    /// refused) never rescales or moves it (WR-12) and a longer sentence wraps or is cut within it. 0: the words' own.
    /// </summary>
    private double _planWidth;
    private int _stateLines = 1;

    /// <summary>[S-12806] The glyph's box when the face gives one smaller than its step's, else 0.</summary>
    private double _mark0;
    private readonly TextBlock _label;
    private readonly TextBlock _state;
    private readonly Grid _switchRow;
    private readonly FontIcon _switchGlyph;
    private readonly StackPanel _switchLines;
    private readonly ToggleSwitch _switch;

    /// <summary>[S-17150] The room WinUI's ToggleSwitch template keeps over and under its 20 track (measured: 38 tall, the track 10 from its top).</summary>
    private const double SwitchTemplateAbove = 10, SwitchTemplateBelow = 8;
    private readonly bool _ringed;
    private TileLook _look;
    private TileState _tone;
    private ItemState _face = ItemState.Rest;
    private TypeStep _step = FamilyType.Big28;
    private TypeStep _nameStep = FamilyType.Body;
    private TypeStep _stateStep = FamilyType.Caption;
    private double _width;
    private bool _planLabel = true;
    private bool _planState = true;
    private bool _inSwitch;

    /// <param name="look">The tile's look.</param>
    /// <param name="ringed">[S-6736] Whether the glyph stands inside a ring (a power button's arm ring).</param>
    public TileBlock(TileLook look = TileLook.Tile, bool ringed = false)
    {
        _look = look;
        _ringed = ringed;
        _glyph = Family.Glyph(string.Empty, _step.FontSize, ColourRole.TextPrimary);
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.VerticalAlignment = VerticalAlignment.Center;
        _circle = new Border { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = _circleOverlay };
        Ring = new ChartRing { Visibility = Visibility.Collapsed };
        Ring.Role(ColourRole.Critical);
        _ringRoom = new RingRoom { Children = { Ring } };

        // [S-15740] The ring showing (armed) or going is a change of the face's ink: laid out again.
        Ring.RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => Apply());
        _mark = new Grid { HorizontalAlignment = HorizontalAlignment.Center, Children = { _circle, _glyph, _ringRoom } };

        _label = Family.Text(FamilyType.Body, ColourRole.TextPrimary);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.Margin = new Thickness(0, FamilySpace.S2, 0, 0);
        _state = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
        _state.HorizontalAlignment = HorizontalAlignment.Center;

        // [S-15710] A state line longer than the tile's inset (an icon's why) is cut with its ellipsis, never past the edge.
        _state.TextTrimming = TextTrimming.CharacterEllipsis;
        _stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _mark, _label, _state } };

        // The switch look: a row, the label and state at the text edge, the switch at the end.
        _switchGlyph = Family.Glyph(string.Empty);
        _switchGlyph.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        _switchLines = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, FamilySpace.ColumnGap, 0) };
        _switch = new ToggleSwitch
        {
            OnContent = string.Empty,
            OffContent = string.Empty,
            MinWidth = 0,
            Width = TileRules.SwitchWidth,
            // [S-17150] The row lays out the track alone (ShellMetrics.Page.SwitchHeight, 20): the control's template stands 10 over
            // and 8 under it, which a one-line row's inset would otherwise have to hold.
            Margin = new Thickness(0, -SwitchTemplateAbove, 0, -SwitchTemplateBelow),
            AllowFocusOnInteraction = false,
            IsTabStop = false,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };

        // The tile's own inset is the row's: no padding of its own.
        _switchRow = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        SetColumn(_switchLines, 1);
        SetColumn(_switch, 2);
        _switchRow.Children.Add(_switchGlyph);
        _switchRow.Children.Add(_switchLines);
        _switchRow.Children.Add(_switch);

        _plannedFace.Children.Add(_stack);
        _fit.Parts.Add(_plannedFace);
        Children.Add(_overlay);
        Children.Add(_fit);
        Children.Add(_switchRow);
        CornerRadius = Family.Radius;
        _overlay.CornerRadius = Family.Radius;
        Apply();
    }

    /// <summary>
    /// [S-15755] The lines <paramref name="name"/> takes under a tile's glyph at <paramref name="width"/> (the inset's), on
    /// the Body step it is planned at: 1, or 2 where it wraps (<see cref="TileRules.NameLines"/> at most).
    /// </summary>
    public static int NameLines(string name, double width) => Lines(name, width, FamilyType.Body);

    /// <summary>
    /// [S-15764] The words' plan in an inset <paramref name="inner"/> wide: their box as wide as the widest of the name (Body)
    /// and every sentence the state line may say (<paramref name="states"/>, Caption) on one line, at most the inset; the
    /// name's lines and the state line's lines (the most any sentence takes) at that width.
    /// </summary>
    public static (double Width, int NameLines, int StateLines) Plan(string name, IEnumerable<string> states, double inner)
    {
        // [S-17153] One box whatever the words say: the widest name a quick setting or a power button has in this language on
        // one line, so every tile of one size draws its glyph and name alike and at one place, none wrapped.
        var width = TileRules.WordsRoom(inner, WidestName());
        var stateLines = states.Where(s => !string.IsNullOrEmpty(s)).Select(s => Lines(s, width, FamilyType.Caption)).DefaultIfEmpty(1).Max();
        return (width, 1, stateLines);
    }

    /// <summary>[S-17153] The widest quick setting's or power button's name on one Body line in the current language (measured once a language).</summary>
    private static double WidestName()
    {
        var locale = Strings.Current;
        if (_widest is { } known && known.Locale == locale)
        {
            return known.Width;
        }

        var names = QuickToggleRules.All.Select(t => QuickToggleRules.Name(locale, t)).Concat(PowerActionRules.All.Select(a => PowerActionRules.Label(locale, a)));
        var width = Math.Ceiling(names.Select(n => LinesAndWidth(n, double.PositiveInfinity, FamilyType.Body).Width).DefaultIfEmpty(0).Max());
        _widest = (locale, width);
        return width;
    }

    private static (Locale Locale, double Width)? _widest;

    /// <summary>[S-17153] Whether the fit takes the words' planned box whole (its words planned) or the stack's own ink (the glyph alone).</summary>
    private void Plan(bool planned)
    {
        if (_plannedFace.Whole != planned)
        {
            _plannedFace.Whole = planned;
            _fit.Refit();
        }
    }

    /// <summary>[S-15764] The lines <paramref name="text"/> takes at <paramref name="width"/> on <paramref name="step"/> (1 or 2), and its one-line width.</summary>
    public static int Lines(string text, double width, TypeStep step) => LinesAndWidth(text, width, step).Lines;

    /// <summary>[S-15764] <see cref="Lines"/>, with the width the text takes on one line.</summary>
    public static (int Lines, double Width) LinesAndWidth(string text, double width, TypeStep step)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (1, 0);
        }

        var probe = Family.Text(step, ColourRole.TextPrimary);
        probe.Text = text;
        probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var one = probe.DesiredSize.Width;
        if (!(width > 0) || !double.IsFinite(width) || one <= width)
        {
            return (1, one);
        }

        probe.TextWrapping = TextWrapping.WrapWholeWords;
        probe.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
        return (Math.Clamp((int)Math.Round(probe.DesiredSize.Height / step.LineHeight), 1, TileRules.NameLines), one);
    }

    /// <summary>[S-6736] The arm ring over the glyph (a power button's), the Critical role, hidden until it drains.</summary>
    public ChartRing Ring { get; }

    /// <summary>Show the tile: its glyph, label, state line and what its colours say.</summary>
    public void Show(string glyph, string label, string state, TileState tone)
    {
        _glyph.Glyph = glyph;
        _switchGlyph.Glyph = glyph;
        _label.Text = label;
        _state.Text = state;
        _tone = tone;
        Apply();
    }

    /// <summary>The tile's look.</summary>
    public TileLook Look
    {
        get => _look;
        set
        {
            _look = value;
            Apply();
        }
    }

    /// <summary>[S-6736] The pointer's state over the tile: at rest, under it, or pressed.</summary>
    public void Face(ItemState face)
    {
        if (face != _face)
        {
            _face = face;
            Apply();
        }
    }

    /// <summary>
    /// [S-11882] Lay the glyph out on <paramref name="step"/>, chosen by the box (<see cref="TileRules.Face"/>),
    /// in a box <paramref name="width"/> wide (the switch look's row), planned for a <paramref name="label"/>
    /// on <paramref name="name"/>'s step and a <paramref name="state"/> line on <paramref name="stateStep"/>'s
    /// (Body and Caption when not given) whether or not they say anything now.
    /// </summary>
    public void Use(TypeStep step, double width, bool label = true, bool state = true, TypeStep? name = null, TypeStep? stateStep = null, double mark = 0, double planWidth = 0, int stateLines = 1)
    {
        _planWidth = planWidth;
        _stateLines = Math.Clamp(stateLines, 1, TileRules.NameLines);
        _mark0 = mark;
        _width = width;
        _planLabel = label;
        _planState = state;
        _step = step;
        _nameStep = name ?? FamilyType.Body;
        _stateStep = stateStep ?? FamilyType.Caption;
        Apply();
    }

    /// <summary>
    /// [S-11883] The tile's fill takes the card's corners (round where the card is, square where a
    /// neighbour joins it), so a joined panel of tiles shows no notch at its seams.
    /// </summary>
    public void Corners(CornerRadius corners)
    {
        CornerRadius = corners;
        _overlay.CornerRadius = corners;
    }

    private void Apply()
    {
        var roles = TileRules.Roles(_look, _tone);
        var unavailable = _tone == TileState.Unavailable;
        var item = unavailable ? ItemState.Disabled : _face;
        var onAccent = _look != TileLook.Round && roles.Fill is not null;
        var lit = item switch
        {
            ItemState.Hover => Family.Brush(ColourRole.Subtle),
            ItemState.Pressed => Family.Brush(ColourRole.SubtlePressed),
            _ => null,
        };

        // [S-6513] Not on, the tile is an item of the card's item layer; on, it is the accent's,
        // the subtle fills laid over it under the pointer.
        if (_look == TileLook.Round)
        {
            // [S-15765] The round look's item is the whole tile too (WR-3, WR-13): under the pointer and pressed the item layer's
            // fill lights it edge to edge (the tile's own background, past its padding), not the circle alone.
            _overlay.Background = null;
            if (item is ItemState.Hover or ItemState.Pressed)
            {
                ItemFill.Paint(this, ItemRole.Row, item);
            }
            else
            {
                ItemFill.Release(this);
                Background = null;
            }
        }
        else if (onAccent && roles.Fill is { } fill)
        {
            ItemFill.Release(this);
            Background = Family.Brush(fill);
            _overlay.Background = lit;
        }
        else
        {
            ItemFill.Paint(this, ItemRole.Row, item);
            _overlay.Background = null;
        }

        var switchLook = _look == TileLook.Switch;
        _stack.Visibility = switchLook ? Visibility.Collapsed : Visibility.Visible;
        _switchRow.Visibility = switchLook ? Visibility.Visible : Visibility.Collapsed;

        // The label and state move between the stack and the switch's row with the look.
        if (switchLook != _inSwitch)
        {
            var (from, into) = switchLook ? ((Panel)_stack, (Panel)_switchLines) : (_switchLines, _stack);
            from.Children.Remove(_label);
            from.Children.Remove(_state);
            into.Children.Add(_label);
            into.Children.Add(_state);
            _inSwitch = switchLook;
        }

        _label.Margin = switchLook ? default : new Thickness(0, FamilySpace.S2, 0, 0);
        _label.HorizontalAlignment = switchLook ? HorizontalAlignment.Left : HorizontalAlignment.Center;

        // [S-11804] Under the glyph a long name ("Do not disturb") wraps to a second line, which the tile's
        // square holds (TileRules.SideAt), before it is cut; the switch's row keeps it to one.
        // [S-17153] Planned words keep one line (owner, 2026-11-23: "the text should not wrap"), cut with their ellipsis.
        var oneLine = switchLook || (_planWidth > 0 && _planLabel);
        _label.TextWrapping = oneLine ? TextWrapping.NoWrap : TextWrapping.WrapWholeWords;
        _label.MaxLines = oneLine ? 1 : TileRules.NameLines;
        _label.TextTrimming = TextTrimming.CharacterEllipsis;
        _label.TextAlignment = switchLook ? TextAlignment.Left : TextAlignment.Center;
        _state.HorizontalAlignment = _label.HorizontalAlignment;
        _label.Visibility = _planLabel ? Visibility.Visible : Visibility.Collapsed;
        _state.Visibility = _planState ? Visibility.Visible : Visibility.Collapsed;

        // [S-11882] The words on the steps the box gives them; the switch's row keeps Body and Caption.
        Family.Step(_label, switchLook ? FamilyType.Body : _nameStep);
        Family.Step(_state, switchLook ? FamilyType.Caption : _stateStep);

        // A planned line keeps its height while it says nothing (a reading not answered yet); [S-15764] on the lines its longest
        // sentence takes, cut with its ellipsis past them.
        var stateLines = switchLook ? 1 : _stateLines;
        _state.Height = stateLines * (switchLook ? FamilyType.Caption : _stateStep).LineHeight;
        _state.TextWrapping = stateLines > 1 ? TextWrapping.WrapWholeWords : TextWrapping.NoWrap;
        _state.MaxLines = stateLines;
        _state.TextTrimming = TextTrimming.CharacterEllipsis;
        _state.TextAlignment = switchLook ? TextAlignment.Left : TextAlignment.Center;
        _label.Foreground = Family.Brush(roles.Label);
        _state.Foreground = Family.Brush(roles.State);
        _switchGlyph.Foreground = Family.Brush(roles.Glyph);
        _switch.IsOn = _tone == TileState.On;
        _switch.IsEnabled = !unavailable;

        // [S-6736] The switch's row: the glyph gives way first, then the lines.
        var parts = TileRules.Switch(_width);
        _switchGlyph.Visibility = parts.Glyph ? Visibility.Visible : Visibility.Collapsed;
        _switchLines.Visibility = parts.Lines ? Visibility.Visible : Visibility.Collapsed;

        // [S-12806] A box too small for the step's glyph draws it in a smaller mark (the face's).
        var size = _mark0 > 0 ? _mark0 : _step.FontSize;
        if (_look == TileLook.Round)
        {
            _circle.Width = size;
            _circle.Height = size;
            _circle.CornerRadius = Family.Round(size, size);
            _circleOverlay.CornerRadius = _circle.CornerRadius;
            _circle.Visibility = Visibility.Visible;
            if (roles.Circle is { } circle)
            {
                ItemFill.Release(_circle);
                ItemFill.Release(_glyph);
                _circle.Background = Family.Brush(circle);
                _circleOverlay.Background = lit;
            }
            else
            {
                // [S-6546] The circle is the item; the glyph laid over it is inked as its text.
                ItemFill.Paint(_circle, ItemRole.Tile, item);
                ItemFill.OnItem(_glyph);
                _circleOverlay.Background = null;
            }

            var inner = TileRules.RoundGlyph(size);
            _glyph.FontSize = inner;
            _glyph.Width = inner;
            _glyph.Height = inner;
        }
        else
        {
            _circle.Visibility = Visibility.Collapsed;
            _glyph.FontSize = size;
            _glyph.Width = size;
            _glyph.Height = size;
        }

        // [S-15740] The arm ring round the glyph, drawn past the mark's box on every side and never laid out: the glyph is the
        // mark at its step and the name stands 8 under it, armed or not.
        var ring = _ringed && _look != TileLook.Round ? TileRules.RingAround(size) : size;
        var past = -(ring - size) / 2;
        _ringRoom.Width = ring;
        _ringRoom.Height = ring;
        _ringRoom.Margin = new Thickness(past);

        // While the ring shows, the mark makes room for it, so the face (ring, glyph, sentence) is fitted and centred on it.
        // [S-15751] At rest the ring's box is no part of the face at all (collapsed with its ring): a visible empty box read
        // as ink across the mark's whole width, and a glyph whose ink stands off its em box's middle (the speaker, the
        // microphone, the moon) was centred by its box, its ink off centre (WR-7's "Tiles stand centred").
        var shows = Ring.Visibility == Visibility.Visible;
        _mark.Margin = shows ? new Thickness(-past) : default;
        _ringRoom.Visibility = shows ? Visibility.Visible : Visibility.Collapsed;

        _glyph.Foreground = Family.Brush(roles.Glyph);
        _mark.Width = size;
        _mark.Height = size;

        // [S-14201] Its words wrap at the inset's width, as they did in the tile's own box; [S-14306] the block is as wide as
        // its widest part (the glyph, a wrapped name, the state line), so the fit scales it until the width stops it.
        // [S-15764] A planned width holds the words in one box in every state.
        var planned = !switchLook && _planWidth > 0 && (_planLabel || _planState);
        var wrap = planned ? _planWidth : _planLabel || _planState ? Math.Max(size, _width) : double.PositiveInfinity;
        _label.MaxWidth = wrap;
        _state.MaxWidth = wrap;
        _stack.MinWidth = planned ? Math.Max(size, _planWidth) : 0;

        Plan(planned && _planLabel);

        // [S-14201] Its ink may have moved in a box of the same size (a sentence with a descender, the ring shown, "--"
        // for "Off"), which XAML does not arrange again on its own: the fit places it again.
        _fit.Refit();
    }
}

/// <summary>
/// [S-17153] A tile's planned words and glyph as one box whose ink is the box: a short name's room either side and a
/// one-line name's room over and under count as drawn, so the fit scales every tile of one size alike.
/// </summary>
internal sealed partial class PlannedFace : Grid, IInkedBox
{
    /// <summary>Whether its box's width is its ink (the words planned); else what its stack draws across.</summary>
    public bool Whole { get; set; }

    /// <summary>Over and under: what its stack draws, so a face stands centred on its ink down the tile.</summary>
    public LineInk Ink => Children.Count == 0 ? default : Themia.App.Widgets.Ink.Of(Children[0]).Ink;

    public bool IsRow => false;

    public bool OwnAcross => !Whole;
}

/// <summary>
/// [S-15740] The arm ring's box, drawn round the glyph past the mark's: the face's ink only while the ring shows (armed), so
/// the face stands centred on what it draws (the tile exemption: "the bands over and under a tile's face are alike"), the
/// ring's top included; at rest the glyph's and the words' ink say where the block stands.
/// </summary>
internal sealed partial class RingRoom : Grid, IInked
{
    /// <inheritdoc />
    public LineInk Ink => Children.Any(c => c.Visibility == Visibility.Visible) ? LineInk.None : new(DesiredSize.Height, DesiredSize.Height);

    /// <inheritdoc />
    public bool IsRow => false;
}
