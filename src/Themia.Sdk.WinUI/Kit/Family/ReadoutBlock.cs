using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6702] The Readout block: [lead glyph at the step's size] 8 [value, 600] [unit, its step,
/// secondary] [a delta, Body, Caution or Critical when it is bad] and one line under it at the step's
/// line (<see cref="FamilyType.Under"/>). The step is <see cref="ReadoutRules.Step"/>'s for the
/// widest case the widget names, so it does not jump with the reading. A one-readout widget's
/// readout is centred; every other is start-aligned.
/// </summary>
public sealed partial class ReadoutBlock : StackPanel
{
    private readonly FontIcon _lead;
    private readonly TextBlock _line;
    private readonly Run _value = new();
    private readonly Run _unit = new();
    private readonly Run _delta = new();
    private readonly TextBlock _under;
    private readonly StackPanel _row;
    private TypeStep _step = FamilyType.Big28;
    private ColourRole _valueRole = ColourRole.TextPrimary;
    private ColourRole _deltaRole = ColourRole.TextSecondary;
    private bool _centred;
    private bool _underFits = true;

    public ReadoutBlock()
    {
        _lead = Family.Glyph(string.Empty, _step.FontSize, ColourRole.TextSecondary);
        _lead.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        _lead.Visibility = Visibility.Collapsed;

        _line = new TextBlock { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.None, VerticalAlignment = VerticalAlignment.Center };
        _line.Inlines.Add(_value);
        _line.Inlines.Add(_unit);
        _line.Inlines.Add(_delta);

        _row = new StackPanel { Orientation = Orientation.Horizontal, Children = { _lead, _line } };
        _under = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
        _under.Visibility = Visibility.Collapsed;

        Children.Add(_row);
        Children.Add(_under);
        Apply();
    }

    /// <summary>The step the readout is drawn on.</summary>
    public TypeStep Step => _step;

    /// <summary>The line under the value.</summary>
    public TextBlock UnderText => _under;

    /// <summary>
    /// Show a reading: the value, its unit (a leading space where one is drawn), the line under it, a
    /// delta and its role, a lead glyph and the value's role (Caution or Critical while it is bad).
    /// </summary>
    public void Show(string value, string unit = "", string? under = null, string? delta = null, ColourRole deltaRole = ColourRole.TextSecondary, string? lead = null, ColourRole valueRole = ColourRole.TextPrimary)
    {
        _value.Text = value;
        _unit.Text = unit;

        // An en space before the delta: a run has no margin of its own.
        _delta.Text = string.IsNullOrEmpty(delta) ? string.Empty : " " + delta;
        _deltaRole = deltaRole;
        _valueRole = valueRole;
        _under.Text = under ?? string.Empty;
        _lead.Glyph = lead ?? string.Empty;
        _lead.Visibility = string.IsNullOrEmpty(lead) ? Visibility.Collapsed : Visibility.Visible;
        Apply();
    }

    /// <summary>
    /// [S-6702] Choose the step for the box the readout has (<paramref name="width"/> × <paramref name="height"/>
    /// in a view given <paramref name="area"/>) and the widest case <paramref name="worst"/>; a
    /// one-readout case also centres it.
    /// </summary>
    public void Size(WidgetArea area, double width, double height, ReadoutText worst)
    {
        _step = ReadoutRules.Step(area, width, height, worst);
        _centred = worst.OneReadout;

        // A box too short for the line under even the floor step sheds that line, never the value.
        _underFits = !worst.LineUnder || ReadoutRules.Height(_step, worst) <= height;
        Apply();
    }

    /// <summary>Draw the readout on a step chosen elsewhere (a Tile's, a row of readouts sharing one).</summary>
    public void Use(TypeStep step, bool centred = false)
    {
        _step = step;
        _centred = centred;
        Apply();
    }

    private void Apply()
    {
        Family.Step(_line, _step);
        _line.Foreground = Family.Brush(_valueRole);
        var unit = FamilyType.Unit(_step);
        _unit.FontSize = unit.FontSize;
        _unit.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        _unit.Foreground = Family.Brush(ColourRole.TextSecondary);
        _delta.FontSize = FamilyType.Body.FontSize;
        _delta.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        _delta.Foreground = Family.Brush(_deltaRole);
        _lead.FontSize = _step.FontSize;
        _lead.Width = _step.FontSize;
        _lead.Height = _step.FontSize;
        Family.Step(_under, FamilyType.Under(_step));
        _under.Visibility = _underFits && _under.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        var align = _centred ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _row.HorizontalAlignment = align;
        _under.HorizontalAlignment = align;
        _under.TextAlignment = _centred ? TextAlignment.Center : TextAlignment.Left;
    }
}
