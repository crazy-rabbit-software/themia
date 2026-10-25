using Microsoft.UI.Dispatching;
using Themia.App.Services;
using Themia.App.Services.Toggles;
using Themia.App.Services.Web;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>
/// [S-6913] The host as a card's view sees it: the card's surface decides the ticker and whether it
/// is a preview; the launcher and ⚙ › Accounts are the app's one of each.
/// </summary>
internal sealed class CardWidgetHost(IWidgetCardHost card) : IWidgetHost
{
    public Ticker Ticker => card.Ticker;

    public DispatcherQueue Dispatcher { get; } = DispatcherQueue.GetForCurrentThread()
        ?? throw new InvalidOperationException("a widget card is built on the UI thread");

    public IShellLauncher Launcher => HostLauncher.Instance;

    public bool IsPreview => card.IsPreview;

    public IRecycleBin RecycleBin => HostRecycleBin.Instance;

    /// <summary>[S-1251] Inert unless the card is a real widget on the desktop.</summary>
    public IPowerBackend Power => card.IsPreview ? InertPowerActions.Instance : Win32PowerActions.Instance;

    public IQuickToggles Toggles => HostToggles.Instance;

    public IWeatherAnswers Weather => HostWeatherAnswers.Instance;

    public IQuoteAnswers Quotes => HostQuoteAnswers.Instance;

    public IRateAnswers Rates => HostRateAnswers.Instance;

    public IFeedAnswers Feeds => HostFeedAnswers.Instance;

    public Themia.App.Services.Office.IOfficeSource? Office => Themia.App.Services.Office.OfficeSources.Current;

    public Themia.App.Services.Tasks.ITaskSource? Tasks => Themia.App.Services.AppServices.Tasks;

    public Themia.App.Services.Mail.IMailSource? Mail => Themia.App.Services.AppServices.Mail;

    /// <summary>[S-9925] The host's one service sync; a preview card replays the providers' recorded answers ([S-9929]).</summary>
    public IServiceSources Services => Themia.App.Services.Kit.HostServiceSources.For(card);

    public ISystemFeeds? System => AppServices.System;

    public IDrives Drives => Themia.App.Services.Drives.DriveService.Shared;

    public IAudioOutputs Audio => HostAudioOutputs.Instance;

    public IBrightness Brightness => Themia.App.Services.Brightness.HostBrightness.Instance;

    public IMediaSessions Media => HostMediaSessions.Instance;

    public IAudioCapture Loopback => HostAudioCapture.Instance;

    public IClipboardHistory? ClipboardHistory => AppServices.Clipboard;

    public IAlarms? Alarms => AppServices.Alarms;

    public IFocusSession Focus => Themia.App.Services.Focus.HostFocusSession.Instance;

    public INotifications Notifications => Themia.App.Services.Notifications.HostNotifications.Instance;

    public INotes? Notes => AppServices.Notes;

    public IHabitLists? Habits => AppServices.Habits;

    public IScreenTimeSource? ScreenTime => AppServices.ScreenTime;

    public IGalleries? Gallery => AppServices.Gallery;

    public bool CopyText(string text) => ClipboardWatcher.SetText(text);

    public IFileIcons FileIcons => Themia.App.Services.Files.HostFileIcons.Instance;

    public IFileClipboard FileClipboard => Themia.App.Services.Files.HostFileClipboard.Instance;

    public IFolders Folders => Themia.App.Services.Files.HostFolders.Instance;

    public IShellFileOps FileOps => Themia.App.Services.Files.HostFileOps.Instance;

    public IExplorerMenus ExplorerMenus => Themia.App.Services.Files.HostExplorerMenus.Instance;

    public IRecentItems RecentFiles => Themia.App.Services.Files.HostRecentItems.Instance;

    public IAppLayouts AppLayouts => HostAppLayouts.Instance;

    public IVideoStreams Streams => HostVideoStreams.Instance;

    public IInstalledApps InstalledApps => HostInstalledApps.Instance;

    public IFavicons Favicons => HostFavicons.Instance;

    /// <summary>[S-3558] ⚙ › Accounts on the card's display.</summary>
    public bool ShowAccounts() => card.RequestAccounts();
}
