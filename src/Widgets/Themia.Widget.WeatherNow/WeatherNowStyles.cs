using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the large-icon style draws at one size ([S-6161]).</summary>
/// <param name="Temperature">The temperature's Readout step.</param>
/// <param name="Glyph">The sky's glyph: a family glyph size, no taller than the Readout's block; 0 for none.</param>
/// <param name="Condition">Whether the condition stands under the temperature.</param>
public readonly record struct LargeIconPlan(TypeStep Temperature, double Glyph, bool Condition);

/// <summary>
/// [S-6161] [S-6162] The current conditions' other two styles, on the Readout's steps ([S-6702]): the
/// same reading, never filling the box (a data widget's number stops at 40, [S-666]).
/// </summary>
public static class WeatherNowStyles
{
    /// <summary>The widest reading, the condition under it; no lead: the large icon stands beside it.</summary>
    public static readonly ReadoutText Worst = new("-18", "°F", LineUnder: true);

    /// <summary>The minimal style's widest reading: the temperature alone.</summary>
    public static readonly ReadoutText Bare = new("-18", "°F");

    /// <summary>The family's glyph sizes ([S-6704]: 16, 32, 48 and the Readout steps), smallest first.</summary>
    private static IEnumerable<double> GlyphSizes =>
        new[] { FamilyShape.Icon, FamilyShape.IconLarge, FamilyShape.IconHuge }.Concat(FamilyType.Big.Select(step => step.FontSize)).Order();

    /// <summary>
    /// [S-6161] The sky's glyph beside the temperature: the largest Readout step whose block (its line and the
    /// condition's under it) fits the content's height, within the area's ceiling, with a glyph beside it;
    /// the glyph the largest family size no taller than that block and as wide as the width the temperature
    /// leaves (8 between). Too narrow even for 28 with a 16 glyph, the temperature stands alone.
    /// </summary>
    public static LargeIconPlan LargeIcon(WidgetArea area)
    {
        var (width, height) = MeterPlan.Content(area);
        var ceiling = ReadoutRules.Ceiling(area);
        LargeIconPlan? chosen = null;

        foreach (var step in FamilyType.Big)
        {
            var block = ReadoutRules.Height(step, Worst);
            var glyph = Glyph(width, step, block);
            if (FamilyType.BaseOf(step) <= ceiling && block <= height && glyph > 0)
            {
                chosen = new LargeIconPlan(step, glyph, Condition: true);
            }
        }

        if (chosen is { } plan)
        {
            return plan;
        }

        var floor = FamilyType.Big28;
        var under = ReadoutRules.Height(floor, Worst) <= height;
        return new LargeIconPlan(floor, Glyph(width, floor, under ? ReadoutRules.Height(floor, Worst) : floor.LineHeight), under);
    }

    /// <summary>The largest family glyph no taller than <paramref name="block"/> that leaves the temperature its width.</summary>
    private static double Glyph(double width, TypeStep step, double block)
    {
        var room = width - FamilySpace.GlyphGap - ReadoutRules.Width(step, Worst);
        return GlyphSizes.Where(size => size <= block && size <= room).DefaultIfEmpty(0).Max();
    }

    /// <summary>[S-6162] The temperature alone: the Readout's step for the content box (28, or 40 once expanded both ways).</summary>
    public static TypeStep Minimal(WidgetArea area)
    {
        var (width, height) = MeterPlan.Content(area);
        return ReadoutRules.Step(area, width, height, Bare);
    }
}
