using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6715] The Tile block, a one-readout widget's whole body: the glyph centred on the step
/// <see cref="TileRules.GlyphStep"/> gives its box, its label (Body) 8 under it and its state line
/// (Caption) under that. On, <see cref="TileLook.Tile"/> fills the whole tile with the accent and
/// draws on it in the on-accent roles; <see cref="TileLook.Round"/> puts the glyph in a circle (the
/// accent when on); <see cref="TileLook.Switch"/> is a Row with a switch as its value. Not on, the
/// tile (or the circle) is the item layer's ([S-6513]). Missing on this PC, every part is the disabled
/// role ([S-1940]).
/// <para>
/// [S-6736] It only draws: its owner keeps the whole tile as the hit target and tells it the
/// pointer's state (<see cref="Face"/>): the item layer's hover and press, or over the accent the
/// subtle fills laid on it. A ringed tile (a power button's) keeps its glyph inside the ring
/// (<see cref="TileRules.RingGlyph"/>), so the arm ring (<see cref="Ring"/>, the Chart's ring, drained
/// on the compositor) moves nothing when it shows.
/// </para>
/// </summary>
public sealed partial class TileBlock : Grid
{
    private readonly Border _overlay = new() { IsHitTestVisible = false };
    private readonly StackPanel _stack;
    private readonly Grid _mark;
    private readonly Border _circle;
    private readonly Border _circleOverlay = new() { IsHitTestVisible = false };
    private readonly FontIcon _glyph;
    private readonly TextBlock _label;
    private readonly TextBlock _state;
    private readonly Grid _switchRow;
    private readonly FontIcon _switchGlyph;
    private readonly StackPanel _switchLines;
    private readonly ToggleSwitch _switch;
    private readonly bool _ringed;
    private TileLook _look;
    private TileState _tone;
    private ItemState _face = ItemState.Rest;
    private TypeStep _step = FamilyType.Big28;
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
        _mark = new Grid { HorizontalAlignment = HorizontalAlignment.Center, Children = { _circle, _glyph, Ring } };

        _label = Family.Text(FamilyType.Body, ColourRole.TextPrimary);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.Margin = new Thickness(0, FamilySpace.S2, 0, 0);
        _state = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
        _state.HorizontalAlignment = HorizontalAlignment.Center;
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

        Children.Add(_overlay);
        Children.Add(_stack);
        Children.Add(_switchRow);
        CornerRadius = Family.Radius;
        _overlay.CornerRadius = Family.Radius;
        Apply();
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
    /// [S-6715] Lay the glyph out for the tile's box less its inset, planned for a
    /// <paramref name="label"/> and a <paramref name="state"/> line whether or not they say anything
    /// now, so a change of state moves nothing.
    /// </summary>
    public void Size(double width, double height, bool label = true, bool state = true)
    {
        _width = width;
        _planLabel = label;
        _planState = state;
        _step = TileRules.GlyphStep(width, height, label, state);
        Apply();
    }

    /// <summary>
    /// [S-11307] Lay the glyph out on <paramref name="step"/>, chosen by the widget's Size, in a box
    /// <paramref name="width"/> wide (the switch look's row), planned for a <paramref name="label"/> and a
    /// <paramref name="state"/> line whether or not they say anything now.
    /// </summary>
    public void Use(TypeStep step, double width, bool label = true, bool state = true)
    {
        _width = width;
        _planLabel = label;
        _planState = state;
        _step = step;
        Apply();
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
            ItemFill.Release(this);
            Background = null;
            _overlay.Background = null;
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
        _state.HorizontalAlignment = _label.HorizontalAlignment;
        _label.Visibility = _planLabel ? Visibility.Visible : Visibility.Collapsed;
        _state.Visibility = _planState ? Visibility.Visible : Visibility.Collapsed;

        // A planned line keeps its height while it says nothing (a reading not answered yet).
        _state.Height = FamilyType.Caption.LineHeight;
        _label.Foreground = Family.Brush(roles.Label);
        _state.Foreground = Family.Brush(roles.State);
        _switchGlyph.Foreground = Family.Brush(roles.Glyph);
        _switch.IsOn = _tone == TileState.On;
        _switch.IsEnabled = !unavailable;

        // [S-6736] The switch's row: the glyph gives way first, then the lines.
        var parts = TileRules.Switch(_width);
        _switchGlyph.Visibility = parts.Glyph ? Visibility.Visible : Visibility.Collapsed;
        _switchLines.Visibility = parts.Lines ? Visibility.Visible : Visibility.Collapsed;

        var size = _step.FontSize;
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
            var inner = _ringed ? TileRules.RingGlyph(size) : size;
            _glyph.FontSize = inner;
            _glyph.Width = inner;
            _glyph.Height = inner;
        }

        _glyph.Foreground = Family.Brush(roles.Glyph);
        _mark.Width = size;
        _mark.Height = size;
    }
}
