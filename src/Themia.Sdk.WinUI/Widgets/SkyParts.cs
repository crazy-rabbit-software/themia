using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Astronomy;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

// [S-3100] [S-6735] The parts the Sun and Moon widgets share, on the family's blocks: one fact row and
// one moon drawing, so the two views stay thin and cannot drift apart.

/// <summary>
/// The moon at its current phase, drawn the same way by the Moon widget's readout and its rows' leads. [S-169]
/// <para>
/// The lit limb comes from <see cref="MoonDisc"/>, so the large disc and a row's lead are the same
/// drawing and cannot disagree about how much of the moon is lit. [S-6735] It is a lead, so it is in
/// the lead's role (secondary); the unlit part keeps a hairline rim in the tertiary role, so a thin
/// crescent still reads as a moon rather than as a stray mark; below the horizon both step down a role
/// (tertiary, disabled) rather than being faded with an opacity.
/// </para>
/// </summary>
public sealed class SkyMoonDisc
{
    private readonly Ellipse _rim = new();
    private readonly Polygon _lit = new();

    private double _size;
    private double _phase;
    private bool _southern;

    public SkyMoonDisc()
    {
        _rim.StrokeThickness = FamilyShape.Hairline;
        _rim.Fill = null;
        _rim.HorizontalAlignment = HorizontalAlignment.Center;
        _rim.VerticalAlignment = VerticalAlignment.Center;
        Paint(up: true);

        Root = new Grid { Children = { _rim, _lit } };
        Resize(FamilyShape.Icon);
    }

    public Grid Root { get; }

    public void Resize(double size)
    {
        if (Math.Abs(size - _size) < 0.5)
        {
            return;
        }

        _size = size;
        Root.Width = size;
        Root.Height = size;
        _rim.Width = size - FamilyShape.Hairline;
        _rim.Height = size - FamilyShape.Hairline;
        Rebuild();
    }

    public void Update(double phase, bool southern, bool up)
    {
        Paint(up);

        if (Math.Abs(phase - _phase) < 0.0005 && southern == _southern)
        {
            return;
        }

        _phase = phase;
        _southern = southern;
        Rebuild();
    }

    private void Paint(bool up)
    {
        _lit.Fill = Family.Brush(up ? ColourRole.TextSecondary : ColourRole.TextTertiary);
        _rim.Stroke = Family.Brush(up ? ColourRole.TextTertiary : ColourRole.TextDisabled);
    }

    private void Rebuild()
    {
        var radius = Math.Max(1, (_size - FamilyShape.Hairline) / 2);
        var centre = _size / 2;
        var points = new PointCollection();

        foreach (var point in MoonDisc.LitLimb(centre, centre, radius, _phase, _southern))
        {
            points.Add(new Point(point.X, point.Y));
        }

        _lit.Points = points;
    }
}

/// <summary>
/// [S-6735] One row of the Sun or Moon widget: the family's fact row ([S-6711]) — a 16 lead (a glyph, or
/// the moon drawn at 16), what it is (secondary), the time at the end (primary). The next event's
/// name is BodyStrong, never in the accent; its countdown, or a plain row's note ("civil", "35°"), is
/// the row's second line where the tier gives rows two lines ([S-6712]). The name takes its short form
/// where the long one and the value do not fit side by side (<see cref="SkyRows.Name"/>).
/// </summary>
public sealed class SkyEventRow
{
    private string _name = string.Empty;
    private string? _shortName;
    private string _value = string.Empty;
    private string? _second;
    private bool _next;
    private double _room = double.PositiveInfinity;

    /// <summary>A row led by a 16 glyph.</summary>
    public SkyEventRow(string glyph)
    {
        Root.LeadGlyph(glyph);
    }

    /// <summary>A row led by a drawing of the lead's size (the moon at its phase).</summary>
    public SkyEventRow(FrameworkElement lead)
    {
        Root.Lead(lead);
    }

    public RowBlock Root { get; } = new(RowKind.Fact);

    /// <summary>[S-6712] The lines the row shows (its pitch follows): the second is the countdown or the note.</summary>
    public int Lines
    {
        get => Root.Lines;
        set => Root.Lines = value;
    }

    /// <summary>The width the name and the value share (<see cref="SkyRows.Room"/> of the row's width).</summary>
    public double Room
    {
        get => _room;
        set
        {
            _room = value;
            Show();
        }
    }

    /// <summary>The name, and a shorter one ("Rise" for "Moonrise") for a narrow row.</summary>
    public void SetLabel(string label, string? shortLabel = null)
    {
        _name = label;
        _shortName = shortLabel;
        Show();
    }

    /// <summary>A rise or a set: the next one (a countdown given) is emphasised and carries it as its second line.</summary>
    public void Set(string value, string? countdown)
    {
        _value = value;
        _second = countdown;
        _next = countdown is not null;
        Show();
    }

    /// <summary>A row that is never "next": its value, and a quiet note as its second line.</summary>
    public void SetPlain(string value, string? note)
    {
        _value = value;
        _second = note;
        _next = false;
        Show();
    }

    private void Show()
    {
        Root.Strong = _next;
        Root.Show(SkyRows.Name(_room, _name, _shortName, _value), _value, _second);
    }
}
