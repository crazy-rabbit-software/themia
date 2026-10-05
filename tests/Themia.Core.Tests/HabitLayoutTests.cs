using Themia.Core.Habits;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1427] The Habits widget's ladder at each size tier (the slot is the card less its ~44px
/// header), and [S-1428] its keys.
/// </summary>
public class HabitLayoutTests
{
    private static HabitLayoutPlan Plan(double width, double height, int count = 3, bool writable = true, bool streak = true, int dots = 7)
        => HabitLayout.Plan(new WidgetArea(width, height), new HabitLayoutInput(count, writable, streak, dots), 40, 32);

    [Fact]
    public void S1427_xs_shows_two_habits_as_box_and_name_only()
    {
        var plan = Plan(120, 76);

        Assert.True(plan.CompactRows);
        Assert.Equal(2, plan.Rows);
        Assert.Equal(8, plan.Inset);
        Assert.False(plan.Streak);
        Assert.Equal(0, plan.Dots);
        Assert.False(plan.AddRow);
        Assert.False(plan.Summary);
        Assert.Equal(0, plan.ChartHeight);
    }

    [Fact]
    public void S1427_s_adds_the_run_as_a_number()
    {
        var plan = Plan(200, 106);

        Assert.Equal(3, plan.Rows);
        Assert.True(plan.Streak);
        Assert.False(plan.StreakWords);
        Assert.Equal(0, plan.Dots);
    }

    [Fact]
    public void S1427_m_adds_the_dots_and_the_add_row()
    {
        var plan = Plan(320, 196);

        Assert.False(plan.CompactRows);
        Assert.Equal(7, plan.Dots);
        Assert.True(plan.AddRow);
        Assert.Equal(3, plan.Rows);
        Assert.False(plan.Rate);

        // [S-1571] The height three whole rows leave carries the summary's line, without its bar.
        Assert.True(plan.Summary);
        Assert.False(plan.SummaryBar);
    }

    [Fact]
    public void S1427_a_wide_strip_spells_the_run_out_and_adds_the_rate_beside()
    {
        var plan = Plan(480, 116);

        Assert.True(plan.CompactRows);
        Assert.True(plan.StreakWords);
        Assert.Equal(7, plan.Dots);
        Assert.True(plan.Rate);
        Assert.False(plan.Best);
        Assert.Equal(3, plan.Rows);
        Assert.False(plan.AddRow);
    }

    [Fact]
    public void S1427_tall_adds_the_summary_and_a_chart_in_what_the_rows_leave()
    {
        var plan = Plan(200, 356);

        Assert.True(plan.Summary);
        Assert.True(plan.AddRow);
        Assert.Equal(3, plan.Rows);
        Assert.Equal(0, plan.Dots);
        Assert.Equal(356 - HabitLayout.ListBottomPadding - (3 * 40) - 40 - HabitLayout.SummaryHeight, plan.ChartHeight, 3);
        Assert.Equal(12, plan.ChartBars);
    }

    [Fact]
    public void S1427_l_has_every_rung_but_the_best_run()
    {
        var plan = Plan(560, 376);

        Assert.True(plan.StreakWords);
        Assert.Equal(7, plan.Dots);
        Assert.True(plan.Rate);
        Assert.False(plan.Best);
        Assert.True(plan.Summary);
        Assert.True(plan.ChartHeight >= HabitLayout.ChartMinHeight);
        Assert.Equal(HabitRules.MaxHistory, plan.ChartBars);
    }

    [Fact]
    public void S1427_xl_adds_the_best_run()
    {
        var plan = Plan(800, 516);

        Assert.True(plan.Best);
        Assert.True(plan.Rate);
        Assert.True(plan.ChartHeight > 0);
    }

    [Fact]
    public void S1427_the_dots_yield_width_before_the_name_does()
    {
        // 31 dots asked for on a medium tile: as many as fit beside a readable name, never more.
        var plan = Plan(320, 196, dots: 31);

        Assert.InRange(plan.Dots, 1, 30);
        var used = (2 * plan.Inset) + HabitLayout.BoxWidth + HabitLayout.ColumnGap + HabitLayout.StreakNumberWidth
            + HabitLayout.ColumnGap + (plan.Dots * HabitLayout.DotPitch) - (HabitLayout.DotPitch - HabitLayout.DotSize);
        Assert.True(320 - used >= HabitLayout.NameMinWidth);
    }

    [Fact]
    public void S1427_too_many_habits_for_the_tile_scroll_and_drop_the_chart()
    {
        var plan = Plan(320, 196, count: 9);

        Assert.Equal(3, plan.Rows);
        Assert.Equal(0, plan.ChartHeight);

        var tall = Plan(200, 356, count: 12);
        Assert.True(tall.Rows < 12);
        Assert.Equal(0, tall.ChartHeight);
    }

    [Fact]
    public void S1425_an_empty_list_always_has_its_add_row_even_at_xs()
    {
        var plan = Plan(120, 76, count: 0);

        Assert.True(plan.AddRow);
        Assert.Equal(0, plan.Rows);
        Assert.False(plan.Summary);

        Assert.False(Plan(120, 76, count: 0, writable: false).AddRow);
    }

    [Fact]
    public void S1427_settings_switch_their_rungs_off()
    {
        var plan = Plan(560, 376, streak: false, dots: 0);

        Assert.False(plan.Streak);
        Assert.Equal(0, plan.Dots);
    }

    [Fact]
    public void S1427_an_unmeasured_area_still_plans_one_row()
    {
        var plan = Plan(0, 0);

        Assert.Equal(1, plan.Rows);
        Assert.False(plan.Streak);
    }

    // ---- [S-1428] keys ------------------------------------------------------------------------------

    [Theory]
    [InlineData(WidgetKey.Up, 0, HabitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.Down, 2, HabitKeyCommand.Focus, 2)]
    [InlineData(WidgetKey.Down, 0, HabitKeyCommand.Focus, 1)]
    [InlineData(WidgetKey.Home, 2, HabitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.End, 0, HabitKeyCommand.Focus, 2)]
    [InlineData(WidgetKey.Tab, 2, HabitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.ShiftTab, 0, HabitKeyCommand.Focus, 2)]
    [InlineData(WidgetKey.Enter, 1, HabitKeyCommand.Toggle, 1)]
    [InlineData(WidgetKey.Rename, 1, HabitKeyCommand.Rename, 1)]
    [InlineData(WidgetKey.Delete, 1, HabitKeyCommand.Delete, 1)]
    [InlineData(WidgetKey.Escape, 1, HabitKeyCommand.Release, 1)]
    public void S1428_arrows_clamp_tab_wraps_and_enter_ticks(WidgetKey key, int index, HabitKeyCommand command, int target)
        => Assert.Equal(new HabitKeyResult(command, target), HabitLayout.Decide(key, index, 3));

    [Fact]
    public void S1428_an_empty_list_only_answers_escape()
    {
        Assert.Equal(HabitKeyCommand.Release, HabitLayout.Decide(WidgetKey.Escape, 0, 0).Command);
        Assert.Equal(HabitKeyCommand.None, HabitLayout.Decide(WidgetKey.Enter, 0, 0).Command);
    }

    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(2, 3, 1)]
    [InlineData(0, 1, -1)]
    public void S1428_after_a_delete_the_focus_goes_to_the_row_in_its_place(int index, int count, int expected)
        => Assert.Equal(expected, HabitLayout.AfterDelete(index, count));
}
