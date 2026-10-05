using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// Package PH: Clipboard, Countdown and Mood ladders and rules ([S-1440]..[S-1457]). Areas are the
/// size tiers' slots (card less a 44px header).
/// </summary>
public class TrackersLayoutTests
{
    private static readonly WidgetArea XS = new(120, 76);
    private static readonly WidgetArea S = new(200, 106);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea Wide = new(480, 116);
    private static readonly WidgetArea Tall = new(200, 356);
    private static readonly WidgetArea L = new(560, 376);
    private static readonly WidgetArea XL = new(800, 516);

    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.FromHours(2));

    // ---- Clipboard ---------------------------------------------------------------------------

    [Fact]
    public void S1440_capacity_is_1_to_200_default_50()
    {
        Assert.Equal(1, ClipboardHistory.MinCapacity);
        Assert.Equal(200, ClipboardHistory.MaxCapacity);
        Assert.Equal(50, new ClipboardConfig().EffectiveCapacity);
        Assert.Equal(200, new ClipboardConfig { Capacity = 500 }.EffectiveCapacity);
        Assert.Equal(1, new ClipboardConfig { Capacity = 0 }.EffectiveCapacity);
    }

    [Fact]
    public void S1441_one_line_preview_is_the_collapsed_preview()
    {
        const string text = "line one\n  line   two\n";
        Assert.Equal(ClipboardFormatting.Preview(text, 120), ClipboardLayout.PreviewLines(text, 1, 120));
    }

    [Fact]
    public void S1441_multi_line_preview_keeps_breaks_drops_blank_lines_and_marks_more()
    {
        const string text = "Street 1\r\n\r\n  8000   Zurich \nSwitzerland\nEurope";

        Assert.Equal("Street 1\n8000 Zurich", ClipboardLayout.PreviewLines(text, 2, 120).Split(" …")[0]);
        Assert.EndsWith(" …", ClipboardLayout.PreviewLines(text, 2, 120));
        Assert.Equal("Street 1\n8000 Zurich\nSwitzerland …", ClipboardLayout.PreviewLines(text, 3, 120));
        Assert.Equal("Street 1\n8000 Zurich\nSwitzerland\nEurope", ClipboardLayout.PreviewLines(text, 4, 120));
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData("", 3)]
    [InlineData("abc", 0)]
    public void S1441_nothing_to_preview_is_empty(string? text, int lines)
        => Assert.Equal(string.Empty, ClipboardLayout.PreviewLines(text, lines, 120));

    [Theory]
    [InlineData("https://example.com/a?b=1", ClipboardContent.Link)]
    [InlineData("anna@example.org", ClipboardContent.Email)]
    [InlineData("#60cdff", ClipboardContent.Colour)]
    [InlineData("rgb(1, 2, 3)", ClipboardContent.Colour)]
    [InlineData("123", ClipboardContent.Number)]
    [InlineData("-1,234.5", ClipboardContent.Number)]
    [InlineData("NaN", ClipboardContent.Text)]
    [InlineData("cafe00", ClipboardContent.Text)]
    [InlineData("two\nlines", ClipboardContent.Lines)]
    [InlineData("hello world", ClipboardContent.Text)]
    [InlineData("a@b", ClipboardContent.Text)]
    [InlineData("C:\\Windows", ClipboardContent.Text)]
    public void S1442_entries_are_classified_conservatively(string text, ClipboardContent expected)
        => Assert.Equal(expected, ClipboardLayout.Classify(text));

    [Fact]
    public void S1442_a_colour_entry_yields_its_swatch()
    {
        Assert.True(ClipboardLayout.TryColour(" #ff0000 ", out var red));
        Assert.Equal(new RgbColor(255, 0, 0), red);
        Assert.False(ClipboardLayout.TryColour("ff0000", out _));
    }

    [Fact]
    public void S1443_clipboard_ladder_by_tier()
    {
        Assert.Equal(new ClipboardPlan(1, false, false, false, true), ClipboardLayout.Plan(XS));
        Assert.Equal(new ClipboardPlan(1, false, false, false, true), ClipboardLayout.Plan(S));
        Assert.Equal(new ClipboardPlan(1, false, true, false, false), ClipboardLayout.Plan(M));
        Assert.Equal(new ClipboardPlan(2, false, true, false, false), ClipboardLayout.Plan(Wide));
        Assert.Equal(new ClipboardPlan(2, true, false, false, false), ClipboardLayout.Plan(Tall));
        Assert.Equal(new ClipboardPlan(3, true, false, true, false), ClipboardLayout.Plan(L));
        Assert.Equal(new ClipboardPlan(3, true, false, true, false), ClipboardLayout.Plan(XL));
    }

    [Fact]
    public void S1443_unmeasured_area_is_one_plain_line()
        => Assert.Equal(1, ClipboardLayout.Plan(default).PreviewLines);

    // ---- Keyboard cursor ---------------------------------------------------------------------

    [Theory]
    [InlineData(5, null, WidgetKey.Down, 0)]
    [InlineData(5, null, WidgetKey.Up, 4)]
    [InlineData(5, 2, WidgetKey.Down, 3)]
    [InlineData(5, 4, WidgetKey.Down, 4)]
    [InlineData(5, 0, WidgetKey.Up, 0)]
    [InlineData(5, 3, WidgetKey.Home, 0)]
    [InlineData(5, 1, WidgetKey.End, 4)]
    [InlineData(5, 2, WidgetKey.Right, 3)]
    [InlineData(5, 2, WidgetKey.Left, 1)]
    [InlineData(5, 2, WidgetKey.Enter, 2)]
    [InlineData(5, 9, WidgetKey.Enter, null)]
    [InlineData(5, 2, WidgetKey.Escape, null)]
    [InlineData(0, 2, WidgetKey.Down, null)]
    public void S1457_cursor_moves_clamped(int count, int? current, WidgetKey key, int? expected)
        => Assert.Equal(expected, ItemCursor.Move(count, current, key));

    // ---- Countdown ---------------------------------------------------------------------------

    private static List<CountdownTimer> Timers(params int[] hoursFromNow)
        => [.. hoursFromNow.Select((h, i) => CountdownTimer.Create($"t{i}", Now.AddHours(h)))];

    [Fact]
    public void S1446_featured_is_the_soonest_ahead_else_the_latest_past()
    {
        Assert.Equal(2, CountdownLayout.Featured(Timers(30, -1, 5, 100), Now));
        Assert.Equal(1, CountdownLayout.Featured(Timers(-30, -1, -5), Now));
        Assert.Equal(-1, CountdownLayout.Featured([], Now));
    }

    [Fact]
    public void S1446_next_line_names_the_soonest_other_upcoming()
    {
        var timers = Timers(30, -1, 5, 100);
        Assert.Equal("Next: t0 in 1d 6h", CountdownLayout.NextLine(timers, 2, Now, Locale.En));
        Assert.Null(CountdownLayout.NextLine(Timers(5, -1), 0, Now, Locale.En));
    }

    [Fact]
    public void S1447_countdown_ladder_by_tier()
    {
        Assert.Equal(CountdownHero.Only, CountdownLayout.Plan(XS, 3).Hero);
        Assert.True(CountdownLayout.Plan(S, 3).NextLine);
        Assert.Equal(CountdownHero.Top, CountdownLayout.Plan(M, 3).Hero);
        Assert.Equal(CountdownHero.Left, CountdownLayout.Plan(Wide, 3).Hero);
        Assert.Equal(CountdownHero.Top, CountdownLayout.Plan(Tall, 3).Hero);
        Assert.Equal(CountdownHero.Top, CountdownLayout.Plan(L, 3).Hero);
        Assert.True(CountdownLayout.Plan(L, 3).HeroProgress);
        Assert.False(CountdownLayout.Plan(L, 3).RowProgress);
        Assert.True(CountdownLayout.Plan(XL, 3).RowProgress);

        // One timer is the whole tile at any size; none is the empty state.
        Assert.Equal(CountdownHero.Only, CountdownLayout.Plan(L, 1).Hero);
        Assert.False(CountdownLayout.Plan(L, 1).NextLine);
        Assert.Equal(CountdownHero.None, CountdownLayout.Plan(L, 0).Hero);
    }

    [Fact]
    public void S1448_progress_runs_from_creation_to_target()
    {
        var timer = CountdownTimer.Create("x", Now.AddHours(10), Now.AddHours(-30));
        Assert.Equal(0.75, CountdownLayout.Progress(timer, Now)!.Value, 6);
        Assert.Equal(1, CountdownLayout.Progress(timer, Now.AddDays(5)));
        Assert.Equal(0, CountdownLayout.Progress(timer, Now.AddDays(-5)));

        Assert.Null(CountdownLayout.Progress(CountdownTimer.Create("x", Now), Now));
        Assert.Null(CountdownLayout.Progress(CountdownTimer.Create("x", Now, Now.AddHours(1)), Now));
    }

    [Fact]
    public void S1448_clone_keeps_the_creation_time()
    {
        var timer = CountdownTimer.Create("x", Now.AddHours(10), Now);
        Assert.Equal(Now, timer.Clone().Created);
    }

    [Fact]
    public void S1447_hero_width_is_the_formats_widest()
    {
        Assert.True(CountdownLayout.HeroCharacters(CountdownFormat.DaysHoursMinutesSeconds, false)
            > CountdownLayout.HeroCharacters(CountdownFormat.Days, false));
        Assert.Equal(CountdownLayout.HeroCharacters(CountdownFormat.Auto, false) + 4, CountdownLayout.HeroCharacters(CountdownFormat.Auto, true));
    }

    // ---- Mood --------------------------------------------------------------------------------

    private static MoodPlan Mood(WidgetArea area, MoodTrackerConfig? config = null)
        => MoodLayout.Plan(area, config ?? new MoodTrackerConfig(), monthWeeks: 5);

    [Fact]
    public void S1452_stacked_tiers_keep_the_picker_and_shed_the_rest()
    {
        var xs = Mood(XS);
        Assert.Equal(MoodArrangement.Stacked, xs.Arrangement);
        Assert.False(xs.Note);
        Assert.False(xs.Stats);
        Assert.True(xs.Weeks >= 1);

        var s = Mood(S);
        Assert.False(s.Note);
        Assert.True(s.Weeks >= 2);
    }

    [Fact]
    public void S1452_M_shows_the_thought_box_footer_and_three_weeks()
    {
        var m = Mood(M);
        Assert.Equal(MoodArrangement.Stacked, m.Arrangement);
        Assert.True(m.Note);
        Assert.True(m.Stats);
        Assert.True(m.Weeks >= 3);
    }

    [Fact]
    public void S1452_tall_fills_with_weeks_and_labels()
    {
        var tall = Mood(Tall);
        Assert.True(tall.Note && tall.Stats && tall.Labels);
        Assert.True(tall.Weeks >= 8);
        Assert.True(tall.Cell >= MoodLayout.LabelCell);
    }

    [Fact]
    public void S1452_settings_turn_parts_off()
    {
        var m = Mood(M, new MoodTrackerConfig { ShowNote = false, ShowStats = false });
        Assert.False(m.Note);
        Assert.False(m.Stats);
    }

    [Fact]
    public void S1453_wide_tiers_split_with_weeks_as_columns_and_fill_the_width()
    {
        foreach (var area in (WidgetArea[])[Wide, L, XL])
        {
            var plan = Mood(area);
            var used = plan.LeftWidth + (2 * MoodLayout.Gap) + plan.MapWidth;

            Assert.Equal(MoodArrangement.Split, plan.Arrangement);
            Assert.True(plan.LeftWidth >= MoodLayout.PickerMinWidth);
            Assert.Equal(area.Width - (2 * MoodLayout.Inset), used, 3);
            Assert.InRange(plan.Weeks, 1, 12);
        }

        Assert.True(Mood(Wide).Note);
        Assert.False(Mood(Wide).Distribution);
    }

    [Fact]
    public void S1454_large_tiers_add_insights_and_recent_thoughts()
    {
        var l = Mood(L);
        Assert.True(l.Distribution && l.Weekdays);
        Assert.True(l.RecentNotes >= 3);

        Assert.False(l.Summary);

        var xl = Mood(XL);
        Assert.True(xl.Summary);
        Assert.True(xl.RecentNotes > l.RecentNotes);
        Assert.Equal(12, xl.Weeks);
    }

    [Fact]
    public void S1453_a_month_view_is_whole_or_it_is_the_recent_weeks_never_a_partial_month()
    {
        // The invariant, over every tier and every month length: either all of the month's weeks
        // (Recent false), or a flagged replacement with fewer weeks than the month has. [S-1573]
        var config = new MoodTrackerConfig { Span = MoodSpan.Month };

        foreach (var area in (WidgetArea[])[XS, S, M, Wide, Tall, L, XL])
        {
            foreach (var monthWeeks in (int[])[4, 5, 6])
            {
                var plan = MoodLayout.Plan(area, config, monthWeeks);

                if (plan.Recent)
                {
                    Assert.Equal(MoodArrangement.Stacked, plan.Arrangement);
                    Assert.InRange(plan.Weeks, 1, monthWeeks - 1);
                }
                else
                {
                    Assert.Equal(monthWeeks, plan.Weeks);
                }
            }
        }

        // Where the tile is big enough the month is whole.
        Assert.False(MoodLayout.Plan(Wide, config, 6).Recent);
        Assert.Equal(6, MoodLayout.Plan(Wide, config, 6).Weeks);
        Assert.False(MoodLayout.Plan(M, config, 6).Recent);
        Assert.False(MoodLayout.Plan(Tall, config, 6).Recent);
    }

    private static MoodEntry Entry(string id, DateTimeOffset at, int level, string note = "")
        => new() { Id = id, At = at, Level = level, Note = note };

    [Fact]
    public void S1454_distribution_and_weekday_averages_count_the_window_only()
    {
        var today = LocalDay.From(Now); // Sunday 4 Oct 2026
        List<MoodEntry> entries =
        [
            Entry("a", Now, 5),
            Entry("b", Now.AddDays(-1), 1),        // Saturday
            Entry("c", Now.AddDays(-7), 4),        // Sunday
            Entry("d", Now.AddDays(-40), 3),       // outside
            Entry("e", Now, 9),                    // invalid
        ];

        var from = today.AddDays(-13);
        Assert.Equal([1, 0, 0, 1, 1], MoodInsights.Distribution(entries, from, today));

        var averages = MoodInsights.WeekdayAverages(entries, from, today, DayOfWeek.Monday);
        Assert.Equal(4.5, averages[6]);   // Sunday is the last column of a Monday week
        Assert.Equal(1, averages[5]);     // Saturday
        Assert.Null(averages[0]);
    }

    [Fact]
    public void S1454_summary_counts_logged_days_best_run_and_thoughts_up_to_today()
    {
        var today = LocalDay.From(Now);
        List<MoodEntry> entries =
        [
            Entry("a", Now, 5, "x"),
            Entry("b", Now.AddDays(-1), 1),
            Entry("b2", Now.AddDays(-1), 2, "y"),
            Entry("c", Now.AddDays(-2), 4),
            Entry("d", Now.AddDays(-5), 3),
            Entry("e", Now.AddDays(-30), 3, "outside"),
        ];

        // A month-like window running past today counts only the days that have happened.
        var summary = MoodInsights.Summary(entries, today.AddDays(-9), today.AddDays(20), today);
        Assert.Equal(new MoodSummary(LoggedDays: 4, Days: 10, BestRun: 3, Thoughts: 2), summary);
    }

    [Fact]
    public void S1454_recent_notes_are_newest_first_and_capped()
    {
        List<MoodEntry> newestFirst =
        [
            Entry("a", Now, 4, "walk"),
            Entry("b", Now.AddHours(-1), 3),
            Entry("c", Now.AddDays(-1), 2, " rain "),
            Entry("d", Now.AddDays(-2), 5, "beach"),
        ];

        var notes = MoodInsights.RecentNotes(newestFirst, 2);
        Assert.Equal(["a", "c"], notes.Select(n => n.Id));
        Assert.Equal("Sat, Oct 3 · Bad: rain", MoodInsights.RecentLine(Themia.Core.Localization.Locale.En, notes[1]));
        Assert.Equal("Sa., 3. Okt. · Schlecht: rain", MoodInsights.RecentLine(Themia.Core.Localization.Locale.De, notes[1]));
    }

    [Fact]
    public void S1450_tooltip_lists_every_entry_with_its_thought()
    {
        var day = LocalDay.From(Now);
        List<MoodEntry> entries = [Entry("b", Now.AddHours(2), 5, "dinner"), Entry("a", Now.AddHours(-3), 2)];

        var dayEntries = MoodInsights.EntriesOn(entries, day);
        var byDay = MoodRules.ByDay(entries);
        var tooltip = MoodInsights.Tooltip(Themia.Core.Localization.Locale.En, MoodRules.Read(byDay, day), dayEntries).Replace(' ', ' ');
        var german = MoodInsights.Tooltip(Themia.Core.Localization.Locale.De, MoodRules.Read(byDay, day), dayEntries);

        Assert.Equal(["a", "b"], dayEntries.Select(e => e.Id));
        Assert.EndsWith("\n9:00 AM · Bad\n2:00 PM · Great: dinner", tooltip);
        Assert.EndsWith("\n09:00 · Schlecht\n14:00 · Super: dinner", german);
    }

    [Fact]
    public void S1451_delete_removes_only_that_entry()
    {
        List<MoodEntry> entries = [Entry("a", Now, 4), Entry("b", Now, 3)];

        Assert.False(MoodInsights.Delete(entries, "zzz"));
        Assert.False(MoodInsights.Delete(entries, string.Empty));
        Assert.True(MoodInsights.Delete(entries, "a"));
        Assert.Equal(["b"], entries.Select(e => e.Id));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, DayOfWeek.Monday, 0)]
    [InlineData(DayOfWeek.Sunday, DayOfWeek.Monday, 6)]
    [InlineData(DayOfWeek.Saturday, DayOfWeek.Saturday, 0)]
    [InlineData(DayOfWeek.Friday, DayOfWeek.Saturday, 6)]
    public void S1453_weekday_column_follows_the_week_start(DayOfWeek day, DayOfWeek start, int column)
        => Assert.Equal(column, MoodInsights.Column(day, start));
}
