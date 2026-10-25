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

    /// <summary>
    /// [S-3525] What Windows owns is read now and followed while the menu is open; [S-3559] so are
    /// the accounts and their numbers.
    /// </summary>
    private void SettingsMenuOpened()
    {
        _settingsIcons ??= _host.WatchDesktopIcons();
        _host.SettingsMenuOpened();
    }

    /// <summary>[S-3557] Closed: what was followed stops, and with the last menu every form empties.</summary>
    private void SettingsMenuClosed()
    {
        _settingsIcons?.Dispose();
        _settingsIcons = null;
        _host.SettingsMenuClosed();
    }

    /// <summary>
    /// [S-3573] [S-3558] Edit mode entered and ⚙ open on this display at <paramref name="path"/> (the
    /// lines' keys): where "See plans" and a widget's way to its accounts land. Whether it opened.
    /// </summary>
    public bool RequestSettings(IReadOnlyList<string> path)
    {
        if (_closed)
        {
            return false;
        }

        _host.SetEditMode(true);
        OpenBarMenu(BarMenu.Settings, path);
        return BarMenuOpen;
    }

    /// <summary>
    /// [S-3576] ⚙ open at <paramref name="path"/> on this display, for a question asked once Windows'
    /// dialog has gone (an import's): opened again where it was closed meanwhile. Whether it is open.
    /// </summary>
    private bool SettingsOpenAt(IReadOnlyList<string> path)
    {
        if (_openBarMenu != BarMenu.Settings)
        {
            OpenBarMenu(BarMenu.Settings, path);
        }

        return _openBarMenu == BarMenu.Settings;
    }

    /// <summary>[S-3572] [S-3576] This display's window: what the Store's dialog and Windows' file dialogs are owned by.</summary>
    private Microsoft.UI.WindowId WindowId => Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);

    /// <summary>
    /// [S-3558] A widget's way to its accounts: edit mode entered and ⚙ open on this display at
    /// Accounts, or at Add account while there is no account to manage. Its menu and its card's
    /// links all come here; nothing opens a settings window for an account any more.
    /// </summary>
    public bool RequestAccounts()
    {
        if (_closed)
        {
            return false;
        }

        string[] path = (AppServices.Accounts?.Accounts.Count ?? 0) == 0
            ? [SettingsMenu.Keys.Accounts, SettingsMenu.Keys.AddAccount]
            : [SettingsMenu.Keys.Accounts];
        return RequestSettings(path);
    }

    private void OnSuspendedForSettings(bool suspended) => SyncBarMenu();

    /// <summary>
    /// [S-3524] What this display's settings menu reads and writes: the app's state and what Windows
    /// owns, through the host; [S-3570] [S-3575] [S-3580] the licence, the settings file and the app
    /// itself, with this display's window owning what Windows shows for them.
    /// </summary>
    private sealed class SurfaceSettingsHost(DesktopWindow surface) : ISettingsMenuHost, ILicenceMenuHost, IBackupMenuHost, IAboutMenuHost
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

        public IAccountsMenuHost? Accounts => AppServices.Accounts is null ? null : Host.AccountsMenu;

        public Core.Localization.Locale WindowsLanguage => Host.WindowsLanguage;

        public void SetLanguage(AppLanguage language) => Host.SetLanguage(language);

        public void Confirm(SettingsConfirm confirm) => surface._barMenus[BarMenu.Settings].Confirm(confirm);

        public void Quit() => Host.RequestExit();

        public ILicenceMenuHost Licence => this;

        public IBackupMenuHost Backup => this;

        public IAboutMenuHost About => this;

        public SettingsOutcomes Outcomes => Host.SettingsOutcomes;

        // ---- [S-3570] Themia Pro ------------------------------------------------------------------

        public Core.Licensing.LicenseStatus Status => Host.LicenceMenu.Status;

        public bool CanPurchase => Host.LicenceMenu.CanPurchase;

        public string? Price => Host.LicenceMenu.Price;

        public string? Refusal => Host.LicenceMenu.Refusal;

        public void Purchase() => Host.LicenceMenu.Purchase(surface._hwnd);

        public void Restore() => Host.LicenceMenu.Restore();

        // ---- [S-3575] Back up & reset -------------------------------------------------------------

        public string StatePath => Host.StatePath;

        public void Export() => Host.Backup.Export(surface.WindowId);

        public void Import() => Host.Backup.Import(surface.WindowId, confirm =>
        {
            // Asked beside this display's menu, open again at Back up & reset if it closed meanwhile.
            if (surface.SettingsOpenAt([SettingsMenu.Keys.Backup]))
            {
                Confirm(confirm);
            }
            else
            {
                Core.Diagnostics.Log.Warn(Scope, "the settings menu could not be opened to ask about the import; nothing was imported");
            }
        });

        public void OpenFile() => Host.Backup.OpenFile();

        public void Reset() => Host.Backup.Reset();

        // ---- [S-3580] About Themia ----------------------------------------------------------------

        public string Version => AppInfo.Version;

        public string Runtime => AppInfo.RuntimeLine;

        public void Open(Uri page)
        {
            var outcome = page.Scheme == "mailto" ? ShellLauncher.OpenMailLink(page.OriginalString) : ShellLauncher.Open(page.AbsoluteUri);
            if (outcome != LaunchOutcome.Opened)
            {
                Core.Diagnostics.Log.Warn(Scope, $"could not open {page} ({outcome})");
            }
        }

        public UninstallPlan Uninstall => Uninstaller.Preview;

        public bool DeleteData => Host.UninstallDeletesData;

        public void SetDeleteData(bool on) => Host.SetUninstallDeletesData(on);

        public void UninstallNow(bool deleteData) => Uninstaller.Run(Host, deleteData);
    }
}
