using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Quote.Tests;

/// <summary>
/// How a quote is set. The two things a pull quote has to get right at both ends of the size range:
/// which ramp step it takes, and its punctuation.
/// </summary>
public class QuoteLayoutTests
{
    /// <summary>A quote of about the length the bundled set runs to.</summary>
    private const int TypicalQuote = 60;

    // -----------------------------------------------------------------------------------------
    // [S-210] the step, never an invented number
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Every_answer_is_a_step_on_the_ramp()
    {
        foreach (var width in new[] { 40.0, 180, 320, 800, 2000 })
        {
            foreach (var height in new[] { 30.0, 120, 300, 600, 1400 })
            {
                var step = QuoteLayout.Fit(width, height, TypicalQuote, FluentType.Caption, FluentType.Title);

                Assert.Contains(step, FluentType.Ramp);
            }
        }
    }

    [Fact]
    public void A_bigger_tile_never_takes_a_smaller_step()
    {
        var small = QuoteLayout.Fit(180, 120, TypicalQuote, FluentType.Caption, FluentType.Title);
        var medium = QuoteLayout.Fit(400, 300, TypicalQuote, FluentType.Caption, FluentType.Title);
        var large = QuoteLayout.Fit(800, 600, TypicalQuote, FluentType.Caption, FluentType.Title);

        Assert.True(small.FontSize <= medium.FontSize);
        Assert.True(medium.FontSize <= large.FontSize);
        Assert.True(small.FontSize < large.FontSize);
    }

    [Fact]
    public void A_long_quote_takes_a_smaller_step_than_a_short_one_in_the_same_box()
    {
        var shortQuote = QuoteLayout.Fit(400, 300, 20, FluentType.Caption, FluentType.Title);
        var longQuote = QuoteLayout.Fit(400, 300, 400, FluentType.Caption, FluentType.Title);

        Assert.True(longQuote.FontSize < shortQuote.FontSize);
    }

    [Fact]
    public void The_floor_and_the_ceiling_are_respected()
    {
        // A quote far too long for the box still gets the floor rather than something illegible.
        var crowded = QuoteLayout.Fit(180, 120, 5000, FluentType.Body, FluentType.Title);
        Assert.Equal(FluentType.Body, crowded);

        // And a very short quote in a very large box stops at the ceiling.
        var roomy = QuoteLayout.Fit(2000, 1400, 3, FluentType.Caption, FluentType.Subtitle);
        Assert.Equal(FluentType.Subtitle, roomy);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-5, 100)]
    [InlineData(double.NaN, 100)]
    [InlineData(100, double.PositiveInfinity)]
    public void An_unmeasured_box_gets_the_floor_rather_than_a_NaN_on_the_render_path(double width, double height)
        => Assert.Equal(FluentType.Body, QuoteLayout.Fit(width, height, TypicalQuote, FluentType.Body, FluentType.Title));

    [Fact]
    public void A_quote_with_no_characters_still_gets_a_step()
        => Assert.Contains(
            QuoteLayout.Fit(400, 300, 0, FluentType.Caption, FluentType.Title),
            FluentType.Ramp);

    // -----------------------------------------------------------------------------------------
    // Capacity
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A_box_too_short_for_one_line_holds_nothing()
        => Assert.Equal(0, QuoteLayout.Capacity(FluentType.Title, 400, FluentType.Title.LineHeight - 1));

    [Fact]
    public void A_box_too_narrow_for_one_character_holds_nothing()
        => Assert.Equal(0, QuoteLayout.Capacity(FluentType.Title, 4, 400));

    [Fact]
    public void Capacity_counts_whole_lines_of_whole_characters()
    {
        // Two lines of a step whose characters are about 8px wide across 80px.
        var capacity = QuoteLayout.Capacity(FluentType.Body, 80, FluentType.Body.LineHeight * 2);

        Assert.True(capacity >= 18, $"{capacity} is fewer characters than two lines of 80px holds");
        Assert.True(capacity <= 24, $"{capacity} is more characters than two lines of 80px holds");
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
