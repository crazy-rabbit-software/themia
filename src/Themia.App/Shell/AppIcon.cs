using Microsoft.UI.Windowing;
using Themia.Core.Diagnostics;

namespace Themia.App.Shell;

/// <summary>
/// [S-1850] The one place a window gets Themia's icon. A WinUI window without an explicit icon
/// shows a blank one in its title bar, the taskbar, Alt+Tab and Task Manager, so every window the
/// app opens goes through here — the settings window, the layout editor, each display's surface
/// (hidden from the switchers, but still listed by Task Manager and used as the owner of dialogs)
/// and the Debug render windows. The exe carries the same file as its resource icon
/// (<c>ApplicationIcon</c> in the csproj), which Explorer, Start and the uninstall entry show.
/// </summary>
internal static class AppIcon
{
    private const string Scope = "app-icon";

    /// <summary>Where the icon is deployed next to the exe (copied from <c>Assets\themia.ico</c>).</summary>
    public static string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "themia.ico");

    public static void Apply(AppWindow window)
    {
        if (!File.Exists(FilePath))
        {
            // A broken deployment, not a user error: the window still works, with a blank icon.
            Log.Warn(Scope, $"icon file missing at {FilePath}; window '{window.Title}' keeps the default icon");
            return;
        }

        try
        {
            window.SetIcon(FilePath);
        }
        catch (Exception error)
        {
            Log.Warn(Scope, $"could not set the icon on window '{window.Title}'", error);
        }
    }
}
