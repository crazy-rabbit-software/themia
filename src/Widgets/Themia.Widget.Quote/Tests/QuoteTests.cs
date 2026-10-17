using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Quote.Tests;

/// <summary>
/// [S-1495] [S-1990] [S-6404] [S-6405] The quote: the original's bundled rotation, its keys, its
/// styles, and its list through the real state file. Moved with the widget from Core's
/// DeskParityTests, WidgetKeyMapTests, ListStylesTests and DeskSketchConfigTests, unchanged in what
/// they assert.
/// </summary>
public sealed class QuoteTests
{
    private static readonly WidgetId Id = new("quote");

    [Fact]
    public void S1495_a_daily_unshuffled_rotation_shows_the_quote_the_original_showed_that_day()
    {
        // The original: QUOTES[localDayNumber % 16], localDayNumber = local days since 1970-01-01.
        var day = new DateTimeOffset(2026, 10, 4, 15, 0, 0, TimeSpan.FromHours(2));
        var localDayNumber = (long)(new DateTime(2026, 10, 4) - DateTime.UnixEpoch).TotalDays;

        var period = QuoteRotation.Period(day, QuoteCadence.Daily);
        var index = QuoteRotation.Select(QuoteRotation.Bundled.Count, period, seed: 0, shuffle: false);

        Assert.Equal(localDayNumber, period);
        Assert.Equal((int)(localDayNumber % 16), index);
        Assert.Equal(16, QuoteRotation.Bundled.Count);
        Assert.Equal("Steve Jobs", QuoteRotation.Bundled[5].Author);
        Assert.Equal("Sheryl Sandberg", QuoteRotation.Bundled[15].Author);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S1495_the_shuffle_button_always_lands_on_another_quote(bool shuffle)
    {
        for (var roll = 0; roll < 200; roll++)
        {
            const long period = 20_000;
            var step = QuoteRotation.RandomStep(16, period, seed: 7, shuffle, roll);

            Assert.NotEqual(
                QuoteRotation.Select(16, period, 7, shuffle),
                QuoteRotation.Select(16, period + step, 7, shuffle));
        }
    }

    [Fact]
    public void S1495_one_quote_has_nowhere_to_shuffle_to()
        => Assert.Equal(0, QuoteRotation.RandomStep(1, 5, 0, shuffle: false, roll: 3));

    [Fact]
    public void S6404_plain_quotes_in_the_language_marks_and_large_marks_leave_the_text_bare()
    {
        Assert.Equal("“Less is more.”", QuoteLayout.Set(Locale.En, " Less is more. ", QuoteStyle.Plain));
        Assert.Equal("„Weniger ist mehr.“", QuoteLayout.Set(Locale.De, "Weniger ist mehr.", QuoteStyle.Plain));
        Assert.Equal("Less is more.", QuoteLayout.Set(Locale.En, " Less is more. ", QuoteStyle.Marks));
        Assert.Equal(string.Empty, QuoteLayout.Set(Locale.En, "   ", QuoteStyle.Marks));
    }

    [Theory]
    [InlineData(120, 76, 28)]
    [InlineData(320, 196, 28)]
    [InlineData(560, 376, 40)]
    [InlineData(800, 516, 68)]
    public void S6405_the_large_mark_is_a_ramp_step_chosen_by_the_smaller_side(double width, double height, double size)
        => Assert.Equal(size, QuoteLayout.MarkSize(new WidgetArea(width, height)));

    [Theory]
    [InlineData(WidgetKey.Right, 3, ControlKey.Next)]
    [InlineData(WidgetKey.Space, 3, ControlKey.Next)]
    [InlineData(WidgetKey.Enter, 3, ControlKey.Next)]
    [InlineData(WidgetKey.Left, 3, ControlKey.Previous)]
    [InlineData(WidgetKey.Up, 3, ControlKey.None)]
    [InlineData(WidgetKey.Right, 1, ControlKey.None)]
    [InlineData(WidgetKey.Left, 0, ControlKey.None)]
    public void S1990_quote_steps_only_with_somewhere_to_go(WidgetKey key, int quotes, ControlKey expected)
        => Assert.Equal(expected, QuoteKeys.Of(key, quotes));

    [Fact]
    public async Task A_quote_list_survives_the_state_file_without_doubling()
    {
        var reloaded = await StateRoundTrip.Of(Id, new QuoteConfig());

        Assert.Equal(QuoteRotation.Bundled.Count, reloaded.Quotes.Count);
    }

    [Fact]
    public async Task The_users_own_quotes_and_rotation_come_back_as_they_were()
    {
        var config = new QuoteConfig
        {
            Cadence = QuoteCadence.Hourly,
            Shuffle = true,
            Seed = 42,
            ShowAuthor = false,
            Quotes = [new QuoteItem { Text = "Measure twice.", Author = "Anyone" }],
        };

        var reloaded = await StateRoundTrip.Of(Id, config);

        Assert.Equal(QuoteCadence.Hourly, reloaded.Cadence);
        Assert.True(reloaded.Shuffle);
        Assert.Equal(42, reloaded.Seed);
        Assert.False(reloaded.ShowAuthor);
        var quote = Assert.Single(reloaded.EffectiveQuotes);
        Assert.Equal("Measure twice.", quote.Text);
    }

    [Fact]
    public void Duplicating_a_quote_widget_copies_its_list()
    {
        var original = new QuoteConfig { Quotes = [new QuoteItem { Text = "One", Author = "A" }] };

        var copy = Assert.IsType<QuoteConfig>(original.Clone());
        copy.Quotes.Add(new QuoteItem { Text = "Two", Author = "B" });

        Assert.Single(original.Quotes);
    }
}
