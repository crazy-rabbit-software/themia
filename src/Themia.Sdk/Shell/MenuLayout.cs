namespace Themia.Core.Shell;

/// <summary>
/// [S-1740] The commands Windows 11 draws as a row of icons above a context menu's list, in the
/// order the row shows them. File Explorer's row is Cut, Copy, (Paste), Rename, Share, Delete;
/// Themia adds Duplicate (views and items, not widgets) and Settings, which its items use as often.
/// Share is the folder widget's, for files ([S-6416]).
/// </summary>
public enum QuickCommand
{
    Cut,
    Copy,
    Paste,
    Rename,
    Share,
    Duplicate,
    Settings,
    Delete,
}

/// <summary>One line of a context menu, as far as its layout cares: a separator, or an entry that may be a quick command.</summary>
public readonly record struct MenuLine(bool IsSeparator, QuickCommand? Quick = null)
{
    public static MenuLine Separator => new(true);

    public static MenuLine Entry(QuickCommand? quick = null) => new(false, quick);
}

/// <summary>
/// Where each line of a menu goes: indexes into the lines it was made from. <see cref="Row"/> is the
/// icon row in <see cref="QuickCommand"/> order; <see cref="List"/> is the labelled list in the
/// lines' own order, with <see cref="SeparatorIndex"/> where a divider goes.
/// </summary>
public sealed record MenuSplit(IReadOnlyList<int> Row, IReadOnlyList<int> List)
{
    /// <summary>A divider in <see cref="List"/>.</summary>
    public const int SeparatorIndex = -1;
}

/// <summary>
/// [S-1740]..[S-1742] How a context menu is laid out: the Windows 11 icon row on top, the labelled
/// list below. Every menu Themia shows (a widget's card, an item inside a widget, the empty desktop
/// in edit mode) goes through this one rule, so they all look and order alike.
/// </summary>
public static class ContextMenuLayout
{
    /// <summary>
    /// [S-1741] Fewer quick commands than this and there is no row: a lone icon above a list reads
    /// as a stray button, not as a row, and it has no label to say what it is.
    /// </summary>
    public const int MinRow = 2;

    /// <summary>
    /// [S-1740]..[S-1742] Split <paramref name="lines"/> into the icon row and the list.
    /// <list type="bullet">
    /// <item>A quick command goes to the row, in <see cref="QuickCommand"/> order whatever order the
    /// caller listed it in; a second entry claiming the same command stays in the list.</item>
    /// <item>With fewer than <see cref="MinRow"/> distinct quick commands every entry stays in the list.</item>
    /// <item>The list keeps the callers' order; separators collapse: none leading, trailing or doubled,
    /// so callers can add groups conditionally.</item>
    /// </list>
    /// </summary>
    public static MenuSplit Split(IReadOnlyList<MenuLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var firstOf = new Dictionary<QuickCommand, int>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].IsSeparator && lines[i].Quick is { } quick)
            {
                firstOf.TryAdd(quick, i);
            }
        }

        var row = firstOf.Count >= MinRow
            ? firstOf.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList()
            : [];
        var inRow = new HashSet<int>(row);

        var list = new List<int>();
        var pendingSeparator = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].IsSeparator)
            {
                pendingSeparator = list.Count > 0;
                continue;
            }

            if (inRow.Contains(i))
            {
                continue;
            }

            if (pendingSeparator)
            {
                list.Add(MenuSplit.SeparatorIndex);
                pendingSeparator = false;
            }

            list.Add(i);
        }

        return new MenuSplit(row, list);
    }
}
