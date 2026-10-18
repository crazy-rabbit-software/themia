namespace Themia.Core.Habits;

/// <summary>
/// [S-1621] The arithmetic of dragging a habit row to a new place (original S-13). The rows are one
/// column of equal height, so a drag is one number: how far the pointer has travelled since the
/// press. From it come the slot the row would land in and how far every other row steps aside to
/// make room. The view draws that and commits once on release, through
/// <see cref="HabitLists.MoveHabit"/>, the same write Move up / Move down use.
/// </summary>
public static class HabitDrag
{
    /// <summary>How far the pointer moves before a press becomes a drag; less is a click (the original's 5 px).</summary>
    public const double Threshold = 5;

    /// <summary>True once the pointer has left the press by <see cref="Threshold"/> in any direction.</summary>
    public static bool IsDrag(double dx, double dy) => (dx * dx) + (dy * dy) >= Threshold * Threshold;

    /// <summary>
    /// The slot a row that started at <paramref name="from"/> lands in after moving
    /// <paramref name="offset"/> DIPs (down is positive): it passes a neighbour once it is more than
    /// half way over it, and never leaves the list.
    /// </summary>
    public static int Target(int from, int count, double offset, double pitch)
    {
        if (count <= 0 || from < 0 || from >= count || pitch <= 0 || !double.IsFinite(offset))
        {
            return Math.Clamp(from, 0, Math.Max(0, count - 1));
        }

        var steps = (int)Math.Round(offset / pitch, MidpointRounding.AwayFromZero);
        return Math.Clamp(from + steps, 0, count - 1);
    }

    /// <summary>
    /// How far the row at <paramref name="index"/> is shifted while the row from
    /// <paramref name="from"/> hovers over <paramref name="to"/>: the rows in between step one pitch
    /// towards the gap the dragged row left; every other row stays put. The dragged row itself
    /// follows the pointer and is not placed by this.
    /// </summary>
    public static double Shift(int index, int from, int to, double pitch)
    {
        if (index == from)
        {
            return 0;
        }

        if (to > from && index > from && index <= to)
        {
            return -pitch;
        }

        if (to < from && index < from && index >= to)
        {
            return pitch;
        }

        return 0;
    }
}
