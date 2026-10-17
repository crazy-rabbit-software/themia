namespace Themia.Core.Widgets;

/// <summary>
/// [S-3802] Putting a list the user is editing in order: one place up or down (Alt+Shift+Up / Down),
/// or onto another item (a drag).
/// <para>
/// Its own type because every settings list needs exactly this, and a second copy of a swap is a
/// second place for an off-by-one at the ends of the list, which is the only part of it anyone ever
/// gets wrong.
/// </para>
/// </summary>
public static class DeskListOrder
{
    /// <summary>
    /// Move the item at <paramref name="index"/> by <paramref name="delta"/> places, and say whether
    /// anything moved.
    /// <para>
    /// An out-of-range index or an edge is a no-op rather than an exception: Alt+Shift+Up on the
    /// first row is pressable, and should simply do nothing rather than crash a settings page.
    /// </para>
    /// </summary>
    public static bool Move<T>(IList<T>? items, int index, int delta)
    {
        if (items is null || delta == 0 || index < 0 || index >= items.Count)
        {
            return false;
        }

        var target = index + delta;

        if (target < 0 || target >= items.Count)
        {
            return false;
        }

        (items[index], items[target]) = (items[target], items[index]);
        return true;
    }

    /// <summary>
    /// [S-3802] A drop: the item at <paramref name="from"/> takes the place of the item at
    /// <paramref name="to"/>, and the items between close up (what the Views page does with views,
    /// [S-1703]). Says whether anything moved; the same index or one out of range is a no-op.
    /// </summary>
    public static bool MoveTo<T>(IList<T>? items, int from, int to)
    {
        if (items is null || from == to || from < 0 || to < 0 || from >= items.Count || to >= items.Count)
        {
            return false;
        }

        var moving = items[from];
        items.RemoveAt(from);
        items.Insert(to, moving);
        return true;
    }
}
