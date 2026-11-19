using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Sdk.Kit;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6717] [S-6750] [S-6751] [S-6753] The one chart every graph widget draws through (owner, 2026-11-19: "they should
/// reuse more of the same components"): the histories, the weather graphs, a price's period, a rate's days. It draws a
/// <see cref="ChartModel"/> in its box: behind, the shaded spans, the days' hairlines and (Detailed) the value grid with
/// its values; then the series as its type says (the line at 2 with its 16 % wash, the line alone, round columns from
/// the foot, or candles rising in the accent and falling Critical), each piece in its role, the second series tertiary;
/// over it the moment now, the figures, and (Detailed) the times along the foot, the low and the high with their dots and
/// the latest value's hairline and pill. A series with nothing to draw is drawn at its baseline, the frame and all, with
/// its caption: never no chart ([S-6753]). Geometry is <see cref="ChartPlot"/>'s and <see cref="ChartRules"/>'; the
/// chart redraws when its box or its model changes. Time runs left to right in every language: its owner says so.
/// </summary>
public sealed partial class Chart : Grid
{
    /// <summary>[S-1310] A marked point's dot: the line's stroke either side of the track, so it reads on the 2 px line.</summary>
    public const double MarkerDot = FamilyShape.Track + FamilyShape.Stroke;

    private readonly Canvas _canvas = new();

    // What is drawn in the chart's role (a sample with no role of its own): recoloured in place when the role changes.
    private readonly List<(Shape? Shape, Border? Bar, bool Fill)> _roled = [];
    private ChartModel _model = ChartModel.Empty;
    private Func<Size, ChartModel>? _layout;
    private ColourRole _role = ColourRole.Accent;

    public Chart()
    {
        IsHitTestVisible = false;
        Children.Add(_canvas);
        SizeChanged += (_, _) => Draw();
    }

    /// <summary>What it draws now.</summary>
    public ChartModel Model => _model;

    /// <summary>Draw <paramref name="model"/>; it is drawn again whenever the chart's box changes.</summary>
    public void Show(ChartModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _layout = null;
        Draw();
    }

    /// <summary>Draw the model <paramref name="layout"/> lays out for the chart's box, laid out again whenever the box changes.</summary>
    public void Show(Func<Size, ChartModel> layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Draw();
    }

    /// <summary>[S-6717] The role of the samples with none of their own: the accent, or Caution/Critical over a threshold; recoloured in place.</summary>
    public void Role(ColourRole role)
    {
        if (role == _role)
        {
            return;
        }

        _role = role;
        var brush = Family.Brush(role);
        foreach (var (shape, bar, fill) in _roled)
        {
            if (bar is not null)
            {
                bar.Background = brush;
            }
            else if (fill)
            {
                shape!.Fill = brush;
            }
            else
            {
                shape!.Stroke = brush;
            }
        }
    }

    private void Draw()
    {
        _canvas.Children.Clear();
        _roled.Clear();
        var (w, h) = (ActualWidth, ActualHeight);
        if (!(w > 0) || !(h > 0))
        {
            return;
        }

        if (_layout is { } layout)
        {
            _model = layout(new Size(w, h));
        }

        var model = _model;
        var candles = model.Type == ChartType.Candles && !model.Laid ? Candles(model, w) : [];
        if (candles.Count > 0)
        {
            model = model with { Low = Math.Min(model.Low, candles.Min(c => c.Low)), High = Math.Max(model.High, candles.Max(c => c.High)) };
        }

        var plot = new Plot(model, w, h);
        // [S-6753] A chart with nothing to draw stands at its baseline; a curve its owner laid out is drawn as given.
        var samples = model.Laid ? model.Series : ChartPlot.Drawn(model, w);
        var empty = !ChartPlot.HasLine(model.Series) && candles.Count == 0;

        foreach (var shade in model.Shades)
        {
            var from = Math.Clamp(shade.From, 0, w);
            var to = Math.Clamp(shade.To, 0, w);
            if (to - from > 0)
            {
                Add(new Rectangle { Width = to - from, Height = h, Fill = Family.Brush(ColourRole.Subtle), RadiusX = FamilyShape.Radius, RadiusY = FamilyShape.Radius }, from, 0);
            }
        }

        foreach (var x in model.Rules)
        {
            Down(x, h, ColourRole.Track);
        }

        if (plot.Detailed)
        {
            Grid(model, plot, w, labels: false);
        }

        if (model.Type == ChartType.Bars && !model.Laid)
        {
            Bars(samples, plot, w);
            Second(model, plot);
        }
        else if (candles.Count > 0)
        {
            Candles(candles, plot, w);
        }
        else
        {
            Lines(model, samples, plot, h);
        }

        if (model.Now is { } now && now >= 0 && now <= w)
        {
            Now(model, samples, plot, now, h);
        }

        if (plot.Detailed)
        {
            Grid(model, plot, w, labels: true);
        }

        Marks(model, plot, w, h);
        if (plot.Detailed && !empty && !model.Laid)
        {
            Extremes(model, samples, candles, plot, w, h);
        }

        if (plot.Detailed)
        {
            Times(model.Times, w, h);
        }

        if (model.Caption.Length > 0)
        {
            Add(Family.Text(FamilyType.Caption, ColourRole.TextSecondary, model.Caption), FamilySpace.S1, 0);
        }
    }

    /// <summary>The value scale in this box: the owner's room over and under the line, a Detailed chart's room, and where a value stands.</summary>
    private readonly struct Plot
    {
        private readonly ChartModel _model;

        public Plot(ChartModel model, double w, double h)
        {
            _model = model;
            Detailed = model.Detail == ChartDetail.Detailed;
            var (top, bottom) = ChartPlot.Room(model.Detail, times: true);
            Top = Math.Max(model.PadTop, top);
            Band = Math.Max(1, h - Top - Math.Max(model.PadBottom, bottom));

            // Columns stand on the plot's foot (over the times when Detailed) and rise into what is left under the top room.
            Foot = h - (Detailed ? FamilySpace.S1 + FamilyType.CaptionLine : 0);
            Room = Math.Max(0, Foot - Top);
            Width = w;
        }

        public bool Detailed { get; }

        public double Top { get; }

        public double Band { get; }

        public double Foot { get; }

        public double Room { get; }

        public double Width { get; }

        /// <summary>A value's height on the line.</summary>
        public double Y(double value) => _model.Laid ? value : Top + ChartRules.Y(_model.Fraction(value), Band);

        /// <summary>A value's height as a column's top.</summary>
        public double Column(double value) => Foot - (_model.Fraction(value) * Room);

        /// <summary>Where a value is drawn for this type.</summary>
        public double At(double value) => _model.Type == ChartType.Bars ? Column(value) : Y(value);
    }

    /// <summary>
    /// [S-6751] The value grid: a Track hairline at each nice step behind the series, and (<paramref name="labels"/>,
    /// drawn over the series so a column never hides it) its value over it at the left.
    /// </summary>
    private void Grid(ChartModel model, Plot plot, double w, bool labels)
    {
        if (model.Laid)
        {
            return;
        }

        var line = FamilyType.CaptionLine;
        foreach (var tick in ChartPlot.Ticks(model.Low, model.High, ChartPlot.ValueRoom(model.Type == ChartType.Bars ? plot.Room : plot.Band), model.TickUnit))
        {
            var y = plot.At(tick);
            if (!labels)
            {
                Add(new Rectangle { Width = w, Height = FamilyShape.Hairline, Fill = Family.Brush(ColourRole.Track) }, 0, y - (FamilyShape.Hairline / 2));
                continue;
            }

            // The foot's own value is not written: it is where the line rests, and its words would lie on it. A chart
            // with a caption (nothing to draw) writes no values: they would say a scale of nothing, under its words.
            if (model.Format is { } format && tick > model.Low && model.Caption.Length == 0)
            {
                Add(Family.Text(FamilyType.Caption, ColourRole.TextTertiary, format(tick)), FamilySpace.S1, y - line < 0 ? y : y - line);
            }
        }
    }

    /// <summary>The line and its wash: each run of samples as stretches of one role; washes, then the second series, then the lines.</summary>
    private void Lines(ChartModel model, IReadOnlyList<ChartSample> samples, Plot plot, double h)
    {
        var foot = model.WashToFloor ? plot.Y(model.Low) : h;
        var type = model.Type == ChartType.Line ? ChartType.Line : ChartType.Area;
        var washes = new List<(Path, ColourRole?)>();
        var lines = new List<(Path, ColourRole?)>();
        var dots = new List<(Point, ColourRole?)>();
        foreach (var (first, last) in ChartPlot.Runs(samples))
        {
            if (first == last)
            {
                if (model.LoneDots)
                {
                    dots.Add((new Point(samples[first].X, plot.Y(samples[first].Value)), samples[first].Role));
                }

                continue;
            }

            var start = first;
            while (start < last)
            {
                var role = Piece(samples[start], samples[start + 1]);
                var end = start + 1;
                while (end < last && Piece(samples[end], samples[end + 1]) == role)
                {
                    end++;
                }

                var points = new List<ChartPoint>(end - start + 1);
                for (var i = start; i <= end; i++)
                {
                    points.Add(new ChartPoint(samples[i].X, plot.Y(samples[i].Value)));
                }

                var outline = ChartPlot.Outline(points, type, foot);
                if (outline.Area.Count > 0)
                {
                    washes.Add((new Path { Data = Figure(outline.Area, closed: true), Opacity = FamilyShape.Wash }, role));
                }

                var stroke = new Path { Data = Figure(outline.Line, closed: false), StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round };
                if (model.RoundCaps)
                {
                    stroke.StrokeStartLineCap = PenLineCap.Round;
                    stroke.StrokeEndLineCap = PenLineCap.Round;
                }

                lines.Add((stroke, role));
                start = end;
            }
        }

        foreach (var (wash, role) in washes)
        {
            Paint(wash, role, fill: true);
        }

        Second(model, plot);
        foreach (var (stroke, role) in lines)
        {
            Paint(stroke, role, fill: false);
        }

        foreach (var (at, role) in dots)
        {
            Dot(at, role, FamilyShape.Track * 2);
        }
    }

    /// <summary>The role a piece between two samples is drawn in: the chart's own (null) when neither has one, else the worse of the two.</summary>
    private ColourRole? Piece(ChartSample a, ChartSample b)
        => a.Role is null && b.Role is null ? null : ChartPlot.Worse(a.Role ?? _role, b.Role ?? _role);

    /// <summary>The second series: tertiary, each run on the same scale.</summary>
    private void Second(ChartModel model, Plot plot)
    {
        foreach (var (first, last) in ChartPlot.Runs(model.Second))
        {
            if (last == first)
            {
                continue;
            }

            var points = new List<ChartPoint>(last - first + 1);
            for (var i = first; i <= last; i++)
            {
                points.Add(new ChartPoint(model.Second[i].X, plot.Y(model.Second[i].Value)));
            }

            Add(new Path { Data = Figure(points, closed: false), Stroke = Family.Brush(ColourRole.TextTertiary), StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round }, 0, 0);
        }
    }

    /// <summary>
    /// [S-6750] [S-6753] A round column per sample over nothing, filled from the foot in its role; with no column to draw
    /// (nothing yet, or every value nothing: a dry day), the foot's hairline, so the chart still stands.
    /// </summary>
    private void Bars(IReadOnlyList<ChartSample> samples, Plot plot, double w)
    {
        if (!samples.Any(sample => _model.Fraction(sample.Value) > 0))
        {
            Add(new Rectangle { Width = w, Height = FamilyShape.Hairline, Fill = Family.Brush(ColourRole.Track) }, 0, plot.Foot - FamilyShape.Hairline);
            return;
        }

        var width = ChartPlot.BarWidth(w / samples.Count);
        foreach (var sample in samples)
        {
            var share = _model.Fraction(sample.Value);
            if (!(share > 0))
            {
                continue;
            }

            // A wet hour never under the track, so a drizzle is a low pill, not a dot as tall as a downpour.
            var height = Math.Max(Math.Min(width, FamilyShape.Track), share * plot.Room);
            var bar = new Border { Width = width, Height = height, CornerRadius = Family.Round(width, height) };
            Paint(bar, sample.Role);
            Add(bar, sample.X - (width / 2), plot.Foot - height);
        }
    }

    /// <summary>[S-6750] The candles of the samples: as many periods as the width has slots for.</summary>
    private IReadOnlyList<Candle> Candles(ChartModel model, double w)
    {
        var source = model.Ohlc is { Count: > 0 } ohlc ? ohlc : [.. model.Series.Where(s => double.IsFinite(s.Value)).Select(s => Candle.Point(s.Value))];
        return ChartPlot.Bucket(source, ChartPlot.CandleCount(source.Count, w, real: model.Ohlc is { Count: > 0 }));
    }

    /// <summary>[S-6750] Each candle: its wick a hairline from the low to the high, its body from the open to the close, up in the accent, down Critical.</summary>
    private void Candles(IReadOnlyList<Candle> candles, Plot plot, double w)
    {
        var cell = w / candles.Count;
        for (var i = 0; i < candles.Count; i++)
        {
            var shape = ChartPlot.Shape(candles[i], (i + 0.5) * cell, cell, plot.Y);
            var brush = Family.Brush(shape.Rises ? ColourRole.Accent : ColourRole.Critical);
            Add(new Rectangle { Width = FamilyShape.Hairline, Height = Math.Max(FamilyShape.Hairline, shape.WickBottom - shape.WickTop), Fill = brush }, shape.WickX - (FamilyShape.Hairline / 2), shape.WickTop);
            Add(new Rectangle { Width = shape.BodyWidth, Height = shape.BodyHeight, Fill = brush, RadiusX = FamilyShape.Hairline, RadiusY = FamilyShape.Hairline }, shape.BodyLeft, shape.BodyTop);
        }
    }

    /// <summary>The moment now: a secondary hairline down the plot and, on a line, a dot where the line is between its two samples.</summary>
    private void Now(ChartModel model, IReadOnlyList<ChartSample> samples, Plot plot, double x, double h)
    {
        Down(x, h, ColourRole.TextSecondary);
        if (model.Type is ChartType.Bars or ChartType.Candles || samples.Count == 0)
        {
            return;
        }

        var i = 0;
        while (i < samples.Count - 1 && samples[i + 1].X <= x)
        {
            i++;
        }

        var j = Math.Min(i + 1, samples.Count - 1);
        if (!double.IsFinite(samples[i].Value))
        {
            return;
        }

        var a = samples[i].Value;
        var b = double.IsFinite(samples[j].Value) ? samples[j].Value : a;
        var span = samples[j].X - samples[i].X;
        var t = span > 0 ? Math.Clamp((x - samples[i].X) / span, 0, 1) : 0;
        Dot(new Point(x, plot.Y(a + ((b - a) * t))), samples[i].Role, FamilyShape.Track * 2);
    }

    /// <summary>The figures at their points: over or under the point (over a column), kept inside the plot; a dot where asked.</summary>
    private void Marks(ChartModel model, Plot plot, double w, double h)
    {
        var bars = model.Type == ChartType.Bars && !model.Laid;
        var width = ChartPlot.BarWidth(w / Math.Max(1, model.Series.Count));
        foreach (var mark in model.Marks)
        {
            if (!double.IsFinite(mark.Value))
            {
                continue;
            }

            if (mark.Dot)
            {
                Dot(new Point(mark.X, plot.Y(mark.Value)), null, MarkerDot);
            }

            var y = bars ? plot.Foot - Math.Max(Math.Min(width, FamilyShape.Track), model.Fraction(mark.Value) * plot.Room) : plot.Y(mark.Value);
            Figure(mark.Text, mark.TextRole, mark.X, y, mark.Under, w, h, bars);
        }
    }

    /// <summary>A figure centred on <paramref name="x"/>, over <paramref name="y"/> or under it, kept inside the plot; its box.</summary>
    private Rect Figure(string text, ColourRole role, double x, double y, bool under, double w, double h, bool onColumn)
    {
        var line = FamilyType.CaptionLine;
        var block = Family.Text(FamilyType.Caption, role, text);
        block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = block.DesiredSize.Width;

        // A figure keeps a step off the plot's sides, as text does at a card's edge.
        var left = Math.Clamp(x - (width / 2), FamilySpace.S1, Math.Max(FamilySpace.S1, w - width - FamilySpace.S1));
        var top = onColumn ? y - line : y + (under ? FamilyShape.Stroke : -FamilyShape.Stroke - line);
        top = Math.Clamp(top, 0, Math.Max(0, h - line));
        Add(block, left, top);
        return new Rect(left, top, width, line);
    }

    /// <summary>
    /// [S-6751] Detailed: the latest value's hairline across the plot and its pill at the right edge, then the high over
    /// its point and the low under its point, each with its dot, where they leave the pill clear.
    /// </summary>
    private void Extremes(ChartModel model, IReadOnlyList<ChartSample> samples, IReadOnlyList<Candle> candles, Plot plot, double w, double h)
    {
        var format = model.Format ?? (v => Themia.Core.Localization.LocaleFormat.Number(Themia.Core.Localization.Strings.Current, v, 2));
        // The newest value: the last candle's close, or the last sample of the series, or of the second where it runs on
        // past the series (a price's after-hours bars).
        var finite = samples.Where(s => double.IsFinite(s.Value)).ToList();
        var after = model.Second.Where(s => double.IsFinite(s.Value)).ToList();
        double? latest = candles.Count > 0 ? candles[^1].Close
            : after.Count > 0 && (finite.Count == 0 || after[^1].X > finite[^1].X) ? after[^1].Value
            : finite.Count > 0 ? finite[^1].Value : null;
        var pill = Rect.Empty;
        if (latest is { } now)
        {
            var y = plot.At(now);
            Add(new Rectangle { Width = w, Height = FamilyShape.Hairline, Fill = Family.Brush(ColourRole.TextTertiary) }, 0, y - (FamilyShape.Hairline / 2));
            var text = Family.Text(FamilyType.Caption, ColourRole.TextPrimary, format(now));
            var box = new Border
            {
                Child = text,
                Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
                CornerRadius = Family.Radius,
                Background = Family.Brush(ColourRole.Solid),
            };
            box.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var line = FamilyType.CaptionLine;
            pill = new Rect(Math.Max(0, w - box.DesiredSize.Width), Math.Clamp(y - (line / 2), 0, Math.Max(0, h - line)), box.DesiredSize.Width, line);
            Add(box, pill.X, pill.Y);
        }

        if (model.Marks.Count > 0)
        {
            // The owner writes its own figures (a weather graph's high and low): never twice.
            return;
        }

        var cell = candles.Count > 0 ? w / candles.Count : 0;
        (double X, double Value)? high = null, low = null;
        if (candles.Count > 0)
        {
            for (var i = 0; i < candles.Count; i++)
            {
                if (high is null || candles[i].High > high.Value.Value)
                {
                    high = ((i + 0.5) * cell, candles[i].High);
                }

                if (low is null || candles[i].Low < low.Value.Value)
                {
                    low = ((i + 0.5) * cell, candles[i].Low);
                }
            }
        }
        else
        {
            foreach (var s in samples.Where(s => double.IsFinite(s.Value)))
            {
                high = high is null || s.Value > high.Value.Value ? (s.X, s.Value) : high;
                low = low is null || s.Value < low.Value.Value ? (s.X, s.Value) : low;
            }
        }

        if (high is not { } top || low is not { } foot)
        {
            return;
        }

        Extreme(top, under: false);
        if (foot.Value < top.Value)
        {
            Extreme(foot, under: true);
        }

        void Extreme((double X, double Value) at, bool under)
        {
            var y = plot.At(at.Value);
            if (candles.Count == 0)
            {
                Dot(new Point(at.X, y), null, MarkerDot);
            }

            var line = FamilyType.CaptionLine;
            var text = format(at.Value);
            var block = Family.Text(FamilyType.Caption, ColourRole.TextSecondary, text);
            block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var width = block.DesiredSize.Width;
            var left = Math.Clamp(at.X - (width / 2), FamilySpace.S1, Math.Max(FamilySpace.S1, w - width - FamilySpace.S1));
            var labelTop = Math.Clamp(y + (under ? FamilyShape.Stroke : -FamilyShape.Stroke - line), 0, Math.Max(0, h - line));
            var box = new Rect(left, labelTop, width, line);
            box.Intersect(pill);
            if (box.IsEmpty)
            {
                Add(block, left, labelTop);
            }
        }
    }

    /// <summary>[S-6751] The times along the foot, each centred on its place and kept inside; one that would touch the last is left out.</summary>
    private void Times(IReadOnlyList<ChartTick> times, double w, double h)
    {
        var right = double.NegativeInfinity;
        foreach (var tick in times)
        {
            var block = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, tick.Text);
            block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var width = block.DesiredSize.Width;
            var left = Math.Clamp(tick.At - (width / 2), FamilySpace.S1, Math.Max(FamilySpace.S1, w - width - FamilySpace.S1));
            if (left < right + FamilySpace.S2)
            {
                continue;
            }

            right = left + width;
            Add(block, left, h - FamilyType.CaptionLine);
        }
    }

    private void Down(double x, double h, ColourRole role)
        => Add(new Rectangle { Width = FamilyShape.Hairline, Height = h, Fill = Family.Brush(role) }, x - (FamilyShape.Hairline / 2), 0);

    private void Dot(Point at, ColourRole? role, double size)
    {
        var dot = new Ellipse { Width = size, Height = size };
        Paint(dot, role, fill: true);
        Canvas.SetLeft(dot, at.X - (size / 2));
        Canvas.SetTop(dot, at.Y - (size / 2));
    }

    private void Paint(Shape shape, ColourRole? role, bool fill)
    {
        var brush = Family.Brush(role ?? _role);
        if (fill)
        {
            shape.Fill = brush;
        }
        else
        {
            shape.Stroke = brush;
        }

        if (role is null)
        {
            _roled.Add((shape, null, fill));
        }

        _canvas.Children.Add(shape);
    }

    private void Paint(Border bar, ColourRole? role)
    {
        bar.Background = Family.Brush(role ?? _role);
        if (role is null)
        {
            _roled.Add((null, bar, true));
        }
    }

    private void Add(UIElement element, double left, double top)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        _canvas.Children.Add(element);
    }

    private static PathGeometry? Figure(IReadOnlyList<ChartPoint> points, bool closed)
    {
        if (points.Count < 2)
        {
            return null;
        }

        var figure = new PathFigure { StartPoint = new Point(points[0].X, points[0].Y), IsClosed = closed, IsFilled = closed };
        var segment = new PolyLineSegment();
        for (var i = 1; i < points.Count; i++)
        {
            segment.Points.Add(new Point(points[i].X, points[i].Y));
        }

        figure.Segments.Add(segment);
        return new PathGeometry { Figures = { figure } };
    }
}
