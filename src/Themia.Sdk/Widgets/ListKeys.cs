namespace Themia.Core.Widgets;

/// <summary>What a key press asks the list to do.</summary>
public enum ListCommand
{
    /// <summary>Nothing: the key means nothing here (Enter with no row focused, any key on an empty list).</summary>
    None,

    /// <summary>Move the focus to <see cref="ListKeyResult.Index"/>.</summary>
    Focus,

    /// <summary>Open the focused row: a message, a file, a folder.</summary>
    Open,

    /// <summary>Delete the focused row, if the row's owner allows it.</summary>
    Delete,

    /// <summary>Rename the focused row, if the list renames at all.</summary>
    Rename,

    /// <summary>[S-1560] Open the focused row's context menu (the Menu key, Shift+F10).</summary>
    Menu,

    /// <summary>Let the list go: clear the focus and give the keyboard back.</summary>
    Release,
}

/// <summary>A key's decision: the command, and the row it is about.</summary>
public readonly record struct ListKeyResult(ListCommand Command, int Index);

/// <summary>
/// [S-1288] Keyboard navigation in a widget's list, decided once for every list widget (Mail,
/// OneDrive, and the file explorer later): Up/Down step, Page Up/Down jump <see cref="PageStep"/>
/// rows, Home/End go to the ends, Enter opens, Delete deletes, F2 renames, the Menu key opens the
/// row's menu, Escape lets go.
/// <para>
/// Pure — which row a key lands on is arithmetic that is easy to get one-off wrong at the ends of
/// the list, so it is tested here rather than in a view. [S-1560] It speaks the shell's one key
/// vocabulary, <see cref="WidgetKey"/> (the keyboard lease's, mapped once in
/// <see cref="WidgetKeys"/>); the view carries out the <see cref="ListCommand"/>. Keys a list has no
/// use for (Left/Right, Tab, Space) are <see cref="ListCommand.None"/>, so they go on to the surface.
/// </para>
/// </summary>
public static class ListKeys
{
    /// <summary>How far Page Up and Page Down move, as the original did.</summary>
    public const int PageStep = 10;

    /// <param name="key">The key pressed.</param>
    /// <param name="index">The focused row, or negative when none is.</param>
    /// <param name="count">How many rows the list holds.</param>
    public static ListKeyResult Decide(WidgetKey key, int index, int count)
    {
        if (key == WidgetKey.Escape)
        {
            return new ListKeyResult(ListCommand.Release, -1);
        }

        if (count <= 0)
        {
            return new ListKeyResult(ListCommand.None, -1);
        }

        var last = count - 1;

        // No row focused yet: a movement key lands on an end, the rest have nothing to act on.
        if (index < 0)
        {
            return key switch
            {
                WidgetKey.Up or WidgetKey.Down or WidgetKey.PageUp or WidgetKey.PageDown or WidgetKey.Home => new ListKeyResult(ListCommand.Focus, 0),
                WidgetKey.End => new ListKeyResult(ListCommand.Focus, last),
                _ => new ListKeyResult(ListCommand.None, -1),
            };
        }

        // The list may have shrunk under the focus (a message deleted elsewhere): act on the row now there.
        var at = Math.Min(index, last);

        return key switch
        {
            WidgetKey.Up => new ListKeyResult(ListCommand.Focus, Math.Max(0, at - 1)),
            WidgetKey.Down => new ListKeyResult(ListCommand.Focus, Math.Min(last, at + 1)),
            WidgetKey.PageUp => new ListKeyResult(ListCommand.Focus, Math.Max(0, at - PageStep)),
            WidgetKey.PageDown => new ListKeyResult(ListCommand.Focus, Math.Min(last, at + PageStep)),
            WidgetKey.Home => new ListKeyResult(ListCommand.Focus, 0),
            WidgetKey.End => new ListKeyResult(ListCommand.Focus, last),
            WidgetKey.Enter => new ListKeyResult(ListCommand.Open, at),
            WidgetKey.Delete => new ListKeyResult(ListCommand.Delete, at),
            WidgetKey.Rename => new ListKeyResult(ListCommand.Rename, at),
            WidgetKey.Menu => new ListKeyResult(ListCommand.Menu, at),
            _ => new ListKeyResult(ListCommand.None, at),
        };
    }

    /// <summary>
    /// [S-1288] Where the focus goes after its row left the list (deleted, archived, moved): the row
    /// that took its place, else the new last row, else nowhere.
    /// </summary>
    public static int AfterRemoval(int index, int newCount) => newCount <= 0 ? -1 : Math.Clamp(index, 0, newCount - 1);
}
