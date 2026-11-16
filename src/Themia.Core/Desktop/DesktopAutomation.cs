namespace Themia.Core.Desktop;

/// <summary>
/// [S-5576] The desktop's list to UI Automation: the order its children are given in. The desktop's items
/// first, each in its place in the desktop's order (its position in the set, 1-based), then every other
/// child as the tree has it. Pure: the surface's automation peer reorders the list XAML handed it in place.
/// </summary>
public static class DesktopAutomation
{
    /// <summary>
    /// The children's indices in that order, from each child's position in the set (0 or less: no item).
    /// Null when they already stand in it, so nothing is touched.
    /// </summary>
    public static int[]? Order(IReadOnlyList<int> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        var order = Enumerable.Range(0, positions.Count)
            .OrderBy(i => positions[i] > 0 ? 0 : 1)
            .ThenBy(i => positions[i] > 0 ? positions[i] : i)
            .ToArray();

        return order.Select((at, i) => at == i).All(same => same) ? null : order;
    }
}
