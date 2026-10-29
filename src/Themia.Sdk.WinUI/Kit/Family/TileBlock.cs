using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6715] The Tile block, a one-readout widget's whole body: the glyph centred on the step
/// <see cref="TileRules.GlyphStep"/> gives its box, its label (Body) 8 under it and its state line
/// (Caption) under that. On, <see cref="TileLook.Tile"/> fills the whole tile with the accent and
/// draws on it in the on-accent roles; <see cref="TileLook.Round"/> puts the glyph in a circle (the
/// accent when on); <see cref="TileLook.Switch"/> is a Row with a switch as its value. Missing on this
/// PC, every part is the disabled role ([S-1940]). A power button's arm ring is laid over the glyph
/// (<see cref="Ring"/>, the Chart's stroke).
/// </summary>
public sealed partial class TileBlock : Grid
{
    private readonly StackPanel _stack;
    private readonly Grid _mark;
    private readonly Border _circle;
    private readonly FontIcon _glyph;
    private readonly TextBlock _label;
    private readonly TextBlock _state;
    private readonly Grid _switchRow;
    private readonly FontIcon _switchGlyph;
    private readonly StackPanel _switchLines;
    private readonly ToggleSwitch _switch;
    private TileLook _look;
    private bool _on;
    private bool _available = true;
    private TypeStep _step = FamilyType.Big28;
    private bool _inSwitch;

    public TileBlock(TileLook look = TileLook.Tile)
    {
        _look = look;
        _glyph = Family.Glyph(string.Empty, _step.FontSize, ColourRole.TextPrimary);
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _circle = new Border { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Ring = new ChartRing { Visibility = Visibility.Collapsed };
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
        _switchLines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _switch = new ToggleSwitch { OnContent = string.Empty, OffContent = string.Empty, MinWidth = 0, AllowFocusOnInteraction = false, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
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

        Children.Add(_stack);
        Children.Add(_switchRow);
        CornerRadius = Family.Radius;
        Apply();
    }

    /// <summary>The arm ring over the glyph (a power button's), hidden until shown.</summary>
    public ChartRing Ring { get; }

    /// <summary>Show the tile: its glyph, label, state line, whether it is on and whether what it switches exists here.</summary>
    public void Show(string glyph, string label, string state, bool on, bool available = true)
    {
        _glyph.Glyph = glyph;
        _switchGlyph.Glyph = glyph;
        _label.Text = label;
        _state.Text = state;
        _state.Visibility = state.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _on = on;
        _available = available;
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

    /// <summary>[S-6715] Lay the glyph out for the tile's box less its inset.</summary>
    public void Size(double width, double height)
    {
        _step = TileRules.GlyphStep(width, height, label: _label.Text.Length > 0, state: _state.Text.Length > 0);
        Apply();
    }

    private void Apply()
    {
        var roles = TileRules.Roles(_look, _on, _available);
        Background = roles.Fill is { } fill ? Family.Brush(fill) : null;

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
        _label.Foreground = Family.Brush(roles.Label);
        _state.Foreground = Family.Brush(roles.State);
        _switchGlyph.Foreground = Family.Brush(roles.Glyph);
        _switch.IsOn = _on;
        _switch.IsEnabled = _available;

        var size = _step.FontSize;
        if (_look == TileLook.Round)
        {
            _circle.Width = size;
            _circle.Height = size;
            _circle.CornerRadius = Family.Round(size, size);
            _circle.Background = roles.Circle is { } circle ? Family.Brush(circle) : null;
            _circle.Visibility = Visibility.Visible;
            var inner = TileRules.RoundGlyph(size);
            _glyph.FontSize = inner;
            _glyph.Width = inner;
            _glyph.Height = inner;
            _glyph.Foreground = Family.Brush(_on && _available ? ColourRole.TextOnAccent : roles.Glyph);
        }
        else
        {
            _circle.Visibility = Visibility.Collapsed;
            _glyph.FontSize = size;
            _glyph.Width = size;
            _glyph.Height = size;
            _glyph.Foreground = Family.Brush(roles.Glyph);
        }

        _mark.Width = size;
        _mark.Height = size;
    }
}
