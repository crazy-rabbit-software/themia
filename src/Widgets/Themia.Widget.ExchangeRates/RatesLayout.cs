using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-5146] The exchange-rate widget's ladder for one size.</summary>
/// <param name="HeroRoom">The readout's height (the rate line and its caption).</param>
/// <param name="ChartHeight">[S-6168] The 90-day chart under the readout; 0 for none.</param>
/// <param name="Beside">[S-6168] A strip: the readout is a column on the left, the chart beside it with the strip's height.</param>
/// <param name="HeroWidth">[S-6168] The readout column's width when <paramref name="Beside"/>; else 0.</param>
public sealed record RatesPlan(double HeroRoom, double ChartHeight, bool Beside = false, double HeroWidth = 0);

/// <summary>[S-5146] [S-6168] The exchange-rate widget's ladders: the number style's and the chart style's.</summary>
public static class RatesLayout
{
    /// <summary>[S-5146] The hero chart needs this much to show a trend rather than a squiggle.</summary>
    public const double HeroChartMin = 56;

    /// <summary>
    /// [S-5146] The number style: the pair's rate is the whole tile, as large as the tile allows.
    /// The chart style ([S-6168]) puts its 90 days under it.
    /// </summary>
    public static RatesPlan Rates(WidgetArea area, ExchangeRatesStyle style, double heroFloor)
    {
        if (!area.IsMeasured)
        {
            return new RatesPlan(0, 0);
        }

        var box = area.Deflate(0, OnlineLayouts.VerticalInset);
        var width = box.Width - (2 * OnlineLayouts.Inset);

        return style == ExchangeRatesStyle.Chart
            ? RatesChart(area, box, width, heroFloor)
            : new RatesPlan(box.Height, 0);
    }

    /// <summary>
    /// [S-6168] The chart style: the rate large, and its 90 days in what the readout leaves, edge to
    /// edge and down to the card's bottom; on a strip (at least 400 wide, 2:1) the readout is a column
    /// beside a chart of the strip's height. Where the chart would have under
    /// <see cref="HeroChartMin"/>, the readout alone.
    /// </summary>
    private static RatesPlan RatesChart(WidgetArea area, WidgetArea box, double width, double heroFloor)
    {
        if (area.Shape == AreaShape.Wide && area.Width >= 400)
        {
            var column = Math.Clamp(width * 0.42, 150, 280);
            return new RatesPlan(box.Height, Math.Max(HeroChartMin, area.Height), Beside: true, HeroWidth: column);
        }

        var room = new Disclosure(box.Width, box.Height, OnlineLayouts.Gap);
        room.Reserve(heroFloor);

        if (width < 160 || !room.Fits(HeroChartMin))
        {
            return new RatesPlan(box.Height, 0);
        }

        // The readout grows into a share of what is spare, the chart takes the rest (the stock rule, [S-1311]).
        var spare = Math.Max(0, room.Remaining - OnlineLayouts.Gap - HeroChartMin);
        var heroRoom = heroFloor + (spare * 0.4);
        var chart = area.Height - OnlineLayouts.VerticalInset - heroRoom - OnlineLayouts.Gap;
        return new RatesPlan(heroRoom, chart);
    }
}
