using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.HabitTracker.Tests;

/// <summary>[S-6408] [S-6409] [S-6410] [S-11532] The habit tracker's styles: the dots' own settings, the heatmap and the checklist. Moved with the widget from Core's ListStylesTests, unchanged in what they assert.</summary>
public sealed class HabitTrackerStylesTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void S6408_only_the_dots_draw_the_run_and_the_history()
    {
        Assert.Equal([HabitsSetting.Streak, HabitsSetting.RunWords, HabitsSetting.History, HabitsSetting.Lines], new HabitTrackerConfig { Style = HabitTrackerStyle.Dots }.ShownSettings);
        Assert.Equal([HabitsSetting.Streak, HabitsSetting.History, HabitsSetting.Lines], new HabitTrackerConfig { Style = HabitTrackerStyle.Dots, ShowStreak = false }.ShownSettings);
        Assert.Equal([HabitsSetting.PerHabit], new HabitTrackerConfig { Style = HabitTrackerStyle.Heatmap }.ShownSettings);
        Assert.Empty(new HabitTrackerConfig { Style = HabitTrackerStyle.Checklist }.ShownSettings);
    }

    [Fact]
    public void S6409_a_daily_grid_is_calendar_weeks_from_the_first_day_with_the_rest_of_this_week_blank()
    {
        var cadence = new HabitCadence(HabitPeriod.Daily);
        var wednesday = HabitPeriods.At(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero), cadence, Utc);

        var cells = HabitHeatmap.Cells(wednesday, cadence, Utc, 3, DayOfWeek.Monday);

        Assert.Equal(21, cells.Count);
        Assert.Equal("2026-10-12", cells[14]!.Value.Key);          // Monday, top of the last column
        Assert.Equal("2026-10-14", cells[16]!.Value.Key);          // today
        Assert.All(cells.Skip(17), c => Assert.Null(c));           // Thursday .. Sunday to come
        Assert.Equal("2026-09-28", cells[0]!.Value.Key);           // two Mondays back
        Assert.Equal("2026-10-11", cells[13]!.Value.Key);          // the Sunday before

        var sunday = HabitHeatmap.Cells(wednesday, cadence, Utc, 1, DayOfWeek.Sunday);
        Assert.Equal("2026-10-11", sunday[0]!.Value.Key);
        Assert.Equal("2026-10-14", sunday[3]!.Value.Key);
    }

    [Fact]
    public void S11532_one_grid_for_the_list_or_one_per_habit_and_a_wider_card_more_weeks()
    {
        var shared = HabitHeatmap.Plan(276, perHabit: false);
        Assert.Equal((false, HabitHeatmap.SharedCell), (shared.PerHabit, shared.Cell));
        Assert.True((shared.Weeks * shared.Pitch) - HabitHeatmap.Gap <= 276);
        Assert.True(((shared.Weeks + 1) * shared.Pitch) - HabitHeatmap.Gap > 276);

        var each = HabitHeatmap.Plan(276, perHabit: true);
        Assert.Equal((true, HabitHeatmap.PerHabitCell), (each.PerHabit, each.Cell));
        Assert.True(HabitHeatmap.Plan(496, perHabit: false).Weeks > shared.Weeks);
        Assert.Equal(HabitHeatmap.MaxWeeks, HabitHeatmap.Plan(4000, false).Weeks);
        Assert.Equal(HabitHeatmap.GridOf(shared.Cell), shared.GridHeight);
    }

    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(1, 5, 0.2)]
    [InlineData(4, 5, 0.8)]
    [InlineData(5, 5, 1)]
    [InlineData(7, 5, 1)]
    [InlineData(3, 0, 0)]
    public void S6409_a_shared_cell_fills_from_its_foot_by_the_share_done(int done, int count, double fill)
        => Assert.Equal(fill, HabitHeatmap.Fill(done, count), 6);

    [Theory]
    [InlineData(6, 3)]
    [InlineData(8, 4)]
    [InlineData(20, 4)]
    public void S6409_a_cell_has_the_family_radius_or_is_round(double cell, double radius)
        => Assert.Equal(radius, HabitHeatmap.Radius(cell));

    [Fact]
    public void S6409_any_other_cadence_fills_to_the_last_cell_one_period_a_step()
    {
        var cadence = new HabitCadence(HabitPeriod.Weekly);
        var now = HabitPeriods.At(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero), cadence, Utc);

        var cells = HabitHeatmap.Cells(now, cadence, Utc, 2, DayOfWeek.Monday);

        Assert.Equal(now.Key, cells[^1]!.Value.Key);
        Assert.All(cells, c => Assert.NotNull(c));
        Assert.Equal(HabitPeriods.Previous(now, cadence, Utc).Key, cells[^2]!.Value.Key);
        Assert.Equal(14, cells.Select(c => c!.Value.Key).Distinct().Count());
    }

    [Fact]
    public void S6409_nothing_to_draw_is_an_empty_plan()
    {
        Assert.Empty(HabitHeatmap.Cells(default, new HabitCadence(HabitPeriod.Daily), Utc, 0, DayOfWeek.Monday));
    }

    [Fact]
    public void S6410_a_checklist_is_the_box_and_the_name()
    {
        var plan = HabitLayout.Plan(new HabitLayoutInput(5, true, true, 7, Checklist: true, Lines: 2, RunWords: true));

        // One line per habit: no run, dots, rate or best run beside or under the name.
        Assert.False(plan.Streak);
        Assert.Equal(0, plan.Dots);
        Assert.Equal(1, plan.Lines);
        Assert.True(plan.AddRow);
    }
}
