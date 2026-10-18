namespace Themia.Core.Widgets;

/// <summary>A caption's footprint on the chart, in DIPs from the chart's top-left corner.</summary>
public readonly record struct LabelBox(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

/// <summary>A high or low marker: the dot's centre and the price written beside it.</summary>
public readonly record struct ChartMarker(double X, double Y, string Text, bool Above);

/// <summary>The band the trace is drawn in: <see cref="Top"/> from the chart's top, <see cref="Usable"/> tall.</summary>
public readonly record struct ChartBand(double Top, double Usable);

/// <summary>
/// [S-1310] [S-1311] What is written on the stock chart, which is full-bleed (edge to edge and down to
/// the card's bottom edge): the high/low markers' prices and the start and end times. Everything sits
/// inside the chart, so none of it costs the chart an inset; the fill still reaches the bottom edge
/// under the times. There is no price axis: the markers already say the period's high and low.
/// </summary>
public static class StockChartLabels
{
    /// <summary>Space kept between a marker's price and the dot.</summary>
    public const double Clearance = 2;

    /// <summary>The thickness the trace is kept in by when nothing is written above or below it.</summary>
    public const double Stroke = 2;

    /// <summary>The time labels' distance from the chart's bottom edge.</summary>
    public const double TimesBottom = 4;

    private static double Line => FluentType.Caption.LineHeight;

    /// <summary>
    /// The trace's band in a chart <paramref name="height"/> tall: a caption line above for the high's
    /// price and below for the low's when <paramref name="markers"/>, and the times' line under that
    /// when <paramref name="times"/>, so the lowest point and its price never sit on a time.
    /// </summary>
    public static ChartBand Band(double height, bool markers, bool times)
    {
        var top = markers ? Line : Stroke;
        var bottom = (markers ? Line : Stroke) + (times ? Line + TimesBottom : 0);
        return new ChartBand(top, Math.Max(1, height - top - bottom));
    }

    /// <summary>Where the start and end times' line starts, from the chart's top.</summary>
    public static double TimesTop(double height) => Math.Max(0, height - TimesBottom - Line);

    /// <summary>The marker captions: centred on the dot, above or under it, kept inside the chart's width.</summary>
    public static IReadOnlyList<LabelBox> Place(double width, IReadOnlyList<ChartMarker> markers)
    {
        ArgumentNullException.ThrowIfNull(markers);

        var placed = new List<LabelBox>(markers.Count);

        foreach (var marker in markers)
        {
            var estimate = FluentType.EstimateWidth(FluentType.Caption, marker.Text.Length);
            var left = Math.Clamp(marker.X - (estimate / 2), StockLayout.Gap, Math.Max(StockLayout.Gap, width - estimate - StockLayout.Gap));
            var top = marker.Above ? marker.Y - Line - Clearance : marker.Y + Clearance;
            placed.Add(new LabelBox(left, top, estimate, Line));
        }

        return placed;
    }
}
