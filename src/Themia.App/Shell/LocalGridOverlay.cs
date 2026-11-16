using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Geometry;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Themia.App.Shell;

/// <summary>
/// [S-11874] The local grid of a move on the locked desktop: the grid lines only round the box the block
/// lands in, fading out with their distance from it (<see cref="LocalGrid"/>), and the target's cells lit
/// in the accent's wash. Fades in as the move starts and out, slower ([S-11886]), as it lands. One geometry
/// path per strength step (never one element per line), rebuilt only when the target moves to another cell.
/// </summary>
internal sealed partial class LocalGridOverlay : Canvas
{
    /// <summary>How many strengths the lines are drawn in: the falloff rounded to these steps reads as smooth.</summary>
    private const int Steps = 10;

    private readonly Path[] _lines = new Path[Steps];
    private readonly Rectangle _lit = new()
    {
        Fill = HexBrush.Wash(ColourRole.Accent),
        IsHitTestVisible = false,
    };

    private FracRect? _target;
    private (double Width, double Height, double Scale) _drawnFor;

    public LocalGridOverlay()
    {
        IsHitTestVisible = false;
        Opacity = 0;
        OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(LocalGrid.FadeInMs) };
        Children.Add(_lit);
        for (var i = 0; i < Steps; i++)
        {
            _lines[i] = new Path
            {
                Stroke = DesignApplier.Brush("TextFillColorPrimaryBrush", Colors.Gray),
                Opacity = LocalGrid.PeakOpacity * (i + 1) / Steps,
                IsHitTestVisible = false,
            };
            Children.Add(_lines[i]);
        }
    }

    /// <summary>
    /// Show the local grid round <paramref name="target"/> on a screen of <paramref name="metrics"/> drawn at
    /// <paramref name="scale"/>, or fade it out with null. Redrawn only when the target or the screen changed.
    /// </summary>
    public void Show(FracRect? target, ScreenMetrics metrics, double scale)
    {
        if (target is not { } box || !metrics.IsUsable)
        {
            // [S-11886] Out slower than in: the grid lingers a moment where the block landed.
            Fade(LocalGrid.FadeOutMs, 0);
            _target = null;
            return;
        }

        var s = scale > 0 && double.IsFinite(scale) ? scale : 1;
        if (_target != box || _drawnFor != (metrics.Width, metrics.Height, s))
        {
            _target = box;
            _drawnFor = (metrics.Width, metrics.Height, s);
            Draw(box, metrics, s);
        }

        Fade(LocalGrid.FadeInMs, 1);
    }

    /// <summary>Fade to <paramref name="to"/> over <paramref name="ms"/>, the transition set before the opacity changes.</summary>
    private void Fade(double ms, double to)
    {
        if (Math.Abs(Opacity - to) < 1e-9)
        {
            return;
        }

        OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(ms) };
        Opacity = to;
    }

    private void Draw(FracRect target, ScreenMetrics metrics, double scale)
    {
        var (minorPx, _) = GridGeometry.LineWidthsPx(scale);
        var groups = new GeometryGroup[Steps];
        for (var i = 0; i < Steps; i++)
        {
            groups[i] = new GeometryGroup();
        }

        foreach (var segment in LocalGrid.Lines(target, metrics))
        {
            var step = Math.Clamp((int)Math.Ceiling(segment.Alpha * Steps) - 1, 0, Steps - 1);
            var vertical = segment.X1 == segment.X2;
            var start = vertical
                ? new Point(GridGeometry.CrispLine(segment.X1, scale, minorPx), segment.Y1)
                : new Point(segment.X1, GridGeometry.CrispLine(segment.Y1, scale, minorPx));
            var end = vertical
                ? new Point(start.X, segment.Y2)
                : new Point(segment.X2, start.Y);
            groups[step].Children.Add(new LineGeometry { StartPoint = start, EndPoint = end });
        }

        for (var i = 0; i < Steps; i++)
        {
            _lines[i].StrokeThickness = minorPx / scale;
            _lines[i].Data = groups[i];
        }

        var lit = LocalGrid.Lit(target, metrics);
        SetLeft(_lit, lit.X);
        SetTop(_lit, lit.Y);
        _lit.Width = lit.Width;
        _lit.Height = lit.Height;
    }
}
