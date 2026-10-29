using Themia.App.Interop;
using Themia.Core.Model;
using Themia.Core.Shell;

namespace Themia.App.Services;

/// <summary>
/// [S-5500]..[S-5506] The desktop layer: what the desktop shows (⚙'s one choice) applied to Explorer's
/// icons, held across Explorer restarts and given back on quit.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>[S-5501] Themia's items show: the surfaces cover their displays and the item widgets have cards.</summary>
    public bool ShowsDesktopItems => State.Preferences.Desktop == DesktopShows.ThemiaItems;

    /// <summary>
    /// [S-5503] [S-5504] At start, before any surface: the choice applied. A choice that hides holds
    /// the list hidden (no flash of icons first); any other undoes what an earlier run that crashed or
    /// was killed may have left hidden.
    /// </summary>
    private void StartDesktop()
    {
        _command.ShellRestarted += DesktopIcons.ShellRestarted;
        _command.DesktopChanged += Files.HostDesktopItems.Instance.OnNotification;
        ApplyDesktop(picked: false);
    }

    /// <summary>[S-5504] Quit: the hook goes and Explorer's icons come back if this run hid them.</summary>
    private void StopDesktop()
    {
        _command.ShellRestarted -= DesktopIcons.ShellRestarted;
        _command.DesktopChanged -= Files.HostDesktopItems.Instance.OnNotification;
        Files.HostDesktopItems.Instance.Stop();
        DesktopIcons.RestoreIfHidden();
    }

    /// <summary>
    /// [S-5503] [S-5505] The choice applied to Explorer's icons: hidden for the session under Themia's
    /// items and Nothing, else shown again as Windows has them, and, on the user's pick of Windows'
    /// icons while Windows hides them, Windows' own setting asked once.
    /// </summary>
    private void ApplyDesktop(bool picked)
    {
        var shows = State.Preferences.Desktop;

        // [S-5511] The namespace is read and watched only while its items show.
        if (shows == DesktopShows.ThemiaItems)
        {
            Files.HostDesktopItems.Instance.Start(_command.Handle, _dispatcher);
        }
        else
        {
            Files.HostDesktopItems.Instance.Stop();
        }

        if (DesktopIconRules.Hides(shows))
        {
            DesktopIcons.Hold();
            return;
        }

        DesktopIcons.Release();
        if (picked && DesktopIconRules.AsksWindows(shows, DesktopIcons.WindowsHides()))
        {
            DesktopIcons.Request(true);
        }
    }
}
