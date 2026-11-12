using Themia.Core.Habits;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.HabitTracker.Tests;

/// <summary>
/// [S-11530] [S-11531] The Habits widget's rows as its config says, and [S-1428] its keys.
/// </summary>
public class HabitLayoutTests
{
    private static HabitLayoutPlan Plan(int count = 3, bool writable = true, bool streak = true, int dots = 7, int rows = 4, int lines = 1, bool words = false, bool heatmap = false)
        => HabitLayout.Plan(new HabitLayoutInput(count, writable, streak, dots, Rows: rows, Lines: lines, RunWords: words, Heatmap: heatmap));

    [Fact]
    public void S11530_the_rows_are_the_configs_and_the_add_footer_is_always_there()
    {
        var plan = Plan(count: 9);
        Assert.Equal((4, 1, FamilyShape.Row1, true), (plan.Rows, plan.Lines, plan.RowHeight, plan.AddRow));
        Assert.Equal(12, Plan(rows: 40).Rows);
        Assert.Equal(1, Plan(rows: 0).Rows);
        Assert.True(Plan(count: 0).AddRow);
        Assert.False(Plan(writable: false).AddRow);

        // The heatmap draws no rows: its footer only under an empty list.
        Assert.False(Plan(heatmap: true).AddRow);
        Assert.True(Plan(count: 0, heatmap: true).AddRow);
    }

    [Fact]
    public void S11531_the_run_the_dots_and_the_lines_are_the_configs_at_every_width()
    {
        var plan = Plan(dots: 31, lines: 2, words: true);
        Assert.Equal((true, true, 31, 2, FamilyShape.Row2), (plan.Streak, plan.StreakWords, plan.Dots, plan.Lines, plan.RowHeight));

        var bare = Plan(streak: false, dots: 0, words: true);
        Assert.Equal((false, false, 0), (bare.Streak, bare.StreakWords, bare.Dots));
        Assert.Equal(2, Plan(lines: 7).Lines);
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
