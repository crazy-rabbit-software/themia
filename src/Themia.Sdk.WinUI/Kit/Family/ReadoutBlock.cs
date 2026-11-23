using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Windows.Foundation;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6702] The Readout block: [lead glyph at the step's size] 8 [value, 600] [unit, its step,
/// secondary] [a delta, Body, Caution or Critical when it is bad] and one line under it at the step's
/// line (<see cref="FamilyType.Under"/>). The step is <see cref="ReadoutRules.Step"/>'s for the
/// widest case the widget names, so it does not jump with the reading. A one-readout widget's
/// readout is centred; every other is start-aligned.
/// <para>
/// [S-12808] Its lines stand together at their natural spacing, always; a one-readout view scales the whole block
/// (<see cref="ReadingFit"/>), reading where its ink stands (<see cref="Ink"/>).
/// </para>
/// </summary>
public sealed partial class ReadoutBlock : SpreadStack, IInked, IFacePieces
{
    private readonly FontIcon _lead;
    private readonly TextBlock _line;
    private readonly Run _value = new();
    private readonly Run _unit = new();
    private readonly Run _delta = new();
    private readonly TextBlock _under;
    private readonly StackPanel _row;

    /// <summary>[S-14319] The value's line and, under it unseen, its held form: the cell is as wide as the wider.</summary>
    private readonly Grid _cell;
    private readonly TextBlock _held;
    private readonly Run _heldValue = new();
    private readonly Run _heldUnit = new();
    private string _template = string.Empty;

    /// <summary>[S-16607] Whether a held value stands where the alignment puts it, never padded.</summary>
    private bool _aligned;

    /// <summary>The value as last shown, before any padding.</summary>
    private string _shown = string.Empty;
    private string _templateUnit = string.Empty;
    private TypeStep _step = FamilyType.Big28;
    private ColourRole _valueRole = ColourRole.TextPrimary;
    private ColourRole _deltaRole = ColourRole.TextSecondary;
    private ColourRole _leadRole = ColourRole.TextSecondary;
    private ColourRole _underRole = ColourRole.TextSecondary;
    private bool _painted;
    private bool _centred;

    /// <summary>[S-12833] The least text size drawn: its unit, delta, lead and line under give way under it (0: all drawn).</summary>
    private double _floor;
    private string _unitText = string.Empty;
    private string _deltaText = string.Empty;
    private string _leadGlyph = string.Empty;
    private HorizontalAlignment? _align;

    public ReadoutBlock()
    {
        // [S-12814] Given more height than its lines (a block filling its card), the value stands on the top and the
        // line under on the foot, the room between them.
        Justify = true;
        _lead = Family.Glyph(string.Empty, _step.FontSize, ColourRole.TextSecondary);
        _lead.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        _lead.Visibility = Visibility.Collapsed;

        _line = new TextBlock { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.None, VerticalAlignment = VerticalAlignment.Center };
        _line.Inlines.Add(_value);
        _line.Inlines.Add(_unit);
        _line.Inlines.Add(_delta);

        _held = new TextBlock { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.None, VerticalAlignment = VerticalAlignment.Center, Opacity = 0, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        _held.Inlines.Add(_heldValue);
        _held.Inlines.Add(_heldUnit);
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_held, FontNumeralAlignment.Tabular);
        AutomationProperties.SetAccessibilityView(_held, AccessibilityView.Raw);
        _cell = new Grid { Children = { _held, _line } };

        _row = new StackPanel { Orientation = Orientation.Horizontal, Children = { _lead, _cell } };
        _under = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
        _under.Visibility = Visibility.Collapsed;

        Children.Add(_row);
        Children.Add(_under);
        Apply();
    }

    /// <summary>The step the readout is drawn on.</summary>
    public TypeStep Step => _step;

    /// <summary>
    /// [S-12808] The room in its box over its first ink and under its last: the value's (or its lead glyph's ink, its
    /// box centred on the value's line, where that stands higher: [S-14240]) and the line under's, or the value's.
    /// [S-14200] As they are drawn (<see cref="InkMeter"/>: the figures' own height, a descender, the unit beside the
    /// value on its baseline). [S-12813] A placeholder value ("--") as it is drawn too, its dashes lower than capitals (no
    /// meter: the model, <see cref="LineInk.Placeholder"/>), so a block scaled to fit (<see cref="ReadingFit"/>) stands its
    /// dashes on the top and its last ink on the foot.
    /// </summary>
    public LineInk Ink => Inked(Drawn());

    /// <summary>[S-14200] The value's line as drawn: the value and, on its baseline, the unit and the delta beside its last line.</summary>
    private LineInk Drawn()
    {
        var font = Themia.App.Widgets.Ink.FontOf(_line.FontFamily);
        var sized = SizedValue;
        var value = InkMeter.Measure(new InkText(sized, font, _step.FontSize, _line.FontWeight.Weight, _step.LineHeight, Tabular: Family.Tabular(_line)))
            ?? (LineInk.Capless(sized) ? LineInk.Placeholder(_step, _unit.Text.Length > 0 ? _unit.FontSize : 0) : LineInk.Of(_step));
        var oneLine = !sized.Contains('\n', StringComparison.Ordinal);
        foreach (var run in new[] { (Text: SizedUnit, _unit.FontSize, _unit.FontWeight), (Text: _delta.Text, _delta.FontSize, _delta.FontWeight) })
        {
            if (InkMeter.Measure(new InkText(run.Text, font, run.FontSize, run.FontWeight.Weight, _step.LineHeight, Tabular: Family.Tabular(_line))) is { } beside)
            {
                value = new LineInk(oneLine ? Math.Min(value.Over, beside.Over) : value.Over, Math.Min(value.Under, beside.Under));
            }
        }

        return value;
    }

    private LineInk Inked(LineInk line)
    {
        // [S-14240] The lead glyph's ink in its box, measured as every glyph's is (Ink): its whole box where unmeasured.
        var leads = _lead.Visibility == Visibility.Visible;
        var value = line.With(leads ? Themia.App.Widgets.Ink.Of(_lead).Ink : LineInk.None, leads ? _lead.Height : 0, _step.LineHeight);
        var step = FamilyType.Under(_step);
        var under = _under.Visibility != Visibility.Visible ? value.Under
            : InkMeter.Line(step, _under.Text, Themia.App.Widgets.Ink.FontOf(_under.FontFamily), _under.FontWeight.Weight).Under;
        return new LineInk(value.Over, under);
    }

    /// <inheritdoc />
    public bool IsRow => false;

    /// <summary>The line under the value.</summary>
    public TextBlock UnderText => _under;

    /// <summary>
    /// [S-14319] Hold the value to <paramref name="template"/> (<see cref="FigureTemplate"/>: its format's widest form) and
    /// the unit to <paramref name="unit"/> (its widest, a meridiem's): the readout is sized and its ink stood by them, never
    /// by the value it shows, which is padded at its start to the template's places. Empty: sized by what it shows.
    /// [S-16607] <paramref name="aligned"/>: the value is never padded but stands in the held box where the readout's
    /// alignment puts it (<see cref="Use(TypeStep, HorizontalAlignment)"/>: centred, or on its start), for a face whose
    /// other lines align so: its figures still tabular, the box still the template's, so it never moves as it runs.
    /// </summary>
    public void Hold(string template, string unit = "", bool aligned = false)
    {
        if (_template == (template ?? string.Empty) && _templateUnit == (unit ?? string.Empty) && _aligned == aligned)
        {
            return;
        }

        _template = template ?? string.Empty;
        _templateUnit = unit ?? string.Empty;
        _aligned = aligned;
        Set(_value, Held && !_aligned ? FigureTemplate.Pad(_shown, _template) : _shown);
        _heldValue.Text = _template;
        _heldUnit.Text = _unit.Text.Length > 0 ? _templateUnit : string.Empty;
        _held.Visibility = _template.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // [S-14319] Its figures tabular, every digit one width, whatever the design's type tokens say. [S-16432] Let go, the
        // readout is judged by what it draws again (its box was read as the template's after a hold let go).
        if (Held)
        {
            Family.Figures(_line);
            HoldMark.Mark(_cell);
        }
        else
        {
            HoldMark.Forget(_cell);
        }

        InvalidateMeasure();
    }

    /// <summary>[S-14319] Whether the readout is held to a template.</summary>
    private bool Held => _template.Length > 0;

    /// <summary>[S-14319] The value as sized: its template while held.</summary>
    private string SizedValue => Held ? _template : _value.Text;

    /// <summary>[S-14319] The unit as sized: its widest while held (none while none is drawn).</summary>
    private string SizedUnit => Held && _unit.Text.Length > 0 && _templateUnit.Length > 0 ? _templateUnit : _unit.Text;

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
        _shown = value ?? string.Empty;
        Set(_value, Held && !_aligned ? FigureTemplate.Pad(_shown, _template) : _shown);
        _unitText = unit ?? string.Empty;

        // An en space before the delta: a run has no margin of its own.
        _deltaText = string.IsNullOrEmpty(delta) ? string.Empty : " " + delta;
        _leadGlyph = lead ?? string.Empty;
        if (_under.Text != (under ?? string.Empty))
        {
            _under.Text = under ?? string.Empty;
        }

        Keep(_floor);

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
        _heldUnit.FontSize = unit.FontSize;
        _heldUnit.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        _delta.FontSize = FamilyType.Body.FontSize;
        _delta.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        _lead.FontSize = _step.FontSize;
        _lead.Width = _step.FontSize;
        _lead.Height = _step.FontSize;
        Family.Step(_under, FamilyType.Under(_step));
        Keep(_floor);

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
        _held.TextAlignment = _line.TextAlignment;
        _under.HorizontalAlignment = align;
        _under.TextAlignment = text;
        Paint();
    }

    private void ShowUnder()
    {
        var shown = _under.Text.Length > 0 && Draws(FamilyType.Under(_step).FontSize) ? Visibility.Visible : Visibility.Collapsed;
        if (_under.Visibility != shown)
        {
            _under.Visibility = shown;
        }
    }

    /// <summary>
    /// [S-12833] Draw only the pieces of at least <paramref name="floor"/> DIP (<see cref="FaceFit.Floor"/>): the unit, the
    /// delta, the lead glyph (as the text it reads like, <see cref="ScaleFloor.AsText"/>) and the line under give way, the
    /// value never. 0 draws them all.
    /// </summary>
    public void Keep(double floor)
    {
        _floor = double.IsFinite(floor) && floor > 0 ? floor : 0;
        Set(_unit, Draws(FamilyType.Unit(_step).FontSize) ? _unitText : string.Empty);
        Set(_heldUnit, _unit.Text.Length > 0 ? _templateUnit : string.Empty);
        Set(_delta, Draws(FamilyType.Body.FontSize) ? _deltaText : string.Empty);
        if (_lead.Glyph != _leadGlyph)
        {
            _lead.Glyph = _leadGlyph;
        }

        var lead = _leadGlyph.Length > 0 && Draws(ScaleFloor.AsText(_step.FontSize)) ? Visibility.Visible : Visibility.Collapsed;
        if (_lead.Visibility != lead)
        {
            _lead.Visibility = lead;
        }

        ShowUnder();
    }

    private bool Draws(double size) => size >= _floor - 1e-9;

    /// <summary>
    /// [S-14240] The lead glyph's ink, not its box, starts the line (WR-3: what stands on the start inset is ink, as a
    /// text's is): its box reaches back over the room its glyph leaves on the start side, as measured
    /// (<see cref="InkMeter.Room"/>), the gap to the value still from its box. Right to left the start is its right, the
    /// glyph drawn unmirrored. [S-14247] And the line's ink, not its advance, at its ends: the room its first and last
    /// letters leave there, as measured (a figure's, a unit's, a placeholder's dashes': <see cref="InkRoom.Ends"/>), so a
    /// block the width stops stands its ink on both side insets. Unmeasured (no meter): the boxes.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        // [S-14319] The held form in the line's type, as the design restyles the line.
        if (Held)
        {
            _held.FontSize = _line.FontSize;
            _held.LineHeight = _line.LineHeight;
            _held.LineStackingStrategy = _line.LineStackingStrategy;
            _held.FontWeight = _line.FontWeight;
            _held.CharacterSpacing = _line.CharacterSpacing;
            _held.FontFamily = _line.FontFamily;
        }

        var rtl = FlowDirection == FlowDirection.RightToLeft;
        var leads = _lead.Visibility == Visibility.Visible;
        var glyph = leads ? InkMeter.Room(new InkText(_lead.Glyph, Themia.App.Widgets.Ink.FontOf(_lead.FontFamily), _lead.FontSize, _lead.FontWeight.Weight, 0)) : null;

        // The glyph's em box stands centred in the icon's box (Ink's glyph).
        var em = double.IsFinite(_lead.Width) ? Math.Max(0, (_lead.Width - _lead.FontSize) / 2) : 0;
        var margin = new Thickness(glyph is { } g ? -(Math.Max(0, g.Start(rtl)) + em) : 0, 0, FamilySpace.GlyphGap, 0);
        if (_lead.Margin != margin)
        {
            _lead.Margin = margin;
        }

        // The line's tracking moves where its box ends after its last ink, as XAML puts it after each character.
        var font = Themia.App.Widgets.Ink.FontOf(_line.FontFamily);
        var tracking = _line.CharacterSpacing;
        // [S-14319] Held, by its template's ink: the line stands where the template's does, whatever it shows.
        var value = InkMeter.Room(new InkText(SizedValue, font, _step.FontSize, _line.FontWeight.Weight, _step.LineHeight, Tracking: tracking, Tabular: Family.Tabular(_line)));
        var run = _delta.Text.Length > 0 ? (Text: _delta.Text, _delta.FontSize, _delta.FontWeight)
            : _unit.Text.Length > 0 ? (Text: SizedUnit, _unit.FontSize, _unit.FontWeight) : default;
        var last = run.Text is null ? value : InkMeter.Room(new InkText(run.Text, font, run.FontSize, run.FontWeight.Weight, _step.LineHeight, Tracking: tracking, Tabular: Family.Tabular(_line)));
        var (start, end) = InkRoom.Ends(value, last, rtl, leads);
        var line = new Thickness(-start, 0, -end, 0);
        if (_cell.Margin != line)
        {
            _cell.Margin = line;
        }

        // [S-14301] The line under too: its ink, not its advance, at its ends (a block the width holds by its caption, the
        // weather's "Can't reach the service", stood 4.7 short of the end's inset).
        var step = FamilyType.Under(_step);
        var under = _under.Visibility == Visibility.Visible
            ? InkMeter.Room(new InkText(_under.Text, Themia.App.Widgets.Ink.FontOf(_under.FontFamily), step.FontSize, _under.FontWeight.Weight, step.LineHeight, Tracking: _under.CharacterSpacing))
            : null;
        var (underStart, underEnd) = InkRoom.Ends(under, under, rtl, led: false);
        var below = new Thickness(-underStart, 0, -underEnd, 0);
        if (_under.Margin != below)
        {
            _under.Margin = below;
        }

        return base.MeasureOverride(availableSize);
    }

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
