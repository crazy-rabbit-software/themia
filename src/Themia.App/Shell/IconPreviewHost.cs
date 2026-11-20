using Microsoft.UI.Dispatching;
using Themia.App.Services;
using Themia.App.Services.Toggles;
using Themia.App.Services.Web;
using Themia.Core.Files;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-13707] The host of a desktop icon drawn where nothing acts through it: the Unplaced icons page's icons. A preview
/// card's host (<see cref="IsPreview"/>: the icon takes no gesture itself), but the desktop it reads is
/// <paramref name="desk"/>: the page's.
/// </summary>
internal sealed class IconPreviewHost(IWidgetHost inner, IDesktopItems desk) : IWidgetHost
{
    public Ticker Ticker => inner.Ticker;

    public DispatcherQueue Dispatcher => inner.Dispatcher;

    public IShellLauncher Launcher => inner.Launcher;

    /// <summary>The icon acts on nothing itself: the page has its press, its drag and its keys.</summary>
    public bool IsPreview => true;

    public bool ShowAccounts() => false;

    public IReadOnlyList<Themia.App.Services.Accounts.AccountInfo> AccountsWith(FeatureId feature) => inner.AccountsWith(feature);

    public IRecycleBin RecycleBin => inner.RecycleBin;

    public IPowerBackend Power => inner.Power;

    public IQuickToggles Toggles => inner.Toggles;

    public IWeatherAnswers Weather => inner.Weather;

    public IQuoteAnswers Quotes => inner.Quotes;

    public IRateAnswers Rates => inner.Rates;

    public IFeedAnswers Feeds => inner.Feeds;

    public Themia.App.Services.Office.IOfficeSource? Office => inner.Office;

    public Themia.App.Services.Mail.IMailSource? Mail => inner.Mail;

    public Themia.App.Services.Tasks.ITaskSource? Tasks => inner.Tasks;

    public IServiceSources Services => inner.Services;

    public ISystemFeeds? System => inner.System;

    public IDrives Drives => inner.Drives;

    public IDeviceBatteries DeviceBatteries => inner.DeviceBatteries;

    public IAudioOutputs Audio => inner.Audio;

    public IBrightness Brightness => inner.Brightness;

    public IMediaSessions Media => inner.Media;

    public IAudioCapture Loopback => inner.Loopback;

    public IClipboardHistory? ClipboardHistory => inner.ClipboardHistory;

    public IAlarms? Alarms => inner.Alarms;

    public IFocusSession Focus => inner.Focus;

    public INotifications Notifications => inner.Notifications;

    public INotes? Notes => inner.Notes;

    public IHabitLists? Habits => inner.Habits;

    public IScreenTimeSource? ScreenTime => inner.ScreenTime;

    public IGalleries? Gallery => inner.Gallery;

    public bool CopyText(string text) => false;

    public IFileIcons FileIcons => inner.FileIcons;

    public IDesktopItems DesktopItems => desk;

    public IFileClipboard FileClipboard => inner.FileClipboard;

    public IFolders Folders => inner.Folders;

    public IShellFileOps FileOps => inner.FileOps;

    public IExplorerMenus ExplorerMenus => inner.ExplorerMenus;

    public IRecentItems RecentFiles => inner.RecentFiles;

    public IAppLayouts AppLayouts => inner.AppLayouts;

    public IVideoStreams Streams => inner.Streams;

    public IInstalledApps InstalledApps => inner.InstalledApps;

    public IFavicons Favicons => inner.Favicons;
}
