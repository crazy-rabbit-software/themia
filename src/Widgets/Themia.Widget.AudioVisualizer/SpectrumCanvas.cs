using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using Point = Windows.Foundation.Point;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3205] [S-3216] The audio visualizer's drawing, for every design: shapes placed once per layout
/// from a <see cref="VisualizerPlan"/>, and a frame that only writes numbers into them (a scale, an
/// opacity, a curve's points). No layout pass and no allocation per frame,
/// which is what lets it run at the capture rate on the UI thread for nothing. Edge to edge: the
/// canvas is the card's whole content area and the card's rounded corners clip it.
/// </summary>
internal sealed partial class SpectrumCanvas : Canvas
{
    /// <summary>An unlit LED cell: there, so the column reads as a meter, but quiet.</summary>
    private const double DimOpacity = 0.18;

    /// <summary>The wave's fill under its line.</summary>
    private const double AreaOpacity = 0.35;

    private const double LineThickness = 2;

    private readonly AudioVisualizerStyle _style;
    private readonly Brush _fill = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private readonly float[] _grouped = new float[Spectrum.Bands];
    private VisualizerPlan _plan;

    // Bars, mirror and radial: one rectangle per bar (two per band for radial), scaled from its base.
    private readonly Rectangle[] _bars;
    private readonly ScaleTransform[] _scales;

    // Dots: a pool of cells that only grows, and how many each column has lit.
    private readonly List<Rectangle> _cells = [];
    private readonly int[] _lit = new int[Spectrum.Bands];

    // Wave: one filled area and one line over it, sharing the same control points.
    private readonly Path? _area;
    private readonly Path? _line;
    private readonly PathFigure? _areaFigure;
    private readonly PathFigure? _lineFigure;
    private readonly LineSegment? _areaRise;
    private readonly LineSegment? _areaFall;
    private readonly List<BezierSegment> _areaCurve = [];
    private readonly List<BezierSegment> _lineCurve = [];
    private readonly double[] _heights = new double[Spectrum.Bands];
    private readonly (double X, double Y)[] _controls = new (double X, double Y)[3 * (Spectrum.Bands - 1)];

    // Scope ([S-3212]): one line through the trace's points, its segments a pool that only grows.
    private readonly Path? _scope;
    private readonly PathFigure? _scopeFigure;
    private readonly List<LineSegment> _scopeSegments = [];
    private readonly float[] _trace = new float[Oscilloscope.Points];
    private readonly float[] _scopePoints = new float[Oscilloscope.Points];

    public SpectrumCanvas(AudioVisualizerStyle style)
    {
        _style = style;
        _plan = VisualizerLayout.Plan(default, style);

        var shapes = style switch
        {
            AudioVisualizerStyle.Bars or AudioVisualizerStyle.Mirrored => Spectrum.Bands,
            AudioVisualizerStyle.Radial => 2 * Spectrum.Bands,
            _ => 0,
        };

        _bars = new Rectangle[shapes];
        _scales = new ScaleTransform[shapes];

        for (var i = 0; i < shapes; i++)
        {
            var scale = new ScaleTransform();
            _scales[i] = scale;
            _bars[i] = new Rectangle { Fill = _fill, RenderTransform = scale, Visibility = Visibility.Collapsed };
            Children.Add(_bars[i]);
        }

        if (style == AudioVisualizerStyle.Wave)
        {
            _areaRise = new LineSegment();
            _areaFall = new LineSegment();
            _areaFigure = new PathFigure { IsClosed = true, IsFilled = true };
            _lineFigure = new PathFigure { IsClosed = false, IsFilled = false };
            _area = new Path
            {
                Fill = _fill,
                Opacity = AreaOpacity,
                Data = new PathGeometry { Figures = { _areaFigure } },
            };
            _line = new Path
            {
                Stroke = _fill,
                StrokeThickness = LineThickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = new PathGeometry { Figures = { _lineFigure } },
            };
            Children.Add(_area);
            Children.Add(_line);
        }

        if (style == AudioVisualizerStyle.Waveform)
        {
            _scopeFigure = new PathFigure { IsClosed = false, IsFilled = false };
            _scope = new Path
            {
                Stroke = _fill,
                StrokeThickness = LineThickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = new PathGeometry { Figures = { _scopeFigure } },
            };
            Children.Add(_scope);
        }
    }

    /// <summary>Place the shapes for <paramref name="plan"/>; the levels are kept.</summary>
    public void Arrange(VisualizerPlan plan)
    {
        _plan = plan;

        switch (_style)
        {
            case AudioVisualizerStyle.Bars:
            case AudioVisualizerStyle.Mirrored:
                ArrangeBars(plan, mirrored: _style == AudioVisualizerStyle.Mirrored);
                break;
            case AudioVisualizerStyle.Radial:
                ArrangeSpokes(plan);
                break;
            case AudioVisualizerStyle.Led:
                ArrangeCells(plan);
                break;
            case AudioVisualizerStyle.Wave:
                ArrangeWave(plan);
                break;
            case AudioVisualizerStyle.Waveform:
                ArrangeScope(plan);
                break;
        }

        Draw();
    }

    /// <summary>[S-3214] Draw one frame of the shared capture: the trace for the waveform, the bands for every other design.</summary>
    public void SetFrame(AudioFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (_style == AudioVisualizerStyle.Waveform)
        {
            SetTrace(frame.Trace);
            return;
        }

        SetLevels(frame.Bands);
    }

    /// <summary>[S-3212] Draw a waveform trace (−1..1); missing points are the centre line.</summary>
    public void SetTrace(ReadOnlySpan<float> trace)
    {
        _trace.AsSpan().Clear();
        trace[..Math.Min(trace.Length, _trace.Length)].CopyTo(_trace);
        Draw();
    }

    /// <summary>Draw one frame of the analyser's bands; missing levels are silence.</summary>
    public void SetLevels(ReadOnlySpan<float> bands)
    {
        VisualizerLayout.Group(bands, _grouped.AsSpan(0, Math.Min(_plan.Bars, _grouped.Length)));
        Draw();
    }

    /// <summary>[S-3206] Every level down to the baseline: the idle state.</summary>
    public void Clear()
    {
        Array.Clear(_grouped);
        Array.Clear(_trace);
        Draw();
    }

    private double Radius(double thickness) => Math.Min(thickness / 2, Fluent.ControlRadius / 2);

    private void ArrangeBars(VisualizerPlan plan, bool mirrored)
    {
        var radius = Radius(plan.Thickness);
        var origin = mirrored ? 0.5 : (plan.Horizontal ? 0 : 1);

        for (var i = 0; i < _bars.Length; i++)
        {
            var bar = _bars[i];

            if (i >= plan.Bars)
            {
                bar.Visibility = Visibility.Collapsed;
                continue;
            }

            var step = i * (plan.Thickness + plan.Gap);
            bar.RadiusX = radius;
            bar.RadiusY = radius;

            if (plan.Horizontal)
            {
                // A column: the bars lie across it, the bass at the bottom.
                bar.Width = plan.Length;
                bar.Height = plan.Thickness;
                bar.RenderTransformOrigin = new Point(origin, 0.5);
                SetLeft(bar, 0);
                SetTop(bar, ((plan.Bars - 1) * (plan.Thickness + plan.Gap)) - step);
                _scales[i].ScaleY = 1;
            }
            else
            {
                bar.Width = plan.Thickness;
                bar.Height = plan.Length;
                bar.RenderTransformOrigin = new Point(0.5, origin);
                SetLeft(bar, step);
                SetTop(bar, 0);
                _scales[i].ScaleX = 1;
            }

            bar.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// Each spoke stands with its base on the ring and is turned about that base to point outward:
    /// the bass at the top, the treble at the bottom, the left half the right's mirror.
    /// </summary>
    private void ArrangeSpokes(VisualizerPlan plan)
    {
        var radius = Radius(plan.Thickness);

        for (var i = 0; i < _bars.Length; i++)
        {
            var bar = _bars[i];
            var band = i % Spectrum.Bands;

            if (band >= plan.Bars)
            {
                bar.Visibility = Visibility.Collapsed;
                continue;
            }

            var angle = VisualizerLayout.SpokeAngle(band, plan.Bars, left: i >= Spectrum.Bands);
            var radians = angle * Math.PI / 180;
            var baseX = plan.CenterX + (plan.Radius * Math.Sin(radians));
            var baseY = plan.CenterY - (plan.Radius * Math.Cos(radians));

            bar.Width = plan.Thickness;
            bar.Height = plan.Length;
            bar.RadiusX = radius;
            bar.RadiusY = radius;
            bar.RenderTransformOrigin = new Point(0.5, 1);

            // The rotation sits outside the scale, so the frame only ever writes ScaleY.
            bar.RenderTransform = new TransformGroup { Children = { _scales[i], new RotateTransform { Angle = angle } } };
            _scales[i].ScaleX = 1;
            SetLeft(bar, baseX - (plan.Thickness / 2));
            SetTop(bar, baseY - plan.Length);
            bar.Visibility = Visibility.Visible;
        }
    }

    private void ArrangeCells(VisualizerPlan plan)
    {
        var needed = plan.Bars * plan.Cells;

        while (_cells.Count < needed)
        {
            var cell = new Rectangle { Fill = _fill };
            _cells.Add(cell);
            Children.Add(cell);
        }

        var radius = Radius(Math.Min(plan.Thickness, plan.Cell));

        for (var index = 0; index < _cells.Count; index++)
        {
            var cell = _cells[index];

            if (index >= needed)
            {
                cell.Visibility = Visibility.Collapsed;
                continue;
            }

            var column = index / plan.Cells;
            var row = index % plan.Cells;
            var columnOffset = column * (plan.Thickness + plan.Gap);
            var rowOffset = row * (plan.Cell + plan.Gap);
            cell.RadiusX = radius;
            cell.RadiusY = radius;

            if (plan.Horizontal)
            {
                cell.Width = plan.Cell;
                cell.Height = plan.Thickness;
                SetLeft(cell, rowOffset);
                SetTop(cell, ((plan.Bars - 1) * (plan.Thickness + plan.Gap)) - columnOffset);
            }
            else
            {
                cell.Width = plan.Thickness;
                cell.Height = plan.Cell;
                SetLeft(cell, columnOffset);
                SetTop(cell, plan.Length - plan.Cell - rowOffset);
            }

            cell.Opacity = DimOpacity;
            cell.Visibility = Visibility.Visible;
        }

        // Every column redraws from dark on the next frame.
        Array.Clear(_lit);
    }

    private void ArrangeWave(VisualizerPlan plan)
    {
        var segments = Math.Max(1, plan.Bars - 1);

        while (_areaCurve.Count < segments)
        {
            _areaCurve.Add(new BezierSegment());
            _lineCurve.Add(new BezierSegment());
        }

        _areaFigure!.Segments.Clear();
        _lineFigure!.Segments.Clear();
        _areaFigure.Segments.Add(_areaRise);

        for (var i = 0; i < segments; i++)
        {
            _areaFigure.Segments.Add(_areaCurve[i]);
            _lineFigure.Segments.Add(_lineCurve[i]);
        }

        _areaFigure.Segments.Add(_areaFall);
    }

    private void Draw()
    {
        switch (_style)
        {
            case AudioVisualizerStyle.Bars:
            case AudioVisualizerStyle.Mirrored:
                for (var i = 0; i < _plan.Bars && i < _scales.Length; i++)
                {
                    var level = Math.Max(_plan.Floor, _grouped[i]);

                    if (_plan.Horizontal)
                    {
                        _scales[i].ScaleX = level;
                    }
                    else
                    {
                        _scales[i].ScaleY = level;
                    }
                }

                break;

            case AudioVisualizerStyle.Radial:
                for (var i = 0; i < _scales.Length; i++)
                {
                    var band = i % Spectrum.Bands;

                    if (band < _plan.Bars)
                    {
                        _scales[i].ScaleY = Math.Max(_plan.Floor, _grouped[band]);
                    }
                }

                break;

            case AudioVisualizerStyle.Led:
                DrawCells();
                break;

            case AudioVisualizerStyle.Wave:
                DrawWave();
                break;

            case AudioVisualizerStyle.Waveform:
                DrawScope();
                break;
        }
    }

    /// <summary>[S-3213] The line's segments for the plan's points: placed once per layout, moved per frame.</summary>
    private void ArrangeScope(VisualizerPlan plan)
    {
        if (_scope is null || _scopeFigure is null)
        {
            return;
        }

        var segments = Math.Max(1, plan.Bars - 1);
        while (_scopeSegments.Count < segments)
        {
            _scopeSegments.Add(new LineSegment());
        }

        _scopeFigure.Segments.Clear();
        for (var i = 0; i < segments; i++)
        {
            _scopeFigure.Segments.Add(_scopeSegments[i]);
        }

        _scope.StrokeThickness = plan.Thickness;
    }

    /// <summary>
    /// [S-3213] [S-3206] The trace fitted to the plan's points, swinging about the centre line within
    /// the short side less the line's own half-thickness; silence is the flat centre line.
    /// </summary>
    private void DrawScope()
    {
        var n = Math.Min(_plan.Bars, _scopePoints.Length);

        if (n < 2 || _scopeFigure is null || _scopeFigure.Segments.Count < n - 1)
        {
            return;
        }

        Oscilloscope.Resample(_trace, _scopePoints.AsSpan(0, n));

        var middle = _plan.Length / 2;
        var swing = Math.Max(0, middle - (_plan.Thickness / 2));
        var step = _plan.Run / (n - 1);

        _scopeFigure.StartPoint = Map(0, middle + (swing * _scopePoints[0]), _plan.Run);
        for (var i = 1; i < n; i++)
        {
            _scopeSegments[i - 1].Point = Map(i * step, middle + (swing * _scopePoints[i]), _plan.Run);
        }
    }

    /// <summary>Only the cells whose state changed are written.</summary>
    private void DrawCells()
    {
        if (_plan.Cells <= 0 || _cells.Count < _plan.Bars * _plan.Cells)
        {
            return;
        }

        for (var column = 0; column < _plan.Bars; column++)
        {
            var lit = VisualizerLayout.LitCells(_grouped[column], _plan.Cells);
            var was = _lit[column];

            if (lit == was)
            {
                continue;
            }

            var low = Math.Min(lit, was);
            var high = Math.Max(lit, was);

            for (var row = low; row < high; row++)
            {
                _cells[(column * _plan.Cells) + row].Opacity = row < lit ? 1 : DimOpacity;
            }

            _lit[column] = lit;
        }
    }

    private void DrawWave()
    {
        var n = _plan.Bars;

        if (n < 2 || _areaFigure is null || _lineFigure is null || _areaRise is null || _areaFall is null)
        {
            return;
        }

        // The line's own half-thickness is kept inside the box, so the peaks are not clipped flat.
        var room = Math.Max(0, _plan.Length - LineThickness);

        for (var i = 0; i < n; i++)
        {
            _heights[i] = (LineThickness / 2) + (room * Math.Max(_plan.Floor, _grouped[i]));
        }

        var along = ActualRun();
        VisualizerLayout.Wave(_heights.AsSpan(0, n), along, _plan.Length, _controls);

        var start = Map(0, _heights[0], along);
        _areaFigure.StartPoint = Map(0, 0, along);
        _areaRise.Point = start;
        _lineFigure.StartPoint = start;

        for (var i = 0; i < n - 1; i++)
        {
            var c1 = Map(_controls[3 * i].X, _controls[3 * i].Y, along);
            var c2 = Map(_controls[(3 * i) + 1].X, _controls[(3 * i) + 1].Y, along);
            var end = Map(_controls[(3 * i) + 2].X, _controls[(3 * i) + 2].Y, along);
            var area = _areaCurve[i];
            area.Point1 = c1;
            area.Point2 = c2;
            area.Point3 = end;
            var line = _lineCurve[i];
            line.Point1 = c1;
            line.Point2 = c2;
            line.Point3 = end;
        }

        _areaFall.Point = Map(along, 0, along);
    }

    /// <summary>The run's length: the bars and their gaps, which is the whole long side.</summary>
    private double ActualRun() => (_plan.Bars * _plan.Thickness) + ((_plan.Bars - 1) * _plan.Gap);

    /// <summary>A point of the run (position along it, height above its base) on the canvas.</summary>
    private Point Map(double position, double height, double along) => _plan.Horizontal
        ? new Point(height, along - position)
        : new Point(position, _plan.Length - height);
}
