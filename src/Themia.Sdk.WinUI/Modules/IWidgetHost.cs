using Microsoft.UI.Dispatching;
using Themia.App.Services;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6913] What a widget may ask of the host it runs in, handed to its module's
/// <see cref="IWidgetModule.CreateView"/>: the facade a view uses instead of reaching the host's
/// statics. One per card (the card's surface decides the ticker and whether it is a preview), every
/// member shared by all instances behind it, so however many widgets are on the desktop there is one
/// ticker, one launcher and one of each service.
/// <para>
/// Not here, and why: the notices are the SDK's own <c>Notices</c> (the host connects them to its
/// surfaces); the keyboard lease and text input are the card's (<see cref="Themia.App.Widgets.IWidgetSurface"/>,
/// through <see cref="Themia.App.Widgets.IWidgetSurfaceAware"/>); the theme and item fills are
/// <see cref="ThemeBrush"/> and <c>ItemFill</c>, installed once.
/// </para>
/// </summary>
public interface IWidgetHost
{
    /// <summary>The shared ticker every timing of the view comes from (the one <c>Attach</c> hands over too).</summary>
    Ticker Ticker { get; }

    /// <summary>The UI thread the view lives on: a service's callback is brought back here.</summary>
    DispatcherQueue Dispatcher { get; }

    /// <summary>The one place anything is opened.</summary>
    IShellLauncher Launcher { get; }

    /// <summary>[S-1251] The card is a preview (a debug render): a view whose buttons act on the machine is inert.</summary>
    bool IsPreview { get; }

    /// <summary>Open Settings › Accounts, for a widget whose empty state is "sign in". False where there is none to open (a debug render).</summary>
    bool ShowAccounts();

    /// <summary>[S-4863] The Recycle Bin: the one answer how full it is, and Windows' own empty.</summary>
    IRecycleBin RecycleBin { get; }

    /// <summary>
    /// [S-1243] [S-1251] What runs a power action: the machine's own calls on a desktop card, an inert
    /// backend that logs and runs nothing on a preview (a debug render). Asked when the view attaches.
    /// </summary>
    IPowerBackend Power { get; }

    /// <summary>[S-3907] The quick settings a toggle widget follows and switches: one shared source per toggle.</summary>
    IQuickToggles Toggles { get; }

    /// <summary>[S-2663] The open-meteo answers (conditions, forecast, air, rain) the background sync keeps fresh.</summary>
    IWeatherAnswers Weather { get; }

    /// <summary>[S-2663] The market quotes the background sync keeps fresh.</summary>
    IQuoteAnswers Quotes { get; }

    /// <summary>[S-5147] The ECB reference rates the background sync keeps fresh.</summary>
    IRateAnswers Rates { get; }

    /// <summary>
    /// [S-2500] The system figures (CPU, memory, disk, network, battery, GPU, processes, ping): the
    /// host's one hub. Null only before the host has started it, where there is nothing to read yet.
    /// </summary>
    ISystemFeeds? System { get; }

    /// <summary>[S-4655] The drives: the host's one walk every drive widget shares.</summary>
    IDrives Drives { get; }

    /// <summary>[S-440] [S-1220] The output devices, their levels and sessions: the host's one Core Audio worker.</summary>
    IAudioOutputs Audio { get; }

    /// <summary>[S-4441] Every display's brightness: the host's one worker.</summary>
    IBrightness Brightness { get; }

    /// <summary>[S-6913] Windows' own media session: what is playing, its transport and position.</summary>
    IMediaSessions Media { get; }

    /// <summary>[S-3203] What the speakers play: the host's one loopback capture, for the visualizer.</summary>
    IAudioCapture Loopback { get; }

    /// <summary>[S-1458] The app's one clipboard history. Null where the host keeps none (a debug render, or no Clipboard widget yet).</summary>
    IClipboardHistory? ClipboardHistory { get; }

    /// <summary>[S-4623] The alarm of every alarm widget: the host's one alarm service. Null where it runs none (a debug render).</summary>
    IAlarms? Alarms { get; }

    /// <summary>[S-4421] The app's one focus session, which holds Windows' do not disturb host-side while it runs.</summary>
    IFocusSession Focus { get; }

    /// <summary>[S-4408] Windows' notification list: the host's one feed.</summary>
    INotifications Notifications { get; }

    /// <summary>[S-1348] The notes kept on this PC: the host's one pool. Null where it keeps none (a debug render).</summary>
    INotes? Notes { get; }

    /// <summary>[S-1420] The habit lists kept on this PC: the host's one pool. Null where it keeps none.</summary>
    IHabitLists? Habits { get; }

    /// <summary>[S-458] The screen-time history: the host's one tracker. Null where it runs none (a debug render).</summary>
    IScreenTimeSource? ScreenTime { get; }

    /// <summary>[S-3410] Where every gallery gets its pictures: the host's one service. Null where it runs none.</summary>
    IGalleries? Gallery { get; }

    /// <summary>[S-1444] Put <paramref name="text"/> on the clipboard, exactly as given. False when Windows refused it (logged).</summary>
    bool CopyText(string text);

    /// <summary>[S-1324] A file's shell icon, thumbnail, type name and a folder's count: the host's one cache.</summary>
    IFileIcons FileIcons { get; }

    /// <summary>[S-1329] Files on the real Windows clipboard, interchangeable with Explorer.</summary>
    IFileClipboard FileClipboard { get; }

    /// <summary>[S-1321] A folder's listing and watch, its repository, Explorer's New file types, a ZIP.</summary>
    IFolders Folders { get; }

    /// <summary>[S-1330] The shell's own file operations (Explorer's dialogs and undo; delete always recycles) and a drag out.</summary>
    IShellFileOps FileOps { get; }

    /// <summary>[S-6416] File Explorer's own menus: the full shell menu, a verb, a file's Open with apps.</summary>
    IExplorerMenus ExplorerMenus { get; }

    /// <summary>[S-4841] Windows' Recent Items: the host's one list.</summary>
    IRecentItems RecentFiles { get; }

    /// <summary>[S-1411] The app-layout editor and the layout launch.</summary>
    IAppLayouts AppLayouts { get; }

    /// <summary>[S-471] Camera connections: MJPEG, snapshots, or a video for Windows' own player.</summary>
    Themia.App.Services.Web.IVideoStreams Streams { get; }

    /// <summary>The apps on the Start menu, for a Start-menu app dropped on a widget.</summary>
    IInstalledApps InstalledApps { get; }

    /// <summary>[S-1304] A site's own icon: the host's one cache.</summary>
    IFavicons Favicons { get; }
}
