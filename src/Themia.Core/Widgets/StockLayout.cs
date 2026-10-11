namespace Themia.Core.Widgets;

/// <summary>What the stock widget is asked to fit.</summary>
/// <param name="PriceCharacters">The price's worst-case length in characters (at least seven).</param>
/// <param name="ExtrasCharacters">The change and currency beside it, in characters.</param>
/// <param name="Chart">A chart is wanted and there are at least two bars.</param>
/// <param name="Extended">An extended-hours print is to be shown.</param>
/// <param name="MinMax">The high and low markers are switched on.</param>
public readonly record struct StockInputs(
    double PriceCharacters,
    double ExtrasCharacters,
    bool Chart,
    bool Extended,
    bool MinMax);

/// <summary>One layout of the stock widget.</summary>
public readonly record struct StockPlan(
    TypeStep Price,
    TypeStep Change,
    bool ExtrasUnder,
    bool Currency,
    bool BadgeText,
    bool Beside,
    double ReadoutWidth,
    bool ExtendedLine,
    bool Chart,
    double ChartHeight,
    bool Labels,
    bool MinMax);

/// <summary>
/// [S-1311] The stock widget's disclosure ladder, replacing the fixed chart band (34% of the tile,
/// capped at 120px) that left a dead band over the price on a large tile.
/// <list type="number">
/// <item>Rung 0 at every size: the price and its signed, coloured change.</item>
/// <item>The currency and the session glyph from 180px wide; the session's words from 300px.</item>
/// <item>The extended-hours print under the price, when there is one and a line for it.</item>
/// <item>The chart, at least 24px, full-bleed: edge to edge and down to the card's bottom edge. The
/// start and end times (only those) from 128px of height, drawn inside the chart's bottom band;
/// the high/low markers (if on) from 48px of chart. No price axis: the markers carry the range's
/// prices, and an axis would need an inset of its own.</item>
/// <item>A wide strip puts the readout <i>beside</i> the chart, which then has the strip's whole
/// height, instead of a 50px band under a readout.</item>
/// </list>
/// [S-2770] Nothing else at any size: no facts (previous close, ranges, volume) and no range
/// chips; the period is a setting. The chart takes what is left, not a fixed share; the price
/// grows only to a cap set by the tile's size class, so a big tile shows a big chart rather than a
/// huge number over a strip.
/// </summary>
public static class StockLayout
{
    public const double Inset = 16;
    public const double Gap = 4;
    public const double MinChart = 24;

    private static double LabelLine => FluentType.Caption.LineHeight;

    public static StockPlan Plan(WidgetArea area, StockInputs input)
    {
        var chars = Math.Max(7, input.PriceCharacters);

        if (!area.IsMeasured)
        {
            return new StockPlan(FluentType.Subtitle, FluentType.Body, false, true, true, false, 0, input.Extended,
                input.Chart, MinChart, false, false);
        }

        var text = Math.Max(0, area.Width - (2 * Inset));
        var currency = area.Width >= 180;
        var badgeText = area.Width >= 300;

        // A strip: readout on the left, the chart beside it with the strip's whole height.
        if (input.Chart && area.Shape == AreaShape.Wide && area.Width >= 400)
        {
            var readout = Math.Clamp(text * 0.42, 150, 280);
            var room = new Disclosure(readout, Math.Max(0, area.Height - (2 * Gap)), Gap);
            room.Reserve(FluentType.Subtitle.LineHeight);
            room.Reserve(FluentType.Body.LineHeight);
            var extendedBeside = input.Extended && room.Take(LabelLine);

            var price = FluentType.Fit(readout, FluentType.Subtitle.LineHeight + room.Remaining, chars, FluentType.Subtitle, Cap(area));
            // The strip's whole height; the times are drawn inside it.
            var chart = Math.Max(MinChart, area.Height);
            var labels = area.Height >= 96;

            return new StockPlan(price, ChangeStep(price), true, currency, true, true, readout, extendedBeside,
                true, chart, labels, input.MinMax && chart >= 48);
        }

        var budget = new Disclosure(area.Width, Math.Max(0, area.Height - Gap), Gap);

        // Rung 0: the price at its smallest, and the change line it may need under it.
        budget.Reserve(FluentType.Subtitle.LineHeight);

        var smallest = FluentType.Subtitle;
        var under = input.ExtrasCharacters > 1 && !ReadoutLayout.CaptionFitsBeside(
            text * 0.98, smallest, chars, ChangeStep(smallest), input.ExtrasCharacters, 8);

        if (under)
        {
            budget.Reserve(FluentType.Body.LineHeight);
        }

        var extended = input.Extended && budget.Take(LabelLine);

        var chartWanted = input.Chart && budget.Take(MinChart);

        // The times are drawn inside the chart's bottom band, so their line is the chart's height.
        var labelsLine = chartWanted && area.HeightClass >= SizeClass.Regular && budget.Take(LabelLine);

        // What is left: the price grows to its cap, the chart takes the rest.
        var leftover = budget.Remaining;
        var priceRoom = FluentType.Subtitle.LineHeight + (chartWanted ? Math.Min(leftover, leftover * 0.45) : leftover);
        var priceWidth = under ? text : text * 0.66;
        var priceStep = FluentType.Fit(priceWidth, priceRoom, chars, FluentType.Subtitle, chartWanted ? Cap(area) : FluentType.Display);

        // Re-check the change line against the grown price: beside where it fits, under where it does not.
        var extrasUnder = input.ExtrasCharacters > 1 && !ReadoutLayout.CaptionFitsBeside(
            text * 0.98, priceStep, chars, ChangeStep(priceStep), input.ExtrasCharacters, 8);

        if (extrasUnder && !under)
        {
            // The change now needs a line of its own; take it from the price, never from rung 0.
            var withLine = FluentType.Fit(text, priceRoom - FluentType.Body.LineHeight - Gap, chars, FluentType.Subtitle, priceStep);
            priceStep = withLine;
            leftover -= FluentType.Body.LineHeight + Gap;
        }

        var chartHeight = chartWanted
            ? Math.Max(MinChart, MinChart + leftover - (priceStep.LineHeight - FluentType.Subtitle.LineHeight))
                + (labelsLine ? LabelLine + Gap : 0)
            : 0;

        return new StockPlan(
            priceStep,
            ChangeStep(priceStep),
            extrasUnder,
            currency,
            badgeText,
            false,
            text,
            extended,
            chartWanted,
            chartHeight,
            labelsLine,
            input.MinMax && chartWanted && chartHeight >= 48);
    }

    /// <summary>
    /// The change steps once, with the price, rather than sliding: next to a 28px number a 14px
    /// line is right, next to a 68px one it is a footnote.
    /// </summary>
    public static TypeStep ChangeStep(TypeStep price)
        => price.FontSize >= FluentType.Title.FontSize ? FluentType.BodyLarge : FluentType.Body;

    /// <summary>How large the price may grow while a chart shares the tile, by the tile's smaller class.</summary>
    private static TypeStep Cap(WidgetArea area) => area.Least switch
    {
        SizeClass.Huge => FluentType.Display,
        SizeClass.Expanded => FluentType.TitleLarge,
        _ => FluentType.Title,
    };
}
