using Themia.App.Interop;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Services;

/// <summary>
/// [S-3525] What the edit bar's settings menu shows that Windows owns: start with Windows and the
/// desktop icons. Read when a menu opens and followed while one is open, never trusted from what
/// Themia last wrote; every display's menu reads the one answer.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>[S-3525] The desktop icons' watch while a settings menu is open, and how many are.</summary>
    private IDisposable? _iconWatch;

    private int _iconWatchers;

    /// <summary>[S-3550] What ⚙ › Accounts reads and does, the same on every display.</summary>
    public Themia.App.Services.Accounts.AccountsMenuHost AccountsMenu { get; }

    /// <summary>[S-935] What Windows said of start with Windows when it was last read or changed; null until then.</summary>
    public AutostartStatus? StartupStatus { get; private set; }

    /// <summary>
    /// [S-3531] Something a settings menu shows that is not the state changed (what Windows said of
    /// start with Windows, its desktop icons): the open menus show it. Raised on the UI thread.
    /// </summary>
    public event Action? SettingsOutdated;

    /// <summary>[S-3570] What ⚙ › Themia Pro reads and does, the same on every display.</summary>
    public Licensing.LicenceMenu LicenceMenu { get; }

    /// <summary>[S-3575] What ⚙ › Back up &amp; reset does to the settings file.</summary>
    public SettingsBackup Backup { get; }

    /// <summary>[S-3572] [S-3576] How what ⚙'s lines started went, said under each, on every display.</summary>
    public Themia.Core.Settings.SettingsOutcomes SettingsOutcomes { get; } = new();

    /// <summary>[S-3582] A loose build's "Also delete my widgets, settings and accounts", unticked again with the last menu.</summary>
    public bool UninstallDeletesData { get; private set; }

    /// <summary>How many displays have their settings menu open.</summary>
    private int _settingsMenus;

    /// <summary>
    /// [S-3525] [S-3559] A display's settings menu opened: what Windows owns is read now, the
    /// accounts are followed, and [S-3572] the Store is asked for Pro's price.
    /// </summary>
    public void SettingsMenuOpened()
    {
        _settingsMenus++;
        ReadStartup();
        AccountsMenu.Opened();
        LicenceMenu.Opened();
    }

    /// <summary>
    /// [S-3557] A display's settings menu closed; with the last one every account form empties,
    /// [S-3573] the refusal has been said, what stood under the lines goes and the uninstall's
    /// "also delete" is unticked again.
    /// </summary>
    public void SettingsMenuClosed()
    {
        AccountsMenu.Closed();
        if (_settingsMenus == 0 || --_settingsMenus > 0)
        {
            return;
        }

        LicenceMenu.Closed();
        UninstallDeletesData = false;
        SettingsOutcomes.Clear();
    }

    /// <summary>[S-1553] Tick or untick the uninstall's "also delete my data"; the open menus show it.</summary>
    public void SetUninstallDeletesData(bool on)
    {
        UninstallDeletesData = on;
        SettingsOutdated?.Invoke();
    }

    /// <summary>[S-936] Read start with Windows afresh: the user may have switched Themia off in Task Manager since.</summary>
    public async void ReadStartup()
    {
        try
        {
            AdoptStartup(await Autostart.ReadAsync().ConfigureAwait(true));
        }
        catch (Exception ex)
        {
            // An async void: nothing may escape.
            Log.Error(Scope, "reading the startup setting threw", ex);
        }
    }

    /// <summary>[S-933] [S-935] Turn start with Windows on or off; what Windows reached is what the menus show.</summary>
    public async void SetStartup(bool on)
    {
        try
        {
            AdoptStartup(await Autostart.SetAsync(on).ConfigureAwait(true));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "changing the startup setting threw", ex);
        }
    }

    /// <summary>[S-935] What Windows said, shown; the stored value follows a state that could be read.</summary>
    private void AdoptStartup(AutostartStatus status)
    {
        StartupStatus = status;
        if (status.State != AutostartState.Unknown && State.Design.RunAtStartup != status.WillStart)
        {
            Mutate(s => s.Design.RunAtStartup = status.WillStart, relayout: false);
        }

        SettingsOutdated?.Invoke();
    }

    /// <summary>[S-930] Whether the desktop icons show, as Windows has them (the cache where the shell cannot be read).</summary>
    public bool DesktopIconsShown => DesktopIconRules.Shown(DesktopIcons.AreVisible(), State.Preferences.DesktopIconsVisible);

    /// <summary>[S-930] [S-931] Ask Windows to show or hide its icons; the menus follow once it has.</summary>
    public void SetDesktopIcons(bool shown) => DesktopIcons.Request(shown);

    /// <summary>
    /// [S-932] Follow the desktop icons while a settings menu is open: shown or hidden from anywhere
    /// (the desktop's own menu included), the open menus show it. Dispose when the menu closes.
    /// </summary>
    public IDisposable WatchDesktopIcons()
    {
        if (_iconWatchers++ == 0)
        {
            _iconWatch = DesktopIcons.Watch(AdoptDesktopIcons);
            DesktopIcons.Settled += OnDesktopIconsSettled;
        }

        AdoptDesktopIcons();
        return new IconWatcher(this);
    }

    private void StopWatchingDesktopIcons()
    {
        if (_iconWatchers == 0 || --_iconWatchers > 0)
        {
            return;
        }

        DesktopIcons.Settled -= OnDesktopIconsSettled;
        _iconWatch?.Dispose();
        _iconWatch = null;
    }

    /// <summary>[S-932] The live state adopted: the cache follows Windows, and the menus show it.</summary>
    private void AdoptDesktopIcons()
    {
        // While a request is being applied the list shows and hides on the way; Settled reads the end.
        if (DesktopIcons.Busy)
        {
            return;
        }

        if (DesktopIconRules.Adopt(DesktopIcons.AreVisible(), State.Preferences.DesktopIconsVisible) is bool cached)
        {
            Mutate(s => s.Preferences.DesktopIconsVisible = cached, relayout: false);
        }

        SettingsOutdated?.Invoke();
    }

    /// <summary>[S-939] Raised on the worker thread: the icons are read on the UI thread.</summary>
    private void OnDesktopIconsSettled()
    {
        if (!_dispatcher.TryEnqueue(AdoptDesktopIcons))
        {
            Log.Debug("desktop-icons", "could not queue reading the icons; the menu reads them when it opens again");
        }
    }

    /// <summary>One open menu's hold on the desktop icons' watch.</summary>
    private sealed class IconWatcher(AppHost host) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                host.StopWatchingDesktopIcons();
            }
        }
    }
}
