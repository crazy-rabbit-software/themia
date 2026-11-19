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
/// [S-11874] The local grid of a move: the grid lines only round the box the block lands in, fading out with their
/// distance from it (<see cref="LocalGrid"/>), and the target's cells lit in the accent's wash. Fades in as the move
/// starts and out, slower ([S-11886]), as it lands. [S-11890] The lines are the whole screen's grid, drawn once for a
/// screen in <see cref="LocalGrid.Steps"/> layers (one geometry path each, never one element per line); a new target
/// only moves each layer's clip (<see cref="LocalGrid.Levels"/>) and the lit box, so following the pointer from cell
/// to cell builds nothing.
/// </summary>
internal sealed partial class LocalGridOverlay : Canvas
{
    private readonly Path[] _layers = new Path[LocalGrid.Steps];
    private readonly RectangleGeometry[] _clips = new RectangleGeometry[LocalGrid.Steps];
    private readonly Rectangle _lit = new()
    {
        Fill = HexBrush.Wash(ColourRole.Accent),
        IsHitTestVisible = false,
    };

    private (FracRect Box, bool Lines)? _target;
    private (double Width, double Height, double Scale) _drawnFor;

    public LocalGridOverlay()
    {
        IsHitTestVisible = false;
        Opacity = 0;
        OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(LocalGrid.FadeInMs) };
        Children.Add(_lit);
        for (var i = 0; i < LocalGrid.Steps; i++)
        {
            _clips[i] = new RectangleGeometry();
            _layers[i] = new Path
            {
                Stroke = DesignApplier.Brush("TextFillColorPrimaryBrush", Colors.Gray),
                IsHitTestVisible = false,
                Clip = _clips[i],
            };
            Children.Add(_layers[i]);
        }
    }

    /// <summary>
    /// Show the local grid round <paramref name="target"/> on a screen of <paramref name="metrics"/> drawn at
    /// <paramref name="scale"/>, or fade it out with null. With <paramref name="lines"/> false only the lit box shows
    /// (edit mode, whose whole grid is drawn already). The lines are drawn again only for another screen; another
    /// target moves their clips.
    /// </summary>
    public void Show(FracRect? target, ScreenMetrics metrics, double scale, bool lines = true)
    {
        if (target is not { } box || !metrics.IsUsable)
        {
            // [S-11886] Out slower than in: the grid lingers a moment where the block landed.
            Fade(LocalGrid.FadeOutMs, 0);
            _target = null;
            return;
        }

        var drawn = Prepare(metrics, scale);
        if (drawn || _target != (box, lines))
        {
            _target = (box, lines);
            Place(box, metrics, lines);
        }

        Fade(LocalGrid.FadeInMs, 1);
    }

    /// <summary>
    /// [S-11890] Draw the lines for a screen of <paramref name="metrics"/> at <paramref name="scale"/> if they are not
    /// drawn for it yet, so the first move on a surface builds nothing; true when they were drawn now.
    /// </summary>
    public bool Prepare(ScreenMetrics metrics, double scale)
    {
        var s = scale > 0 && double.IsFinite(scale) ? scale : 1;
        if (!metrics.IsUsable || _drawnFor == (metrics.Width, metrics.Height, s))
        {
            return false;
        }

        _drawnFor = (metrics.Width, metrics.Height, s);
        var (minorPx, _) = GridGeometry.LineWidthsPx(s);
        var (cols, rows) = GridGeometry.GridDims(metrics);
        for (var i = 0; i < LocalGrid.Steps; i++)
        {
            // Each layer its own geometry: a geometry is drawn by one path only.
            var group = new GeometryGroup();
            for (var c = 0; c <= cols; c++)
            {
                var x = GridGeometry.CrispLine(GridFit.LineDip(c, cols, metrics.Width), s, minorPx);
                group.Children.Add(new LineGeometry { StartPoint = new Point(x, 0), EndPoint = new Point(x, metrics.Height) });
            }

            for (var r = 0; r <= rows; r++)
            {
                var y = GridGeometry.CrispLine(GridFit.LineDip(r, rows, metrics.Height), s, minorPx);
                group.Children.Add(new LineGeometry { StartPoint = new Point(0, y), EndPoint = new Point(metrics.Width, y) });
            }

            _layers[i].StrokeThickness = minorPx / s;
            _layers[i].Data = group;
        }

        return true;
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

    /// <summary>Each layer's clip and opacity for <paramref name="target"/>, and the lit box.</summary>
    private void Place(FracRect target, ScreenMetrics metrics, bool lines)
    {
        var levels = LocalGrid.Levels(target, metrics);
        for (var i = 0; i < LocalGrid.Steps; i++)
        {
            var shown = lines && i < levels.Count;
            _layers[i].Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            if (shown)
            {
                var region = levels[i].Region;
                _clips[i].Rect = new Rect(region.X, region.Y, Math.Max(0, region.Width), Math.Max(0, region.Height));
                _layers[i].Opacity = levels[i].Opacity;
            }
        }

        var lit = LocalGrid.Lit(target, metrics);
        SetLeft(_lit, lit.X);
        SetTop(_lit, lit.Y);
        _lit.Width = lit.Width;
        _lit.Height = lit.Height;
    }
}
