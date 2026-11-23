using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Sdk.Kit;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6717] [S-6750] [S-6751] [S-6753] [S-6754] The one chart every graph widget draws through (owner, 2026-11-19: "they
/// should reuse more of the same components"): the histories, the weather graphs, a price's period, a rate's days, the
/// rain's next two hours. Its box is the plot, the card's edge to edge (owner, 2026-11-19: "Plot edge to edge"): behind,
/// the shaded spans, the days' hairlines and (Detailed) the value grid; then the series as its type says (the line at 2
/// with its 16 % wash down to the foot, the line alone, round columns, or candles rising in the accent and falling
/// Critical), each piece in its role, the second series tertiary; over it the moment now, the figures, and what it writes
/// inside the plot at the family's inset (<see cref="ChartPlot.Frame"/>): the values on the grid, the times (Detailed) or
/// the window's ends (Simple) along the foot, the low and the high with their dots and the latest value's hairline and
/// pill, and a caption. Its owner's parts over the plot (a reading, a top line, the wind's arrows) stand in the room the
/// model names, the line kept clear of them. A series with nothing to draw is drawn at its baseline, the frame and all,
/// with its caption: never no chart ([S-6753]). Geometry is <see cref="ChartPlot"/>'s and <see cref="ChartRules"/>'; the
/// chart redraws when its box or its model changes. Time runs left to right in every language: its owner says so.
/// </summary>
public sealed partial class Chart : Grid
{
    /// <summary>[S-1310] A marked point's dot: the line's stroke either side of the track, so it reads on the 2 px line.</summary>
    public const double MarkerDot = FamilyShape.Track + FamilyShape.Stroke;

    private readonly Canvas _canvas = new();

    // What is drawn in the chart's role (a sample with no role of its own): recoloured in place when the role changes.
    private readonly List<(Shape? Shape, Border? Bar, bool Fill)> _roled = [];
    // [S-6754] What it writes this draw, each with its box and its rank (the lower first): kept where it touches none kept before.
    private readonly List<(UIElement Element, Rect Box, int Rank)> _written = [];
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

    /// <summary>[S-6754] Where it laid its parts the last time it drew (null before it had a box).</summary>
    public ChartFrame? Frame { get; private set; }

    /// <summary>[S-6754] It drew in a new frame: an owner's parts over the plot (the wind's arrows, a marker) follow it.</summary>
    public event Action<ChartFrame>? Drawn;

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

    /// <summary>[S-6754] Which written label gives way to which where two would touch: the lower rank stays.</summary>
    private const int CaptionRank = 0, LatestRank = 1, FigureRank = 2, ExtremeRank = 3, TimeRank = 4, ValueRank = 5;

    private void Draw()
    {
        _canvas.Children.Clear();
        _roled.Clear();
        _written.Clear();
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

        // [S-6754] The plot is the whole box; what is written stands inside it, the line clear of it.
        var frame = ChartPlot.Frame(model, w, h);
        var plot = new Plot(model, frame);

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
            Down(x, frame, ColourRole.Track);
        }

        if (plot.Detailed)
        {
            Grid(model, plot, frame, labels: false);
        }

        if (model.Type == ChartType.Bars && !model.Laid)
        {
            Bars(samples, plot, w, h);
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
            Now(model, samples, plot, frame, now);
        }

        if (plot.Detailed)
        {
            Grid(model, plot, frame, labels: true);
        }

        Marks(model, plot, frame, w);
        if (plot.Detailed && !empty && !model.Laid)
        {
            Extremes(model, samples, candles, plot, frame, w);
        }

        if (plot.Detailed)
        {
            Times(model.Times, frame);
        }
        else if (model.Ends is { } ends)
        {
            Ends(ends, frame, model.Type == ChartType.Bars && model.Tracks);
        }

        if (model.Caption.Length > 0)
        {
            var caption = Family.Text(FamilyType.Caption, ColourRole.TextSecondary, model.Caption);
            caption.MaxWidth = frame.Text.Width;
            caption.TextTrimming = TextTrimming.CharacterEllipsis;
            caption.TextWrapping = TextWrapping.NoWrap;
            Written(caption, frame.Text.Left, frame.CaptionTop, CaptionRank);
        }

        Settle();
        Frame = frame;
        Drawn?.Invoke(frame);
    }

    /// <summary>The value scale in the frame's band: where a value stands on the line and as a column's top.</summary>
    private readonly struct Plot
    {
        private readonly ChartModel _model;

        public Plot(ChartModel model, ChartFrame frame)
        {
            _model = model;
            Detailed = model.Detail == ChartDetail.Detailed;
            Top = frame.LineTop;
            Band = frame.Band;
            Width = frame.Plot.Width;

            // Columns stand on the band's foot (over what is written at the plot's foot) and rise to its top.
            Foot = frame.LineBottom;
            Room = Math.Max(0, Foot - Top);
        }

        public bool Detailed { get; }

        public double Width { get; }

        public double Top { get; }

        public double Band { get; }

        public double Foot { get; }

        public double Room { get; }

        /// <summary>A value's height on the line.</summary>
        public double Y(double value) => _model.Laid ? value : Top + ChartRules.Y(_model.Fraction(value), Band);

        /// <summary>A value's height as a column's top.</summary>
        public double Column(double value) => Foot - (_model.Fraction(value) * Room);

        /// <summary>Where a value is drawn for this type.</summary>
        public double At(double value) => _model.Type == ChartType.Bars ? Column(value) : Y(value);
    }

    /// <summary>
    /// [S-6751] [S-6754] The value grid: a Track hairline at each nice step across the whole plot behind the series, and
    /// (<paramref name="labels"/>, drawn over the series so a column never hides it) its value over it at the text's inset,
    /// on the Solid pill: the series runs under the values at the plot's left side, and a line or a column through a value
    /// would be read as part of it.
    /// </summary>
    private void Grid(ChartModel model, Plot plot, ChartFrame frame, bool labels)
    {
        if (model.Laid)
        {
            return;
        }

        var w = frame.Plot.Width;
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
                Write(format(tick), ColourRole.TextTertiary, frame.Text.Left, ChartPlot.Within(frame, y - line, line), scrim: true, ValueRank);
            }
        }
    }

    /// <summary>The line and its wash: each run of samples as stretches of one role; washes, then the second series, then the lines.</summary>
    private void Lines(ChartModel model, IReadOnlyList<ChartSample> samples, Plot plot, double h)
    {
        // [S-6754] The wash runs down to the plot's foot, the card's: the fill reaches the edge under what is written there.
        var type = model.Type == ChartType.Line ? ChartType.Line : ChartType.Area;
        samples = model.Laid ? samples : ChartPlot.ToEdges(samples, plot.Width);
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

                var outline = ChartPlot.Outline(points, type, h);
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
        var second = model.Laid ? model.Second : ChartPlot.ToEdges(model.Second, plot.Width);
        foreach (var (first, last) in ChartPlot.Runs(second))
        {
            if (last == first)
            {
                continue;
            }

            var points = new List<ChartPoint>(last - first + 1);
            for (var i = first; i <= last; i++)
            {
                points.Add(new ChartPoint(second[i].X, plot.Y(second[i].Value)));
            }

            Add(new Path { Data = Figure(points, closed: false), Stroke = Family.Brush(ColourRole.TextTertiary), StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round }, 0, 0);
        }
    }

    /// <summary>
    /// [S-6750] [S-6753] A round column per sample over nothing, filled from the band's foot in its role; with no column to
    /// draw (nothing yet, or every value nothing: a dry day), the foot's hairline across the plot, so the chart still stands.
    /// </summary>
    private void Bars(IReadOnlyList<ChartSample> samples, Plot plot, double w, double h)
    {
        if (_model.Tracks)
        {
            Tracked(samples, plot, w, h);
            return;
        }

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

    /// <summary>
    /// [S-16436] Each slot's column in its track: the track the band's top to the plot's foot in the Track role, the fill
    /// rising from the foot by its share of that room in its role (a wet slot never under the track's width); a slot at
    /// nothing its track alone. Both stand on the foot square, round at the top. [S-16450] The columns spread across the plot
    /// edge to edge, the first on its left edge and the last on its right, 4 apart (<see cref="ChartPlot.Spread"/>).
    /// </summary>
    private void Tracked(IReadOnlyList<ChartSample> samples, Plot plot, double w, double h)
    {
        if (samples.Count == 0)
        {
            return;
        }

        var room = Math.Max(0, h - plot.Top);
        for (var i = 0; i < samples.Count; i++)
        {
            var (left, width) = ChartPlot.Spread(i, samples.Count, w);
            Add(new Border { Width = width, Height = room, CornerRadius = Top(width, room), Background = Family.Brush(ColourRole.Track) }, left, plot.Top);
            var share = _model.Fraction(samples[i].Value);
            if (!(share > 0))
            {
                continue;
            }

            var height = Math.Max(Math.Min(width, FamilyShape.Track), share * room);
            var bar = new Border { Width = width, Height = height, CornerRadius = Top(width, height) };
            Paint(bar, samples[i].Role);
            Add(bar, left, h - height);
        }

        static CornerRadius Top(double width, double height)
        {
            var round = Math.Min(Family.Round(width, height).TopLeft, FamilyShape.Radius);
            return new CornerRadius(round, round, 0, 0);
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
    private void Now(ChartModel model, IReadOnlyList<ChartSample> samples, Plot plot, ChartFrame frame, double x)
    {
        Down(x, frame, ColourRole.TextSecondary);
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

    /// <summary>The figures at their points: over or under the point (over a column), kept inside the text box; a dot where asked.</summary>
    private void Marks(ChartModel model, Plot plot, ChartFrame frame, double w)
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
            Figure(mark.Text, mark.TextRole, mark.X, y, mark.Under, frame, bars);
        }
    }

    /// <summary>A figure centred on <paramref name="x"/>, over <paramref name="y"/> or under it, kept inside the text box.</summary>
    private void Figure(string text, ColourRole role, double x, double y, bool under, ChartFrame frame, bool onColumn)
    {
        var line = FamilyType.CaptionLine;
        var block = Family.Text(FamilyType.Caption, role, text);
        block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var left = ChartPlot.Across(frame, x, block.DesiredSize.Width);
        var top = ChartPlot.Within(frame, onColumn ? y - line : y + (under ? FamilyShape.Stroke : -FamilyShape.Stroke - line), line);
        Written(block, left, top, FigureRank);
    }

    /// <summary>
    /// [S-6751] Detailed: the latest value's hairline across the plot and its pill at the text's right edge, then the high
    /// over its point and the low under its point, each with its dot (their figures where they leave the pill clear).
    /// </summary>
    private void Extremes(ChartModel model, IReadOnlyList<ChartSample> samples, IReadOnlyList<Candle> candles, Plot plot, ChartFrame frame, double w)
    {
        var format = model.Format ?? (v => Themia.Core.Localization.LocaleFormat.Number(Themia.Core.Localization.Strings.Current, v, 2));
        // The newest value: the last candle's close, or the last sample of the series, or of the second where it runs on
        // past the series (a price's after-hours bars).
        var finite = samples.Where(s => double.IsFinite(s.Value)).ToList();
        var after = model.Second.Where(s => double.IsFinite(s.Value)).ToList();
        double? latest = candles.Count > 0 ? candles[^1].Close
            : after.Count > 0 && (finite.Count == 0 || after[^1].X > finite[^1].X) ? after[^1].Value
            : finite.Count > 0 ? finite[^1].Value : null;
        var line = FamilyType.CaptionLine;
        if (latest is { } now)
        {
            var y = plot.At(now);
            Add(new Rectangle { Width = w, Height = FamilyShape.Hairline, Fill = Family.Brush(ColourRole.TextTertiary) }, 0, y - (FamilyShape.Hairline / 2));
            var box = Pill(Family.Text(FamilyType.Caption, ColourRole.TextPrimary, format(now)));
            box.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Written(box, Math.Max(frame.Text.Left, frame.Text.Right - box.DesiredSize.Width), ChartPlot.Within(frame, y - (line / 2), line), LatestRank);
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

            var block = Family.Text(FamilyType.Caption, ColourRole.TextSecondary, format(at.Value));
            block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var left = ChartPlot.Across(frame, at.X, block.DesiredSize.Width);
            Written(block, left, ChartPlot.Within(frame, y + (under ? FamilyShape.Stroke : -FamilyShape.Stroke - line), line), ExtremeRank);
        }
    }

    /// <summary>[S-6751] The times along the foot, each centred on its place and kept inside the text box; one that would touch the last is left out.</summary>
    private void Times(IReadOnlyList<ChartTick> times, ChartFrame frame)
    {
        var right = double.NegativeInfinity;
        foreach (var tick in times)
        {
            var block = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, tick.Text);
            block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var width = block.DesiredSize.Width;
            var left = ChartPlot.Across(frame, tick.At, width);
            if (left < right + FamilySpace.S2)
            {
                continue;
            }

            right = left + width;
            Written(block, left, frame.FootLine, TimeRank);
        }
    }

    /// <summary>
    /// [S-6754] Simple: the window's two ends along the foot, at the text's left and right edges; the end left out where it
    /// would touch the start. [S-16436] Over columns that stand on the foot, each on its scrim, the text where it would be.
    /// </summary>
    private void Ends(ChartEnds ends, ChartFrame frame, bool scrim)
    {
        var lead = scrim ? FamilySpace.S1 : 0;
        FrameworkElement Box(TextBlock text) => scrim ? Pill(text) : text;
        var start = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, ends.Start);
        start.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var end = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, ends.End);
        end.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        if (ends.Start.Length > 0)
        {
            Written(Box(start), frame.Text.Left - lead, frame.FootLine, TimeRank);
        }

        var endLeft = frame.Text.Right - end.DesiredSize.Width;
        if (ends.End.Length > 0 && (ends.Start.Length == 0 || endLeft >= frame.Text.Left + start.DesiredSize.Width + FamilySpace.S2 + (2 * lead)))
        {
            Written(Box(end), endLeft - lead, frame.FootLine, TimeRank);
        }
    }

    /// <summary>A Caption pill: the text on the Solid scrim, 4 either side, the family's radius.</summary>
    private static Border Pill(TextBlock text)
        => new()
        {
            Child = text,
            Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
            CornerRadius = Family.Radius,
            Background = Family.Brush(ColourRole.Solid),
        };

    /// <summary>[S-6754] Text written over the plot at (<paramref name="left"/>, <paramref name="top"/>); on a scrim (its pill round it, the text where it would be) when asked.</summary>
    private void Write(string text, ColourRole role, double left, double top, bool scrim, int rank)
    {
        var block = Family.Text(FamilyType.Caption, role, text);
        if (scrim)
        {
            Written(Pill(block), left - FamilySpace.S1, top, rank);
        }
        else
        {
            Written(block, left, top, rank);
        }
    }

    /// <summary>[S-6754] A label to write at (<paramref name="left"/>, <paramref name="top"/>), settled with the others at the end of the draw.</summary>
    private void Written(FrameworkElement element, double left, double top, int rank)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _written.Add((element, new Rect(left, top, element.DesiredSize.Width, element.DesiredSize.Height), rank));
    }

    /// <summary>
    /// [S-6754] Write the labels: by rank (the caption, the latest value's pill, the owner's figures, the high and the low,
    /// the times, the values on the grid), each where it touches none written before it; one that would is left out, so
    /// no two labels ever stand on each other.
    /// </summary>
    private void Settle()
    {
        var kept = new List<Rect>(_written.Count);
        foreach (var (element, box, _) in _written.OrderBy(w => w.Rank))
        {
            if (kept.Any(k => Overlaps(k, box)))
            {
                continue;
            }

            kept.Add(box);
            Add(element, box.X, box.Y);
        }
    }

    /// <summary>Whether two labels' boxes share more than a hairline.</summary>
    private static bool Overlaps(Rect a, Rect b)
        => Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X) > FamilyShape.Hairline
            && Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y) > FamilyShape.Hairline;

    /// <summary>A hairline down the plot in <paramref name="role"/>, between the owner's parts at its top and what is written at its foot.</summary>
    private void Down(double x, ChartFrame frame, ColourRole role)
        => Add(new Rectangle { Width = FamilyShape.Hairline, Height = Math.Max(0, frame.RulesBottom - frame.RulesTop), Fill = Family.Brush(role) }, x - (FamilyShape.Hairline / 2), frame.RulesTop);

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
