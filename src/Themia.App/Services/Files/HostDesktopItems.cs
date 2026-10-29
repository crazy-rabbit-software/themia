using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-5540]..[S-5549] What the desktop's gestures need of the host beyond the listing: the one
/// selection over every display's item widgets, the shell's menus and operations, the drags and the
/// rename box. The app host is it (<c>AppHost.DesktopDesk.cs</c>); a debug render has none, so its
/// previews act on nothing.
/// </summary>
internal interface IDesktopDesk
{
    bool IsSelected(string key);

    bool ShowsFocus(string key);

    bool Active { get; }

    string? RenamingWidget { get; }

    string? DropTargetWidget { get; }

    void Press(string key, bool ctrl, bool shift);

    void Release(string key);

    void Context(string key, int screenX, int screenY, nint owner);

    bool Key(WidgetKey key, bool shift, string? widgetId);

    bool Type(char character);

    void Open(string key);

    void DragOut(string key, nint window);

    bool Dragging(string key);

    void SetDropTarget(string? widgetId);

    string? CommitRename(string key, string text);

    void CancelRename();

    bool IsFocused(string key);

    (int Position, int Count)? PositionOf(string key);

    string? RenameTo(string key, string name);
}

/// <summary>
/// [S-5510]..[S-5512] [S-5530] <see cref="IWidgetHost.DesktopItems"/>: the host's one listing of the
/// desktop's namespace, read off the UI thread and read again when the shell says the desktop changed
/// (never on a clock), the items' overlaid icons, and the desktop's gestures handed to the host's
/// <see cref="IDesktopDesk"/> (the one selection, [S-5540]).
/// <para>
/// Running only while the desktop shows Themia's items (<see cref="Start"/>, <see cref="Stop"/>); a
/// debug render never starts it, so its previews draw the items they are given and never read the
/// user's desktop. Reads are coalesced without a timer: one runs at a time, and a change heard while it
/// runs queues exactly one more. UI thread, except the read itself.
/// </para>
/// </summary>
internal sealed class HostDesktopItems : IDesktopItems
{
    private const string Scope = "desktop-items";
    private const int IconCap = 512;

    public static HostDesktopItems Instance { get; } = new();

    private LoadCache<ImageSource> _icons = new(IconCap, StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Old, string New)> _renames = [];
    private IReadOnlyList<DesktopItem> _items = [];
    private Dictionary<string, DesktopItem> _byKey = new(DesktopItemKeys.Comparer);
    private DesktopNotifications? _notifications;
    private DispatcherQueue? _dispatcher;
    private bool _reading;
    private bool _again;
    private int _generation;

    private HostDesktopItems()
    {
    }

    /// <inheritdoc />
    public IReadOnlyList<DesktopItem> Items => _items;

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>
    /// [S-5520] A read came back: the listing, and the renames the shell reported since the last one,
    /// for the reconcile. UI thread.
    /// </summary>
    public event Action<IReadOnlyList<DesktopItem>, IReadOnlyList<(string Old, string New)>>? Listed;

    /// <summary>Whether a listing is held (a read succeeded since <see cref="Start"/>).</summary>
    public bool HasListing { get; private set; }

    /// <summary>The host's side of the gestures, installed at start; null in a debug render.</summary>
    public IDesktopDesk? Desk { get; set; }

    /// <inheritdoc />
    public DesktopItem? Find(string? key) => key is not null && _byKey.TryGetValue(key, out var item) ? item : null;

    /// <inheritdoc />
    public bool IsSelected(string key) => Desk?.IsSelected(key) == true;

    /// <inheritdoc />
    public bool ShowsFocus(string key) => Desk?.ShowsFocus(key) == true;

    /// <inheritdoc />
    public bool Active => Desk?.Active == true;

    /// <inheritdoc />
    public bool IsCut(string key) => HostFileClipboard.Instance.IsCut(key);

    /// <inheritdoc />
    public string? RenamingWidget => Desk?.RenamingWidget;

    /// <inheritdoc />
    public string? DropTargetWidget => Desk?.DropTargetWidget;

    /// <inheritdoc />
    public void Press(string key, bool ctrl, bool shift) => Desk?.Press(key, ctrl, shift);

    /// <inheritdoc />
    public void Release(string key) => Desk?.Release(key);

    /// <inheritdoc />
    public void Context(string key, int screenX, int screenY, nint owner) => Desk?.Context(key, screenX, screenY, owner);

    /// <inheritdoc />
    public bool Key(WidgetKey key, bool shift, string? widgetId) => Desk?.Key(key, shift, widgetId) == true;

    /// <inheritdoc />
    public bool Type(char character) => Desk?.Type(character) == true;

    /// <inheritdoc />
    public void Open(string key) => Desk?.Open(key);

    /// <inheritdoc />
    public void DragOut(string key, nint window) => Desk?.DragOut(key, window);

    /// <inheritdoc />
    public bool Dragging(string key) => Desk?.Dragging(key) == true;

    /// <inheritdoc />
    public void SetDropTarget(string? widgetId) => Desk?.SetDropTarget(widgetId);

    /// <inheritdoc />
    public string? CommitRename(string key, string text) => Desk?.CommitRename(key, text);

    /// <inheritdoc />
    public void CancelRename() => Desk?.CancelRename();

    /// <inheritdoc/>
    public bool IsFocused(string key) => Desk?.IsFocused(key) == true;

    /// <inheritdoc/>
    public (int Position, int Count)? PositionOf(string key) => Desk?.PositionOf(key);

    /// <inheritdoc/>
    public string? RenameTo(string key, string name) => Desk?.RenameTo(key, name);

    /// <summary>The selection, a rename box or a drag's target changed: every item widget redraws. UI thread.</summary>
    public void Repaint() => Raise();

    /// <summary>[S-5545] [S-5547] F5, or Explorer's view settings changed: the desktop is read again (coalesced as any read).</summary>
    public void Refresh()
    {
        if (_notifications is not null)
        {
            Read();
        }
    }

    /// <inheritdoc />
    public bool TryIcon(DesktopItem item, int pixels, out ImageSource? image) => _icons.TryGet(IconKey(item, pixels), out image);

    /// <inheritdoc />
    public Task<ImageSource?> IconAsync(DesktopItem item, int pixels)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _icons.GetAsync(IconKey(item, pixels), () => AppIcons.LoadOverlaidAsync(item.Key, item.Name, item.Kind, pixels));
    }

    private static string IconKey(DesktopItem item, int pixels)
        => string.Concat(pixels.ToString(System.Globalization.CultureInfo.InvariantCulture), "|", item.Key);

    /// <summary>
    /// [S-5511] Follow the desktop: register for the shell's notifications on <paramref name="window"/>
    /// (the command window, which hands each one to <see cref="OnNotification"/>) and read it now.
    /// Twice is once. UI thread.
    /// </summary>
    public void Start(nint window, DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        if (_notifications is null && window != 0)
        {
            _notifications = DesktopNotifications.Register(window);
        }

        Read();
    }

    /// <summary>
    /// [S-5506] Stop following: the registration goes, the listing and the selection empty (a read
    /// still running is dropped when it lands), and the widgets hear it. UI thread.
    /// </summary>
    public void Stop()
    {
        _notifications?.Dispose();
        _notifications = null;
        _generation++;
        _again = false;
        _renames.Clear();
        HasListing = false;
        Adopt([]);
    }

    /// <summary>[S-5511] One notification, in the window procedure: a rename's names kept, stale icons dropped, a read queued.</summary>
    public void OnNotification(nint wParam, nint lParam)
    {
        if (_notifications is null)
        {
            return;
        }

        var change = DesktopNotifications.Read(wParam, lParam);
        if (change.Renamed is { } renamed)
        {
            _renames.Add(renamed);
        }

        // The shell's icons changed (an association, an overlay's state): every drawn icon is drawn again.
        if (change.IconsChanged)
        {
            _icons = new(IconCap, StringComparer.OrdinalIgnoreCase);
        }

        Read();
    }

    /// <summary>Read the namespace off the UI thread; while one read runs, a second request queues one more.</summary>
    private void Read()
    {
        if (_dispatcher is not { } dispatcher)
        {
            return;
        }

        if (_reading)
        {
            _again = true;
            return;
        }

        _reading = true;
        var generation = _generation;
        _ = Task.Run(() =>
        {
            IReadOnlyList<DesktopItem>? read;
            try
            {
                read = DesktopNamespace.Read();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "reading the desktop threw", ex);
                read = null;
            }

            if (!dispatcher.TryEnqueue(() => Landed(read, generation)))
            {
                Log.Warn(Scope, "could not bring the desktop's items back to the UI thread; they are read again on the next change");
            }
        });
    }

    private void Landed(IReadOnlyList<DesktopItem>? read, int generation)
    {
        _reading = false;
        if (generation != _generation)
        {
            return;
        }

        if (read is not null)
        {
            HasListing = true;
            var renames = _renames.ToList();
            _renames.Clear();
            Adopt(read);
            Listed?.Invoke(read, renames);
        }

        if (_again)
        {
            _again = false;
            Read();
        }
    }

    private void Adopt(IReadOnlyList<DesktopItem> items)
    {
        _items = items;
        _byKey = new Dictionary<string, DesktopItem>(DesktopItemKeys.Comparer);
        foreach (var item in items)
        {
            _byKey.TryAdd(item.Key, item);
        }

        Raise();
    }

    private void Raise()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a desktop items listener threw", ex);
        }
    }
}
