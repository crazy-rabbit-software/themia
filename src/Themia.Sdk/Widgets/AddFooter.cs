namespace Themia.Core.Widgets;

/// <summary>
/// [S-3850] The one rule for a list widget's in-place "Add …" row (Habits, Tasks): a footer pinned
/// to the bottom of the content box, under the list, never right under the last item.
/// <para>
/// It is a rung of the widget's own ladder, asked for right after the first row: shown only where an
/// add is possible, always under an empty list (then it is the way to begin, so it is reserved at any
/// size), and otherwise from Regular height when a whole row is still left for it. A tile too small
/// for it keeps every row for the items.
/// </para>
/// </summary>
public static class AddFooter
{
    /// <summary>Whether the footer shows; spends its row from <paramref name="room"/> when it does.</summary>
    /// <param name="room">The pass's budget, with the first row already reserved.</param>
    /// <param name="writable">Whether an item can be added to what the widget shows.</param>
    /// <param name="empty">The list has nothing in it.</param>
    /// <param name="rowHeight">The list's row height: the footer is one of its rows.</param>
    public static bool Show(Disclosure room, WidgetArea area, bool writable, bool empty, double rowHeight)
    {
        ArgumentNullException.ThrowIfNull(room);

        if (!writable)
        {
            return false;
        }

        if (empty)
        {
            room.Reserve(rowHeight);
            return true;
        }

        return area.HeightClass >= SizeClass.Regular && room.Take(rowHeight);
    }
}
