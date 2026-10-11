namespace Themia.Core.Settings;

/// <summary>A window-wide key of the settings window ([S-1977]).</summary>
public enum SettingsKey
{
    /// <summary>Not one of the window's: the focused control has it.</summary>
    None = 0,

    /// <summary>Ctrl+F or Ctrl+E: focus "Find a setting".</summary>
    FocusSearch,

    /// <summary>F6: the next pane (title bar, navigation, page).</summary>
    NextPane,

    /// <summary>Shift+F6: the previous pane.</summary>
    PreviousPane,

    /// <summary>Ctrl+W: close the window.</summary>
    Close,
}

/// <summary>The panes F6 walks through, in order ([S-1977]).</summary>
public enum SettingsPane
{
    Search = 0,
    Navigation = 1,
    Page = 2,
}

/// <summary>
/// [S-1977] The settings window's own keys. Alt+Left / Alt+Right (history) stay the
/// <c>KeyboardAccelerator</c>s they were; everything a control answers itself (arrows in the
/// navigation, Enter, Space, Escape in a flyout) is not here.
/// </summary>
public static class SettingsKeys
{
    private const int VkE = 0x45;
    private const int VkF = 0x46;
    private const int VkW = 0x57;
    private const int VkF6 = 0x75;

    public static SettingsKey Decide(int virtualKey, bool shift, bool ctrl, bool alt)
    {
        if (alt)
        {
            return SettingsKey.None;
        }

        if (ctrl)
        {
            return virtualKey switch
            {
                VkF or VkE when !shift => SettingsKey.FocusSearch,
                VkW when !shift => SettingsKey.Close,
                _ => SettingsKey.None,
            };
        }

        return virtualKey == VkF6 ? (shift ? SettingsKey.PreviousPane : SettingsKey.NextPane) : SettingsKey.None;
    }

    /// <summary>
    /// The pane F6 moves to from <paramref name="current"/>, wrapping. A pane that cannot take the
    /// focus now (the navigation folded away in a narrow window) is skipped; with none available the
    /// focus stays.
    /// </summary>
    public static SettingsPane Next(SettingsPane current, bool backwards, Func<SettingsPane, bool> available)
    {
        ArgumentNullException.ThrowIfNull(available);

        SettingsPane[] panes = [SettingsPane.Search, SettingsPane.Navigation, SettingsPane.Page];
        var at = Array.IndexOf(panes, current);
        for (var k = 1; k <= panes.Length; k++)
        {
            var i = backwards ? (at - k + (panes.Length * 2)) % panes.Length : (at + k) % panes.Length;
            if (available(panes[i]))
            {
                return panes[i];
            }
        }

        return current;
    }
}
