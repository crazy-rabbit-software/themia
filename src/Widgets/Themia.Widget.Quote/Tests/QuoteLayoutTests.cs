using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Quote.Tests;

/// <summary>
/// How a quote is set. The two things a pull quote has to get right at both ends of the size range:
/// which family step it takes, and its punctuation.
/// </summary>
public class QuoteLayoutTests
{
    private const string Newton = "“What we know is a drop, what we don’t know is an ocean.”";

    private static readonly WidgetArea XS = new(120, 76);
    private static readonly WidgetArea S = new(200, 106);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea XL = new(800, 516);

    private static IEnumerable<TypeStep> Steps => [FamilyType.Body, FamilyType.Title, .. FamilyType.Big];

    // -----------------------------------------------------------------------------------------
    // [S-1496] the step: the family's, the Big ramp under the area's ceiling
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S1496_every_step_is_one_of_the_familys()
    {
        foreach (var width in new[] { 40.0, 180, 320, 800, 2000 })
        {
            foreach (var height in new[] { 30.0, 120, 300, 600, 1400 })
            {
                Assert.Contains(QuoteLayout.Plan(new WidgetArea(width, height), Newton, author: true, marks: false).Step, Steps);
            }
        }
    }

    [Fact]
    public void S1496_the_big_steps_stop_at_the_areas_ceiling()
    {
        Assert.Equal(FamilyType.Big68, QuoteLayout.Plan(new WidgetArea(2000, 1400), "“Done.”", author: true, marks: false).Step);
        Assert.Equal(FamilyType.Big40, QuoteLayout.Plan(new WidgetArea(320, 400), "“Done.”", author: true, marks: false).Step);
        Assert.Equal(FamilyType.Big28, QuoteLayout.Plan(S, "“Done.”", author: true, marks: false).Step);
    }

    [Fact]
    public void S1496_a_bigger_tile_never_takes_a_smaller_step()
    {
        var small = QuoteLayout.Plan(S, Newton, author: true, marks: false).Step;
        var medium = QuoteLayout.Plan(M, Newton, author: true, marks: false).Step;
        var large = QuoteLayout.Plan(XL, Newton, author: true, marks: false).Step;

        Assert.True(small.FontSize <= medium.FontSize);
        Assert.True(medium.FontSize <= large.FontSize);
        Assert.True(small.FontSize < large.FontSize);
    }

    [Fact]
    public void S1496_a_long_quote_takes_a_smaller_step_than_a_short_one_in_the_same_box()
    {
        var shortQuote = QuoteLayout.Plan(M, "“Done is better than perfect.”", author: true, marks: false).Step;
        var longQuote = QuoteLayout.Plan(M, string.Join(' ', Enumerable.Repeat(Newton, 3)), author: true, marks: false).Step;

        Assert.True(longQuote.FontSize < shortQuote.FontSize);
    }

    [Fact]
    public void S1496_the_wrapped_lines_and_the_attribution_fit_the_content_box()
    {
        var plan = QuoteLayout.Plan(M, Newton, author: true, marks: false);
        var lines = QuoteLayout.Lines(Newton, plan.Step, M.Width - 32);
        var under = FamilyType.Under(plan.Step).LineHeight;

        Assert.True(plan.Author);
        Assert.True((lines * plan.Step.LineHeight) + under <= M.Height - 16);
        Assert.Equal((int)((M.Height - 16 - under) / plan.Step.LineHeight), plan.MaxLines);
    }

    [Fact]
    public void S1496_the_author_gives_way_before_the_quote_is_cut()
    {
        // Body holds the quote in three lines here, and the author's line would be a fourth it has no room for.
        var plan = QuoteLayout.Plan(new WidgetArea(200, 76), "“Well done is better than well said, and better than nothing.”", author: true, marks: false);

        Assert.Equal(FamilyType.Body, plan.Step);
        Assert.False(plan.Author);
    }

    [Fact]
    public void S1496_a_quote_too_long_for_body_ends_at_the_last_whole_line()
    {
        var plan = QuoteLayout.Plan(XS, string.Join(' ', Enumerable.Repeat(Newton, 4)), author: true, marks: false);

        Assert.Equal(FamilyType.Body, plan.Step);
        Assert.False(plan.Author);
        Assert.Equal(3, plan.MaxLines);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-5, 100)]
    [InlineData(double.NaN, 100)]
    public void S1496_an_unmeasured_box_gets_body_rather_than_a_NaN_on_the_render_path(double width, double height)
    {
        var plan = QuoteLayout.Plan(new WidgetArea(width, height), Newton, author: true, marks: true);

        Assert.Equal(FamilyType.Body, plan.Step);
        Assert.True(plan.MaxLines >= 1);
    }

    [Fact]
    public void S1496_a_quote_wraps_word_by_word()
    {
        var body = FamilyType.Body;

        Assert.Equal(1, QuoteLayout.Lines(string.Empty, body, 100));
        Assert.Equal(1, QuoteLayout.Lines("Done.", body, 100));
        Assert.Equal(2, QuoteLayout.Lines("Done is better", body, HeroFit.Ems("Done is") * body.FontSize));

        // A word wider than the line breaks across as many lines as it needs.
        var word = new string('a', 20);
        Assert.Equal(3, QuoteLayout.Lines(word, body, (HeroFit.Ems(word) * body.FontSize / 3) + 0.5));
        Assert.Equal(int.MaxValue, QuoteLayout.Lines("Done.", body, 0));
    }

    // -----------------------------------------------------------------------------------------
    // [S-6405] the large mark
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S6405_the_mark_is_a_glyph_on_the_quotes_step()
    {
        Assert.Equal(FamilyType.Big40.FontSize, QuoteLayout.MarkSize(FamilyType.Big40));
        Assert.Equal(FamilyType.Big28.FontSize, QuoteLayout.MarkSize(FamilyType.Big28));
        Assert.Equal(16, QuoteLayout.MarkSize(FamilyType.Title));
        Assert.Equal(16, QuoteLayout.MarkSize(FamilyType.Body));
    }

    [Fact]
    public void S6405_the_mark_takes_its_row_off_the_top_before_the_quote_is_fitted()
    {
        var plain = QuoteLayout.Plan(M, Newton, author: true, marks: false);
        var marked = QuoteLayout.Plan(M, Newton, author: true, marks: true);
        var lines = QuoteLayout.Lines(Newton, marked.Step, M.Width - 32);

        Assert.Equal(0, plain.Mark);
        Assert.Equal(QuoteLayout.MarkSize(marked.Step), marked.Mark);
        Assert.True(marked.Step.FontSize <= plain.Step.FontSize);
        Assert.True((lines * marked.Step.LineHeight) + marked.Mark + 8 + FamilyType.Under(marked.Step).LineHeight <= M.Height - 16);
    }

    // -----------------------------------------------------------------------------------------
    // Punctuation
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A_quote_is_set_in_typographic_quotation_marks()
        => Assert.Equal("“Done is better than perfect.”", QuoteLayout.Quoted(Locale.En, "Done is better than perfect."));

    [Theory]
    [InlineData(Locale.De, "„Kurz.“")]
    [InlineData(Locale.Fr, "« Kurz. »")]
    [InlineData(Locale.Ja, "「Kurz.」")]
    public void The_marks_are_the_languages_own(Locale locale, string expected)
        => Assert.Equal(expected, QuoteLayout.Quoted(locale, " Kurz. "));

    [Fact]
    public void The_marks_are_added_round_the_words_and_not_round_the_padding()
        => Assert.Equal("“Brevity.”", QuoteLayout.Quoted(Locale.En, "   Brevity.   "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_is_not_quoted_as_an_empty_pair_of_marks(string? text)
        => Assert.Equal(string.Empty, QuoteLayout.Quoted(Locale.De, text));

    [Fact]
    public void An_attribution_is_em_dashed()
        => Assert.Equal("— Mark Twain", QuoteLayout.Attribution("  Mark Twain "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void An_anonymous_quote_gets_no_dash_with_nothing_after_it(string? author)
        => Assert.Equal(string.Empty, QuoteLayout.Attribution(author));
}
