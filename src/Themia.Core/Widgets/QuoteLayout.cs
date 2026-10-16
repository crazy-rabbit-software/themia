using Themia.Core.Localization;
namespace Themia.Core.Widgets;

/// <summary>
/// How a quote is set: which ramp step a paragraph of it fits at, and how it is punctuated.
/// <para>
/// <see cref="FluentType.Fit"/> sizes a <i>line</i> — it asks whether the characters fit across the
/// box once. A quote is a paragraph: it wraps, so the question is whether the characters fit in the
/// box <b>at all</b>, across as many lines as the box has room for. Sizing a wrapping quote with the
/// single-line rule pins every quote at the floor, because no interesting quote fits on one line of
/// a 180px widget.
/// </para>
/// </summary>
public static class QuoteLayout
{
    /// <summary>
    /// The average character advance as a fraction of the font size, matching the one
    /// <see cref="FluentType"/> uses for the same reason: this only has to be close, because being
    /// slightly out changes nothing until it crosses a whole ramp step.
    /// </summary>
    private const double AverageAdvance = 0.58;

    /// <summary>
    /// The largest ramp step at which <paramref name="characters"/> of wrapped text still fit the
    /// box, never below <paramref name="floor"/> and never above <paramref name="ceiling"/>.
    /// <para>
    /// A step is still a step: the quote on a large tile moves from Body to Subtitle to Title, so
    /// every size on screen is one Windows itself uses. [S-210]
    /// </para>
    /// </summary>
    public static TypeStep Fit(double width, double height, int characters, TypeStep floor, TypeStep ceiling)
    {
        if (!IsUsable(width) || !IsUsable(height))
        {
            return floor;
        }

        var wanted = Math.Max(1, characters);
        var chosen = floor;

        foreach (var step in FluentType.Ramp)
        {
            if (step.FontSize < floor.FontSize || step.FontSize > ceiling.FontSize)
            {
                continue;
            }

            if (Capacity(step, width, height) >= wanted)
            {
                chosen = step;
            }
        }

        return chosen;
    }

    /// <summary>
    /// Roughly how many characters the box holds at a step: whole lines down, whole characters
    /// across. Fewer than one line of either means it holds nothing.
    /// </summary>
    public static double Capacity(TypeStep step, double width, double height)
    {
        if (!IsUsable(width) || !IsUsable(height) || step.FontSize <= 0)
        {
            return 0;
        }

        var lines = Math.Floor(height / step.LineHeight);
        var perLine = Math.Floor(width / (step.FontSize * AverageAdvance));

        return lines <= 0 || perLine <= 0 ? 0 : lines * perLine;
    }

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

        return trimmed.Length == 0 ? string.Empty : Strings.Format(locale, Str.WidgetsC_Quote_Quoted, ("text", trimmed));
    }

    /// <summary>
    /// [S-6405] The size of the large-marks style's opening mark, a step of the type ramp chosen by
    /// the card's smaller side: Title up to Regular, TitleLarge at Expanded, Display at Huge. The
    /// mark is an ornament over the quote, so it grows with the card rather than with the text.
    /// </summary>
    public static double MarkSize(WidgetArea area) => area.Least switch
    {
        SizeClass.Huge => FluentType.Display.FontSize,
        SizeClass.Expanded => FluentType.TitleLarge.FontSize,
        _ => FluentType.Title.FontSize,
    };

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

    private static bool IsUsable(double value) => double.IsFinite(value) && value > 0;
}
