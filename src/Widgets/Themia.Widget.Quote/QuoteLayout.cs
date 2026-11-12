using Themia.Core.Localization;
using Themia.Sdk.Kit;
using Themia.Widget.Quote;
namespace Themia.Core.Widgets;

/// <summary>[S-1496] How a quote is set at one size.</summary>
/// <param name="Step">The quote's step: Body, Title, or a Readout step up to the area's ceiling.</param>
/// <param name="Author">Whether the attribution stands under it, on the step's line under.</param>
/// <param name="Mark">The large mark's size over it (a glyph on the step, 16 under Big 28), 0 for none.</param>
/// <param name="MaxLines">The whole lines the quote may take, at least one; a longer quote ends in an ellipsis.</param>
public readonly record struct QuotePlan(TypeStep Step, bool Author, double Mark, int MaxLines);

/// <summary>
/// How a quote is set: on which family step a paragraph of it fits, and how it is punctuated.
/// <para>
/// [S-1496] A quote is the widget, so it is set on the Big ramp (28 and 40, capped as every data
/// widget's number is by the area, <see cref="ReadoutRules.Ceiling"/>) where its wrapped lines fit,
/// and on Title, then Body, where even 28 does not. A quote is a paragraph: the question is how many
/// lines it wraps to on a step (<see cref="Lines"/>, word by word on Segoe UI Variable's advances,
/// <see cref="HeroFit.Ems"/>), not whether it fits across the box once.
/// </para>
/// </summary>
public static class QuoteLayout
{
    /// <summary>
    /// [S-1496] The plan for <paramref name="text"/> in a view of <paramref name="area"/> (the card less
    /// its header): the largest step on which the mark (with <paramref name="marks"/>), the wrapped quote
    /// and the attribution (with <paramref name="author"/>) fit the content box; the attribution gives
    /// way when not even Body holds the quote with it; a quote too long even for Body alone ends in an
    /// ellipsis on the last line that fits.
    /// </summary>
    public static QuotePlan Plan(WidgetArea area, string? text, bool author, bool marks)
    {
        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, area.Height - FamilySpace.Inset);
        var quote = text ?? string.Empty;

        foreach (var withAuthor in author ? new[] { true, false } : [false])
        {
            QuotePlan? best = null;
            foreach (var step in Steps(area))
            {
                var mark = marks ? MarkSize(step) : 0;
                var room = height - (mark > 0 ? mark + FamilySpace.S2 : 0) - (withAuthor ? FamilyType.Under(step).LineHeight : 0);
                var lines = Lines(quote, step, width);
                if (lines * step.LineHeight <= room + 0.01)
                {
                    best = new QuotePlan(step, withAuthor, mark, Whole(room, step));
                }
            }

            if (best is { } plan)
            {
                return plan;
            }
        }

        // Too long even for Body alone: Body, cut at the last whole line; the mark only where a line still fits under it.
        var body = FamilyType.Body;
        var marked = marks && height - MarkSize(body) - FamilySpace.S2 >= body.LineHeight;
        var left = height - (marked ? MarkSize(body) + FamilySpace.S2 : 0);
        return new QuotePlan(body, false, marked ? MarkSize(body) : 0, Whole(left, body));
    }

    /// <summary>[S-1496] The steps a quote may take in <paramref name="area"/>, smallest first: Body, Title, then Big up to the area's ceiling.</summary>
    public static IReadOnlyList<TypeStep> Steps(WidgetArea area)
    {
        var ceiling = ReadoutRules.Ceiling(area);
        return [FamilyType.Body, FamilyType.Title, .. FamilyType.Big.Where(s => FamilyType.BaseOf(s) <= ceiling)];
    }

    /// <summary>
    /// [S-6405] The large mark's size: a glyph on the quote's own step (as a lead glyph beside a 40 is
    /// 40), 16 for a quote set on Title or Body.
    /// </summary>
    public static double MarkSize(TypeStep step) => FamilyType.BaseOf(step) >= 28 ? step.FontSize : FamilyShape.Icon;

    /// <summary>
    /// [S-1496] How many lines <paramref name="text"/> wraps to on <paramref name="step"/> across
    /// <paramref name="width"/>: word by word, a word too long for a line broken across as many as it
    /// needs. An empty text is one line; a box with no width holds none.
    /// </summary>
    public static int Lines(string? text, TypeStep step, double width)
    {
        if (!(width > 0) || !double.IsFinite(width))
        {
            return int.MaxValue;
        }

        var space = HeroFit.Ems(" ") * step.FontSize;
        var lines = 1;
        var run = 0.0;
        foreach (var word in (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = HeroFit.Ems(word) * step.FontSize;
            if (w > width)
            {
                // A word wider than the line breaks across lines of its own, the last one partly filled.
                var spans = (int)Math.Ceiling(w / width);
                lines += (run > 0 ? 1 : 0) + spans - 1;
                run = w - ((spans - 1) * width);
            }
            else if (run == 0)
            {
                run = w;
            }
            else if (run + space + w <= width)
            {
                run += space + w;
            }
            else
            {
                lines++;
                run = w;
            }
        }

        return lines;
    }

    private static int Whole(double height, TypeStep step) => Math.Max(1, (int)Math.Floor((height + 0.01) / step.LineHeight));

    /// <summary>
    /// The quote with the typographic quotation marks around it.
    /// <para>
    /// Added here rather than stored, so that the user's list holds the words and only the words:
    /// a quote that carried its own marks would end up inside a second pair.
    /// </para>
    /// </summary>
    public static string Quoted(string? text) => Quoted(Strings.Current, text);

    /// <summary>
    /// <see cref="Quoted(string?)"/> with the quotation marks of <paramref name="locale"/>:
    /// “…” in English, „…“ in German, « … » in French, 「…」 in Japanese.
    /// </summary>
    public static string Quoted(Locale locale, string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        return trimmed.Length == 0 ? string.Empty : QuoteStr.WidgetsC_Quote_Quoted.Format(locale, ("text", trimmed));
    }

    /// <summary>
    /// [S-6404] The quote as the style sets it: in the language's own marks for Plain, bare under
    /// the large mark for Marks (a mark at each end as well would say it twice).
    /// </summary>
    public static string Set(Locale locale, string? text, Model.QuoteStyle style)
        => style == Model.QuoteStyle.Marks ? text?.Trim() ?? string.Empty : Quoted(locale, text);

    /// <summary>
    /// The attribution line, em-dashed the way a pull quote is set. An anonymous quote gets no line
    /// at all rather than a dash with nothing after it.
    /// </summary>
    public static string Attribution(string? author)
    {
        var trimmed = author?.Trim() ?? string.Empty;

        return trimmed.Length == 0 ? string.Empty : $"— {trimmed}";
    }
}
