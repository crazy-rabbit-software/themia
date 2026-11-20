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
