using Themia.Core.Habits;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.HabitTracker.Tests;

/// <summary>
/// [S-1427] The Habits widget's ladder at each size tier (the slot is the card less its 36
/// header), and [S-1428] its keys.
/// </summary>
public class HabitLayoutTests
{
    private static HabitLayoutPlan Plan(double width, double height, int count = 3, bool writable = true, bool streak = true, int dots = 7)
        => HabitLayout.Plan(new WidgetArea(width, height), new HabitLayoutInput(count, writable, streak, dots));

    [Fact]
    public void S1427_xs_shows_one_habit_as_check_and_name_only()
    {
        // The view's area less the card's bottom inset holds two rows of 28 ([S-6703], [S-6712]).
        var plan = Plan(120, 84);

        Assert.Equal(1, plan.Lines);
        Assert.Equal(28, plan.RowHeight);
        Assert.Equal(2, plan.Rows);
        Assert.False(plan.Streak);
        Assert.Equal(0, plan.Dots);
        Assert.False(plan.AddRow);
    }

    [Fact]
    public void S1427_s_adds_the_run_as_a_number_and_no_lonely_dot()
    {
        var plan = Plan(200, 114);

        Assert.Equal(3, plan.Rows);
        Assert.True(plan.Streak);
        Assert.False(plan.StreakWords);

        // One dot would fit beside the run: fewer than three is no history.
        Assert.Equal(0, plan.Dots);
    }

    [Fact]
    public void S1427_m_adds_the_dots_the_rate_line_and_the_add_row()
    {
        var plan = Plan(320, 204);

        Assert.Equal(2, plan.Lines);
        Assert.Equal(40, plan.RowHeight);
        Assert.Equal(7, plan.Dots);
        Assert.True(plan.AddRow);
        Assert.Equal(3, plan.Rows);
    }

    [Fact]
    public void S1427_a_wide_strip_spells_the_run_out_on_rows_of_one_line()
    {
        var plan = Plan(480, 124);

        Assert.Equal(1, plan.Lines);
        Assert.True(plan.StreakWords);
        Assert.Equal(7, plan.Dots);
        Assert.Equal(3, plan.Rows);
        Assert.False(plan.AddRow);
    }

    [Fact]
    public void S1427_tall_leaves_what_the_rows_do_not_use_empty()
    {
        var plan = Plan(200, 364);

        Assert.Equal(2, plan.Lines);
        Assert.True(plan.AddRow);
        Assert.Equal(3, plan.Rows);
        Assert.Equal(0, plan.Dots);
    }

    [Theory]
    [InlineData(560, 384)]
    [InlineData(800, 524)]
    public void S1427_l_and_xl_give_each_row_its_rate_and_best_run(double width, double height)
    {
        var plan = Plan(width, height);

        Assert.Equal(3, plan.Lines);
        Assert.Equal(56, plan.RowHeight);
        Assert.True(plan.StreakWords);
        Assert.Equal(7, plan.Dots);
    }

    [Fact]
    public void S1427_without_streaks_a_row_has_no_best_run_line()
    {
        Assert.Equal(2, Plan(560, 384, streak: false).Lines);
    }

    [Fact]
    public void S1427_the_dots_yield_width_before_the_name_does()
    {
        // 31 dots asked for on a medium tile: as many as fit beside a readable name, never more.
        var plan = Plan(320, 204, dots: 31);

        Assert.InRange(plan.Dots, HabitLayout.MinDots, 30);
        var used = (2 * FamilySpace.Inset) + HabitLayout.BoxWidth + HabitLayout.ColumnGap + HabitLayout.StreakNumberWidth
            + HabitLayout.ColumnGap + (plan.Dots * HabitLayout.DotPitch) - HabitLayout.DotGap;
        Assert.True(320 - used >= HabitLayout.NameMinWidth);
    }

    [Fact]
    public void S1427_fewer_dots_asked_than_the_floor_still_show()
    {
        Assert.Equal(2, Plan(320, 204, dots: 2).Dots);
    }

    [Fact]
    public void S1427_too_many_habits_for_the_tile_scroll()
    {
        var plan = Plan(320, 204, count: 9);
        Assert.Equal(4, plan.Rows);

        // No summary strip takes height from the rows (retired, concise widgets): a tall tile
        // shows every whole row under its add row.
        var tall = Plan(200, 364, count: 12);
        Assert.Equal(8, tall.Rows);
    }

    [Fact]
    public void S1425_an_empty_list_always_has_its_add_row_even_at_xs()
    {
        var plan = Plan(120, 84, count: 0);

        Assert.True(plan.AddRow);
        Assert.Equal(0, plan.Rows);

        Assert.False(Plan(120, 84, count: 0, writable: false).AddRow);
    }

    [Fact]
    public void S1427_settings_switch_their_rungs_off()
    {
        var plan = Plan(560, 384, streak: false, dots: 0);

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
