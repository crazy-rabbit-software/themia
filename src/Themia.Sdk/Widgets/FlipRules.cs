using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-6740] A row of split-flap cards as planned: the step their figures are set on, each card's width (its height is the step's line) and whether the line under the row shows.</summary>
public readonly record struct FlipPlan(TypeStep Step, double CardWidth, bool Under)
{
    /// <summary>A card's height: the step's line.</summary>
    public double CardHeight => Step.LineHeight;
}

/// <summary>
/// [S-6740] The split-flap cards of the clock's Flip style and the Countdown's (<c>FlipCard</c>): one
/// card per part of the time, 8 apart, each its figures' widest case on a family step with 12 either
/// side, as tall as the step's line. The step is chosen as a Readout's is, never computed: the largest
/// of the steps the widget may take (the Hero steps for a one-readout widget, [S-6737]) on which every
/// card, the gaps and what stands beside the row fit the width, and the line and the line under the
/// row (<see cref="FamilyType.Under"/>) fit the height; never below Big 28.
/// </summary>
public static class FlipRules
{
    /// <summary>The room either side of a card's figures.</summary>
    public const double Padding = FamilySpace.S3;

    /// <summary>The gap between two cards.</summary>
    public const double Gap = FamilySpace.S2;

    /// <summary>A card's width on <paramref name="step"/> for figures as wide as <paramref name="widest"/> ("88").</summary>
    public static double CardWidth(TypeStep step, string widest) => Math.Ceiling(HeroFit.Ems(widest) * step.FontSize) + (2 * Padding);

    /// <summary>How wide <paramref name="cards"/> cards are on <paramref name="step"/>, the gaps between them included.</summary>
    public static double RowWidth(TypeStep step, int cards, string widest)
        => cards <= 0 ? 0 : (cards * CardWidth(step, widest)) + ((cards - 1) * Gap);

    /// <summary>
    /// [S-6740] The plan for <paramref name="cards"/> cards of <paramref name="widest"/> in a
    /// <paramref name="width"/> × <paramref name="height"/> box of a view given <paramref name="area"/>:
    /// <paramref name="beside"/> is how wide what stands after the row is on a step (the clock's
    /// meridiem, its gap included), <paramref name="under"/> whether a line stands under the row.
    /// </summary>
    public static FlipPlan Plan(WidgetArea area, double width, double height, int cards, string widest, bool oneReadout, Func<TypeStep, double>? beside = null, bool under = false)
    {
        var chosen = FamilyType.Big28;
        if (double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0)
        {
            var ceiling = oneReadout ? double.PositiveInfinity : ReadoutRules.Ceiling(area);
            foreach (var step in ReadoutRules.Steps(oneReadout))
            {
                if (FamilyType.BaseOf(step) > ceiling)
                {
                    continue;
                }

                var wide = RowWidth(step, cards, widest) + (beside?.Invoke(step) ?? 0);
                var tall = step.LineHeight + (under ? FamilyType.Under(step).LineHeight : 0);
                if (wide <= width && tall <= height)
                {
                    chosen = step;
                }
            }
        }

        // A box too short for the line under even the floor step sheds that line, never the cards.
        var fits = !under || chosen.LineHeight + FamilyType.Under(chosen).LineHeight <= height;
        return new FlipPlan(chosen, CardWidth(chosen, widest), under && fits);
    }
}
