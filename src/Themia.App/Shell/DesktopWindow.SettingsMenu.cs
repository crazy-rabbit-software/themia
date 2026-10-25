using Themia.App.Services;
using Themia.App.Shell.Motion;
using Themia.Core.Model;
using Themia.Core.Motion;
using Themia.Core.Settings;
using Themia.Core.Shell;

namespace Themia.App.Shell;

/// <summary>
/// [S-3520] The edit bar's settings menu (⚙) on this display: its lines, what they read and write,
/// and what it follows of Windows while it is open. Opened and closed as every bar menu is
/// (<see cref="OpenBarMenu"/>).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-3525] The desktop icons followed while the menu is open.</summary>
    private IDisposable? _settingsIcons;

    /// <summary>[S-3524] The settings menu's lines, as the state and Windows hold them now.</summary>
    private Func<IReadOnlyList<SettingsLine>> SettingsMenuLines()
    {
        var host = new SurfaceSettingsHost(this);
        return () => SettingsMenu.Root(host);
    }

    /// <summary>[S-3525] What Windows owns is read now and followed while the menu is open.</summary>
    private void SettingsMenuOpened()
    {
        _settingsIcons ??= _host.WatchDesktopIcons();
        _host.ReadStartup();
    }

    private void SettingsMenuClosed()
    {
        _settingsIcons?.Dispose();
        _settingsIcons = null;
    }

    private void OnSuspendedForSettings(bool suspended) => SyncBarMenu();

    /// <summary>[S-3524] What this display's settings menu reads and writes: the app's state and what Windows owns, through the host.</summary>
    private sealed class SurfaceSettingsHost(DesktopWindow surface) : ISettingsMenuHost
    {
        private AppHost Host => surface._host;

        public AppState State => Host.State;

        public AutostartStatus? Startup => Host.StartupStatus;

        public bool DesktopIconsShown => Host.DesktopIconsShown;

        public bool Suspended => Host.Suspended;

        public bool AnimationsEnabled => SystemMotion.AnimationsEnabled;

        public void Mutate(Action<AppState> change, bool relayout = true) => Host.Mutate(change, relayout);

        public void SetStartup(bool on) => Host.SetStartup(on);

        public void SetDesktopIcons(bool shown) => Host.SetDesktopIcons(shown);

        public void SetGameModeSuspend(bool on) => Host.SetGameModeSuspend(on);

        public void ReplayIntro(IntroMode mode) => Host.ReplayIntro(mode);

        public Core.Localization.Locale WindowsLanguage => Host.WindowsLanguage;

        public void SetLanguage(AppLanguage language) => Host.SetLanguage(language);

        public void Confirm(SettingsConfirm confirm) => surface._barMenus[BarMenu.Settings].Confirm(confirm);

        public void ShowAllSettings() => Host.ShowSettings();

        public void Quit() => Host.RequestExit();
    }
}
