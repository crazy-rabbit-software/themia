using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-3210] The audio visualizer's designs. Never persisted: each design is its own widget kind and
/// <see cref="VisualizerLayout.StyleOf"/> maps the kind to its design.
/// </summary>
public enum VisualizerStyle
{
    /// <summary>Bars standing on the bottom edge.</summary>
    Bars = 0,

    /// <summary>Bars symmetric around the centre line.</summary>
    Mirror = 1,

    /// <summary>A smooth curve through the bands, the area under it filled.</summary>
    Wave = 2,

    /// <summary>Columns of LED cells, lit from the bottom.</summary>
    Dots = 3,

    /// <summary>Spokes around a ring, the spectrum mirrored left and right.</summary>
    Radial = 4,

    /// <summary>[S-3212] The waveform itself, a line moving about the centre: an oscilloscope, not a spectrum.</summary>
    Scope = 5,
}

/// <summary>
/// What the audio visualizer draws at one size. [S-3205] Edge to edge: the box is the whole content
/// area, and the card's rounded corners clip what reaches them.
/// </summary>
/// <param name="Style">The design.</param>
/// <param name="Horizontal">The run lies along a tall column's height (levels grow from its start
/// edge, bass at the bottom) rather than along a tile's or a strip's width.</param>
/// <param name="Bars">How many bands are drawn (bars, columns, curve points; spokes per side for
/// <see cref="VisualizerStyle.Radial"/>): a divisor of <see cref="Spectrum.Bands"/>.</param>
/// <param name="Thickness">One bar's (column's, spoke's) thickness, DIPs.</param>
/// <param name="Gap">The space between two bars (and two cells), DIPs.</param>
/// <param name="Length">The room a level has to grow in, DIPs: the short axis of the box, a spoke's length.</param>
/// <param name="Floor">The idle baseline as a fraction of <see cref="Length"/>: a quiet band is a stub, never nothing.</param>
/// <param name="Cells">LED cells per column (<see cref="VisualizerStyle.Dots"/>), else 0.</param>
/// <param name="Cell">One cell's length along the column, DIPs.</param>
/// <param name="Radius">The inner ring the spokes stand on (<see cref="VisualizerStyle.Radial"/>), DIPs.</param>
/// <param name="CenterX">The ring's centre, DIPs from the box's left.</param>
/// <param name="CenterY">The ring's centre, DIPs from the box's top.</param>
/// <param name="Run">[S-3213] The waveform's run along the long side (<see cref="VisualizerStyle.Scope"/>), DIPs; <see cref="Bars"/> is its point count there.</param>
public readonly record struct VisualizerPlan(
    VisualizerStyle Style,
    bool Horizontal,
    int Bars,
    double Thickness,
    double Gap,
    double Length,
    double Floor,
    int Cells = 0,
    double Cell = 0,
    double Radius = 0,
    double CenterX = 0,
    double CenterY = 0,
    double Run = 0);

/// <summary>
/// [S-3205] [S-3210] The audio visualizer's layout. The tile is the spectrum and nothing else, so the
/// ladder has one part and the size decides its grain: the bands run along the tile's long side, as
/// many as fit at <see cref="MinThickness"/> from <see cref="Counts"/>, and then they get thicker. The
/// analyser has <see cref="Spectrum.Bands"/> bands, so a larger tile draws the same bands larger
/// rather than inventing detail between them ([S-666]). Every design is one plan here and one
/// drawing in the App's <c>SpectrumCanvas</c>; the rules with a number in them are tested.
/// </summary>
public static class VisualizerLayout
{
    /// <summary>The bar counts, most first. Every one divides <see cref="Spectrum.Bands"/>.</summary>
    public static IReadOnlyList<int> Counts { get; } = [24, 12, 8, 6];

    /// <summary>A bar thinner than this reads as a hairline, not a bar.</summary>
    public const double MinThickness = 6;

    /// <summary>An LED cell smaller than this reads as noise, not as a light: the columns are fewer instead.</summary>
    public const double MinLed = 10;

    /// <summary>A spoke thinner than this is lost around the ring.</summary>
    public const double MinSpoke = 3;

    /// <summary>The idle baseline's height, DIPs: a flat line of stubs while nothing plays.</summary>
    public const double Baseline = 3;

    /// <summary>The most LED cells in one column: past it the cells only grow.</summary>
    public const int MaxCells = 24;

    /// <summary>The ring the spokes stand on, as a share of the room from the centre to the nearer edge.</summary>
    public const double RingShare = 0.42;

    /// <summary>A spoke's share of its slot of the ring's circumference; the rest is the gap.</summary>
    public const double SpokeShare = 0.6;

    /// <summary>[S-3210] The design a visualizer kind draws, or null for any other kind.</summary>
    public static VisualizerStyle? StyleOf(WidgetKind kind) => kind switch
    {
        WidgetKind.AudioVisualizer => VisualizerStyle.Bars,
        WidgetKind.VisualizerMirror => VisualizerStyle.Mirror,
        WidgetKind.VisualizerWave => VisualizerStyle.Wave,
        WidgetKind.VisualizerDots => VisualizerStyle.Dots,
        WidgetKind.VisualizerRadial => VisualizerStyle.Radial,
        WidgetKind.VisualizerScope => VisualizerStyle.Scope,
        _ => null,
    };

    /// <summary>Plan the design for the widget's content area, edge to edge.</summary>
    public static VisualizerPlan Plan(WidgetArea area, VisualizerStyle style = VisualizerStyle.Bars)
    {
        if (!area.IsMeasured)
        {
            return new VisualizerPlan(style, false, Counts[^1], MinThickness, 2, 0, 1);
        }

        return style switch
        {
            VisualizerStyle.Radial => PlanRadial(area),
            VisualizerStyle.Scope => PlanScope(area),
            _ => PlanLinear(area, style),
        };
    }

    /// <summary>[S-3213] The waveform's points are this far apart along the run: close enough to read as a curve.</summary>
    public const double ScopeStep = 4;

    /// <summary>[S-3213] Fewer points than this and a waveform is a zigzag.</summary>
    public const int MinScopePoints = 16;

    /// <summary>
    /// [S-3213] The waveform: along the long side like the linear designs (down a Tall column), a point
    /// every <see cref="ScopeStep"/> DIPs up to the trace's <see cref="Oscilloscope.Points"/>, the line 2
    /// to 4 DIPs thick as the tile grows, swinging about the centre line across the short side.
    /// </summary>
    private static VisualizerPlan PlanScope(WidgetArea area)
    {
        var horizontal = area.Shape == AreaShape.Tall;
        var along = horizontal ? area.Height : area.Width;
        var across = horizontal ? area.Width : area.Height;
        var points = Math.Clamp((int)Math.Floor(along / ScopeStep) + 1, MinScopePoints, Oscilloscope.Points);
        var line = Math.Clamp(Math.Round(across / 60), 2, 4);
        return new VisualizerPlan(VisualizerStyle.Scope, horizontal, points, line, 0, across, 0, Run: along);
    }

    private static VisualizerPlan PlanLinear(WidgetArea area, VisualizerStyle style)
    {
        var horizontal = area.Shape == AreaShape.Tall;
        var along = horizontal ? area.Height : area.Width;
        var across = horizontal ? area.Width : area.Height;

        // The gap grows with the run, so a large tile does not read as a fence of thin slits.
        var gap = Math.Clamp(Math.Round(along / 100), 2, 6);
        var bars = Counts[^1];
        var least = style == VisualizerStyle.Dots ? MinLed : MinThickness;

        foreach (var count in Counts)
        {
            if (Thickness(along, count, gap) >= least)
            {
                bars = count;
                break;
            }
        }

        var thickness = Math.Max(0, Thickness(along, bars, gap));

        if (style == VisualizerStyle.Dots)
        {
            // Square cells where they fit, stretched a little so the column ends at the edge exactly.
            var cells = across > 0 && thickness > 0
                ? Math.Clamp((int)Math.Floor((across + gap + Disclosure.Tolerance) / (thickness + gap)), 1, MaxCells)
                : 1;
            var cell = Math.Max(0, (across - (gap * (cells - 1))) / cells);
            return new VisualizerPlan(style, horizontal, bars, thickness, gap, across, 1.0 / cells, cells, cell);
        }

        var floor = across > 0 ? Math.Min(1, Baseline / across) : 1;
        return new VisualizerPlan(style, horizontal, bars, thickness, gap, across, floor);
    }

    private static VisualizerPlan PlanRadial(WidgetArea area)
    {
        var outer = Math.Min(area.Width, area.Height) / 2;
        var ring = outer * RingShare;
        var length = outer - ring;
        var bars = Counts[^1];

        foreach (var count in Counts)
        {
            if (Spoke(ring, count) >= MinSpoke)
            {
                bars = count;
                break;
            }
        }

        var floor = length > 0 ? Math.Min(1, Baseline / length) : 1;
        return new VisualizerPlan(VisualizerStyle.Radial, false, bars, Spoke(ring, bars), 0, length, floor,
            Radius: ring, CenterX: area.Width / 2, CenterY: area.Height / 2);
    }

    private static double Thickness(double along, int count, double gap) => (along - (gap * (count - 1))) / count;

    /// <summary>A spoke's thickness when <paramref name="perSide"/> spokes share each half of the ring.</summary>
    private static double Spoke(double ring, int perSide) => Math.Max(0, Math.PI * ring / perSide * SpokeShare);

    /// <summary>
    /// [S-3210] The angle of spoke <paramref name="index"/> of <paramref name="perSide"/>, degrees
    /// clockwise from the top: the bass at the top, the treble at the bottom, the right side positive
    /// and the left its mirror (negative).
    /// </summary>
    public static double SpokeAngle(int index, int perSide, bool left)
    {
        var angle = (index + 0.5) * 180.0 / Math.Max(1, perSide);
        return left ? -angle : angle;
    }

    /// <summary>[S-3210] How many of a column's <paramref name="cells"/> a level lights: at least one (the idle stub), at most all.</summary>
    public static int LitCells(float level, int cells)
    {
        if (cells <= 0)
        {
            return 0;
        }

        var lit = float.IsFinite(level) ? (int)Math.Ceiling((Math.Clamp(level, 0, 1) * cells) - 1e-6) : 0;
        return Math.Clamp(lit, 1, cells);
    }

    /// <summary>
    /// [S-3210] The wave's curve: a Catmull-Rom spline through <paramref name="points"/> (y values at
    /// even steps across <paramref name="width"/>), as cubic Bézier controls, three points per segment
    /// written to <paramref name="controls"/> (c1, c2, end). Controls are clamped into 0..<paramref name="bottom"/>,
    /// so the curve (inside its controls' hull) never leaves the box however sharp a peak is.
    /// </summary>
    public static void Wave(ReadOnlySpan<double> points, double width, double bottom, Span<(double X, double Y)> controls)
    {
        var n = points.Length;

        if (n < 2 || controls.Length < 3 * (n - 1))
        {
            return;
        }

        var step = width / (n - 1);

        for (var i = 0; i < n - 1; i++)
        {
            var before = points[Math.Max(0, i - 1)];
            var from = points[i];
            var to = points[i + 1];
            var after = points[Math.Min(n - 1, i + 2)];
            var x0 = i * step;
            var x1 = (i + 1) * step;
            var c1y = Math.Clamp(from + ((to - before) / 6), 0, bottom);
            var c2y = Math.Clamp(to - ((after - from) / 6), 0, bottom);
            controls[3 * i] = (x0 + (step / 3), c1y);
            controls[(3 * i) + 1] = (x1 - (step / 3), c2y);
            controls[(3 * i) + 2] = (x1, to);
        }
    }

    /// <summary>
    /// Fold the analyser's bands into <paramref name="bars"/>: each bar is the loudest band of its
    /// group, so a kick that lights one band is not averaged away by its quiet neighbours. A level
    /// outside 0..1 (or not a number) is clamped, never drawn past the tile.
    /// </summary>
    public static void Group(ReadOnlySpan<float> bands, Span<float> bars)
    {
        if (bars.Length == 0)
        {
            return;
        }

        if (bands.Length == 0)
        {
            bars.Clear();
            return;
        }

        for (var i = 0; i < bars.Length; i++)
        {
            var low = i * bands.Length / bars.Length;
            var high = Math.Max(low + 1, (i + 1) * bands.Length / bars.Length);
            var peak = 0f;

            for (var b = low; b < high && b < bands.Length; b++)
            {
                var level = float.IsFinite(bands[b]) ? bands[b] : 0;
                peak = Math.Max(peak, level);
            }

            bars[i] = Math.Clamp(peak, 0, 1);
        }
    }
}
