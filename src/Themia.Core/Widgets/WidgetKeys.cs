namespace Themia.Core.Widgets;

/// <summary>
/// [S-803] Which <see cref="WidgetKey"/> a platform key press means. The one place that knows the
/// Win32 virtual-key codes, so the mapping — the modifiers in particular — is tested here and not
/// rediscovered by every list.
/// </summary>
public static class WidgetKeys
{
    private const int VkTab = 0x09;
    private const int VkReturn = 0x0D;
    private const int VkEscape = 0x1B;
    private const int VkSpace = 0x20;
    private const int VkPrior = 0x21;
    private const int VkNext = 0x22;
    private const int VkEnd = 0x23;
    private const int VkHome = 0x24;
    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;
    private const int VkDelete = 0x2E;
    private const int VkApps = 0x5D;
    private const int VkF2 = 0x71;
    private const int VkF10 = 0x79;
    private const int VkM = 0x4D;
    private const int VkA = 0x41;
    private const int VkC = 0x43;
    private const int VkV = 0x56;
    private const int VkX = 0x58;
    private const int VkBack = 0x08;
    private const int VkAdd = 0x6B;
    private const int VkSubtract = 0x6D;
    private const int VkOemPlus = 0xBB;
    private const int VkOemMinus = 0xBD;

    /// <summary>
    /// The widget key for a virtual-key code, or null for one no widget answers. Ctrl and Alt chords
    /// are never a widget's (they are the user's shortcuts, and Alt+arrows is window navigation);
    /// Shift only matters where it changes the meaning — Tab backwards, F10 as the Menu key — and
    /// otherwise leaves the key alone.
    /// </summary>
    public static WidgetKey? FromVirtualKey(int virtualKey, bool shift, bool ctrl, bool alt)
    {
        // [S-1327] The four clipboard chords are the one exception: in a file list they are what
        // the user expects, and a view without a clipboard does not answer them, so they carry on
        // to wherever they went before. Ctrl alone — Ctrl+Shift+C is somebody else's shortcut.
        if (ctrl && !alt && !shift)
        {
            return virtualKey switch
            {
                VkC => WidgetKey.Copy,
                VkX => WidgetKey.Cut,
                VkV => WidgetKey.Paste,
                VkA => WidgetKey.SelectAll,
                _ => null,
            };
        }

        // [S-3802] Alt+Shift+Up / Down is the reorder shortcut of every list that is dragged into
        // order, the widgets' included; one rule, ReorderKeys, decides it for all of them.
        if (alt && shift && !ctrl)
        {
            var arrow = virtualKey switch
            {
                VkUp => Settings.ReorderArrow.Up,
                VkDown => Settings.ReorderArrow.Down,
                _ => (Settings.ReorderArrow?)null,
            };

            return arrow is { } pressed
                ? Settings.ReorderKeys.Delta(pressed, alt, shift, ctrl) switch
                {
                    < 0 => WidgetKey.MoveUp,
                    > 0 => WidgetKey.MoveDown,
                    _ => null,
                }
                : null;
        }

        if (ctrl || alt)
        {
            return null;
        }

        return virtualKey switch
        {
            // [S-1468] Shift+Left/Right is its own key (seek within a track); a view that has no
            // use for it simply does not answer.
            VkLeft => shift ? WidgetKey.ShiftLeft : WidgetKey.Left,
            VkRight => shift ? WidgetKey.ShiftRight : WidgetKey.Right,
            VkUp => WidgetKey.Up,
            VkDown => WidgetKey.Down,
            VkPrior => WidgetKey.PageUp,
            VkNext => WidgetKey.PageDown,
            VkHome => WidgetKey.Home,
            VkEnd => WidgetKey.End,
            VkTab => shift ? WidgetKey.ShiftTab : WidgetKey.Tab,
            VkReturn => WidgetKey.Enter,
            VkEscape => WidgetKey.Escape,
            VkSpace when !shift => WidgetKey.Space,
            VkDelete when !shift => WidgetKey.Delete,
            VkF2 when !shift => WidgetKey.Rename,
            VkApps => WidgetKey.Menu,
            VkF10 when shift => WidgetKey.Menu,

            // [S-1225] Shift is ignored: '+' is Shift+'=' on most layouts, and either means "up".
            VkAdd or VkOemPlus => WidgetKey.Plus,
            VkSubtract or VkOemMinus => WidgetKey.Minus,
            VkM when !shift => WidgetKey.Mute,
            VkBack when !shift => WidgetKey.Back,
            _ => null,
        };
    }
}
