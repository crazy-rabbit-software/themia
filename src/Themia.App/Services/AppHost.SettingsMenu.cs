using Themia.App.Interop;
using Themia.App.Shell.Config;
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

    /// <summary>[S-3533] The settings window on its first page (temporary: the window goes when its last pages have moved).</summary>
    public void ShowSettings() => ShowConfig(new ConfigTarget.App());

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
