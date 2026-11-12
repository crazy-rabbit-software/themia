using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the stock widget is asked to fit.</summary>
/// <param name="Price">The price and its currency as the Readout draws them ("420.27", " USD").</param>
/// <param name="Delta">The change's widest case in the user's language ("+88.88%"), so the fold does not move with the price.</param>
/// <param name="Rows">[S-2777] [S-1308] The fact rows it has under the price: the holding, the extended-hours print (0 to 2).</param>
/// <param name="Chart">The chart style, with at least two bars to draw.</param>
/// <param name="MinMax">The high and low markers are switched on.</param>
/// <param name="OwnUnder">A line of its own under the price (the market's session, "Market closed").</param>
public readonly record struct StockInputs(ReadoutText Price, string Delta, int Rows, bool Chart, bool MinMax, bool OwnUnder = false);

/// <summary>[S-1311] One layout of the stock widget.</summary>
/// <param name="Price">The price's Readout step.</param>
/// <param name="Fold">Whether the currency is drawn, and whether the change stands beside the price or under it ([S-6707]).</param>
/// <param name="Under">Whether the line under the price is shown (the change folded under it, else the session).</param>
/// <param name="Rows">How many fact rows stand under the price (the first ones).</param>
/// <param name="RowsTop">Where the rows start in the view: 8 under the price.</param>
/// <param name="ChartTop">Where the chart starts in the view: 8 under the price, or 8 under the rows.</param>
/// <param name="ChartHeight">The chart's height, down to the card's foot; 0 for none.</param>
/// <param name="Labels">The start and end times, inside the chart's bottom band.</param>
/// <param name="MinMax">The high and low markers.</param>
public readonly record struct StockPlan(TypeStep Price, ReadoutFold Fold, bool Under, int Rows, double RowsTop, double ChartTop, double ChartHeight, bool Labels, bool MinMax)
{
    public bool Chart => ChartHeight > 0;
}

/// <summary>
/// [S-1311] The stock widget's ladder, on the family's blocks ([S-6702], [S-6711], [S-6717]), read as a
/// meter reads ([S-1762]): the price is a Readout at the view's top, start-aligned, on the step it takes
/// in the area (28, or 40 once expanded both ways, and no larger, [S-666]), the currency after it and the change beside it or,
/// where they do not fit, on the line under it ([S-6707]); the holding and the extended-hours print are
/// fact rows 8 under it ([S-2777], [S-1308]); the chart style's period runs 8 under them, edge to edge and
/// down to the card's foot, as a meter's history ([S-1824]). The chart wins over the rows: a row is shown
/// only while the chart keeps <see cref="BandFloor"/> under it. The start and end times from a regular
/// height, the high and low markers from a chart of <see cref="MarkersFrom"/>. Room left at L and XL is
/// the chart's ([S-2770]); the Number style has no chart and leaves it empty.
/// </summary>
public static class StockLayout
{
    /// <summary>The least chart worth drawing under the price.</summary>
    public const double BandFloor = FamilySpace.S6;

    /// <summary>[S-1310] The markers' prices need a caption line above and below the trace.</summary>
    public const double MarkersFrom = FamilyShape.Row2;

    public static StockPlan Plan(WidgetArea area, StockInputs input)
    {
        var (width, height) = (Math.Max(0, area.Width - (2 * FamilySpace.Inset)), Math.Max(0, area.Height - FamilySpace.Inset));
        var text = input.Price with { LineUnder = true };
        var step = ReadoutRules.Step(area, width, height, text);
        var fold = ReadoutRules.Fold(step, input.Price, input.Delta, width);
        var under = (!fold.DeltaBeside || input.OwnUnder) && ReadoutRules.Height(step, text) <= height;
        var top = step.LineHeight + (under ? FamilyType.Under(step).LineHeight : 0) + FamilySpace.S2;

        if (input.Chart)
        {
            for (var rows = Math.Max(0, input.Rows); rows >= 0; rows--)
            {
                var chartTop = top + (rows > 0 ? (rows * FamilyShape.Row1) + FamilySpace.S2 : 0);
                var band = area.Height - chartTop;
                if (band >= BandFloor)
                {
                    var labels = area.HeightClass >= SizeClass.Regular;
                    return new StockPlan(step, fold, under, rows, top, chartTop, band, labels, input.MinMax && band >= MarkersFrom);
                }
            }
        }

        var fit = Math.Min(Math.Max(0, input.Rows), RowRules.Fit(height - top, 1));
        return new StockPlan(step, fold, under, fit, top, 0, 0, false, false);
    }
}
