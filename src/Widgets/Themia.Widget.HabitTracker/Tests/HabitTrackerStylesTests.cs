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
        Assert.Equal([HabitsSetting.Streak, HabitsSetting.RunWords, HabitsSetting.Lines], new HabitTrackerConfig { Style = HabitTrackerStyle.Dots }.ShownSettings);
        Assert.Equal([HabitsSetting.Streak, HabitsSetting.Lines], new HabitTrackerConfig { Style = HabitTrackerStyle.Dots, ShowStreak = false }.ShownSettings);
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
    public void S11532_S15770_one_grid_for_the_list_or_one_per_habit_and_a_wider_card_more_weeks()
    {
        var grid = ItemGrid.Default();
        var shared = HabitHeatmap.Plan(300, perHabit: false, grid);

        // A box is the mark and the gap in whole grid cells, the mark the box less the gap, half of it padding each side.
        Assert.Equal((false, 20.0, 18.0, 1.0), (shared.PerHabit, shared.Box, shared.Cell, shared.Pad));
        Assert.Equal(7 * 20.0, shared.GridHeight);

        // Every week the width reaches, the oldest cut by the card's left edge (WR-4).
        Assert.Equal(15, shared.Weeks);
        Assert.Equal(16, HabitHeatmap.Plan(301, perHabit: false, grid).Weeks);
        Assert.True(HabitHeatmap.Plan(520, perHabit: false, grid).Weeks > shared.Weeks);
        Assert.Equal(1, HabitHeatmap.Plan(0, perHabit: false, grid).Weeks);

        var each = HabitHeatmap.Plan(300, perHabit: true, grid);
        Assert.Equal((true, 10.0, 8.0), (each.PerHabit, each.Box, each.Cell));
        Assert.Equal(HabitHeatmap.NameOf(grid) + (7 * 10.0), each.Block);
    }

    [Fact]
    public void S15770_the_cells_keep_their_size_at_every_card_size_and_the_last_habit_is_cut_by_the_foot()
    {
        var grid = ItemGrid.Default();

        // The plan takes no height: a taller card never draws larger cells (WR-4, WR-6).
        Assert.Equal(HabitHeatmap.Plan(900, perHabit: false, grid).Box, HabitHeatmap.Plan(200, perHabit: false, grid).Box);

        // On another display's pitch the box is still whole cells of it.
        Assert.Equal(19.8, HabitHeatmap.Plan(300, perHabit: false, new ItemGrid(9.9)).Box, 6);

        // A grid per habit: every block the room reaches, the last cut by its foot, never more than the habits.
        var each = HabitHeatmap.Plan(300, perHabit: true, grid);
        Assert.Equal(1, HabitHeatmap.Blocks(each.Block, 5, each));
        Assert.Equal(2, HabitHeatmap.Blocks(each.Block + 1, 5, each));
        Assert.Equal(3, HabitHeatmap.Blocks(10 * each.Block, 3, each));
        Assert.Equal(1, HabitHeatmap.Blocks(0, 3, each));
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

    [Fact]
    public void S15787_the_weeks_stand_from_the_left_edge_the_next_weeks_column_cut_by_the_right()
    {
        var grid = ItemGrid.Default();

        // 330 wide: sixteen whole weeks of history, the current period's the last of them, and the next week cut to 10.
        var plan = HabitHeatmap.Plan(330, perHabit: false, grid);
        Assert.Equal((17, 16), (plan.Weeks, plan.History));

        // Exactly whole: no week to come; narrower than one: the one week, cut.
        Assert.Equal((15, 15), (HabitHeatmap.Plan(300, perHabit: false, grid).Weeks, HabitHeatmap.Plan(300, perHabit: false, grid).History));
        Assert.Equal((1, 1), (HabitHeatmap.Plan(10, perHabit: false, grid).Weeks, HabitHeatmap.Plan(10, perHabit: false, grid).History));
    }
}
