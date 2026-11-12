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
    private ColourRole _leadRole = ColourRole.TextSecondary;
    private ColourRole _underRole = ColourRole.TextSecondary;
    private bool _painted;
    private bool _centred;
    private HorizontalAlignment? _align;

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
    /// [S-6330] Show a reading: the value, its unit (a leading space where one is drawn), the line under
    /// it, a delta and its role, a lead glyph and its role, and the value's role (the disabled role for
    /// a part missing on this PC, [S-1940]; the unit and the line under follow it). What is the same as
    /// before is not touched, so a live reading costs its changed text alone. [S-6707] A delta that
    /// folds under the value (<see cref="ReadoutRules.Fold"/>) is the line under, in the delta's role
    /// (<paramref name="underRole"/>).
    /// </summary>
    public void Show(string value, string unit = "", string? under = null, string? delta = null, ColourRole deltaRole = ColourRole.TextSecondary, string? lead = null, ColourRole valueRole = ColourRole.TextPrimary, ColourRole leadRole = ColourRole.TextSecondary, ColourRole underRole = ColourRole.TextSecondary)
    {
        Set(_value, value);
        Set(_unit, unit);

        // An en space before the delta: a run has no margin of its own.
        Set(_delta, string.IsNullOrEmpty(delta) ? string.Empty : " " + delta);
        if (_under.Text != (under ?? string.Empty))
        {
            _under.Text = under ?? string.Empty;
            ShowUnder();
        }

        if (_lead.Glyph != (lead ?? string.Empty))
        {
            _lead.Glyph = lead ?? string.Empty;
            _lead.Visibility = string.IsNullOrEmpty(lead) ? Visibility.Collapsed : Visibility.Visible;
        }

        if (deltaRole != _deltaRole || valueRole != _valueRole || leadRole != _leadRole || underRole != _underRole || !_painted)
        {
            _deltaRole = deltaRole;
            _underRole = underRole;
            _valueRole = valueRole;
            _leadRole = leadRole;
            Paint();
        }
    }

    private static void Set(Run run, string text)
    {
        if (run.Text != text)
        {
            run.Text = text;
        }
    }

    /// <summary>Draw the readout on a step chosen elsewhere (a Tile's, a row of readouts sharing one).</summary>
    public void Use(TypeStep step, bool centred = false)
    {
        _step = step;
        _centred = centred;
        _align = null;
        Apply();
    }

    /// <summary>
    /// Draw the readout on a step chosen elsewhere, its value, its lines (a stacked clock's hours over its
    /// minutes) and its line under aligned to <paramref name="align"/>: a widget whose one block the person
    /// aligns ([S-6581], the clock) sets the readout's own alignment with it.
    /// </summary>
    public void Use(TypeStep step, HorizontalAlignment align)
    {
        _step = step;
        _centred = align == HorizontalAlignment.Center;
        _align = align;
        Apply();
    }

    private void Apply()
    {
        Family.Step(_line, _step);
        var unit = FamilyType.Unit(_step);
        _unit.FontSize = unit.FontSize;
        _unit.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        _delta.FontSize = FamilyType.Body.FontSize;
        _delta.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        _lead.FontSize = _step.FontSize;
        _lead.Width = _step.FontSize;
        _lead.Height = _step.FontSize;
        Family.Step(_under, FamilyType.Under(_step));
        ShowUnder();

        var align = _align ?? (_centred ? HorizontalAlignment.Center : HorizontalAlignment.Left);
        var text = align switch
        {
            HorizontalAlignment.Center => TextAlignment.Center,
            HorizontalAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };
        _row.HorizontalAlignment = align;

        // A readout of several lines aligns them only when its owner aligned it (a stacked clock); a
        // one-line readout keeps the start, so its glyphs stand where they always did.
        _line.TextAlignment = _align is null ? TextAlignment.Left : text;
        _under.HorizontalAlignment = align;
        _under.TextAlignment = text;
        Paint();
    }

    private void ShowUnder() => _under.Visibility = _under.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The roles' brushes: a missing part's unit and line under are disabled with its value.</summary>
    private void Paint()
    {
        _painted = true;
        var off = _valueRole == ColourRole.TextDisabled;
        _line.Foreground = Family.Brush(_valueRole);
        _unit.Foreground = Family.Brush(off ? ColourRole.TextDisabled : ColourRole.TextSecondary);
        _delta.Foreground = Family.Brush(off ? ColourRole.TextDisabled : _deltaRole);
        _under.Foreground = Family.Brush(off ? ColourRole.TextDisabled : _underRole);
        _lead.Foreground = Family.Brush(off ? ColourRole.TextDisabled : _leadRole);
    }
}
