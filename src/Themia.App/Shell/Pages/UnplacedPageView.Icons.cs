using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Toggles;
using Themia.App.Services.Web;
using Themia.Core.Files;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-13707] The page's icons are the desktop's own icon view (the desktop item kind's, <see cref="IDesktopIconView"/>),
/// so each is drawn exactly as the desktop draws it: the icon over its name in the desktop's cell at the icon size in
/// force, the shell's icon with its overlay, the name on the wallpaper, the hover and the desktop icon's selection look.
/// Its host is a preview's (it acts on nothing itself: the page takes the gestures), whose desktop is the page's: the
/// listing is the page's items, and the selection it paints is the page's own (<see cref="ItemSelection"/>), never the
/// desktop's.
/// </summary>
internal sealed partial class UnplacedPageView
{
    /// <summary>[S-13707] A preview card's host, but the desktop it reads is the page's (<see cref="IconDesk"/>).</summary>
    private sealed class IconHost(IWidgetHost inner, IDesktopItems desk) : IWidgetHost
    {
        public Ticker Ticker => inner.Ticker;

        public DispatcherQueue Dispatcher => inner.Dispatcher;

        public IShellLauncher Launcher => inner.Launcher;

        /// <summary>The icons act on nothing themselves: the page has their press, their drag and their keys.</summary>
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

    /// <summary>
    /// [S-13707] The desktop as the page's icons read it: its items the page's (named and iconed as the shell has them),
    /// a cut item faded as on the desktop, and the page's selection painted in the desktop icon's look. No gesture reaches
    /// it (the icons' host is a preview's); each says so.
    /// </summary>
    private sealed class IconDesk(UnplacedPageView page, IDesktopItems desk) : IDesktopItems
    {
        private event Action? Painted;

        public IReadOnlyList<DesktopItem> Items => page._model.Items;

        public DesktopItem? Find(string? key)
            => key is null ? null : page._model.Items.FirstOrDefault(i => DesktopItemKeys.Comparer.Equals(i.Key, key)) ?? desk.Find(key);

        /// <summary>The page painted its selection again, or the desktop's listing changed (a name, an icon).</summary>
        public event Action? Changed
        {
            add
            {
                Painted += value;
                desk.Changed += value;
            }

            remove
            {
                Painted -= value;
                desk.Changed -= value;
            }
        }

        /// <summary>Every icon paints the page's selection as it is now.</summary>
        public void Paint() => Painted?.Invoke();

        public bool IsSelected(string key) => page._selection.IsSelected(key);

        public bool ShowsFocus(string key) => page._selection.IsFocus(key) && page._selection.Cues;

        public bool Active => page.Active;

        public bool IsFocused(string key) => false;

        public (int Position, int Count)? PositionOf(string key)
            => page._selection.IndexOf(key) is >= 0 and var at ? (at + 1, page._shown.Count) : null;

        public bool IsCut(string key) => desk.IsCut(key);

        public string? RenamingWidget => null;

        public IDesktopFences Fences => desk.Fences;

        public string? DropTargetWidget => null;

        public void Press(string key, bool ctrl, bool shift, string? widgetId)
        {
            // ignore: the icons act on nothing (a preview's host); the page takes their press.
        }

        public void Release(string key)
        {
            // ignore: as above
        }

        public void Context(string key, int screenX, int screenY, nint owner, bool classic)
        {
            // ignore: the page has no menu of the shell's for an item not on the view
        }

        public bool Key(WidgetKey key, bool shift, string? widgetId) => false;

        public bool Type(char character) => false;

        public void Open(string key)
        {
            // ignore: a double-click on the page places the item ([S-13703]); the page takes it
        }

        public ItemDragOffer? BeginDrag(string key, nint window) => null;

        public DragPicture? DragPicture(ItemDragOffer drag) => null;

        public void EndDrag(ItemDragOffer drag)
        {
            // ignore: no drag begins here ([S-13705] is the page's placement)
        }

        public bool Dragging(string key) => false;

        public void SetDropTarget(string? widgetId)
        {
            // ignore: an icon on the page takes no drop
        }

        public string? CommitRename(string key, string text) => null;

        public void CancelRename()
        {
            // ignore: no rename box opens on the page
        }

        public string? RenameTo(string key, string name) => null;

        public Task<DesktopItemFacts?> FactsAsync(DesktopItem item) => desk.FactsAsync(item);

        public bool TryIcon(DesktopItem item, int pixels, out ImageSource? image) => desk.TryIcon(item, pixels, out image);

        public Task<ImageSource?> IconAsync(DesktopItem item, int pixels) => page._commands.Icon(item, pixels);
    }
}
