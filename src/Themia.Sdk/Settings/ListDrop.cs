namespace Themia.Core.Settings;

/// <summary>
/// [S-3732] Where a line dragged up or down a list lands: the gap the pointer is nearest (before the
/// first line whose middle is below it), and the index the dragged item takes there. Pure: the menu
/// measures its lines and asks, and the same rule draws the drop line and makes the move.
/// </summary>
public static class ListDrop
{
    /// <summary>
    /// The gap at <paramref name="y"/> among lines given top to bottom by their
    /// <paramref name="middles"/>: 0 before the first, <c>middles.Count</c> after the last.
    /// </summary>
    public static int Gap(IReadOnlyList<double> middles, double y)
    {
        ArgumentNullException.ThrowIfNull(middles);

        var gap = 0;
        while (gap < middles.Count && middles[gap] < y)
        {
            gap++;
        }

        return gap;
    }

    /// <summary>
    /// The index the item at <paramref name="from"/> takes when it is dropped in <paramref name="gap"/>
    /// (the gaps either side of it leave it where it is): the gaps after it count it out.
    /// </summary>
    public static int Target(int from, int gap) => gap > from ? gap - 1 : gap;
}
