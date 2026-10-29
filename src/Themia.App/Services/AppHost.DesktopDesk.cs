using Themia.App.Interop;
using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Foundation;

namespace Themia.App.Services;

/// <summary>
/// [S-5540]..[S-5549] [S-5591] The desktop's gestures outside edit mode, as Explorer's desktop has them:
/// the one selection over every display's item widgets (<see cref="DesktopSelection"/>), the keys, F2's
/// box, the shell's menus and file operations, the drags in, out and inside, and where an arrival
/// lands. The rules are Core's; this carries them out with the host's shell parts (the shell's menus,
/// <see cref="HostFileOps"/>, <see cref="HostFileClipboard"/>, <see cref="DragDropInterop"/>), never a
/// copy of them. UI thread.
/// </summary>
internal sealed partial class AppHost : IDesktopDesk
{
    /// <summary>[S-5545] Enter opens at most this many items at once; the rest are logged, not opened.</summary>
    private const int OpenAtMost = 15;

    private readonly DesktopSelection _desk = new();
    private readonly DesktopLandings _landings = new();
    private bool _deskActive;
    private string? _renamingWidget;
    private string? _renamingKey;
    private string? _dropTargetWidget;

    /// <summary>[S-5549] The drag running from the desktop: its keys, the surface it left and the item held.</summary>
    private (HashSet<string> Keys, DesktopWindow? Source, string Held)? _deskDrag;

    /// <summary>[S-5591] The last press on empty desktop: where a paste or New › lands.</summary>
    private (DesktopWindow Surface, double X, double Y)? _emptyPress;

    /// <summary>The first widget showing each key, and the surface it is on, as last laid out.</summary>
    private Dictionary<string, (DesktopWindow Surface, WidgetInstance Widget)> _deskWidgets = new(DesktopItemKeys.Comparer);

    // ---- IDesktopDesk: what the item widgets read --------------------------------------------------

    public bool IsSelected(string key) => _desk.IsSelected(key);

    public bool ShowsFocus(string key) => _desk.ShowsFocus(key);

    public bool Active => _deskActive;

    public string? RenamingWidget => _renamingWidget;

    public string? DropTargetWidget => _dropTargetWidget;

    /// <summary>[S-5570] The focused item, while the desktop has the keyboard: what Narrator is told has the focus.</summary>
    public bool IsFocused(string key) => _deskActive && DesktopItemKeys.Comparer.Equals(_desk.Focus, key);

    /// <summary>[S-5570] The item's place in the desktop's order ("3 of 12").</summary>
    public (int Position, int Count)? PositionOf(string key) => DesktopNav.PositionOf(_desk.Ordered, key);

    /// <summary>[S-5540] The desk follows the host: the listing, the layout and the clipboard's cut redraw every item widget.</summary>
    private void StartDesk()
    {
        HostDesktopItems.Instance.Desk = this;
        HostDesktopItems.Instance.Changed += OnDeskListingChanged;
        HostFileClipboard.Instance.Changed += RepaintDesk;
        StateChanged += OnDeskStateChanged;
        _command.SettingsChanged += HostDesktopItems.Instance.Refresh;
    }

    private void StopDesk()
    {
        _command.SettingsChanged -= HostDesktopItems.Instance.Refresh;
        StateChanged -= OnDeskStateChanged;
        HostFileClipboard.Instance.Changed -= RepaintDesk;
        HostDesktopItems.Instance.Changed -= OnDeskListingChanged;
        HostDesktopItems.Instance.Desk = null;
    }

    private void OnDeskListingChanged() => LayoutDesk();

    private void OnDeskStateChanged()
    {
        var before = _desk.Count;
        LayoutDesk();
        if (_desk.Count != before)
        {
            RepaintDesk();
        }
    }

    private static void RepaintDesk() => HostDesktopItems.Instance.Repaint();

    /// <summary>
    /// [S-5540] The item widgets as they sit now on every display, in physical screen pixels, handed to
    /// the selection (whose order is the desktop's), and the first widget of each key remembered.
    /// </summary>
    private void LayoutDesk()
    {
        var boxes = new List<DesktopBox>();
        var widgets = new Dictionary<string, (DesktopWindow, WidgetInstance)>(DesktopItemKeys.Comparer);
        if (ShowsDesktopItems)
        {
            var display = 0;
            foreach (var surface in _windows.Where(w => w.IsAlive).OrderBy(w => w.Monitor.Bounds.Left).ThenBy(w => w.Monitor.Bounds.Top))
            {
                var bounds = surface.Monitor.Bounds;
                foreach (var widget in State.ViewForMonitor(surface.Monitor.Id).Widgets)
                {
                    if (widget.Config is not IDesktopItemConfig { Item.Length: > 0 } config || HostDesktopItems.Instance.Find(config.Item) is null)
                    {
                        continue;
                    }

                    var r = widget.Rect;
                    boxes.Add(new DesktopBox(config.Item, display, bounds.Left + (r.X * bounds.Width), bounds.Top + (r.Y * bounds.Height), r.W * bounds.Width, r.H * bounds.Height));
                    widgets.TryAdd(config.Item, (surface, widget));
                }

                display++;
            }
        }

        _deskWidgets = widgets;
        _desk.SetLayout(boxes, key => HostDesktopItems.Instance.Find(key)?.Name);
    }

    // ---- presses ----------------------------------------------------------------------------------

    public void Press(string key, bool ctrl, bool shift)
    {
        CloseRenameFor(key);
        _landings.ClearOpen();
        LayoutDesk();
        _desk.Press(key, ctrl, shift);
        _deskActive = true;
        RepaintDesk();
    }

    public void Release(string key)
    {
        _desk.Release(key);
        RepaintDesk();
    }

    /// <summary>[S-5502] [S-5544] A left or right press on empty desktop: the selection goes (a band may follow), the desktop takes the keys.</summary>
    public void DesktopEmptyPressed(DesktopWindow surface, Point dip, bool ctrl, bool shift)
    {
        CloseRenameFor(null);
        _landings.ClearOpen();
        var metrics = surface.Metrics;
        _emptyPress = metrics.IsUsable ? (surface, dip.X / metrics.Width, dip.Y / metrics.Height) : null;
        LayoutDesk();
        if (!ctrl && !shift)
        {
            _desk.Clear();
        }

        DesktopTakeKeys(surface);
        _deskActive = true;
        RepaintDesk();
    }

    /// <summary>[S-5544] The band on <paramref name="surface"/> starts.</summary>
    public void DesktopBandBegin(bool ctrl, bool shift)
    {
        _desk.BeginBand(ctrl, shift);
        RepaintDesk();
    }

    /// <summary>[S-5544] The band covers <paramref name="band"/> (the surface's DIPs): the item widgets it touches there are the band's.</summary>
    public void DesktopBand(DesktopWindow surface, BandRect band)
    {
        var metrics = surface.Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var keys = new List<string>();
        var boxes = new List<ItemBox>();
        foreach (var widget in State.ViewForMonitor(surface.Monitor.Id).Widgets)
        {
            if (widget.Config is IDesktopItemConfig { Item.Length: > 0 } config && HostDesktopItems.Instance.Find(config.Item) is not null)
            {
                keys.Add(config.Item);
                boxes.Add(new ItemBox(widget.Rect.X * metrics.Width, widget.Rect.Y * metrics.Height, widget.Rect.W * metrics.Width, widget.Rect.H * metrics.Height));
            }
        }

        _desk.Band(SelectionBand.Hits(band, boxes).Select(i => keys[i]));
        RepaintDesk();
    }

    public void DesktopBandEnd() => _desk.EndBand();

    public void Context(string key, int screenX, int screenY, nint owner)
    {
        CloseRenameFor(null);
        LayoutDesk();
        _desk.Context(key);
        _deskActive = true;
        RepaintDesk();
        ShowItemMenu(screenX, screenY, owner);
    }

    /// <summary>
    /// [S-5548] The shell's own menu for the selection, every shell extension's lines with it (Send to,
    /// Open with, an archiver's); its Rename is the desktop's own box, not the shell's (the shell has no
    /// view of the desktop's here to rename in).
    /// </summary>
    private void ShowItemMenu(int screenX, int screenY, nint owner)
    {
        var keys = _desk.Selected;
        if (keys.Count == 0)
        {
            return;
        }

        try
        {
            ShellContextMenu.ShowForItems(keys, screenX, screenY, owner, verb =>
            {
                if (!string.Equals(verb, "rename", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                BeginRename(_desk.Focus, null);
                return true;
            });
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the desktop item menu failed", ex);
        }

        RepaintDesk();
    }

    /// <summary>
    /// [S-5548] A right-click on empty desktop: the desktop's own background menu (New ›, Paste,
    /// Display settings, Personalize, third-party lines). What a command there brings (a new folder, a
    /// paste) lands where the click was.
    /// </summary>
    public void DesktopBackgroundMenu(DesktopWindow surface, Point dip)
    {
        var metrics = surface.Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var scale = surface.Monitor.Bounds.Width / metrics.Width;
        var (x, y) = ShellContextMenu.ToScreen(surface.Hwnd, (int)Math.Round(dip.X * scale), (int)Math.Round(dip.Y * scale));
        try
        {
            var choice = ShellContextMenu.ShowForDesktop(x, y, surface.Hwnd, DesktopMenuLines());
            if (choice.Ran)
            {
                // [S-5552] What New › made lands at the click and opens its rename box.
                _landings.Expect(new DesktopLandingHint(surface.CurrentViewId, dip.X / metrics.Width, dip.Y / metrics.Height, [], Renames: choice.New));
            }
            else if (choice.Themia is { } command)
            {
                RunDesktopMenuCommand(surface, dip, command);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the desktop's background menu failed", ex);
        }
    }

    /// <summary>[S-5551] Themia's lines in the background menu, worded as the rest of Themia words them.</summary>
    internal static IReadOnlyList<(DesktopMenuCommand Command, string Text)> DesktopMenuLines()
        => [.. DesktopBackground.ThemiaLines.Select(command => (command, command switch
        {
            DesktopMenuCommand.AddWidget => Str.Shell_Toolbar_AddWidget.Text(),
            _ => Str.Desktop_Menu_Edit.Text(),
        }))];

    /// <summary>
    /// [S-5551] One of Themia's lines: the same commands the card menu's way in and the surface menu's
    /// Add widget run (edit mode on; the picker at the click once the surface is in edit mode).
    /// </summary>
    private void RunDesktopMenuCommand(DesktopWindow surface, Point dip, DesktopMenuCommand command)
    {
        SetEditMode(true);
        if (command != DesktopMenuCommand.AddWidget)
        {
            return;
        }

        if (!_dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => surface.OpenPickerFromDesktop(dip)))
        {
            Log.Warn(Scope, "could not queue Add widget from the desktop's menu; edit mode is on, the bar's Add widget opens it");
        }
    }

    // ---- keys -------------------------------------------------------------------------------------

    public bool Key(WidgetKey key, bool shift, string? widgetId)
    {
        LayoutDesk();
        switch (key)
        {
            case WidgetKey.Deactivate:
                // The keys went to another item widget or to empty desktop: the desktop still has them.
                if (KeyboardHolder?.Model.Config is IDesktopItemConfig || DesktopKeysSurface is not null)
                {
                    return true;
                }

                _desk.Deactivate();
                _deskActive = false;
                RepaintDesk();
                return true;
            case WidgetKey.Enter:
                OpenKeys(_desk.Selected);
                return true;
            case WidgetKey.Rename:
                return BeginRename(_desk.Focus, widgetId);
            case WidgetKey.Delete:
            case WidgetKey.PermanentDelete:
                DeleteSelection(permanent: key == WidgetKey.PermanentDelete);
                return true;
            case WidgetKey.Copy:
            case WidgetKey.Cut:
                CopySelection(cut: key == WidgetKey.Cut);
                return true;
            case WidgetKey.Paste:
                _ = PasteAsync();
                return true;
            case WidgetKey.Properties:
                if (_desk.Selected.Count > 0)
                {
                    HostFileOps.Instance.Properties(_desk.Selected);
                }

                return true;
            case WidgetKey.Refresh:
                HostDesktopItems.Instance.Refresh();
                return true;
            case WidgetKey.Menu:
                MenuFromKeys();
                return true;
        }

        if (!_desk.Key(key, shift))
        {
            return false;
        }

        _deskActive = true;
        RepaintDesk();
        return true;
    }

    public bool Type(char character)
    {
        LayoutDesk();
        if (!_desk.Type(character, Environment.TickCount64))
        {
            return false;
        }

        _deskActive = true;
        RepaintDesk();
        return true;
    }

    /// <summary>[S-5548] The Menu key or Shift+F10: the shell's menu for the selection at the focused item, else the background menu.</summary>
    private void MenuFromKeys()
    {
        var surface = KeyboardSurface ?? DesktopKeysSurface;
        if (surface is null)
        {
            return;
        }

        if (_desk.Selected.Count > 0 && _desk.Focus is { } focus && _desk.Ordered.FirstOrDefault(b => DesktopItemKeys.Comparer.Equals(b.Key, focus)) is { Key: not null } box)
        {
            ShowItemMenu((int)box.CenterX, (int)box.CenterY, surface.Hwnd);
            return;
        }

        var metrics = surface.Metrics;
        DesktopBackgroundMenu(surface, new Point(metrics.Width / 2, metrics.Height / 2));
    }

    // ---- commands ---------------------------------------------------------------------------------

    public void Open(string key)
    {
        LayoutDesk();
        OpenKeys(_desk.IsSelected(key) ? _desk.Selected : [key]);
    }

    /// <summary>[S-5530] [S-5545] Open items through the launcher (a shell item by its shell name), at most <see cref="OpenAtMost"/> at once.</summary>
    private static void OpenKeys(IReadOnlyList<string> keys)
    {
        foreach (var key in keys.Take(OpenAtMost))
        {
            var outcome = HostLauncher.Instance.Open(DesktopItemKeys.LaunchTarget(key));
            if (outcome != LaunchOutcome.Opened)
            {
                Log.Warn(Scope, $"a desktop item did not open ({outcome})");
            }
        }

        if (keys.Count > OpenAtMost)
        {
            Log.Info(Scope, $"{keys.Count - OpenAtMost} more selected desktop item(s) were not opened at once");
        }
    }

    /// <summary>The files of the selection: the shell items (This PC, the Recycle Bin) are no files to copy or delete.</summary>
    private List<string> SelectedFiles() => [.. _desk.Selected.Where(k => !DesktopItemKeys.IsShellItem(k))];

    private nint DeskOwner => (KeyboardSurface ?? DesktopKeysSurface ?? _windows.FirstOrDefault(w => w.IsAlive))?.Hwnd ?? 0;

    /// <summary>
    /// [S-5545] Delete and Shift+Delete: the shell's own "delete" on the selected files, with the shell's
    /// own prompt as the user set it up (the Recycle Bin, or for good with Shift), as Explorer's desktop.
    /// </summary>
    private void DeleteSelection(bool permanent)
    {
        var files = SelectedFiles();
        if (files.Count == 0)
        {
            return;
        }

        try
        {
            if (!ShellContextMenu.InvokeVerb(files, "delete", DeskOwner, shift: permanent))
            {
                Log.Warn(Scope, $"the shell did not delete {files.Count} desktop item(s)");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "deleting desktop items failed", ex);
        }
    }

    /// <summary>[S-5545] Ctrl+C and Ctrl+X: the selected files on the Windows clipboard, as Explorer puts them there (a cut is drawn faded).</summary>
    private void CopySelection(bool cut)
    {
        var files = SelectedFiles();
        if (files.Count > 0 && !HostFileClipboard.Instance.SetFiles(files, cut))
        {
            Log.Warn(Scope, "the desktop selection could not be put on the clipboard");
        }

        RepaintDesk();
    }

    /// <summary>
    /// [S-5545] [S-5591] Ctrl+V: what the clipboard holds into the user's desktop folder through the shell's
    /// copy or move (a cut empties the clipboard after), its arrivals landing in the view of the display
    /// that has the keys, at the last press on its empty desktop, else at the next free cell.
    /// </summary>
    private async Task PasteAsync()
    {
        try
        {
            if (DesktopFolderPath() is not { } desktop || HostFileClipboard.Instance.Read() is not { } content)
            {
                return;
            }

            var surface = KeyboardSurface ?? DesktopKeysSurface ?? _windows.FirstOrDefault(w => w.IsAlive && w.Monitor.IsPrimary);
            if (surface is not null)
            {
                var at = _emptyPress is { } press && ReferenceEquals(press.Surface, surface) ? press : default;
                _landings.Expect(new DesktopLandingHint(
                    surface.CurrentViewId,
                    at.Surface is null ? null : at.X,
                    at.Surface is null ? null : at.Y,
                    content.Paths.Count > 0 ? [.. content.Paths.Select(Path.GetFileName).OfType<string>()] : []));
            }

            if (content.Paths.Count > 0)
            {
                if (content.Effect == DropEffect.Move)
                {
                    if (await HostFileOps.Instance.MoveAsync(content.Paths, desktop).ConfigureAwait(true) == FileOpOutcome.Done)
                    {
                        HostFileClipboard.Instance.CompleteCut();
                    }
                }
                else
                {
                    await HostFileOps.Instance.CopyAsync(content.Paths, desktop).ConfigureAwait(true);
                }
            }
            else if (content.VirtualFiles > 0)
            {
                HostFileClipboard.Instance.SaveVirtualFiles(desktop);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "pasting onto the desktop failed", ex);
        }
    }

    /// <summary>The user's own desktop folder (wherever it is redirected), or null (logged).</summary>
    private static string? DesktopFolderPath()
    {
        var path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(path))
        {
            Log.Warn(Scope, "Windows reports no Desktop folder; nothing is pasted onto the desktop");
            return null;
        }

        return path;
    }

    // ---- rename -----------------------------------------------------------------------------------

    /// <summary>
    /// [S-5543] F2: the focused item's name becomes a box on its widget (the one the keys came from, else
    /// the one on the display with the keyboard, else the first). Shell items are not renamed in place.
    /// </summary>
    private bool BeginRename(string? key, string? widgetId)
    {
        if (key is null || HostDesktopItems.Instance.Find(key) is not { } item || !DesktopRename.CanRename(item))
        {
            return false;
        }

        var widget = widgetId is not null && WidgetShows(widgetId, key) ? widgetId
            : KeyboardHolder?.Model is { Config: IDesktopItemConfig held } holder && DesktopItemKeys.Comparer.Equals(held.Item, key) ? holder.Id
            : _deskWidgets.TryGetValue(key, out var first) ? first.Widget.Id
            : null;
        if (widget is null)
        {
            return false;
        }

        _renamingKey = key;
        _renamingWidget = widget;
        RepaintDesk();
        return true;
    }

    private bool WidgetShows(string widgetId, string key)
        => State.Views.SelectMany(v => v.Widgets).Any(w => w.Id == widgetId && w.Config is IDesktopItemConfig c && DesktopItemKeys.Comparer.Equals(c.Item, key));

    public string? CommitRename(string key, string text)
    {
        if (!DesktopItemKeys.Comparer.Equals(_renamingKey, key) || HostDesktopItems.Instance.Find(key) is not { } item)
        {
            CancelRename();
            return null;
        }

        return Rename(item, text, closeBox: true);
    }

    /// <summary>
    /// [S-5570] A screen reader's value pattern sets the name: F2's rules without the box ([S-5543]);
    /// shell items are not renamed.
    /// </summary>
    public string? RenameTo(string key, string name)
        => HostDesktopItems.Instance.Find(key) is { } item && DesktopRename.CanRename(item)
            ? Rename(item, name, closeBox: false)
            : null;

    /// <summary>[S-5543] One rename by F2's rules: the sentence why not, or null once handed to the shell (or unchanged).</summary>
    private string? Rename(DesktopItem item, string text, bool closeBox)
    {
        var result = DesktopRename.Commit(item, text, DesktopRename.Siblings(item.Key, HostDesktopItems.Instance.Items));
        switch (result.Verdict)
        {
            case DesktopRenameVerdict.Invalid:
                return FileNames.Message(result.Problem);
            case DesktopRenameVerdict.Clash:
                return Str.Desktop_Rename_Clash.Text();
            case DesktopRenameVerdict.Rename when result.FullName is { } full:
                if (closeBox)
                {
                    CancelRename();
                }

                _ = RenameAsync(item.Key, full);
                return null;
            default:
                if (closeBox)
                {
                    CancelRename();
                }

                return null;
        }
    }

    /// <summary>[S-5543] Through the shell's own rename (its prompts, Explorer's undo); the notification then rewrites the key ([S-5520]).</summary>
    private async Task RenameAsync(string key, string fullName)
    {
        try
        {
            var folder = Path.GetDirectoryName(key) ?? string.Empty;
            _desk.SelectWhenShown(Path.Combine(folder, fullName));
            if (await HostFileOps.Instance.RenameAsync(key, fullName).ConfigureAwait(true) != FileOpOutcome.Done)
            {
                Log.Info(Scope, "a desktop item was not renamed (the shell said no, or the user cancelled)");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "renaming a desktop item failed", ex);
        }
    }

    public void CancelRename()
    {
        if (_renamingWidget is null)
        {
            return;
        }

        _renamingWidget = null;
        _renamingKey = null;
        RepaintDesk();
    }

    /// <summary>A press anywhere but on the item being renamed closes its box as it was.</summary>
    private void CloseRenameFor(string? pressed)
    {
        if (_renamingKey is not null && (pressed is null || !DesktopItemKeys.Comparer.Equals(pressed, _renamingKey)))
        {
            CancelRename();
        }
    }

    // ---- drags ------------------------------------------------------------------------------------

    /// <summary>
    /// [S-5549] The selection (the held item alone when it is not selected) dragged out as the shell's
    /// own data object, with the shell's drag image, in OLE's modal loop: onto Explorer, an app, the
    /// Recycle Bin or a folder on the desktop the target does what it does; onto empty desktop of any
    /// display the widgets move ([S-5546]).
    /// </summary>
    public void DragOut(string key, nint window)
    {
        LayoutDesk();
        _desk.CancelPress();
        if (!_desk.IsSelected(key))
        {
            _desk.Press(key, ctrl: false, shift: false);
        }

        var keys = _desk.Selected;
        if (keys.Count == 0)
        {
            return;
        }

        var source = _windows.FirstOrDefault(w => w.IsAlive && w.Hwnd == window) ?? (_deskWidgets.TryGetValue(key, out var at) ? at.Surface : null);
        _deskDrag = (new HashSet<string>(keys, DesktopItemKeys.Comparer), source, key);
        RepaintDesk();
        try
        {
            var effect = DragDropInterop.DragOut(keys, DropEffect.Copy | DropEffect.Move | DropEffect.Link, window);
            Log.Debug(Scope, $"{keys.Count} desktop item(s) dragged: {effect}");
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "dragging desktop items failed", ex);
        }
        finally
        {
            _deskDrag = null;
            _dropTargetWidget = null;
            RepaintDesk();
        }
    }

    public bool Dragging(string key) => _deskDrag is { } drag && drag.Keys.Contains(key);

    /// <summary>[S-5549] Whether a drag from the desktop is running (a drop on empty desktop then moves widgets).</summary>
    public bool DeskDragRunning => _deskDrag is not null;

    public void SetDropTarget(string? widgetId)
    {
        if (!string.Equals(_dropTargetWidget, widgetId, StringComparison.Ordinal))
        {
            _dropTargetWidget = widgetId;
            RepaintDesk();
        }
    }

    /// <summary>
    /// [S-5546] The desktop's own drag dropped on empty desktop of <paramref name="target"/> at
    /// <paramref name="dip"/>: the dragged item widgets of the display it started on move there, the held
    /// one under the pointer and the rest in their shape, to another display's view too (one widget per
    /// item in a view: one already there gives way). One undo step, never a file operation.
    /// </summary>
    public void MoveDeskDrag(DesktopWindow target, Point dip)
    {
        if (_deskDrag is not { } drag || ItemKind is not { } kind)
        {
            return;
        }

        var to = target.Metrics;
        var sourceSurface = drag.Source ?? target;
        var from = sourceSurface.Metrics;
        if (!to.IsUsable || !from.IsUsable)
        {
            return;
        }

        var sourceView = State.ViewForMonitor(sourceSurface.Monitor.Id);
        var targetView = State.ViewForMonitor(target.Monitor.Id);
        var moving = sourceView.Widgets.Where(w => w.Config is IDesktopItemConfig c && drag.Keys.Contains(c.Item)).ToList();
        var held = moving.FindIndex(w => w.Config is IDesktopItemConfig c && DesktopItemKeys.Comparer.Equals(c.Item, drag.Held));
        if (moving.Count == 0 || held < 0)
        {
            return;
        }

        // The moved rects in the target display's fractions, through DIPs.
        FracRect Into(FracRect r) => new(r.X * from.Width / to.Width, r.Y * from.Height / to.Height, r.W * from.Width / to.Width, r.H * from.Height / to.Height);
        var movingKeys = new HashSet<string>(moving.Select(w => ((IDesktopItemConfig)w.Config).Item), DesktopItemKeys.Comparer);
        var others = targetView.Widgets
            .Where(w => !moving.Contains(w) && !(w.Config is IDesktopItemConfig c && movingKeys.Contains(c.Item)))
            .Select(w => w.Rect)
            .ToList();
        var cell = new FracRect(0, 0, kind.DefaultWidth / to.Width, kind.DefaultHeight / to.Height);
        var spots = DesktopCells.Move([.. moving.Select(w => Into(w.Rect))], held, dip.X / to.Width, dip.Y / to.Height, others, cell, to.Allowed);

        Mutate(state =>
        {
            for (var i = 0; i < moving.Count; i++)
            {
                if (spots[i] is not { } spot)
                {
                    continue;
                }

                var widget = moving[i];
                widget.Rect = spot;
                if (!ReferenceEquals(sourceView, targetView))
                {
                    var itemKey = ((IDesktopItemConfig)widget.Config).Item;
                    sourceView.Widgets.Remove(widget);
                    targetView.Widgets.RemoveAll(w => w.Config is IDesktopItemConfig c && DesktopItemKeys.Comparer.Equals(c.Item, itemKey));
                    targetView.Widgets.Add(widget);
                }
            }
        }, undo: UndoAction.Move);
    }

    /// <summary>
    /// [S-5591] Files are about to be dropped on empty desktop of <paramref name="surface"/> at
    /// <paramref name="dip"/> (the shell's own drop into the desktop folder runs next): what arrives
    /// under their names lands there, in the view that display shows.
    /// </summary>
    public void ExpectDeskDrop(DesktopWindow surface, Point dip, IReadOnlyList<string> names)
    {
        var metrics = surface.Metrics;
        if (metrics.IsUsable)
        {
            _landings.Expect(new DesktopLandingHint(surface.CurrentViewId, dip.X / metrics.Width, dip.Y / metrics.Height, names));
        }
    }

    /// <summary>[S-5591] Where one reconcile's arrivals land, and each spot's display.</summary>
    private IReadOnlyList<DesktopLandingSpot?> LandingSpots(IReadOnlyList<DesktopItem> arrivals) => _landings.Place(arrivals);

    private ScreenMetrics? ScreenOfView(string viewId)
        => _windows.FirstOrDefault(w => w.IsAlive && string.Equals(w.CurrentViewId, viewId, StringComparison.Ordinal))?.Metrics;
}
