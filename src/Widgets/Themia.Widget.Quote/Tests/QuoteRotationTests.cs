using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Quote.Tests;

/// <summary>
/// Which quote shows, and when it turns over. Written as a pure rule precisely so that it can be
/// pinned here rather than watched on a desktop for a day.
/// </summary>
public class QuoteRotationTests
{
    private static readonly DateTimeOffset Noon = new(2026, 6, 15, 12, 0, 0, TimeSpan.FromHours(2));

    // -----------------------------------------------------------------------------------------
    // Selection
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void An_empty_list_selects_nothing_rather_than_index_zero()
    {
        Assert.Equal(-1, QuoteRotation.Select(0, 0, 1, shuffle: false));
        Assert.Equal(-1, QuoteRotation.Select(0, 12345, 7, shuffle: true));
        Assert.Equal(-1, QuoteRotation.Select(-3, 0, 1, shuffle: false));
    }

    [Fact]
    public void A_single_quote_never_moves_however_it_is_rotated_or_clicked()
    {
        for (var period = -50L; period <= 50; period++)
        {
            Assert.Equal(0, QuoteRotation.Select(1, period, 1, shuffle: false));
            Assert.Equal(0, QuoteRotation.Select(1, period, 99, shuffle: true));
        }
    }

    [Fact]
    public void In_order_rotation_walks_the_list_and_wraps()
    {
        Assert.Equal(0, QuoteRotation.Select(3, 0, 1, shuffle: false));
        Assert.Equal(1, QuoteRotation.Select(3, 1, 1, shuffle: false));
        Assert.Equal(2, QuoteRotation.Select(3, 2, 1, shuffle: false));
        Assert.Equal(0, QuoteRotation.Select(3, 3, 1, shuffle: false));
    }

    [Fact]
    public void A_period_before_the_epoch_indexes_forwards_not_off_the_front_of_the_list()
    {
        for (var period = -1000L; period < 0; period++)
        {
            var index = QuoteRotation.Select(7, period, 1, shuffle: false);
            Assert.InRange(index, 0, 6);
        }
    }

    [Fact]
    public void The_same_inputs_always_choose_the_same_quote()
    {
        // The whole point of the rule: no clock read inside, no RNG kept, so the quote is
        // reproducible across restarts and across machines.
        var first = QuoteRotation.Select(16, 20_000, 4, shuffle: true);
        var second = QuoteRotation.Select(16, 20_000, 4, shuffle: true);

        Assert.Equal(first, second);
    }

    [Fact]
    public void A_shuffled_index_is_always_inside_the_list()
    {
        for (var period = -500L; period <= 500; period++)
        {
            Assert.InRange(QuoteRotation.Select(16, period, 1, shuffle: true), 0, 15);
            Assert.InRange(QuoteRotation.Select(2, period, int.MinValue, shuffle: true), 0, 1);
        }
    }

    [Fact]
    public void Shuffling_spreads_consecutive_periods_instead_of_walking_the_list()
    {
        var seen = new HashSet<int>();
        var sequential = 0;
        var previous = QuoteRotation.Select(16, 0, 1, shuffle: true);

        for (var period = 1L; period < 200; period++)
        {
            var index = QuoteRotation.Select(16, period, 1, shuffle: true);
            seen.Add(index);

            if (index == (previous + 1) % 16)
            {
                sequential++;
            }

            previous = index;
        }

        // Every quote gets a turn, and consecutive days are not simply the next one down.
        Assert.Equal(16, seen.Count);
        Assert.True(sequential < 40, $"{sequential} of 199 turns were the next quote in order");
    }

    [Fact]
    public void A_different_seed_is_a_different_rotation()
    {
        var withOne = new List<int>();
        var withTwo = new List<int>();

        for (var period = 0L; period < 40; period++)
        {
            withOne.Add(QuoteRotation.Select(16, period, 1, shuffle: true));
            withTwo.Add(QuoteRotation.Select(16, period, 2, shuffle: true));
        }

        Assert.NotEqual(withOne, withTwo);
    }

    // -----------------------------------------------------------------------------------------
    // Periods
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A_manual_rotation_has_no_period_of_its_own()
        => Assert.Equal(0, QuoteRotation.Period(Noon, QuoteCadence.Manual));

    [Fact]
    public void A_daily_period_holds_all_day_and_steps_at_local_midnight()
    {
        var today = QuoteRotation.Period(Noon, QuoteCadence.Daily);

        Assert.Equal(today, QuoteRotation.Period(Noon.AddHours(-11), QuoteCadence.Daily));
        Assert.Equal(today, QuoteRotation.Period(Noon.AddHours(11), QuoteCadence.Daily));
        Assert.Equal(today + 1, QuoteRotation.Period(Noon.AddHours(13), QuoteCadence.Daily));
    }

    [Fact]
    public void A_day_is_the_users_own_day_not_Greenwichs()
    {
        // The same instant, written in two offsets. Half past midnight in Berlin is still the
        // previous day in London, and each widget should show its own day's quote.
        var instant = new DateTimeOffset(2026, 6, 15, 0, 30, 0, TimeSpan.FromHours(2));

        Assert.NotEqual(
            QuoteRotation.Period(instant, QuoteCadence.Daily),
            QuoteRotation.Period(instant.ToOffset(TimeSpan.Zero), QuoteCadence.Daily));
    }

    [Fact]
    public void An_hourly_period_holds_for_the_hour_and_steps_on_it()
    {
        var thisHour = QuoteRotation.Period(Noon, QuoteCadence.Hourly);

        Assert.Equal(thisHour, QuoteRotation.Period(Noon.AddMinutes(59), QuoteCadence.Hourly));
        Assert.Equal(thisHour + 1, QuoteRotation.Period(Noon.AddMinutes(60), QuoteCadence.Hourly));
        Assert.Equal(thisHour - 1, QuoteRotation.Period(Noon.AddMinutes(-1), QuoteCadence.Hourly));
    }

    [Fact]
    public void Periods_before_the_epoch_still_step_by_one()
    {
        var early = new DateTimeOffset(1969, 6, 15, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(
            QuoteRotation.Period(early, QuoteCadence.Daily) + 1,
            QuoteRotation.Period(early.AddDays(1), QuoteCadence.Daily));
    }

    [Fact]
    public void Only_a_rotating_quote_asks_the_app_to_wake_up()
    {
        Assert.Equal(TimeSpan.Zero, QuoteRotation.TickInterval(QuoteCadence.Manual));
        Assert.True(QuoteRotation.TickInterval(QuoteCadence.Hourly) > TimeSpan.Zero);
        Assert.True(QuoteRotation.TickInterval(QuoteCadence.Daily) > TimeSpan.Zero);
    }

    // -----------------------------------------------------------------------------------------
    // The bundled set and the user's own list
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Every_bundled_quote_has_something_to_say_and_someone_who_said_it()
    {
        Assert.NotEmpty(QuoteRotation.Bundled);

        foreach (var quote in QuoteRotation.Bundled)
        {
            Assert.False(string.IsNullOrWhiteSpace(quote.Text));
            Assert.False(string.IsNullOrWhiteSpace(quote.Author));

            // The view draws the quotation marks, so a quote must not bring its own.
            Assert.DoesNotContain('"', quote.Text);
        }
    }

    [Fact]
    public void A_new_widget_starts_with_the_bundled_set()
        => Assert.Equal(QuoteRotation.Bundled.Count, new QuoteConfig().EffectiveQuotes.Count);

    [Fact]
    public void Blank_rows_in_the_editor_are_not_quotes()
    {
        var config = new QuoteConfig
        {
            Quotes =
            [
                new QuoteItem { Text = "  ", Author = "Nobody" },
                new QuoteItem { Text = "  Something  ", Author = "  Someone  " },
                new QuoteItem(),
            ],
        };

        var quote = Assert.Single(config.EffectiveQuotes);
        Assert.Equal("Something", quote.Text);
        Assert.Equal("Someone", quote.Author);
    }

    [Fact]
    public void A_list_the_user_emptied_stays_empty()
    {
        var config = new QuoteConfig { Quotes = [] };

        Assert.Empty(config.EffectiveQuotes);
        Assert.Equal(-1, QuoteRotation.Select(config.EffectiveQuotes.Count, 0, config.Seed, config.Shuffle));
    }
}
