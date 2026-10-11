namespace Themia.Core.Widgets;

/// <summary>A caption's footprint on the chart, in DIPs from the chart's top-left corner.</summary>
public readonly record struct LabelBox(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    /// <summary>Whether the two boxes come closer than <paramref name="gap"/> on both axes.</summary>
    public bool Collides(LabelBox other, double gap)
        => Left < other.Right + gap && other.Left < Right + gap
        && Top < other.Bottom + gap && other.Top < Bottom + gap;
}

/// <summary>A high or low marker: the dot's centre and the price written beside it.</summary>
public readonly record struct ChartMarker(double X, double Y, string Text, bool Above);

/// <summary>Where the chart's captions go. An axis label that would collide with a marker's is null (not drawn).</summary>
public sealed record ChartLabelPlan(IReadOnlyList<LabelBox> Markers, IReadOnlyList<LabelBox?> Axis);

/// <summary>
/// [S-1310] [S-1311] The high/low markers' prices and the price axis share the chart's right edge:
/// the period's high is both the top of the axis and, when it falls near the end of the period,
/// the high marker. The marker wins, because it says the same price and also says where; the axis
/// label it would overlap is left out, never drawn on top of it.
/// </summary>
public static class StockChartLabels
{
    /// <summary>Space kept between a marker's price and the dot, and between any two captions.</summary>
    public const double Clearance = 2;

    /// <summary>
    /// Place the marker captions and the axis captions (top to bottom, spread evenly over the
    /// plotted band from <paramref name="pad"/> down <paramref name="usable"/>).
    /// </summary>
    public static ChartLabelPlan Place(
        double width,
        double height,
        double pad,
        double usable,
        IReadOnlyList<ChartMarker> markers,
        IReadOnlyList<string> axis)
    {
        ArgumentNullException.ThrowIfNull(markers);
        ArgumentNullException.ThrowIfNull(axis);

        var line = FluentType.Caption.LineHeight;
        var placed = new List<LabelBox>(markers.Count);

        foreach (var marker in markers)
        {
            var estimate = FluentType.EstimateWidth(FluentType.Caption, marker.Text.Length);
            var left = Math.Clamp(marker.X - (estimate / 2), StockLayout.Gap, Math.Max(StockLayout.Gap, width - estimate - StockLayout.Gap));
            var top = marker.Above ? marker.Y - line - Clearance : marker.Y + Clearance;
            placed.Add(new LabelBox(left, top, estimate, line));
        }

        var labels = new List<LabelBox?>(axis.Count);

        for (var i = 0; i < axis.Count; i++)
        {
            var fraction = axis.Count == 1 ? 0 : (double)i / (axis.Count - 1);
            var estimate = FluentType.EstimateWidth(FluentType.Caption, axis[i].Length);
            var top = Math.Clamp(pad + (fraction * usable) - (line / 2), 0, Math.Max(0, height - line));
            var box = new LabelBox(width - StockLayout.Inset - estimate, top, estimate, line);

            labels.Add(placed.Any(marker => marker.Collides(box, Clearance)) ? null : box);
        }

        return new ChartLabelPlan(placed, labels);
    }
}
