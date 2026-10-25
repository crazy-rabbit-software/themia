using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Interop;

/// <summary>
/// [S-920] The two signals game mode decides from: what the shell says about the user's attention,
/// and who has the foreground. Both are asked fresh on every check, never remembered — a cached
/// answer is how a desktop stays empty after the game has quit.
/// </summary>
internal static class UserNotification
{
    private const string Scope = "game-mode";

    /// <summary>
    /// <c>SHQueryUserNotificationState</c>: the signal Windows itself uses to hold back notifications
    /// during a full-screen game, a full-screen video or a presentation. A failure reads as
    /// <see cref="UserNotificationState.Unknown"/>, which game mode treats as "not full-screen" ([S-923]).
    /// </summary>
    public static UserNotificationState Query()
    {
        var hr = Win32.SHQueryUserNotificationState(out var raw);
        if (hr < 0)
        {
            Log.Debug(Scope, $"SHQueryUserNotificationState failed (0x{hr:X8})");
            return UserNotificationState.Unknown;
        }

        return GameMode.FromRaw(raw);
    }

    /// <summary>Classify the current foreground window for <see cref="GameMode.Decide"/>.</summary>
    public static ForegroundKind Foreground()
    {
        var foreground = Win32.GetForegroundWindow();
        if (foreground == 0)
        {
            return ForegroundKind.None;
        }

        // Any window of this process: the edit-mode surface, the layout editor. Asked by process,
        // not by the surface list, because a suspended surface is no longer on that list.
        Win32.GetWindowThreadProcessId(foreground, out var pid);
        if (pid == (uint)Environment.ProcessId)
        {
            return ForegroundKind.OwnWindow;
        }

        if (ShellSignals.DesktopIsForeground())
        {
            return ForegroundKind.Desktop;
        }

        return ShellSignals.ShellSurfaceIsForeground() ? ForegroundKind.ShellSurface : ForegroundKind.Application;
    }
}
