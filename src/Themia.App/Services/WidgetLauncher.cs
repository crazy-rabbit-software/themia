using System.Diagnostics;
using Themia.App.Interop;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-1924] Opens what a click on a widget names, through <see cref="ShellLauncher"/>; the
/// <see cref="IWidgetLauncher"/> the cards use. A failure is logged there and shown here as a notice:
/// the user clicked and nothing happened, so they are told.
/// </summary>
internal sealed class WidgetLauncher : IWidgetLauncher
{
    private const string Scope = "launcher";

    /// <summary><c>SW_RESTORE</c>: un-minimise a window before bringing it forward.</summary>
    private const int SwRestore = 9;

    public static WidgetLauncher Instance { get; } = new();

    private WidgetLauncher()
    {
    }

    public WidgetLaunchResult Launch(WidgetLaunchTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        // [S-1926] A desktop player that is already running is brought forward, window and all:
        // its Start menu entry would start a second window (a browser) or nothing at all.
        if (target.Method == WidgetLaunchMethod.MediaApp && BringForward(MediaApps.ProcessName(target.Value)))
        {
            return WidgetLaunchResult.Opened;
        }

        var outcome = ShellLauncher.OpenWidgetTarget(target);
        if (outcome == LaunchOutcome.Opened)
        {
            return WidgetLaunchResult.Opened;
        }

        Notices.Warning(Str.Shell_Launch_Failed.Text(), Str.Shell_Launch_FailedDetail.Text());
        return outcome == LaunchOutcome.Refused ? WidgetLaunchResult.Refused : WidgetLaunchResult.Failed;
    }

    /// <summary>
    /// Bring the main window of a running process with this name to the front. False when there is
    /// no such process with a window, so the caller falls back to the Start menu entry.
    /// </summary>
    private static bool BringForward(string? processName)
    {
        if (processName is null)
        {
            return false;
        }

        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Log.Warn(Scope, $"could not list processes named '{processName}'", ex);
            return false;
        }

        try
        {
            foreach (var process in processes)
            {
                nint window;
                try
                {
                    window = process.MainWindowHandle;
                }
                catch (InvalidOperationException)
                {
                    continue; // ignore: the process exited while the list was walked
                }

                if (window == 0 || !Win32.IsWindowVisible(window))
                {
                    continue;
                }

                if (Win32.IsIconic(window))
                {
                    Win32.ShowWindow(window, SwRestore);
                }

                // The click that asked for this is input this process received, which is what lets
                // it hand the foreground on.
                if (Win32.SetForegroundWindow(window))
                {
                    return true;
                }

                Log.Warn(Scope, $"Windows would not bring '{processName}' to the front");
                return false;
            }

            return false;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
