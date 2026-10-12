using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-4620]..[S-4625] The alarm as numbers (after Karpo's AlarmTests): when it rings next — its days,
/// a time already past, summer time's gap and winter time's repeated hour — and the book that rings
/// many of them: once, late only by a little after sleep or a clock set forward, never again after
/// the clock goes back, snoozed and dismissed, and one that does not repeat turning itself off.
/// </summary>
public sealed class AlarmTests
{
    /// <summary>Central Europe: summer time from 02:00 on 29 March 2026 and back at 03:00 on 25 October 2026.</summary>
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static readonly TimeSpan Snooze = TimeSpan.FromMinutes(10);

    private static DateTime At(int month, int day, int hour, int minute = 0, int second = 0) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, month, day, hour, minute, second, DateTimeKind.Unspecified), Zone);

    private static DateTime Wall(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);

    private static AlarmEntry Entry(string time, bool on = true, params string[] days) =>
        new() { Id = "a", Time = time, On = on, Days = days.Length > 0 ? days : null };

    private static AlarmPlan Plan(string time, params string[] days) => Entry(time, true, days).Plan;

    private static readonly string[] Every = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];

    private static (AlarmBook Book, List<string> Rang, List<string> Switched, List<string> Silenced) Book(AlarmEntry entry, DateTime armed)
    {
        var book = new AlarmBook(() => Zone);
        var rang = new List<string>();
        var switched = new List<string>();
        var silenced = new List<string>();
        book.Rang += rang.Add;
        book.Switched += switched.Add;
        book.Silenced += silenced.Add;
        book.Sync([new AlarmSlot("w/a", entry, Snooze)], armed);
        return (book, rang, switched, silenced);
    }

    // ---- when it rings next -----------------------------------------------------------------

    [Fact]
    public void S4622_it_rings_at_the_next_coming_of_its_time()
    {
        var plan = Plan("07:00");
        Assert.Equal(At(10, 12, 7), AlarmSchedule.Next(plan, At(10, 12, 6), Zone));
        Assert.Equal(At(10, 13, 7), AlarmSchedule.Next(plan, At(10, 12, 7), Zone));
        Assert.Equal(At(10, 13, 7), AlarmSchedule.Next(plan, At(10, 12, 23, 59), Zone));
        Assert.Equal(At(10, 13, 7), AlarmSchedule.Next(plan, At(10, 13, 0, 1), Zone));
    }

    [Fact]
    public void S4622_one_that_repeats_rings_on_its_days_alone()
    {
        // 12 October 2026 is a Monday.
        var plan = Plan("07:00", "mon", "wed");
        Assert.Equal(At(10, 14, 7), AlarmSchedule.Next(plan, At(10, 12, 8), Zone));
        Assert.Equal(At(10, 19, 7), AlarmSchedule.Next(plan, At(10, 14, 7), Zone));
        Assert.Equal(At(10, 18, 9), AlarmSchedule.Next(Plan("09:00", "sun"), At(10, 12, 7), Zone));
    }

    [Fact]
    public void S4622_a_time_summer_time_skips_rings_after_the_gap()
    {
        var plan = Plan("02:30");
        var skipped = AlarmSchedule.Next(plan, At(3, 28, 12), Zone);
        Assert.Equal(new DateTime(2026, 3, 29, 3, 0, 0), Wall(skipped));
        Assert.Equal(new DateTime(2026, 3, 30, 2, 30, 0), Wall(AlarmSchedule.Next(plan, skipped, Zone)));
    }

    [Fact]
    public void S4622_a_time_winter_time_says_twice_rings_once()
    {
        var plan = Plan("02:30", Every);
        var first = AlarmSchedule.Next(plan, At(10, 24, 12), Zone);
        Assert.Equal(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc), first);

        var (book, rang, _, _) = Book(Entry("02:30", true, "sun"), At(10, 24, 12));
        book.Tick(first);
        book.Dismiss("w/a");
        book.Tick(new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Utc));
        book.Tick(new DateTime(2026, 10, 25, 1, 31, 0, DateTimeKind.Utc));
        Assert.Equal(["w/a"], rang);
    }

    // ---- the book ---------------------------------------------------------------------------

    [Fact]
    public void S4623_it_rings_once_and_a_one_shot_turns_itself_off()
    {
        var entry = Entry("07:00");
        var (book, rang, switched, silenced) = Book(entry, At(10, 12, 6));
        Assert.Equal(At(10, 12, 7), book.Due(At(10, 12, 6)));
        Assert.False(book.Tick(At(10, 12, 6, 59, 59)));

        Assert.True(book.Tick(At(10, 12, 7)));
        book.Tick(At(10, 12, 7, 2));
        Assert.Equal(["w/a"], rang);
        Assert.True(book.AnyRinging);
        Assert.Equal(AlarmPhase.Ringing, book.Of("w/a", At(10, 12, 7, 2)).Phase);
        Assert.Equal(At(10, 12, 7) + AlarmBook.RingFor, book.Due(At(10, 12, 7, 2)));

        book.Tick(At(10, 12, 7) + AlarmBook.RingFor);
        Assert.False(book.AnyRinging);
        Assert.False(entry.On);
        Assert.Equal(["w/a"], switched);
        Assert.Equal(["w/a"], silenced);
        Assert.Null(book.Due(At(10, 12, 8)));
    }

    [Fact]
    public void S4623_a_repeating_alarm_stays_on()
    {
        var entry = Entry("07:00", true, "mon", "tue");
        var (book, rang, switched, _) = Book(entry, At(10, 11, 20));
        book.Tick(At(10, 12, 7));
        book.Dismiss("w/a");
        Assert.True(entry.On);
        Assert.Empty(switched);
        Assert.Equal(At(10, 13, 7), book.Of("w/a", At(10, 12, 7, 1)).Next);
        book.Tick(At(10, 13, 7, 0, 30));
        Assert.Equal(["w/a", "w/a"], rang);
    }

    [Fact]
    public void S4623_late_by_sleep_it_rings_only_if_just_late()
    {
        var late = Book(Entry("07:00"), At(10, 11, 23));
        late.Book.Tick(At(10, 12, 7, 3));
        Assert.Equal(["w/a"], late.Rang);

        var entry = Entry("07:00");
        var missed = Book(entry, At(10, 11, 23));
        Assert.True(missed.Book.Tick(At(10, 12, 7, 10)));
        Assert.Empty(missed.Rang);
        Assert.False(entry.On);

        var daily = Entry("07:00", true, Every);
        var away = Book(daily, At(10, 11, 23));
        away.Book.Tick(At(10, 15, 12));
        Assert.Empty(away.Rang);
        Assert.Equal(At(10, 16, 7), away.Book.Of("w/a", At(10, 15, 12)).Next);
    }

    [Fact]
    public void S4623_the_clock_going_back_never_rings_it_again()
    {
        var (book, rang, _, _) = Book(Entry("07:00", true, Every), At(10, 12, 6));
        book.Tick(At(10, 12, 7));
        book.Dismiss("w/a");
        book.Tick(At(10, 12, 6, 1));
        book.Tick(At(10, 12, 7));
        book.Tick(At(10, 12, 7, 1));
        Assert.Equal(["w/a"], rang);
        book.Tick(At(10, 13, 7));
        Assert.Equal(["w/a", "w/a"], rang);
    }

    [Fact]
    public void S4623_set_at_a_time_already_past_it_is_tomorrows()
    {
        var (book, rang, _, _) = Book(Entry("07:00"), At(10, 12, 7, 1));
        book.Tick(At(10, 12, 7, 2));
        Assert.Empty(rang);
        Assert.Equal(At(10, 13, 7), book.Due(At(10, 12, 7, 2)));
    }

    [Fact]
    public void S4623_a_clock_set_back_before_it_was_set_still_lets_its_time_come()
    {
        var (book, rang, _, _) = Book(Entry("07:00"), At(10, 12, 6, 30));
        book.Tick(At(10, 12, 5));
        book.Tick(At(10, 12, 7));
        Assert.Equal(["w/a"], rang);
    }

    [Fact]
    public void S4625_snooze_rings_again_after_the_widgets_snooze_and_dismiss_ends_it()
    {
        var entry = Entry("07:00");
        var (book, rang, switched, _) = Book(entry, At(10, 12, 6));
        book.Tick(At(10, 12, 7));
        Assert.True(book.Snooze("w/a", At(10, 12, 7, 1)));
        Assert.False(book.AnyRinging);

        var status = book.Of("w/a", At(10, 12, 7, 2));
        Assert.Equal(AlarmPhase.Snoozed, status.Phase);
        Assert.Equal(At(10, 12, 7, 11), status.Until);
        Assert.Equal(At(10, 12, 7, 11), book.Due(At(10, 12, 7, 2)));

        book.Tick(At(10, 12, 7, 11));
        Assert.Equal(["w/a", "w/a"], rang);
        Assert.True(book.Dismiss("w/a"));
        Assert.False(entry.On);
        Assert.Equal(["w/a"], switched);
        Assert.False(book.Dismiss("w/a"));
        Assert.False(book.Snooze("w/a", At(10, 12, 8)));
    }

    [Fact]
    public void S4623_sync_keeps_what_an_unchanged_alarm_is_doing_and_resets_a_changed_one()
    {
        var entry = Entry("07:00", true, Every);
        var (book, _, _, silenced) = Book(entry, At(10, 12, 6));
        book.Tick(At(10, 12, 7));

        // The same plan: still ringing.
        book.Sync([new AlarmSlot("w/a", entry, Snooze)], At(10, 12, 7, 1));
        Assert.True(book.AnyRinging);

        // A new time: set from now, the ring over.
        entry.Time = "08:00";
        book.Sync([new AlarmSlot("w/a", entry, Snooze)], At(10, 12, 7, 2));
        Assert.False(book.AnyRinging);
        Assert.Equal(["w/a"], silenced);
        Assert.Equal(At(10, 12, 8), book.Due(At(10, 12, 7, 2)));

        // Gone: forgotten.
        book.Sync([], At(10, 12, 7, 3));
        Assert.Empty(book.Keys);
        Assert.Null(book.Due(At(10, 12, 7, 3)));
    }

    [Fact]
    public void S4623_due_is_the_earliest_of_many()
    {
        var book = new AlarmBook(() => Zone);
        book.Sync(
        [
            new AlarmSlot("w/late", new AlarmEntry { Id = "late", Time = "09:00", On = true }, Snooze),
            new AlarmSlot("w/early", new AlarmEntry { Id = "early", Time = "06:45", On = true }, Snooze),
            new AlarmSlot("w/off", new AlarmEntry { Id = "off", Time = "06:00", On = false }, Snooze),
        ], At(10, 12, 5));

        Assert.Equal(At(10, 12, 6, 45), book.Due(At(10, 12, 5)));
    }

    // ---- words and the file ---------------------------------------------------------------

    [Fact]
    public void S4624_the_hint_says_off_ringing_snoozed_the_days_or_when()
    {
        var plan = Plan("07:00");
        var now = At(10, 12, 6);

        Assert.Equal("Off", AlarmWords.Hint(Locale.En, new AlarmStatus(AlarmPhase.Off), plan, now, Zone));
        Assert.Equal("Ringing", AlarmWords.Hint(Locale.En, new AlarmStatus(AlarmPhase.Ringing), plan, now, Zone));
        Assert.StartsWith("Snoozed until ", AlarmWords.Hint(Locale.En, new AlarmStatus(AlarmPhase.Snoozed, Until: At(10, 12, 7, 10)), plan, now, Zone));
        Assert.Equal("Today", AlarmWords.Hint(Locale.En, new AlarmStatus(AlarmPhase.Set, At(10, 12, 7)), plan, now, Zone));
        Assert.Equal("Tomorrow", AlarmWords.Hint(Locale.En, new AlarmStatus(AlarmPhase.Set, At(10, 13, 7)), plan, now, Zone));
        Assert.Equal("Every day", AlarmWords.Hint(Locale.En, new AlarmStatus(AlarmPhase.Set, At(10, 13, 7)), Plan("07:00", Every), now, Zone));
    }

    [Fact]
    public void S4624_days_are_named_as_a_set_or_one_by_one_in_the_languages_week()
    {
        Assert.Equal("Weekdays", AlarmWords.Days(Locale.En, AlarmDays.Weekdays));
        Assert.Equal("Weekends", AlarmWords.Days(Locale.En, AlarmDays.Weekend));

        var mondayWednesday = AlarmDays.Mask(["wed", "mon"]);
        var text = AlarmWords.Days(Locale.En, mondayWednesday);
        Assert.Contains(", ", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf(LocaleFormat.WeekdayName(Locale.En, DayOfWeek.Monday, NameWidth.Short), StringComparison.Ordinal)
            < text.IndexOf(LocaleFormat.WeekdayName(Locale.En, DayOfWeek.Wednesday, NameWidth.Short), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("07:00", 420)]
    [InlineData("7:05", 425)]
    [InlineData("23:59", 1439)]
    [InlineData("24:00", null)]
    [InlineData("07:60", null)]
    [InlineData("7", null)]
    [InlineData("07:5", null)]
    [InlineData("", null)]
    public void S4621_times_parse_strictly(string text, int? minutes) => Assert.Equal(minutes, AlarmTimes.Parse(text));

    [Fact]
    public void S4621_days_are_written_monday_first_once_each_and_unknown_words_dropped()
    {
        Assert.Equal(["mon", "wed", "sun"], AlarmDays.Normalized(["sun", "WED", "mon", "mon", "xyz"])!);
        Assert.Null(AlarmDays.Normalized(["xyz"]));
        Assert.Null(AlarmDays.Normalized(null));
    }

    [Fact]
    public void S4620_a_hand_edited_file_is_repaired_loudly()
    {
        var config = (AlarmConfig)ConfigJson.Read("alarm", """
            { "kind": "alarm", "alarms": [
                { "id": "x", "time": "25:00", "days": ["fri", "nope"], "on": true, "label": "  Wake  " },
                { "id": "x", "time": "06:30" }
              ], "sound": "Bell", "volume": 300, "snoozeMinutes": 0 }
            """);

        // Load validation has repaired it already; a second repair has nothing left to do.
        Assert.Empty(config.Repair());
        Assert.Equal("07:00", config.Alarms[0].Time);
        Assert.Equal(["fri"], config.Alarms[0].Days!);
        Assert.Equal("Wake", config.Alarms[0].Label);
        Assert.NotEqual(config.Alarms[0].Id, config.Alarms[1].Id);
        Assert.Equal(100, config.Volume);
        Assert.Equal(AlarmConfig.MinSnooze, config.SnoozeMinutes);
    }

    [Fact]
    public void S4620_a_new_widget_has_one_alarm_at_seven_switched_off()
    {
        var config = new AlarmConfig();
        var alarm = Assert.Single(config.Alarms);
        Assert.Equal("07:00", alarm.Time);
        Assert.False(alarm.On);
        Assert.False(WidgetSetup.Needed(config, default));

        config.Alarms.Clear();
        Assert.True(WidgetSetup.Needed(config, default));
    }

    [Fact]
    public void S4620_a_clone_is_deep()
    {
        var config = new AlarmConfig();
        var clone = (AlarmConfig)config.Clone();
        clone.Alarms[0].On = true;
        clone.Alarms.Add(new AlarmEntry());
        Assert.False(config.Alarms[0].On);
        Assert.Single(config.Alarms);
    }
}
