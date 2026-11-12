using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Quote.Tests;

/// <summary>
/// How a quote is set: its size, its lines and its card ([S-11306]), its mark and its punctuation.
/// </summary>
public class QuoteLayoutTests
{
    private const string Newton = "“What we know is a drop, what we don’t know is an ocean.”";

    // -----------------------------------------------------------------------------------------
    // [S-11306] the size, the lines, the card
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S11306_the_size_names_the_step_body_title_28()
    {
        Assert.Equal(FamilyType.Body, QuoteLayout.StepAt(ReadoutSize.Small));
        Assert.Equal(FamilyType.Title, QuoteLayout.StepAt(ReadoutSize.Medium));
        Assert.Equal(FamilyType.Big28, QuoteLayout.StepAt(ReadoutSize.Large));
        Assert.Equal(ReadoutSize.Medium, new QuoteConfig().Size);
        Assert.Equal(3, new QuoteConfig().Lines);
    }

    [Fact]
    public void S11306_the_height_follows_the_lines_and_the_size_the_attribution_line_always_200_to_520_wide()
    {
        var sizing = QuoteLayout.Sizing(new QuoteConfig());

        // The inset, three Title lines, the attribution's Caption line under, the inset.
        Assert.Equal(12 + (3 * 28) + 16 + 12, sizing.Natural.Height);
        Assert.Equal((200d, 320d, 520d), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
        Assert.Equal(3, sizing.HeightSteps!.Count);
        Assert.Equal(12 + (6 * 36) + 16 + 12, QuoteLayout.Sizing(new QuoteConfig { Size = ReadoutSize.Large, Lines = 6 }).Natural.Height);
        Assert.Equal(sizing.Natural.Height - 16, QuoteLayout.Sizing(new QuoteConfig { ShowAuthor = false }).Natural.Height);
    }

    [Fact]
    public void S11306_dragging_its_bottom_edge_sets_its_lines_and_the_given_config_stays()
    {
        var config = new QuoteConfig { Lines = 3 };
        var sizing = QuoteLayout.Sizing(config);

        var five = Assert.IsType<QuoteConfig>(QuoteLayout.SizeSets(config, sizing.Natural with { Height = sizing.Natural.Height + (2 * 28) }));
        Assert.Equal(5, five.Lines);
        Assert.Equal(3, config.Lines);
        Assert.Same(config, QuoteLayout.SizeSets(config, sizing.Natural));
    }

    [Fact]
    public void S11306_lines_and_size_out_of_range_are_repaired_and_copied()
    {
        var config = new QuoteConfig { Lines = 40, Size = (ReadoutSize)7 };
        Assert.NotEmpty(config.Repair());
        Assert.Equal((6, ReadoutSize.Medium), (config.Lines, config.Size));

        var copy = (QuoteConfig)new QuoteConfig { Lines = 2, Size = ReadoutSize.Small }.Clone();
        Assert.Equal((2, ReadoutSize.Small), (copy.Lines, copy.Size));
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
    public void S11306_the_marks_style_puts_its_mark_and_8_over_the_quote()
    {
        var plain = QuoteLayout.Sizing(new QuoteConfig { Size = ReadoutSize.Large });
        var marked = QuoteLayout.Sizing(new QuoteConfig { Size = ReadoutSize.Large, Style = QuoteStyle.Marks });

        Assert.Equal(0, QuoteLayout.MarkOf(new QuoteConfig { Size = ReadoutSize.Large }));
        Assert.Equal(28, QuoteLayout.MarkOf(new QuoteConfig { Size = ReadoutSize.Large, Style = QuoteStyle.Marks }));
        Assert.Equal(plain.Natural.Height + 28 + 8, marked.Natural.Height);
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
