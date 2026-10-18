using Themia.Core.Habits;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.HabitTracker.Tests;

/// <summary>[S-1560]..[S-1579] The widget polish pass: keyboard lease in the remaining lists, layout gaps.</summary>
public sealed class Polish1Tests
{
    // ---- [S-1427] Habits at M: whole rows, no summary line ([S-1571] retired) -----------------------

    private static HabitLayoutPlan Habits(double width, double height, int count)
        => HabitLayout.Plan(new WidgetArea(width, height), new HabitLayoutInput(count, true, true, 7), 40, 32);

    [Fact]
    public void S1427_m_rows_and_the_add_row_never_overflow_the_slot()
    {
        foreach (var count in new[] { 1, 2, 3, 6, 12 })
        {
            var plan = Habits(320, 196, count);
            var used = plan.RowHeight /* add row */ + (plan.Rows * plan.RowHeight);

            Assert.True(used <= 196 - HabitLayout.ListBottomPadding + Disclosure.Tolerance, $"{count} habits use {used}");
        }
    }
}
