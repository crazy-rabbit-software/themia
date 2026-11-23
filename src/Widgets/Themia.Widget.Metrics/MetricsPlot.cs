using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Themia.Widget.Metrics;

/// <summary>[S-16314] What the plot draws: the series (folded to its width), its type, its range ([S-16902]: held as time passes), where it starts and whether it stands behind the head, the slot pointed at.</summary>
internal readonly record struct PlotLook(MetricSeries Series, MetricGraphType Type, GraphRange Range, bool Share, double Top, bool Behind, int? Hovered);

/// <summary>
/// [S-16314] [S-16316] The Metric widget's own graph: a canvas the card's whole box, the series drawn from the plot's top to the
/// card's foot and side to side (a line's first point on the left edge and its last on the right, bars from edge to edge with
/// gaps between them), in the accent (a pair's second in the tertiary text's colour, a falling candle Critical), its wash at
/// the family's wash. Nothing to draw (no reading yet) is the foot's hairline across, so the graph still stands (WR-10).
/// Behind the head it is drawn faint. The pointed slot gets a hairline down the plot and a dot on each of its values.
/// </summary>
internal sealed partial class MetricsPlot : Canvas
{
    public MetricsPlot()
    {
        // Hit by the pointer everywhere in the plot, also over a gap or above the line.
        Background = Fluent.Clear();
        PlotMark.Mark(this);
    }

    public void Draw(PlotLook look, double width, double height)
    {
        Children.Clear();
        Opacity = look.Behind ? MetricFaceRules.BehindShare : 1;
        var series = look.Series;
        var count = series.Slots.Count;
        var stroke = FamilyShape.Stroke;
        var range = look.Range;

        // A line keeps its stroke inside the plot; a fill and a bar stand on the card's foot.
        var lineTop = look.Top + stroke;
        var lineFoot = height - (stroke / 2);
        // [S-16314] The graph's default where the series has no reading (WR-10: "even if there is no cpu data available it should
        // show the default values in the graph and not just not show the graph"): a mark type's empty track a slot, a line's
        // its plot as an empty track and its zero along the foot; the readings over it.
        if (MetricGraphRules.DrawsDefault(series))
        {
            Default(series, look, height, width);
        }

        if (!series.Any)
        {
            return;
        }

        switch (look.Type)
        {
            case MetricGraphType.Bars:
                Bars(series, range, look.Top, height, width);
                break;
            case MetricGraphType.Range:
                Ranges(series, range, lineTop, lineFoot, width);
                break;
            case MetricGraphType.Candles:
                Candles(series, range, lineTop, lineFoot, width);
                break;
            default:
                Lines(series, look.Type, range, lineTop, lineFoot, height, width);
                break;
        }

        if (look.Hovered is { } slot && series.Slots[slot] is { } value)
        {
            var x = MetricGraphRules.X(look.Type, slot, count, width);
            var y = (double v) => MetricGraphRules.Y(v, range, lineTop, lineFoot);
            Add(Down(x, look.Top, height));
            Add(Dot(x, y(value.Value), ColourRole.Accent));
            if (value.Second is { } second)
            {
                Add(Dot(x, y(second), ColourRole.TextTertiary));
            }
        }
    }

    /// <summary>
    /// [S-16314] The default graph: each slot without a reading an empty track from the plot's top to the foot (bars, ranges,
    /// candles: as wide as its mark); a line type with no reading at all its plot one empty track, and its zero along the foot
    /// in the disabled text's colour under any line.
    /// </summary>
    private void Default(MetricSeries series, PlotLook look, double height, double width)
    {
        var count = series.Slots.Count;
        if (MetricGraphRules.Marks(look.Type))
        {
            for (var i = 0; i < count; i++)
            {
                if (series.Slots[i] is null)
                {
                    var bar = MetricGraphRules.Bar(i, count, width, MetricGraphRules.Cap(look.Type));
                    var track = Column(bar, look.Top, height, ColourRole.Track);
                    track.RadiusX = track.RadiusY = Math.Min(bar.Middle - bar.Left, FamilyShape.Radius);
                    Add(track);
                }
            }

            return;
        }

        if (!series.Any)
        {
            // Its default values, zero across the whole plot, drawn as the readings are: the line (and an area's fill) at zero.
            var zero = new MetricValue(0, series.Shape == MetricShape.Pair ? 0 : null);
            var zeros = series with { Slots = [.. series.Slots.Select(_ => (MetricValue?)zero)] };
            var stroke = FamilyShape.Stroke;
            Lines(zeros, look.Type, MetricGraphRules.Range(zeros, fromZero: true, look.Share), look.Top + stroke, height - (stroke / 2), height, width);
            return;
        }

        Add(Rule(0, height - FamilyShape.Stroke, width, ColourRole.TextDisabled));
    }

    /// <summary>A line (its wash under it for Area, its steps for Step) over each run of slots with readings; a pair's second its own line.</summary>
    private void Lines(MetricSeries series, MetricGraphType type, GraphRange range, double top, double foot, double height, double width)
    {
        var count = series.Slots.Count;
        foreach (var second in new[] { true, false })
        {
            foreach (var (first, last) in Runs(series.Slots, second))
            {
                var points = new List<Point>(last - first + 1);
                for (var i = first; i <= last; i++)
                {
                    var v = second ? series.Slots[i]!.Value.Second!.Value : series.Slots[i]!.Value.Value;
                    points.Add(new Point(MetricGraphRules.LineX(i, count, width), MetricGraphRules.Y(v, range, top, foot)));
                }

                if (type == MetricGraphType.Step)
                {
                    points = Stepped(points, count, width);
                }

                if (points.Count == 1)
                {
                    // A lone reading between gaps: its dot.
                    Add(Dot(points[0].X, points[0].Y, second ? ColourRole.TextTertiary : ColourRole.Accent));
                    continue;
                }

                if (!second && type == MetricGraphType.Area)
                {
                    var area = new List<Point>(points) { new(points[^1].X, height), new(points[0].X, height) };
                    Add(new Path { Data = Figure(area, closed: true), Fill = Family.Brush(ColourRole.Accent), Opacity = FamilyShape.Wash });
                }

                Add(new Path
                {
                    Data = Figure(points, closed: false),
                    Stroke = Family.Brush(second ? ColourRole.TextTertiary : ColourRole.Accent),
                    StrokeThickness = FamilyShape.Stroke,
                    StrokeLineJoin = PenLineJoin.Round,
                });
            }
        }
    }

    /// <summary>A step line: each reading held across its slot to the next one's.</summary>
    private static List<Point> Stepped(List<Point> points, int count, double width)
    {
        var half = count > 1 ? width / (count - 1) / 2 : 0;
        var steps = new List<Point>(points.Count * 2);
        for (var i = 0; i < points.Count; i++)
        {
            var left = i == 0 ? points[i].X : points[i].X - half;
            var right = i == points.Count - 1 ? points[i].X : points[i].X + half;
            steps.Add(new Point(left, points[i].Y));
            steps.Add(new Point(right, points[i].Y));
        }

        return steps;
    }

    /// <summary>A column a slot from the foot (or from zero where the range holds less) to its reading; a pair's two side by side.</summary>
    private void Bars(MetricSeries series, GraphRange range, double top, double foot, double width)
    {
        var count = series.Slots.Count;
        var zero = MetricGraphRules.Y(Math.Clamp(0, range.Low, range.High), range, top, foot);
        for (var i = 0; i < count; i++)
        {
            if (series.Slots[i] is not { } value)
            {
                continue;
            }

            var bar = MetricGraphRules.Bar(i, count, width, MetricGraphRules.ColumnCap);
            if (value.Second is { } second)
            {
                var half = new GraphBar(bar.Left, bar.Width / 2);
                Add(Column(half, zero, MetricGraphRules.Y(value.Value, range, top, foot), ColourRole.Accent));
                Add(Column(half with { Left = bar.Left + half.Width }, zero, MetricGraphRules.Y(second, range, top, foot), ColourRole.TextTertiary));
            }
            else
            {
                Add(Column(bar, zero, MetricGraphRules.Y(value.Value, range, top, foot), ColourRole.Accent));
            }
        }
    }

    /// <summary>A bar a slot from its low to its high: a day's temperatures.</summary>
    private void Ranges(MetricSeries series, GraphRange range, double top, double foot, double width)
    {
        var count = series.Slots.Count;
        for (var i = 0; i < count; i++)
        {
            if (series.Slots[i] is { } value)
            {
                var bar = MetricGraphRules.Bar(i, count, width, MetricGraphRules.RangeCap);
                var column = Column(bar, MetricGraphRules.Y(value.Low, range, top, foot), MetricGraphRules.Y(value.High, range, top, foot), ColourRole.Accent);
                column.RadiusX = column.RadiusY = Math.Min(bar.Middle - bar.Left, FamilyShape.Radius);
                Add(column);
            }
        }
    }

    /// <summary>A candle a slot: its wick from high to low, its body from open to close, rising in the accent, falling Critical.</summary>
    private void Candles(MetricSeries series, GraphRange range, double top, double foot, double width)
    {
        var count = series.Slots.Count;
        for (var i = 0; i < count; i++)
        {
            if (series.Slots[i] is not { Candle: { } candle })
            {
                continue;
            }

            var bar = MetricGraphRules.Bar(i, count, width, MetricGraphRules.RangeCap);
            var role = candle.Close >= candle.Open ? ColourRole.Accent : ColourRole.Critical;
            var y = (double v) => MetricGraphRules.Y(v, range, top, foot);
            var wick = new Rectangle { Width = FamilyShape.Hairline, Height = Math.Max(FamilyShape.Hairline, y(candle.Low) - y(candle.High)), Fill = Family.Brush(role) };
            Place(wick, bar.Middle - (FamilyShape.Hairline / 2), y(candle.High));
            Add(wick);
            Add(Column(bar, y(candle.Open), y(candle.Close), role));
        }
    }

    /// <summary>A column of <paramref name="bar"/> between two heights (whichever is higher on top), a hairline at least.</summary>
    private static Rectangle Column(GraphBar bar, double a, double b, ColourRole role)
    {
        var column = new Rectangle
        {
            Width = Math.Max(FamilyShape.Hairline, bar.Width),
            Height = Math.Max(FamilyShape.Hairline, Math.Abs(a - b)),
            Fill = Family.Brush(role),
        };
        Place(column, bar.Left, Math.Min(a, b));
        return column;
    }

    /// <summary>A rule across: the foot's line of a graph with nothing to draw, a stroke high so the card's edge never hides it.</summary>
    private static Rectangle Rule(double x, double y, double width, ColourRole role)
    {
        var rule = new Rectangle { Width = Math.Max(0, width), Height = FamilyShape.Stroke, Fill = Family.Brush(role) };
        Place(rule, x, y);
        return rule;
    }

    /// <summary>The pointed slot's hairline, down the plot.</summary>
    private static Rectangle Down(double x, double top, double foot)
    {
        var line = new Rectangle { Width = FamilyShape.Hairline, Height = Math.Max(0, foot - top), Fill = Family.Brush(ColourRole.TextTertiary) };
        Place(line, x - (FamilyShape.Hairline / 2), top);
        return line;
    }

    /// <summary>A reading's dot.</summary>
    private static Ellipse Dot(double x, double y, ColourRole role)
    {
        var size = FamilyShape.Track * 2;
        var dot = new Ellipse { Width = size, Height = size, Fill = Family.Brush(role) };
        Place(dot, x - (size / 2), y - (size / 2));
        return dot;
    }

    private static void Place(UIElement element, double x, double y)
    {
        SetLeft(element, x);
        SetTop(element, y);
    }

    private void Add(UIElement element) => Children.Add(element);

    /// <summary>The runs of slots with a reading (of its second, for <paramref name="second"/>): first and last index each.</summary>
    private static IEnumerable<(int First, int Last)> Runs(IReadOnlyList<MetricValue?> slots, bool second)
    {
        var start = -1;
        for (var i = 0; i <= slots.Count; i++)
        {
            var held = i < slots.Count && slots[i] is { } v && (!second || v.Second is not null);
            if (held && start < 0)
            {
                start = i;
            }
            else if (!held && start >= 0)
            {
                yield return (start, i - 1);
                start = -1;
            }
        }
    }

    private static PathGeometry Figure(List<Point> points, bool closed)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = closed, IsFilled = closed };
        var segment = new PolyLineSegment();
        for (var i = 1; i < points.Count; i++)
        {
            segment.Points.Add(points[i]);
        }

        figure.Segments.Add(segment);
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
