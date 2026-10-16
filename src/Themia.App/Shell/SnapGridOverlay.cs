using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Geometry;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Themia.App.Shell;

/// <summary>
/// [S-06a]/[S-06c] The edit-mode snap grid: two <see cref="Path"/>s, the fine lines and every
/// <see cref="GridGeometry.MajorEvery"/>th line heavier, each one geometry group rather than a few
/// thousand elements. Redrawn only when the surface lays out, never per frame.
/// </summary>
internal sealed partial class SnapGridOverlay : Grid
{
    /// <summary>
    /// [S-06b] Very faint: the mesh is fine (~10px cells), and at a heavier alpha this many lines
    /// reads as a grey wash over the desktop rather than as a grid.
    /// </summary>
    private const double MinorOpacity = 0.18;

    /// <summary>[S-06c] One in five lines, so it can carry a stronger alpha without the wash.</summary>
    private const double MajorOpacity = 0.26;

    private readonly Path _minor = Lines(MinorOpacity);
    private readonly Path _major = Lines(MajorOpacity);

    public SnapGridOverlay()
    {
        IsHitTestVisible = false;
        Children.Add(_minor);
        Children.Add(_major);
    }

    /// <summary>
    /// Draw the grid of <paramref name="metrics"/> (DIPs) at <paramref name="scale"/> physical pixels
    /// per DIP. Lines sit where the snap rounds to (multiples of screen/cols from the monitor's
    /// top-left), each on whole physical pixels so it stays crisp at any DPI.
    /// </summary>
    public void Draw(ScreenMetrics metrics, double scale)
    {
        var (cols, rows) = GridGeometry.GridDims(metrics);
        var (minorPx, majorPx) = GridGeometry.LineWidthsPx(scale);
        var s = scale > 0 && double.IsFinite(scale) ? scale : 1;

        var minor = new GeometryGroup();
        var major = new GeometryGroup();

        for (var c = 1; c < cols; c++)
        {
            var isMajor = GridGeometry.IsMajorLine(c);
            var x = GridGeometry.CrispLine(c * metrics.Width / cols, s, isMajor ? majorPx : minorPx);
            (isMajor ? major : minor).Children.Add(new LineGeometry { StartPoint = new Point(x, 0), EndPoint = new Point(x, metrics.Height) });
        }

        for (var r = 1; r < rows; r++)
        {
            var isMajor = GridGeometry.IsMajorLine(r);
            var y = GridGeometry.CrispLine(r * metrics.Height / rows, s, isMajor ? majorPx : minorPx);
            (isMajor ? major : minor).Children.Add(new LineGeometry { StartPoint = new Point(0, y), EndPoint = new Point(metrics.Width, y) });
        }

        _minor.StrokeThickness = minorPx / s;
        _major.StrokeThickness = majorPx / s;
        _minor.Data = minor;
        _major.Data = major;
    }

    /// <summary>
    /// Tertiary text, not a control stroke: the stroke brushes are low-alpha overlays meant for an
    /// opaque window, and over a transparent surface they disappear entirely.
    /// </summary>
    private static Path Lines(double opacity) => new()
    {
        Stroke = DesignApplier.Brush("TextFillColorTertiaryBrush", Colors.Gray),
        Opacity = opacity,
        IsHitTestVisible = false,
    };
}
