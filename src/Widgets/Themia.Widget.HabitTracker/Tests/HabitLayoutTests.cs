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
}
