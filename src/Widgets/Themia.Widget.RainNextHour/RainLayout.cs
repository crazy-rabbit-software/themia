using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-5126] The rain widget's ladder for one size.</summary>
/// <param name="Headline">The sentence's step: Title where its line holds it, else Body strong.</param>
/// <param name="Wraps">Whether the sentence takes two lines (not even Body strong holds it on one).</param>
/// <param name="Caption">The intensity / total / chance line under the sentence.</param>
/// <param name="ChartHeight">The bars and their time line, 8 under the text, to the content's foot; 0 for none.</param>
public sealed record RainPlan(TypeStep Headline, bool Wraps, bool Caption, double ChartHeight)
{
    /// <summary>The bars' own height in the chart: the chart less the time line and the gap over it.</summary>
    public double BarsHeight => Math.Max(0, ChartHeight - RainLayout.LabelGap - FamilyType.Caption.LineHeight);
}

/// <summary>[S-5126] The rain widget's ladder, on the family's tokens.</summary>
public static class RainLayout
{
    /// <summary>[S-5126] The bars need this much to read as a timeline; the time line comes under them.</summary>
    public const double ChartMin = 32;

    /// <summary>The bars need a content this wide.</summary>
    public const double ChartWidthMin = 120;

    /// <summary>Between the bars and their time line.</summary>
    public const double LabelGap = FamilySpace.S1;

    /// <summary>
    /// [S-5126] Whether the window has rain to tell of (it starts, stops or goes on): only then the
    /// sentence, its caption and the bars; a dry window or one too short to speak for the hour is the
    /// Status row's one plain sentence.
    /// </summary>
    public static bool Warns(RainHeadline headline) => headline is RainHeadline.StartsIn or RainHeadline.StopsIn or RainHeadline.Continues;

    /// <summary>
    /// [S-5126] The sentence always, at the top of the content: Title where its line holds it across
    /// the content, else Body strong, on two lines where even that cannot hold it on one. Then the
    /// caption line (Body), then the bars, 8 under the text, taking the rest of the height to the
    /// content's foot where at least <see cref="ChartMin"/> of bars and their time line fit. The
    /// sentence never grows past Title: room left goes to the bars ([S-666]).
    /// </summary>
    public static RainPlan Rain(WidgetArea area, string headline, bool hasCaption)
    {
        ArgumentNullException.ThrowIfNull(headline);
        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, area.Height - FamilySpace.Inset);
        var ems = HeroFit.Ems(headline);

        var title = FamilyType.Title;
        var strong = FamilyType.BodyStrong;
        var step = ems * title.FontSize <= width ? title : strong;
        var wraps = step == strong && ems * strong.FontSize > width;
        var used = step.LineHeight * (wraps ? 2 : 1);

        var line = FamilyType.Body.LineHeight;
        var caption = hasCaption && used + line <= height + Disclosure.Tolerance;
        used += caption ? line : 0;

        var spare = height - used - FamilySpace.S2;
        var chart = width >= ChartWidthMin && spare + Disclosure.Tolerance >= ChartMin + LabelGap + FamilyType.Caption.LineHeight ? spare : 0;
        return new RainPlan(step, wraps, caption, chart);
    }
}
