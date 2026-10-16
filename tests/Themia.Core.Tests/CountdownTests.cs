using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The countdown wording, pinned at every threshold in [C-30] and on both sides of the target
/// instant. These boundaries are the whole behaviour of the widget, and every one of them is a
/// place an off-by-one reads as a bug on the desktop.
/// </summary>
public class CountdownFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static string Ahead(TimeSpan distance, CountdownFormat format = CountdownFormat.Auto)
        => CountdownFormatter.Read(Now + distance, Now, format, Locale.En).Text;

    private static string Behind(TimeSpan distance, CountdownFormat format = CountdownFormat.Auto)
        => CountdownFormatter.Read(Now - distance, Now, format, Locale.En).Text;

    // -----------------------------------------------------------------------------------------
    // [C-30] auto thresholds, approaching
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0, 1, "in a moment")]          // the smallest tick above zero
    [InlineData(0, 0, 59, "in a moment")]
    [InlineData(0, 1, 0, "1 min")]                // the minute boundary, singular
    [InlineData(0, 1, 59, "1 min")]
    [InlineData(0, 2, 0, "2 mins")]
    [InlineData(0, 59, 59, "59 mins")]
    [InlineData(1, 0, 0, "1 hr")]                 // the hour boundary, singular and round
    [InlineData(1, 0, 59, "1 hr")]
    [InlineData(1, 1, 0, "1h 1m")]
    [InlineData(2, 0, 0, "2 hrs")]
    [InlineData(23, 59, 59, "23h 59m")]
    public void C30_auto_words_the_hours_and_minutes_below_a_day(int hours, int minutes, int seconds, string expected)
        => Assert.Equal(expected, Ahead(new TimeSpan(0, hours, minutes, seconds)));

    [Theory]
    [InlineData(1, 0, "1 day")]                   // the day boundary, singular and round
    [InlineData(1, 0, "1 day", 59)]
    [InlineData(1, 1, "1d 1h")]
    [InlineData(2, 0, "2 days")]
    [InlineData(6, 23, "6d 23h")]                 // the last wording before the week threshold
    [InlineData(7, 0, "7 days")]                  // at a week, hours stop being shown
    [InlineData(7, 5, "7 days")]
    [InlineData(400, 13, "400 days")]
    public void C30_auto_words_the_days(int days, int hours, string expected, int minutes = 0)
        => Assert.Equal(expected, Ahead(new TimeSpan(days, hours, minutes, 0)));

    // -----------------------------------------------------------------------------------------
    // [C-11] the four formats
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(CountdownFormat.Days, "1 day")]
    [InlineData(CountdownFormat.DaysHoursMinutes, "1d 2h 3m")]
    [InlineData(CountdownFormat.DaysHoursMinutesSeconds, "1d 2h 3m 4s")]
    [InlineData(CountdownFormat.Auto, "1d 2h")]
    public void C11_each_format_words_the_same_distance_its_own_way(CountdownFormat format, string expected)
        => Assert.Equal(expected, Ahead(new TimeSpan(1, 2, 3, 4), format));

    [Fact]
    public void C11_days_only_truncates_rather_than_rounding()
    {
        // 23 hours away is still "today": rounding it up to a day would say the deadline has moved.
        Assert.Equal("0 days", Ahead(new TimeSpan(0, 23, 59, 59), CountdownFormat.Days));
        Assert.Equal("1 day", Ahead(new TimeSpan(1, 23, 59, 59), CountdownFormat.Days));
        Assert.Equal("2 days", Ahead(new TimeSpan(2, 0, 0, 0), CountdownFormat.Days));
    }

    [Fact]
    public void C11_the_fine_formats_keep_their_zero_components()
    {
        // "0d 0h 5m" rather than "5m": a fixed shape is what makes a column of these readable.
        Assert.Equal("0d 0h 5m", Ahead(TimeSpan.FromMinutes(5), CountdownFormat.DaysHoursMinutes));
        Assert.Equal("0d 0h 0m 5s", Ahead(TimeSpan.FromSeconds(5), CountdownFormat.DaysHoursMinutesSeconds));
    }

    // -----------------------------------------------------------------------------------------
    // [C-40]/[C-41] across the target instant
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void C40_the_target_instant_itself_counts_as_elapsed()
    {
        var atTarget = CountdownFormatter.Read(Now, Now, CountdownFormat.Auto, Locale.En);
        Assert.True(atTarget.Elapsed);
        Assert.Equal("just now", atTarget.Text);

        // One tick earlier it is still ahead, and says so.
        var justBefore = CountdownFormatter.Read(Now + TimeSpan.FromMilliseconds(1), Now, CountdownFormat.Auto, Locale.En);
        Assert.False(justBefore.Elapsed);
        Assert.Equal("in a moment", justBefore.Text);
    }

    [Theory]
    [InlineData(1, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1 min ago")]
    [InlineData(3600, "1 hr ago")]
    [InlineData(90000, "1d 1h ago")]
    public void C41_auto_says_just_now_under_a_minute_and_ago_beyond_it(int secondsPast, string expected)
    {
        var reading = CountdownFormatter.Read(Now - TimeSpan.FromSeconds(secondsPast), Now, CountdownFormat.Auto, Locale.En);
        Assert.True(reading.Elapsed);
        Assert.Equal(expected, reading.Text);
    }

    [Fact]
    public void C41_just_now_is_an_auto_rule_only()
    {
        // The explicit formats were asked for because they show the components; swapping them for a
        // phrase under a minute would be the widget overriding the user's choice.
        Assert.Equal("0d 0h 0m 30s ago", Behind(TimeSpan.FromSeconds(30), CountdownFormat.DaysHoursMinutesSeconds));
        Assert.Equal("0d 0h 0m ago", Behind(TimeSpan.FromSeconds(30), CountdownFormat.DaysHoursMinutes));
        Assert.Equal("0 days ago", Behind(TimeSpan.FromSeconds(30), CountdownFormat.Days));
    }

    [Fact]
    public void C40_the_wording_is_symmetric_around_the_target()
    {
        // The same distance reads the same either side, bar the direction word. A countdown that
        // words "3 days" one way and "72h 0m" the other reads as two different widgets.
        foreach (var distance in new[]
                 {
                     TimeSpan.FromMinutes(5),
                     TimeSpan.FromHours(3),
                     TimeSpan.FromHours(25),
                     TimeSpan.FromDays(9),
                 })
        {
            Assert.Equal($"{Ahead(distance)} ago", Behind(distance));
            Assert.False(CountdownFormatter.Read(Now + distance, Now, CountdownFormat.Auto, Locale.En).Elapsed);
            Assert.True(CountdownFormatter.Read(Now - distance, Now, CountdownFormat.Auto, Locale.En).Elapsed);
        }
    }

    // -----------------------------------------------------------------------------------------
    // [C-20] tick cadence
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void C20_only_a_seconds_format_buys_the_one_second_cadence()
    {
        var config = new CountdownConfig();
        Assert.Equal(TimeSpan.FromSeconds(30), config.TickInterval);

        config.Retarget(Now, Now);
        config.Format = CountdownFormat.DaysHoursMinutes;
        Assert.Equal(TimeSpan.FromSeconds(30), config.TickInterval);

        config.Format = CountdownFormat.DaysHoursMinutesSeconds;
        Assert.Equal(TimeSpan.FromSeconds(1), config.TickInterval);
    }

    // -----------------------------------------------------------------------------------------
    // [S-6036] one target per widget
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S6036_a_new_countdown_has_no_target_and_asks_to_be_set_up()
    {
        var config = (CountdownConfig)WidgetConfig.CreateDefault(WidgetKind.Countdown);

        Assert.Null(config.Target);
        Assert.Equal(string.Empty, config.Label);
        Assert.Equal(CountdownFormat.Auto, config.Format);
        Assert.Equal(CountdownStyle.Text, config.Style);
        Assert.True(WidgetSetup.Needed(config, AccountFeatures.None));
    }

    [Fact]
    public void S6036_setting_the_target_starts_its_progress_from_now()
    {
        var config = new CountdownConfig();
        config.Retarget(new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.FromHours(1)), Now);

        Assert.Equal(new DateTimeOffset(2026, 12, 24, 17, 0, 0, TimeSpan.Zero), config.Target);
        Assert.Equal(TimeSpan.Zero, config.Target!.Value.Offset);
        Assert.Equal(Now, config.Created);
        Assert.Equal(0, CountdownLayout.Progress(config, Now));
        Assert.False(WidgetSetup.Needed(config, AccountFeatures.None));
    }

    [Fact]
    public void S6036_the_target_round_trips_through_the_file_and_a_clone()
    {
        var config = (CountdownConfig)ConfigJson.Read("countdown", """
            { "kind": "countdown", "style": "flip", "label": "Launch", "target": "2026-12-24T17:00:00+00:00",
              "format": 3, "created": "2026-10-01T08:00:00+00:00" }
            """);

        Assert.Equal("Launch", config.Label);
        Assert.Equal(new DateTimeOffset(2026, 12, 24, 17, 0, 0, TimeSpan.Zero), config.Target);
        Assert.Equal(CountdownFormat.DaysHoursMinutesSeconds, config.Format);
        Assert.Equal(CountdownStyle.Flip, config.Style);

        var copy = (CountdownConfig)config.Clone();
        Assert.Equal((config.Label, config.Target, config.Format, config.Created, config.Style), (copy.Label, copy.Target, copy.Format, copy.Created, copy.Style));

        // Not set yet: reading it back is still not set.
        Assert.Null(((CountdownConfig)ConfigJson.Read("countdown", """{ "kind": "countdown" }""")).Target);
    }

    [Fact]
    public void S6036_a_hand_edited_file_is_repaired_loudly()
    {
        var config = new CountdownConfig { Format = (CountdownFormat)9, Label = "  Trip\nto Rome  " + new string('x', 80) };
        var repairs = config.Repair();

        Assert.Equal(2, repairs.Count);
        Assert.Equal(CountdownFormat.Auto, config.Format);
        Assert.StartsWith("Trip to Rome", config.Label, StringComparison.Ordinal);
        Assert.True(config.Label.Length <= CountdownConfig.MostLabel);
        Assert.Empty(config.Repair());
        Assert.Empty(new CountdownConfig().Repair());
    }

    // -----------------------------------------------------------------------------------------
    // [C-03]/[C-05] the absolute target, shown in local time
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void C03_the_subtitle_is_the_target_in_local_time()
    {
        var target = new DateTimeOffset(2026, 6, 15, 22, 30, 0, TimeSpan.Zero);
        var east = TimeZoneInfo.CreateCustomTimeZone("Test/East", TimeSpan.FromHours(3), "East", "East");

        Assert.Equal("Jun 15, 10:30 PM", CountdownFormatter.TargetLabel(target, Now, TimeZoneInfo.Utc, Locale.En));

        // Three hours east the same instant is the small hours of the next day, and the row has to
        // say so — otherwise the subtitle contradicts the duration beside it.
        Assert.Equal("Jun 16, 1:30 AM", CountdownFormatter.TargetLabel(target, Now, east, Locale.En));
    }

    [Fact]
    public void C03_the_year_appears_only_when_it_is_not_this_one()
    {
        Assert.Equal("Jun 15, 12:00 PM", CountdownFormatter.TargetLabel(Now, Now, TimeZoneInfo.Utc, Locale.En));

        var nextYear = new DateTimeOffset(2027, 1, 2, 9, 5, 0, TimeSpan.Zero);
        Assert.Equal("Jan 2, 2027, 9:05 AM", CountdownFormatter.TargetLabel(nextYear, Now, TimeZoneInfo.Utc, Locale.En));
    }

    [Fact]
    public void C05_the_tooltip_spells_the_whole_instant_out()
        => Assert.Equal("Monday, June 15, 2026 at 12:00 PM", CountdownFormatter.FullTargetLabel(Now, TimeZoneInfo.Utc, Locale.En));

    // -----------------------------------------------------------------------------------------
    // [C-53]/[C-55] editing the target
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void C53_a_target_round_trips_through_the_pickers()
    {
        var east = TimeZoneInfo.CreateCustomTimeZone("Test/East", TimeSpan.FromHours(3), "East", "East");
        var target = new DateTimeOffset(2026, 6, 15, 22, 30, 0, TimeSpan.Zero);

        var date = CountdownFormatter.LocalDate(target, east);
        var time = CountdownFormatter.LocalTimeOfDay(target, east);

        Assert.Equal(new TimeSpan(1, 30, 0), time);
        Assert.Equal(target, CountdownFormatter.CombineLocal(date, time, east));
    }

    [Fact]
    public void C55_a_cleared_date_leaves_the_target_alone_instead_of_retargeting_to_now()
    {
        // Regression for the original's bug: clearing the field wrote `new Date()` back, silently
        // moving the timer to the current instant and refilling the field mid-edit.
        Assert.Null(CountdownFormatter.CombineLocal(null, TimeSpan.FromHours(9), TimeZoneInfo.Utc));
        Assert.Null(CountdownFormatter.CombineLocal(Now, null, TimeZoneInfo.Utc));
        Assert.Null(CountdownFormatter.CombineLocal(null, null, TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    [InlineData(48)]
    public void C55_an_unusable_time_of_day_is_null_not_a_guess(int hours)
        => Assert.Null(CountdownFormatter.CombineLocal(Now, TimeSpan.FromHours(hours), TimeZoneInfo.Utc));

    [Fact]
    public void C55_a_local_time_that_does_not_exist_is_null_not_a_guess()
    {
        // The hour a spring-forward skips. Writing "02:30" back as 01:30 or 03:30 would be the
        // widget picking a target the user did not.
        var zone = SpringForwardZone();
        var day = new DateTimeOffset(new DateTime(2026, 3, 29), TimeSpan.FromHours(1));

        Assert.Null(CountdownFormatter.CombineLocal(day, new TimeSpan(2, 30, 0), zone));

        // The hour either side of the gap is ordinary and must still work.
        Assert.NotNull(CountdownFormatter.CombineLocal(day, new TimeSpan(1, 30, 0), zone));
        Assert.NotNull(CountdownFormatter.CombineLocal(day, new TimeSpan(3, 30, 0), zone));
    }

    /// <summary>
    /// A zone built here rather than looked up by id: the registry's zone table is not this test's
    /// subject, and a lookup would make the test depend on the machine it runs on.
    /// </summary>
    private static TimeZoneInfo SpringForwardZone()
    {
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 29),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 25));

        return TimeZoneInfo.CreateCustomTimeZone(
            "Test/SpringForward",
            TimeSpan.FromHours(1),
            "Spring Forward",
            "Spring Forward Standard",
            "Spring Forward Daylight",
            [rule]);
    }
}
