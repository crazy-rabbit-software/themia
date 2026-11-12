using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-5146] The exchange-rate widget's layout for one size.</summary>
/// <param name="Rate">The rate's Readout step.</param>
/// <param name="Fold">Whether the quote's code is drawn, and whether the day change stands beside the rate or under it ([S-6707]).</param>
/// <param name="Under">Whether the line under the rate is shown (the change folded under it, else "per CHF").</param>
/// <param name="ChartTop">[S-6168] Where the 90 days start in the view: 8 under the rate.</param>
/// <param name="ChartHeight">[S-6168] The 90 days' height, down to the card's foot; 0 for none.</param>
public readonly record struct RatesPlan(TypeStep Rate, ReadoutFold Fold, bool Under, double ChartTop, double ChartHeight)
{
    public bool Chart => ChartHeight > 0;
}

/// <summary>
/// [S-5146] [S-6168] The exchange-rate widget's layout, on the family's blocks ([S-6702], [S-6717]), read
/// as a meter reads ([S-1762]): the rate is a Readout at the view's top, start-aligned, on the step it
/// takes in the area (28, or 40 once expanded both ways, and no larger, [S-666]), the quote's code after it and the day change
/// beside it or, where they do not fit, on the line under it ([S-6707]); else "per CHF" is that line. The
/// chart style's 90 days run 8 under it, edge to edge and down to the card's foot, from
/// <see cref="BandFloor"/>. Room left at L and XL is the chart's; the Number style leaves it empty.
/// </summary>
public static class RatesLayout
{
    /// <summary>The least chart worth drawing under the rate.</summary>
    public const double BandFloor = FamilySpace.S6;

    /// <summary>The plan for a view of <paramref name="area"/>: the rate and its code (<paramref name="rate"/>), the change's widest case, the style.</summary>
    public static RatesPlan Rates(WidgetArea area, ExchangeRatesStyle style, ReadoutText rate, string delta)
    {
        var (width, height) = (Math.Max(0, area.Width - (2 * FamilySpace.Inset)), Math.Max(0, area.Height - FamilySpace.Inset));
        var text = rate with { LineUnder = true };
        var step = ReadoutRules.Step(area, width, height, text);
        var fold = ReadoutRules.Fold(step, rate, delta, width);
        var under = ReadoutRules.Height(step, text) <= height;
        var top = step.LineHeight + (under ? FamilyType.Under(step).LineHeight : 0) + FamilySpace.S2;
        var band = area.Height - top;
        var chart = style == ExchangeRatesStyle.Chart && band >= BandFloor ? band : 0;
        return new RatesPlan(step, fold, under, chart > 0 ? top : 0, chart);
    }
}
