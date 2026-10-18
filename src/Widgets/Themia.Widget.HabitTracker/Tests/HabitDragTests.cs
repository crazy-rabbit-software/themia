using Themia.Core.Habits;
using Xunit;

namespace Themia.Widget.HabitTracker.Tests;

/// <summary>[S-1621] Dragging a habit row: when a press becomes a drag, where the row lands, and how the others make room.</summary>
public class HabitDragTests
{
    private const double Pitch = 40;

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(3, 3, false)]
    [InlineData(5, 0, true)]
    [InlineData(0, -5, true)]
    [InlineData(3, 4, true)]
    public void S1621_a_press_becomes_a_drag_after_five_pixels(double dx, double dy, bool drag)
        => Assert.Equal(drag, HabitDrag.IsDrag(dx, dy));

    [Theory]
    [InlineData(1, 0, 1)]       // no movement
    [InlineData(1, 19, 1)]      // under half a row
    [InlineData(1, 20, 2)]      // half way over the next row passes it
    [InlineData(1, 41, 2)]
    [InlineData(1, 61, 3)]
    [InlineData(1, -20, 0)]
    [InlineData(1, -19, 1)]
    [InlineData(0, -500, 0)]    // never above the first slot
    [InlineData(3, 500, 4)]     // never below the last
    public void S1621_the_row_lands_where_the_pointer_left_it(int from, double offset, int expected)
        => Assert.Equal(expected, HabitDrag.Target(from, count: 5, offset, Pitch));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(7, 4)]
    public void S1621_a_bad_start_or_an_empty_list_stays_inside_the_list(int from, int expected)
        => Assert.Equal(expected, HabitDrag.Target(from, count: from == 0 ? 0 : 5, 100, Pitch));

    [Fact]
    public void S1621_an_unmeasured_row_or_a_bad_offset_moves_nothing()
    {
        Assert.Equal(2, HabitDrag.Target(2, 5, 100, pitch: 0));
        Assert.Equal(2, HabitDrag.Target(2, 5, double.NaN, Pitch));
    }

    [Fact]
    public void S1621_dragging_down_lifts_the_rows_passed_by_one_pitch()
    {
        // Row 1 over slot 3: rows 2 and 3 step up into the gap; 0 and 4 stay.
        Assert.Equal(0, HabitDrag.Shift(0, 1, 3, Pitch));
        Assert.Equal(-Pitch, HabitDrag.Shift(2, 1, 3, Pitch));
        Assert.Equal(-Pitch, HabitDrag.Shift(3, 1, 3, Pitch));
        Assert.Equal(0, HabitDrag.Shift(4, 1, 3, Pitch));
    }

    [Fact]
    public void S1621_dragging_up_pushes_the_rows_passed_down_by_one_pitch()
    {
        // Row 3 over slot 1: rows 1 and 2 step down; 0 and 4 stay.
        Assert.Equal(0, HabitDrag.Shift(0, 3, 1, Pitch));
        Assert.Equal(Pitch, HabitDrag.Shift(1, 3, 1, Pitch));
        Assert.Equal(Pitch, HabitDrag.Shift(2, 3, 1, Pitch));
        Assert.Equal(0, HabitDrag.Shift(4, 3, 1, Pitch));
    }

    [Fact]
    public void S1621_a_drag_that_comes_home_moves_no_row_and_writes_nothing()
    {
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(0, HabitDrag.Shift(i, 2, 2, Pitch));
        }

        IReadOnlyList<Habit> habits = [new("a", "A", []), new("b", "B", []), new("c", "C", [])];
        Assert.Same(habits, HabitLists.MoveHabit(habits, "b", HabitDrag.Target(1, 3, 10, Pitch) - 1));
    }

    [Fact]
    public void S1621_the_release_commits_one_move_to_the_landing_slot()
    {
        IReadOnlyList<Habit> habits = [new("a", "A", []), new("b", "B", []), new("c", "C", []), new("d", "D", [])];
        var to = HabitDrag.Target(0, habits.Count, 2.6 * Pitch, Pitch);

        var moved = HabitLists.MoveHabit(habits, "a", to - 0);

        Assert.Equal(["b", "c", "d", "a"], moved.Select(h => h.Id));
    }
}
