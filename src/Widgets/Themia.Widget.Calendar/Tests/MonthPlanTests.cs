using Themia.Core.Model;
using Themia.Core.Office;
using Xunit;

namespace Themia.Widget.Calendar.Tests;

/// <summary>[S-11547] The month with titles: its weeks, each day's chips on the config's lines and the "+k more" for the rest.</summary>
public sealed class MonthPlanTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.Utc;

    /// <summary>Monday 2026-11-16, the first day of a Monday week.</summary>
    private static readonly DateOnly Monday = new(2026, 11, 16);

    private static CalendarEvent Timed(string id, DateOnly day, int hour, int hours = 1) => new()
    {
        Id = id,
        Subject = id,
        Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero),
        End = new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero).AddHours(hours),
    };

    private static CalendarEvent AllDay(string id, DateOnly first, int days) => CalendarEvent.AllDayEvent(id, id, first, first.AddDays(days));

    [Fact]
    public void S11547_a_week_without_events_has_no_chips_and_nothing_more()
    {
        var week = MonthPlan.Week([], [], Monday, Zone, 3);
        Assert.Empty(week.Shown);
        Assert.All(week.Items, n => Assert.Equal(0, n));
        Assert.All(week.Hidden, n => Assert.Equal(0, n));
    }

    [Fact]
    public void S11547_a_busy_day_shows_its_lines_and_the_rest_is_its_more()
    {
        var wednesday = Monday.AddDays(2);
        var events = Enumerable.Range(0, 5).Select(i => Timed($"m{i}", wednesday, 8 + i)).ToList();

        var week = MonthPlan.Week(events, [], Monday, Zone, 3);
        Assert.Equal(3, week.Shown.Count);
        Assert.All(week.Shown, chip => Assert.Equal((2, 2), (chip.First, chip.Last)));
        Assert.Equal(new[] { "m0", "m1", "m2" }, week.Shown.Select(c => c.Item.Event!.Id));
        Assert.Equal((5, 2), (week.Items[2], week.Hidden[2]));
        Assert.Equal(0, week.Items[3]);

        // The lines are the config's, 1..6.
        Assert.Single(MonthPlan.Week(events, [], Monday, Zone, 0).Shown);
        Assert.Equal(5, MonthPlan.Week(events, [], Monday, Zone, 99).Shown.Count);
        Assert.Equal(0, MonthPlan.Week(events, [], Monday, Zone, 99).Hidden[2]);
    }

    [Fact]
    public void S11547_a_span_is_one_bar_and_counts_on_every_day_it_covers()
    {
        // Tuesday to Thursday all day, and two meetings on Wednesday under it.
        var trip = AllDay("trip", Monday.AddDays(1), 3);
        var wednesday = Monday.AddDays(2);
        var week = MonthPlan.Week([trip, Timed("a", wednesday, 9), Timed("b", wednesday, 10)], [], Monday, Zone, 2);

        var bar = Assert.Single(week.Shown, c => c.Item.Event == trip);
        Assert.Equal((1, 3, 0), (bar.First, bar.Last, bar.Lane));
        Assert.Equal(new[] { 0, 1, 3, 1, 0, 0, 0 }, week.Items);
        Assert.Equal(new[] { 0, 0, 1, 0, 0, 0, 0 }, week.Hidden);
    }

    [Theory]
    [InlineData(WeekStartDay.Monday, 4, 6, 0, 1)]
    [InlineData(WeekStartDay.Sunday, 5, 6, 0, 2)]
    public void S11547_a_span_across_a_weeks_end_goes_on_into_the_next_row(WeekStartDay start, int fromColumn, int toColumn, int nextFrom, int nextTo)
    {
        // Friday 2026-11-20 to Tuesday 2026-11-24, all day.
        var trip = AllDay("trip", new DateOnly(2026, 11, 20), 5);
        var weeks = MonthPlan.Weeks(new DateOnly(2026, 11, 1), start);
        var row = weeks.Select((w, i) => (w, i)).First(x => x.w <= trip.FirstDay && trip.FirstDay < x.w.AddDays(7)).i;

        var first = Assert.Single(MonthPlan.Week([trip], [], weeks[row], Zone, 3).Shown);
        Assert.Equal((fromColumn, toColumn, false, true), (first.First, first.Last, first.ContinuesBefore, first.ContinuesAfter));

        var next = Assert.Single(MonthPlan.Week([trip], [], weeks[row + 1], Zone, 3).Shown);
        Assert.Equal((nextFrom, nextTo, true, false), (next.First, next.Last, next.ContinuesBefore, next.ContinuesAfter));
    }

    [Fact]
    public void S11547_every_month_is_six_rows_from_its_week_start()
    {
        // November 2026 from Sunday: five weeks of its own, the sixth wholly December's.
        var november = MonthPlan.Weeks(new DateOnly(2026, 11, 15), WeekStartDay.Sunday);
        Assert.Equal(6, november.Count);
        Assert.Equal(new DateOnly(2026, 11, 1), november[0]);
        Assert.Equal(new DateOnly(2026, 12, 6), november[5]);
        Assert.All(november, w => Assert.Equal(DayOfWeek.Sunday, w.DayOfWeek));

        // August 2026 from Monday: six weeks of its own, the 31st in the last row.
        var august = MonthPlan.Weeks(new DateOnly(2026, 8, 1), WeekStartDay.Monday);
        Assert.Equal(new DateOnly(2026, 7, 27), august[0]);
        Assert.True(august[5] <= new DateOnly(2026, 8, 31) && new DateOnly(2026, 8, 31) < august[5].AddDays(7));
        Assert.All(august, w => Assert.Equal(DayOfWeek.Monday, w.DayOfWeek));

        // February 2027 from Monday: four whole weeks, still six rows.
        var february = MonthPlan.Weeks(new DateOnly(2027, 2, 10), WeekStartDay.Monday);
        Assert.Equal((new DateOnly(2027, 2, 1), new DateOnly(2027, 3, 8)), (february[0], february[5]));
    }

    [Fact]
    public void S11547_the_days_around_the_month_carry_their_events_dimmed()
    {
        // November 2026 from Sunday: its sixth row is wholly December's (6..12). The month fetches it
        // too, so a meeting on December 8 is a chip there, dimmed; a span reaching into November is not.
        var anchor = new DateOnly(2026, 11, 15);
        var weeks = MonthPlan.Weeks(anchor, WeekStartDay.Sunday);
        var (first, last) = CalendarRules.ViewRange(CalendarStyle.Month, anchor, anchor, new CalendarPeriod(WeekStartDay.Sunday));
        Assert.Equal((weeks[0], weeks[5].AddDays(6)), (first, last));

        var meeting = Timed("december", new DateOnly(2026, 12, 8), 9);
        var sixth = MonthPlan.Week([meeting], [], weeks[5], Zone, 3);
        var chip = Assert.Single(sixth.Shown);
        Assert.Equal((2, 1), (chip.First, sixth.Items[2]));
        Assert.True(MonthPlan.Dimmed(chip, weeks[5], anchor));

        var offsite = AllDay("offsite", new DateOnly(2026, 11, 30), 3);
        var fifth = MonthPlan.Week([offsite], [], weeks[4], Zone, 3);
        Assert.False(MonthPlan.Dimmed(Assert.Single(fifth.Shown), weeks[4], anchor));
    }

    [Fact]
    public void S11547_spans_are_bars_and_a_days_meetings_are_timed_lines()
    {
        var overnight = Timed("train", Monday, 22, hours: 9);
        var meeting = Timed("standup", Monday, 9);
        var holiday = AllDay("holiday", Monday, 1);
        var week = MonthPlan.Week([overnight, meeting, holiday], [new Birthday("Ana", 11, 17, null)], Monday, Zone, 6);

        MonthChipLook Of(string id) => MonthPlan.Look(week.Shown.Single(c => c.Item.Event?.Id == id), Zone);
        Assert.Equal(MonthChipLook.Timed, Of("standup"));
        Assert.Equal(MonthChipLook.Bar, Of("holiday"));
        Assert.Equal(MonthChipLook.Bar, Of("train"));
        Assert.Equal(MonthChipLook.Bar, MonthPlan.Look(week.Shown.Single(c => c.Item.IsBirthday), Zone));

        // All-day items and birthdays come before the day's meetings on its lines.
        Assert.True(week.Shown.Single(c => c.Item.Event?.Id == "holiday").Lane < week.Shown.Single(c => c.Item.Event?.Id == "standup").Lane);
    }

    [Theory]
    [InlineData(56, false)]
    [InlineData(79.9, false)]
    [InlineData(80, true)]
    [InlineData(180, true)]
    public void S11547_more_is_in_words_from_a_column_of_80(double column, bool words)
        => Assert.Equal(words, MonthPlan.MoreWords(column));
}
