using Themia.Core.Habits;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1560]..[S-1579] The widget polish pass: keyboard lease in the remaining lists, layout gaps.</summary>
public sealed class Polish1Tests
{
    // ---- [S-1571] Habits summary line ---------------------------------------------------------------

    private static HabitLayoutPlan Habits(double width, double height, int count)
        => HabitLayout.Plan(new WidgetArea(width, height), new HabitLayoutInput(count, true, true, 7), 40, 32);

    [Fact]
    public void S1571_m_gives_the_height_no_row_can_use_to_the_summary_line_and_never_overflows()
    {
        foreach (var count in new[] { 1, 2, 3, 6, 12 })
        {
            var plan = Habits(320, 196, count);
            var used = plan.RowHeight /* add row */ + (plan.Rows * plan.RowHeight)
                + (plan.Summary ? (plan.SummaryBar ? HabitLayout.SummaryHeight : HabitLayout.SummaryLineHeight) : 0);

            Assert.True(plan.Summary, $"{count} habits");
            Assert.False(plan.SummaryBar);
            Assert.True(used <= 196 - HabitLayout.ListBottomPadding + Disclosure.Tolerance, $"{count} habits use {used}");
        }
    }

    [Fact]
    public void S1571_the_full_strip_stays_for_expanded_heights_and_compact_heights_get_none()
    {
        Assert.True(Habits(560, 376, 6).SummaryBar);
        Assert.True(Habits(560, 376, 6).Summary);
        Assert.False(Habits(200, 106, 6).Summary);
        Assert.False(Habits(480, 116, 6).Summary);
    }
}
