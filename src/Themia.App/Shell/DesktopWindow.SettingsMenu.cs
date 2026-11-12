using Themia.App.Services;
using Themia.App.Shell.Motion;
using Themia.Core.Model;
using Themia.Core.Motion;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>
/// [S-2300] [S-3534] The edit bar's settings pages on this display (General, Accounts, Appearance): what
/// they read and write, what they follow of Windows while one is open, and every way into them, which
/// lands on its row (page and row, the row lit for a moment). Opened and closed as every page is
/// (<see cref="OpenPage"/>).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>
    /// [S-3525] What Windows owns is read now and followed while the menu is open; [S-3559] so are
    /// the accounts and their numbers.
    /// </summary>
    private void SettingsMenuOpened()
    {
        _host.SettingsMenuOpened();
    }

    /// <summary>[S-3557] Closed: what was followed stops, and with the last menu every form empties.</summary>
    private void SettingsMenuClosed()
    {
        _host.SettingsMenuClosed();
    }

    /// <summary>
    /// [S-3573] [S-3558] [S-3534] Edit mode entered and the settings page that holds the row
    /// <paramref name="key"/> open on this display at it (its section's key opens it at the section): where
    /// "See plans" and a widget's way to its accounts land. Whether it opened.
    /// </summary>
    public bool RequestSettings(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_closed)
        {
            return false;
        }

        _host.SetEditMode(true);
        var host = new SurfaceSettingsHost(this);
        foreach (var page in new[] { SettingsPages.General(host), SettingsPages.Accounts(host), SettingsPages.Appearance(host) })
        {
            if (SettingsPages.PathTo(page, key) is { } path)
            {
                OpenPage(page.Id, path);
                return _openPage == page.Id;
            }
        }

        Core.Diagnostics.Log.Warn(Scope, $"no settings page holds '{key}'");
        return false;
    }

    /// <summary>
    /// [S-66] Themia started again by hand: this display's surface takes the foreground (the copy
    /// that stood down handed it the right) and the General page opens at its top, the keyboard in it; one
    /// already open starts again at its top. Reached from the command window's message.
    /// </summary>
    internal void OpenSettingsOnStart()
    {
        try
        {
            if (_closed)
            {
                return;
            }

            if (Interop.Win32.GetForegroundWindow() != _hwnd && !Interop.Win32.SetForegroundWindow(_hwnd))
            {
                Core.Diagnostics.Log.Info(Scope, "could not take the foreground for the settings menu; typing will not reach it");
            }

            if (_openPage == SettingsPageId.General)
            {
                CloseFlyout(focusEntry: false);
            }

            _host.SetEditMode(true);
            OpenPage(SettingsPageId.General, []);
            if (_openPage != SettingsPageId.General)
            {
                Core.Diagnostics.Log.Warn(Scope, "Themia was started again, but the settings menu did not open");
            }
        }
        catch (Exception ex)
        {
            // Reached from a window message: nothing above may see it.
            Core.Diagnostics.Log.Error(Scope, "could not open the settings menu for a second start", ex);
        }
    }

    /// <summary>
    /// [S-3576] ⚙ open at <paramref name="path"/> on this display, for a question asked once Windows'
    /// dialog has gone (an import's): opened again where it was closed meanwhile. Whether it is open.
    /// </summary>
    private bool SettingsOpenAt(string key) => _openPage == SettingsPageId.General || RequestSettings(key);

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

        if ((AppServices.Accounts?.Accounts.Count ?? 0) == 0)
        {
            return RequestSettings(SettingsMenu.Keys.AddAccount);
        }

        _host.SetEditMode(true);
        OpenPage(SettingsPageId.Accounts, []);
        return _openPage == SettingsPageId.Accounts;
    }

    /// <summary>
    /// [S-3749] A widget's link row: ⚙ open on this display at the level that owns the setting (the
    /// accounts, a regional format's level, a cadence's), the properties menu away while it is open.
    /// Whether it opened.
    /// </summary>
    public bool RequestSetting(SettingLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        return link.Setting switch
        {
            OwnedSetting.Accounts => RequestAccounts(),
            OwnedSetting.OnlineUpdates => RequestSettings(link.Cadence is { } row ? SettingsMenu.Keys.Online + "/" + row.Id.Value : SettingsMenu.Keys.Online),
            _ => RequestSettings(SettingsMenu.Keys.Format(link.Setting)),
        };
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



        public bool Suspended => Host.Suspended;

        public bool AnimationsEnabled => SystemMotion.AnimationsEnabled;

        public void Mutate(Action<AppState> change, bool relayout = true) => Host.Mutate(change, relayout);

        public void SetStartup(bool on) => Host.SetStartup(on);

        public void SetDesktop(DesktopShows shows) => Host.SetDesktop(shows);

        public Core.Desktop.DesktopHolder DesktopHolder => Host.DesktopHolder;

        public void SetGameModeSuspend(bool on) => Host.SetGameModeSuspend(on);

        public void ReplayIntro(IntroMode mode) => Host.ReplayIntro(mode);

        public IAccountsMenuHost? Accounts => AppServices.Accounts is null ? null : Host.AccountsMenu;

        public Core.Localization.Locale WindowsLanguage => Host.WindowsLanguage;

        public void SetLanguage(AppLanguage language) => Host.SetLanguage(language);

        public void Confirm(SettingsConfirm confirm) => surface.ConfirmOnFlyout(confirm);

        public void Quit() => Host.RequestExit();

        public ILicenceMenuHost Licence => this;

        public IBackupMenuHost Backup => this;

        public IAboutMenuHost About => this;

        public IMcpMenuHost? Mcp => Host.McpMenu;

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
            if (surface.SettingsOpenAt(SettingsMenu.Keys.Backup))
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
