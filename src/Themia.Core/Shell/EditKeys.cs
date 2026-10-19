namespace Themia.Core.Shell;

/// <summary>What a key does on the desktop surface in edit mode ([S-70], [S-1970]..[S-1976]).</summary>
public enum EditCommand
{
    /// <summary>Not an edit-mode key: it carries on (to a focused toolbar button, say).</summary>
    None = 0,

    Copy,
    Cut,
    Paste,
    Undo,
    Redo,

    /// <summary>[S-1582] Ctrl+A.</summary>
    SelectAll,

    /// <summary>[S-1592] Delete removes the selection, as one undo step.</summary>
    Delete,

    /// <summary>[S-904] Ctrl+1..9: the view number is <see cref="EditKeyAction.ViewNumber"/>.</summary>
    ShowView,

    /// <summary>[S-1583] Escape backs out one step.</summary>
    Back,

    /// <summary>[S-1970] Arrows move the selection.</summary>
    Move,

    /// <summary>[S-1971] Shift+arrows resize the selection by its right or bottom edge.</summary>
    Resize,

    /// <summary>[S-1973] Tab / Shift+Tab selects the next / previous widget in reading order.</summary>
    Cycle,

    /// <summary>[S-1974] Enter moves the keyboard into the selection's properties menu, where its options are.</summary>
    FocusProperties,

    /// <summary>[S-1974] The Menu key or Shift+F10 opens the selection's context menu.</summary>
    OpenMenu,

    /// <summary>[S-1975] F6 / Shift+F6 moves the keyboard between the widgets and the toolbar.</summary>
    SwitchPane,

    /// <summary>[S-1998] Ctrl+N opens the add menu, as the global Ctrl+Alt+Shift+W does.</summary>
    AddWidget,
}

/// <summary>Which way an arrow key points.</summary>
public enum NudgeDirection
{
    Left = 0,
    Up = 1,
    Right = 2,
    Down = 3,
}

/// <summary>[S-1972] How far one arrow press goes.</summary>
public enum NudgeStep
{
    /// <summary>One snap cell ([S-06b], about 10 DIPs).</summary>
    Cell = 0,

    /// <summary>Ctrl: <see cref="KeyboardNudgeDefaults.FarCells"/> cells.</summary>
    Far = 1,

    /// <summary>Alt: one DIP, and the grid is not applied (Alt suspends the snap, as when dragging, [S-06]).</summary>
    Fine = 2,
}

/// <summary>The constants of the keyboard nudge, shared by the key map and the geometry.</summary>
public static class KeyboardNudgeDefaults
{
    /// <summary>[S-1972] Cells a Ctrl+arrow moves or resizes by.</summary>
    public const int FarCells = 10;
}

/// <summary>One decided edit-mode key.</summary>
/// <param name="Command">What to do.</param>
/// <param name="Direction">For <see cref="EditCommand.Move"/> and <see cref="EditCommand.Resize"/>.</param>
/// <param name="Step">For <see cref="EditCommand.Move"/> and <see cref="EditCommand.Resize"/>.</param>
/// <param name="ViewNumber">For <see cref="EditCommand.ShowView"/>: 1..9.</param>
/// <param name="Backwards">For <see cref="EditCommand.Cycle"/> and <see cref="EditCommand.SwitchPane"/>.</param>
public readonly record struct EditKeyAction(
    EditCommand Command,
    NudgeDirection Direction = NudgeDirection.Left,
    NudgeStep Step = NudgeStep.Cell,
    int ViewNumber = 0,
    bool Backwards = false)
{
    public static readonly EditKeyAction None = new(EditCommand.None);
}

/// <summary>
/// [S-70], [S-1970]..[S-1976] The edit-mode key map: one table from a virtual key and its modifiers
/// to an <see cref="EditCommand"/>, so the surface only carries the command out. Ctrl+Alt is never a
/// shortcut here: it is AltGr on many layouts.
/// </summary>
public static class EditKeys
{
    private const int VkTab = 0x09;
    private const int VkReturn = 0x0D;
    private const int VkEscape = 0x1B;
    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;
    private const int VkDelete = 0x2E;
    private const int Vk1 = 0x31;
    private const int Vk9 = 0x39;
    private const int VkA = 0x41;
    private const int VkC = 0x43;
    private const int VkN = 0x4E;
    private const int VkV = 0x56;
    private const int VkX = 0x58;
    private const int VkY = 0x59;
    private const int VkZ = 0x5A;
    private const int VkApps = 0x5D;
    private const int VkNumpad1 = 0x61;
    private const int VkNumpad9 = 0x69;
    private const int VkF6 = 0x75;
    private const int VkF10 = 0x79;

    public static EditKeyAction Decide(int virtualKey, bool shift, bool ctrl, bool alt)
    {
        if (ctrl && alt)
        {
            return EditKeyAction.None;
        }

        if (Direction(virtualKey) is { } direction)
        {
            var step = alt ? NudgeStep.Fine : ctrl ? NudgeStep.Far : NudgeStep.Cell;
            return new EditKeyAction(shift ? EditCommand.Resize : EditCommand.Move, direction, step);
        }

        if (ctrl)
        {
            return virtualKey switch
            {
                VkC when !shift => new(EditCommand.Copy),
                VkX when !shift => new(EditCommand.Cut),
                VkV when !shift => new(EditCommand.Paste),
                VkA when !shift => new(EditCommand.SelectAll),
                VkN when !shift => new(EditCommand.AddWidget),
                VkZ => new(shift ? EditCommand.Redo : EditCommand.Undo),
                VkY when !shift => new(EditCommand.Redo),
                >= Vk1 and <= Vk9 when !shift => new(EditCommand.ShowView, ViewNumber: virtualKey - Vk1 + 1),
                >= VkNumpad1 and <= VkNumpad9 when !shift => new(EditCommand.ShowView, ViewNumber: virtualKey - VkNumpad1 + 1),
                _ => EditKeyAction.None,
            };
        }

        if (alt)
        {
            return EditKeyAction.None;
        }

        return virtualKey switch
        {
            VkDelete when !shift => new(EditCommand.Delete),
            VkEscape => new(EditCommand.Back),
            VkTab => new(EditCommand.Cycle, Backwards: shift),
            VkReturn when !shift => new(EditCommand.FocusProperties),
            VkApps => new(EditCommand.OpenMenu),
            VkF10 when shift => new(EditCommand.OpenMenu),
            VkF6 => new(EditCommand.SwitchPane, Backwards: shift),
            _ => EditKeyAction.None,
        };
    }

    /// <summary>Whether a key released ends a run of nudges ([S-1972]: one undo step per press-and-hold).</summary>
    public static bool IsArrow(int virtualKey) => Direction(virtualKey) is not null;

    private static NudgeDirection? Direction(int virtualKey) => virtualKey switch
    {
        VkLeft => NudgeDirection.Left,
        VkUp => NudgeDirection.Up,
        VkRight => NudgeDirection.Right,
        VkDown => NudgeDirection.Down,
        _ => null,
    };
}
