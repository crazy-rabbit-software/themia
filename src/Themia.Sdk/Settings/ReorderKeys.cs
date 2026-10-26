namespace Themia.Core.Settings;

/// <summary>An arrow key, as the reorder shortcut reads it.</summary>
public enum ReorderArrow
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>
/// [S-3802] The keyboard's way to reorder a list that is otherwise reordered by dragging (the Views
/// page): Alt+Shift+Up moves the focused item one place up, Alt+Shift+Down one place down, as
/// Windows' own lists (the Start menu's pins, File Explorer's Quick access) take it. There is no
/// visible arrow button: the drag and this shortcut are the two ways.
/// </summary>
public static class ReorderKeys
{
    /// <summary>
    /// How far the focused item moves: -1, +1, or 0 when the keys are not the shortcut. Exactly Alt
    /// and Shift: with Ctrl as well it is somebody else's shortcut, and an arrow alone moves the focus.
    /// </summary>
    public static int Delta(ReorderArrow arrow, bool alt, bool shift, bool ctrl)
    {
        if (!alt || !shift || ctrl)
        {
            return 0;
        }

        return arrow switch
        {
            ReorderArrow.Up => -1,
            ReorderArrow.Down => +1,
            _ => 0,
        };
    }

    /// <summary>
    /// [S-3748] How far a menu's highlighted list row moves: Karpo's Ctrl+Up and Ctrl+Down, and the
    /// Windows lists' Alt+Shift+Up and Alt+Shift+Down ([S-3802]) the same; 0 for anything else.
    /// </summary>
    public static int MenuDelta(ReorderArrow arrow, bool alt, bool shift, bool ctrl)
    {
        var step = arrow switch
        {
            ReorderArrow.Up => -1,
            ReorderArrow.Down => +1,
            _ => 0,
        };

        return ctrl && !alt && !shift ? step : Delta(arrow, alt, shift, ctrl);
    }
}
