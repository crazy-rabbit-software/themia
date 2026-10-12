namespace Themia.Core.Shell;

/// <summary>What a global hotkey asks of the app ([S-65], [S-1998]). The value is the id the hotkey is registered under.</summary>
public enum ShellHotkey
{
    /// <summary>[S-65] Ctrl+Alt+W: edit mode on or off.</summary>
    ToggleEditMode = 1,

    /// <summary>[S-1998] Ctrl+Alt+Shift+W: edit mode on, and the add menu open on the display under the pointer.</summary>
    AddWidget = 2,
}

/// <summary>The modifier flags of <c>RegisterHotKey</c>, with Windows' own values (MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_NOREPEAT).</summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,

    /// <summary>Without it a held key repeats the hotkey dozens of times a second.</summary>
    NoRepeat = 0x4000,
}

/// <summary>One global hotkey: what it does, and the keys it is registered with.</summary>
/// <param name="Hotkey">What it asks for; also its registration id.</param>
/// <param name="Modifiers">The modifier flags, <see cref="HotkeyModifiers.NoRepeat"/> included.</param>
/// <param name="VirtualKey">The Windows virtual-key code.</param>
/// <param name="Keys">The combination as written in the log ("Ctrl+Alt+W").</param>
public readonly record struct HotkeyBinding(ShellHotkey Hotkey, HotkeyModifiers Modifiers, uint VirtualKey, string Keys);

/// <summary>
/// [S-65] [S-1998] The app's global hotkeys: the one table the command window registers, so a second
/// hotkey is a row here rather than a second registration path. Both are on W: Ctrl+Alt+W edits
/// the desktop, and Shift adds "with the add menu open". Not settings: like [S-65], a combination
/// another program owns is logged and the app still works (the toolbar's Add widget, the surface
/// menu and, in edit mode, Ctrl+N reach the same menu).
/// </summary>
public static class ShellHotkeys
{
    private const uint VkW = 0x57;

    public static IReadOnlyList<HotkeyBinding> All { get; } =
    [
        new(ShellHotkey.ToggleEditMode, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.NoRepeat, VkW, "Ctrl+Alt+W"),
        new(ShellHotkey.AddWidget, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.NoRepeat, VkW, "Ctrl+Alt+Shift+W"),
    ];

    /// <summary>The hotkey registered under <paramref name="id"/> (a <c>WM_HOTKEY</c>'s wParam), or null for one that is not ours.</summary>
    public static ShellHotkey? FromId(nint id)
        => All.Any(binding => (nint)(int)binding.Hotkey == id) ? (ShellHotkey)(int)id : null;
}
