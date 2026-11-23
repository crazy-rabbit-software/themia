namespace Themia.Sdk.Kit;

/// <summary>
/// [S-6750] One sample of a chart's series: where it stands across the plot (DIPs from its left edge, the owner's
/// layout: slots, cells, even spacing), its value (in the model's <see cref="ChartModel.Low"/>..<see cref="ChartModel.High"/>
/// units; not finite is a gap that breaks the line) and its role (null: the chart's role, recoloured in place).
/// </summary>
public readonly record struct ChartSample(double X, double Value, ColourRole? Role = null);

/// <summary>
/// [S-6750] A figure written at a point of the series (<paramref name="X"/> DIPs across, <paramref name="Value"/> on the
/// scale): over it or <paramref name="Under"/> it (over a column), in <paramref name="TextRole"/>, with a dot on the point
/// in the series' role when <paramref name="Dot"/> (a marked extreme).
/// </summary>
public readonly record struct ChartMark(double X, double Value, string Text, bool Under, ColourRole TextRole, bool Dot = false);

/// <summary>[S-6750] A span of the plot shaded behind everything (the UV graph's daylight), DIPs from its left edge.</summary>
public readonly record struct ChartShade(double From, double To);

/// <summary>[S-6754] The window's two ends a Simple chart writes at its foot ("Now" and the window's end, a price's first and last time).</summary>
public readonly record struct ChartEnds(string Start, string End);

/// <summary>
/// [S-6750] [S-6751] [S-6754] What the one chart draws: the series (and a second, tertiary), its type and detail, the value
/// scale they are drawn against, the room its owner keeps over and under the line for what it writes (Simple), what its
/// owner stands over the plot's top and foot (a reading, a top line, the wind's arrows) and what is drawn behind and over
/// it: shaded spans, hairlines across (days' borders), the moment now, the figures, the window's ends. Detailed adds the
/// value grid (<see cref="Format"/> writes its values), the <see cref="Times"/> along the foot, the low, the high and the
/// latest marked, and a hairline at the latest. The plot is the chart's whole box, the card edge to edge, and all of it is
/// written inside it (<see cref="ChartPlot.Frame"/>). Built by each graph widget from its data and laid out again when its
/// plot changes size; the geometry is <see cref="ChartPlot"/>'s and <see cref="ChartRules"/>'.
/// </summary>
public sealed record ChartModel
{
    public static ChartModel Empty { get; } = new();

    public ChartType Type { get; init; } = ChartType.Area;

    public ChartDetail Detail { get; init; } = ChartDetail.Simple;

    public IReadOnlyList<ChartSample> Series { get; init; } = [];

    /// <summary>The second series, tertiary, on the same scale (upload, feels like, extended hours).</summary>
    public IReadOnlyList<ChartSample> Second { get; init; } = [];

    /// <summary>[S-6750] Real periods for the candles, one per sample of <see cref="Series"/>; null makes them of the values.</summary>
    public IReadOnlyList<Candle>? Ohlc { get; init; }

    /// <summary>The value the plot's foot stands for. Not finite: the samples' values are already heights in DIPs (a curve its owner laid out).</summary>
    public double Low { get; init; }

    /// <summary>The value the plot's top stands for; equal to <see cref="Low"/>, every value sits in the middle.</summary>
    public double High { get; init; } = 1;

    /// <summary>What the owner keeps free over the line inside the plot (its figures), DIPs.</summary>
    public double PadTop { get; init; }

    /// <summary>What the owner keeps free under the line inside the plot (its figures, its times), DIPs.</summary>
    public double PadBottom { get; init; }

    /// <summary>
    /// [S-6754] What the owner stands over the plot's top (a reading, a top line), DIPs from the plot's top edge, its inset
    /// and its gap to the line included: the line keeps under it, the plot still runs to the edge behind it.
    /// </summary>
    public double Over { get; init; }

    /// <summary>
    /// [S-6754] What the owner stands over the plot's foot, over the line of times or ends (the wind's arrows and their
    /// gap), DIPs: the line keeps over it, the plot still runs to the foot under it.
    /// </summary>
    public double Under { get; init; }

    /// <summary>[S-6754] The window's two ends a Simple chart writes at its foot; null writes none. Detailed writes its <see cref="Times"/> there.</summary>
    public ChartEnds? Ends { get; init; }

    /// <summary>The main line's ends are round (the weather graphs').</summary>
    public bool RoundCaps { get; init; }

    /// <summary>A run of one sample is a dot (a gap on either side of it); off, it is not drawn.</summary>
    public bool LoneDots { get; init; }

    /// <summary>
    /// [S-16436] Columns that stand in their tracks (owner, 2026-11-19: "Plot edge to edge", the plot and its fill reach the
    /// card's edges, the labels stand inside it, over the plot; "even if there is no rain in the next two hours ... it should
    /// show the default values in the graph"): every slot's column has its track, the series' band from its top to the plot's
    /// foot in the Track role, and its fill rises from the plot's foot; a slot at nothing is its track alone, never a bare
    /// hairline. The ends at the foot are written over them on their scrim. Bars only; off, columns stand on the band's foot.
    /// </summary>
    public bool Tracks { get; init; }

    public IReadOnlyList<ChartShade> Shades { get; init; } = [];

    /// <summary>Hairlines down the plot in the Track role (the days' borders), DIPs from its left edge.</summary>
    public IReadOnlyList<double> Rules { get; init; } = [];

    /// <summary>The moment now: a secondary hairline down the plot and a dot where the line is, DIPs from its left edge.</summary>
    public double? Now { get; init; }

    public IReadOnlyList<ChartMark> Marks { get; init; } = [];

    /// <summary>[S-6751] A value as the widget writes it ("32%", "1.2 MB/s", "18°"); null writes no values on the grid.</summary>
    public Func<double, string>? Format { get; init; }

    /// <summary>
    /// [S-6751] The unit the grid's nice steps are counted in: 1, or a power of 1024 for bytes (<see cref="ChartPlot.BinaryUnit"/>),
    /// so a step is 2 MB/s, never 1.9 MB/s.
    /// </summary>
    public double TickUnit { get; init; } = 1;

    /// <summary>[S-6751] The times written along the foot when Detailed, each at its DIPs from the plot's left edge.</summary>
    public IReadOnlyList<ChartTick> Times { get; init; } = [];

    /// <summary>
    /// [S-6753] The value the chart stands at with nothing to draw (no samples yet, a sensor not there, an answer with
    /// nothing in its window, a dry nowcast): the frame and a flat series at it, never no chart. 0 unless the kind's
    /// natural default is another.
    /// </summary>
    public double Baseline { get; init; }

    /// <summary>[S-6753] A small caption written with the chart ("No rain in the next 2 hours", "No data yet"); empty for none.</summary>
    public string Caption { get; init; } = string.Empty;

    /// <summary>A share of the scale (0 at <see cref="Low"/>, 1 at <see cref="High"/>, the middle when they are equal; a gap stays NaN).</summary>
    public double Fraction(double value)
        => !double.IsFinite(value) ? double.NaN : High > Low ? Math.Clamp((value - Low) / (High - Low), 0, 1) : 0.5;

    /// <summary>Whether the values are heights already laid out by the owner.</summary>
    public bool Laid => !double.IsFinite(Low);
}
