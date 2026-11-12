using Themia.Core.Localization;
using Themia.Sdk.Kit;
using Themia.Widget.Quote;

namespace Themia.Core.Widgets;

/// <summary>
/// How a quote is set: on which family step, in how many lines, and how it is punctuated.
/// <para>
/// [S-11306] What a quote shows is its config: its Size names the step (Body, Title, 28), its Lines
/// the most lines it takes (a longer quote ends in an ellipsis), the attribution its line under on the
/// step's own line under, and the Marks style a large mark over it. The card follows; a wider or
/// narrower card only rewraps the quote.
/// </para>
/// </summary>
public static class QuoteLayout
{
    /// <summary>[S-11306] The fewest and the most lines a quote may be given, and a new one's.</summary>
    public const int MinLines = 2, MostLines = 6, DefaultLines = 3;

    /// <summary>[S-11306] The card's width: from 200 to 520, a new one 320.</summary>
    public const double Narrowest = 200, NaturalWidth = 320, Widest = 520;

    /// <summary>[S-11306] The quote's step at each size: Body, Title, Big 28.</summary>
    public static TypeStep StepAt(ReadoutSize size) => ReadoutSizes.Normalise(size) switch
    {
        ReadoutSize.Small => FamilyType.Body,
        ReadoutSize.Large => FamilyType.Big28,
        _ => FamilyType.Title,
    };

    /// <summary>The lines the config gives the quote, held to <see cref="MinLines"/>..<see cref="MostLines"/>.</summary>
    public static int LinesOf(Model.QuoteConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Math.Clamp(config.Lines, MinLines, MostLines);
    }

    /// <summary>[S-6405] [S-11306] The large mark over the quote on the Marks style (0 on Plain): on the quote's step from 28, else 16.</summary>
    public static double MarkOf(Model.QuoteConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Style == Model.QuoteStyle.Marks ? MarkSize(StepAt(config.Size)) : 0;
    }

    /// <summary>
    /// [S-11306] The quote's sizing ([S-2438]): the inset, the mark and its 8 on the Marks style, the
    /// lines on the Size's step, the attribution's line where it is shown, the inset; stepped by a line
    /// and following <see cref="Model.QuoteConfig.Lines"/>, 200..520 wide. No header by default.
    /// </summary>
    public static Sdk.WidgetSizing Sizing(Model.QuoteConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var step = StepAt(config.Size);
        var mark = MarkOf(config);
        var top = FamilySpace.Inset + (mark > 0 ? mark + FamilySpace.S2 : 0);
        var author = config.ShowAuthor ? FamilyType.Under(step).LineHeight : 0;
        var lines = new Sdk.SizeSteps(top + author + FamilySpace.Inset, step.LineHeight, MinLines, MostLines, LinesOf(config));
        return Sdk.WidgetSizing.Range(
                new Sdk.SizeDip(Narrowest, lines.At(MinLines)),
                new Sdk.SizeDip(NaturalWidth, lines.At(LinesOf(config))),
                new Sdk.SizeDip(Widest, lines.At(MostLines)))
            .WithHeightSteps(lines);
    }

    /// <summary>[S-11306] The config a quote dragged to <paramref name="size"/> means: as many lines as its height holds.</summary>
    public static Model.WidgetConfig SizeSets(Model.QuoteConfig config, Sdk.SizeDip size)
    {
        ArgumentNullException.ThrowIfNull(config);
        var lines = Sizing(config).HeightSteps!.Nearest(size.Height);
        if (lines == LinesOf(config))
        {
            return config;
        }

        var next = (Model.QuoteConfig)config.Clone();
        next.Lines = lines;
        return next;
    }

    /// <summary>
    /// [S-6405] The large mark's size: a glyph on the quote's own step (as a lead glyph beside a 40 is
    /// 40), 16 for a quote set on Title or Body.
    /// </summary>
    public static double MarkSize(TypeStep step) => FamilyType.BaseOf(step) >= 28 ? step.FontSize : FamilyShape.Icon;

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
