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
/// [S-5540]..[S-5549] [S-5580]..[S-5587] [S-5591] [S-11877] The desktop's gestures outside edit mode: the
/// one selection over every display's widgets, desktop icons of both sources and cards alike
/// (<see cref="DesktopSelection"/>; a desktop item under its key, an own icon under its own, a card under
/// its card key, <see cref="DesktopPicks.KeyOf"/>), the keys, F2's box, the menus and file operations, the
/// drags in, out and inside, and where an arrival lands. The rules are Core's (<see cref="DesktopCommands"/> says
/// what a command does to a selection that may mix both sources); this carries them out with the
/// host's shell parts (<see cref="HostFileOps"/>, <see cref="HostFileClipboard"/>,
/// <see cref="DragDropInterop"/>), never a copy of them; the menus are in <c>AppHost.DesktopMenus.cs</c>. UI thread.
/// </summary>
internal sealed partial class AppHost : IDesktopDesk
{
    private readonly DesktopSelection _desk = new();
    private readonly DesktopLandings _landings = new();
    private bool _deskActive;
    private string? _renamingWidget;
    private string? _renamingKey;
    private string? _dropTargetWidget;

    /// <summary>[S-5549] The drag running from the desktop, from <see cref="BeginDrag"/> to <see cref="EndDrag"/>.</summary>
    private DeskDragState? _deskDrag;

    /// <summary>[S-5549] One drag from the desktop: what it offers (its identity), its keys, their widgets, the surface it left, the icon held and the pointer (a physical screen point) where it began.</summary>
    private sealed record DeskDragState(ItemDragOffer Offer, HashSet<string> Keys, HashSet<string> Widgets, DesktopWindow? Source, string Held, Win32.Point? Pointer)
    {
        /// <summary>When it began and whether it was dropped on the desktop: said in the log when it is found never to have ended.</summary>
        public long Began { get; } = Environment.TickCount64;

        public bool Dropped { get; set; }
    }

    /// <summary>[S-5591] The last press on empty desktop: where a paste or New › lands.</summary>
    private (DesktopWindow Surface, double X, double Y)? _emptyPress;

    /// <summary>The first widget showing each key (a desktop item's, an own icon's, a card's), and the surface it is on, as last laid out.</summary>
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

        // [S-5548] New ›'s file types are read once, off the UI thread, before the first menu asks for them.
        _ = ShellNewTemplates.GetAsync();
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
        // [S-5593]..[S-5595] A view setting changed with this state: the icons are settled for it.
        SettleDesktopViewSettings();
        var before = _desk.Count;
        LayoutDesk();
        if (_desk.Count != before)
        {
            RepaintDesk();
        }
    }

    /// <summary>[S-11877] The selection changed: every icon redraws its look, and every card on the locked desktop its corners.</summary>
    private void RepaintDesk()
    {
        HostDesktopItems.Instance.Repaint();
        foreach (var surface in _windows.Where(w => w.IsAlive))
        {
            surface.RepaintDeskSelection();
        }
    }

    /// <summary>
    /// [S-5540] [S-11877] The key a widget is selected under on the locked desktop
    /// (<see cref="DesktopPicks.KeyOf"/>): a desktop item the listing holds under its own key, an own icon
    /// under its own, any other widget its card key; null for a desktop item not listed.
    /// </summary>
    private static string? DeskKey(WidgetInstance widget) => DesktopPicks.KeyOf(widget, key => HostDesktopItems.Instance.Find(key) is not null);

    /// <summary>The name type-to-find and a sort read of <paramref name="key"/>: the listing's, an own icon's as drawn, a card's title.</summary>
    private string? DeskName(string key)
        => HostDesktopItems.Instance.Find(key)?.Name
            ?? (_deskWidgets.TryGetValue(key, out var at)
                ? at.Widget.Config is IDesktopItemConfig { Item.Length: 0 } own ? own.OwnName
                    : at.Widget.Config is not IDesktopItemConfig ? (string.IsNullOrWhiteSpace(at.Widget.Label) ? ModuleCatalog.Of(at.Widget.Kind).Name : at.Widget.Label)
                    : null
                : null);

    /// <summary>
    /// [S-11877] Whether the desktop's one selection holds <paramref name="widget"/> (an icon or a card);
    /// [S-11885] a group's member is selected with its whole group (<see cref="DesktopPicks.Selects"/>).
    /// </summary>
    public bool DeskSelects(WidgetInstance widget)
        => State.Views.Find(v => v.Widgets.Contains(widget)) is { } view
            ? DesktopPicks.Selects(view, _desk, widget, DeskKey)
            : DeskKey(widget) is { } key && _desk.IsSelected(key);

    /// <summary>
    /// [S-11879] A move on the locked desktop begins on <paramref name="heldId"/> in <paramref name="view"/>:
    /// what it carries (<see cref="DesktopPicks.Moving"/>), and the press that began it no longer narrows
    /// the selection on its release.
    /// </summary>
    public List<string> DeskMoving(ViewState view, string heldId)
    {
        LayoutDesk();
        _desk.CancelPress();
        return DesktopPicks.Moving(view, _desk, heldId, DeskKey);
    }

    /// <summary>[S-11878] Whether a move of <paramref name="widgets"/> gives another target anything: a desktop item's file, an own icon's path target.</summary>
    public bool DeskCarriesFiles(IEnumerable<WidgetInstance> widgets)
        => DesktopCommands.Drag([.. widgets.Select(w => w.Config).OfType<IDesktopItemConfig>().Select(c => DeskIcon.Of(c, PathExists))]).Paths.Count > 0;

    /// <summary>
    /// [S-11878] Whether the physical screen point <paramref name="at"/> is on Themia's desktop: inside a
    /// live surface's work area, with no app's window on top there (the window under it is a surface of
    /// ours, or the shell's own desktop).
    /// </summary>
    public bool OverDesktop(Win32.Point at)
    {
        var inside = _windows.Any(w => w.IsAlive && w.Monitor.WorkArea is var area
            && at.X >= area.Left && at.X < area.Right && at.Y >= area.Top && at.Y < area.Bottom);
        if (!inside)
        {
            return false;
        }

        var under = Win32.WindowFromPoint(at);
        var root = under == 0 ? 0 : Win32.GetAncestor(under, Win32.GaRoot);
        return root == 0 || _windows.Any(w => w.IsAlive && w.Hwnd == root) || ShellSignals.IsDesktopLayer(root);
    }

    /// <summary>[S-5580] The icon with <paramref name="key"/> as the desktop's commands see it, or null when no widget shows it.</summary>
    private DeskIcon? DeskIconOf(string? key)
        => key is not null && _deskWidgets.TryGetValue(key, out var at) && at.Widget.Config is IDesktopItemConfig config
            ? DeskIcon.Of(config, PathExists)
            : null;

    /// <summary>[S-5580] The icons of <paramref name="keys"/>, in their order.</summary>
    private List<DeskIcon> DeskIcons(IReadOnlyList<string> keys) => [.. keys.Select(DeskIconOf).OfType<DeskIcon>()];

    /// <summary>Whether a local path an own icon points at is there now. Asked for a command only, never on a network path (<see cref="DeskIcon.Of"/>).</summary>
    private static bool PathExists(string path)
    {
        try
        {
            return File.Exists(path) || Directory.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Log.Debug(Scope, $"could not tell whether a desktop icon's target exists: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// [S-5540] [S-11877] The widgets as they sit now on every display, desktop icons and cards alike, in
    /// physical screen pixels, handed to the selection (whose order is the desktop's), and the first widget
    /// of each key remembered. A card that draws nothing on the locked desktop ([S-873]) is left out.
    /// </summary>
    private void LayoutDesk()
    {
        var boxes = new List<DesktopBox>();
        var widgets = new Dictionary<string, (DesktopWindow, WidgetInstance)>(DesktopItemKeys.Comparer);
        var display = 0;
        foreach (var surface in _windows.Where(w => w.IsAlive).OrderBy(w => w.Monitor.Bounds.Left).ThenBy(w => w.Monitor.Bounds.Top))
        {
            var bounds = surface.Monitor.Bounds;
            foreach (var widget in State.ViewForMonitor(surface.Monitor.Id).Widgets)
            {
                if (DeskKey(widget) is not { } key || !surface.Draws(widget.Id))
                {
                    continue;
                }

                var r = widget.Rect;
                boxes.Add(new DesktopBox(key, display, bounds.Left + (r.X * bounds.Width), bounds.Top + (r.Y * bounds.Height), r.W * bounds.Width, r.H * bounds.Height));
                widgets.TryAdd(key, (surface, widget));
            }

            display++;
        }

        _deskWidgets = widgets;
        _desk.SetLayout(boxes, DeskName);
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

    /// <summary>[S-11877] Escape on the locked desktop: the one selection, icons and cards, is cleared.</summary>
    public void ClearDeskSelection()
    {
        if (_desk.Clear())
        {
            RepaintDesk();
        }
    }

    /// <summary>[S-5544] The band starts.</summary>
    public void DesktopBandBegin(bool ctrl, bool shift)
    {
        _desk.BeginBand(ctrl, shift);
        RepaintDesk();
    }

    /// <summary>
    /// [S-5544] The band runs from <paramref name="start"/> to <paramref name="now"/>, both in the DIPs
    /// of <paramref name="surface"/>, which holds the pointer: one rectangle in screen space
    /// (<see cref="Themia.Core.Desktop.DesktopBand"/>), kept inside the work areas of the displays
    /// Themia is on, drawn by every surface it reaches and selecting the icons it touches on any of
    /// them (their boxes are the selection's, in screen pixels: [S-5540]).
    /// </summary>
    public void DesktopBand(DesktopWindow surface, Point start, Point now)
    {
        var surfaces = _windows.Where(w => w.IsAlive).ToList();
        var areas = surfaces.ConvertAll(w => w.Monitor.Info.WorkArea);
        var display = surface.Monitor.Info.Bounds;
        var scale = surface.BandScale;
        var band = Themia.Core.Desktop.DesktopBand.Of(
            areas,
            Themia.Core.Desktop.DesktopBand.ToScreen(display, scale, start.X, start.Y),
            Themia.Core.Desktop.DesktopBand.ToScreen(display, scale, now.X, now.Y));

        foreach (var window in surfaces)
        {
            var info = window.Monitor.Info;
            window.ShowDesktopBand(Themia.Core.Desktop.DesktopBand.On(band, info.Bounds, info.WorkArea, window.BandScale));
        }

        _desk.Band(Themia.Core.Desktop.DesktopBand.Hits(band, _desk.Ordered));
        RepaintDesk();
    }

    /// <summary>[S-5544] The band is over: what it selected stays, and no display draws it any more.</summary>
    public void DesktopBandEnd()
    {
        _desk.EndBand();
        foreach (var window in _windows)
        {
            window.ShowDesktopBand(null);
        }
    }

    /// <summary>
    /// [S-5583] The widgets of the own icons <paramref name="keys"/> are removed: one undoable step with
    /// the way back offered, as edit mode's delete ([S-881]); no prompt, and nothing of their targets is touched.
    /// </summary>
    private void RemoveOwnIcons(IReadOnlyList<string> keys)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (DesktopItemKeys.IsOwn(key) && _deskWidgets.TryGetValue(key, out var at))
            {
                ids.Add(at.Widget.Id);
            }
        }

        if (ids.Count == 0)
        {
            return;
        }

        CloseRenameFor(null);
        Mutate(state =>
        {
            foreach (var view in state.Views)
            {
                view.Widgets.RemoveAll(w => ids.Contains(w.Id));
            }
        }, undo: UndoAction.Delete);
        OfferUndo(Str.Shell_Undo_DeletedWidgets.Plural(ids.Count));
    }

    /// <summary>[S-5582] Properties of an own icon: edit mode with its widget selected, so its page opens (Target, Name, Icon, Name lines, Open with).</summary>
    private void ShowIconProperties(string key)
    {
        if (!_deskWidgets.TryGetValue(key, out var at))
        {
            return;
        }

        var id = at.Widget.Id;
        SetEditMode(true);
        if (!_dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => Select(id)))
        {
            Log.Warn(Scope, "could not queue a desktop icon's properties; edit mode is on, selecting the icon opens its page");
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
                OpenIcons(DeskIcons(_desk.Selected));
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
                ShowSelectionProperties();
                return true;
            case WidgetKey.Refresh:
                HostDesktopItems.Instance.Refresh();
                return true;
            case WidgetKey.Menu:
                // [S-5545] The Menu key opens the Windows 11 menu; Shift+F10 the shell's classic one, as Explorer.
                MenuFromKeys(classic: shift);
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

    // ---- commands ---------------------------------------------------------------------------------

    public void Open(string key)
    {
        LayoutDesk();
        OpenIcons(DeskIcons(_desk.IsSelected(key) ? _desk.Selected : [key]));
    }

    /// <summary>
    /// [S-5530] [S-5581] Open icons by the desktop's rule: a desktop item through the launcher (a shell
    /// item by its shell name), an own icon its target through the launcher (which classifies it again)
    /// or its app layout on the display its widget is on ([S-1407]); at most
    /// <see cref="DesktopCommands.OpenAtMost"/> at once.
    /// </summary>
    private void OpenIcons(IReadOnlyList<DeskIcon> icons)
    {
        var (open, skipped) = DesktopCommands.Open(icons);
        foreach (var one in open)
        {
            if (one.Layout)
            {
                if (_deskWidgets.TryGetValue(one.Key, out var at) && at.Widget.Config is IDesktopItemConfig config)
                {
                    HostAppLayouts.Instance.Launch(config.Layout, at.Surface.Hwnd);
                }

                continue;
            }

            var outcome = HostLauncher.Instance.Open(one.Launch);
            if (outcome != LaunchOutcome.Opened)
            {
                Log.Warn(Scope, $"a desktop icon did not open ({outcome})");
            }
        }

        if (skipped > 0)
        {
            Log.Info(Scope, $"{skipped} more selected desktop icon(s) were not opened at once");
        }
    }

    private nint DeskOwner => (KeyboardSurface ?? DesktopKeysSurface ?? _windows.FirstOrDefault(w => w.IsAlive))?.Hwnd ?? 0;

    /// <summary>
    /// [S-5545] [S-5583] Delete and Shift+Delete: the shell's own "delete" on the selected desktop items'
    /// files, with the shell's own prompt as the user set it up (the Recycle Bin, or for good with Shift),
    /// as Explorer's desktop; the selected own icons' widgets are removed (undoable, no prompt, no file
    /// touched). A mixed selection does both.
    /// </summary>
    private void DeleteSelection(bool permanent)
    {
        var plan = DesktopCommands.Delete(DeskIcons(_desk.Selected));
        if (plan.Files.Count > 0)
        {
            try
            {
                if (!ShellContextMenu.InvokeVerb(plan.Files, "delete", DeskOwner, shift: permanent))
                {
                    Log.Warn(Scope, $"the shell did not delete {plan.Files.Count} desktop item(s)");
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "deleting desktop items failed", ex);
            }
        }

        RemoveOwnIcons(plan.OwnKeys);
    }

    /// <summary>
    /// [S-5545] [S-5585] Ctrl+C and Ctrl+X: the selected desktop items' files on the Windows clipboard, as
    /// Explorer puts them there (a cut is drawn faded), and with a copy each own icon's path target that
    /// is there; never a cut of a target.
    /// </summary>
    private void CopySelection(bool cut)
    {
        var clip = DesktopCommands.Clipboard(DeskIcons(_desk.Selected), cut);
        if (clip.Paths.Count > 0 && !HostFileClipboard.Instance.SetFiles(clip.Paths, clip.Cut))
        {
            Log.Warn(Scope, "the desktop selection could not be put on the clipboard");
        }

        RepaintDesk();
    }

    /// <summary>
    /// [S-5545] [S-5591] Ctrl+V and the menus' Paste: what the clipboard holds into the user's desktop
    /// folder through the shell's copy or move (a cut empties the clipboard after), its arrivals landing
    /// at <paramref name="at"/> (the background menu's click), else in the view of the display that has
    /// the keys, at the last press on its empty desktop, else at the next free cell. With
    /// <paramref name="into"/> (a folder icon's Paste) the files go into that folder instead, as
    /// Explorer's Paste on a folder, and nothing lands on the desktop.
    /// </summary>
    private async Task PasteAsync(string? into = null, (DesktopWindow Surface, double X, double Y)? at = null)
    {
        try
        {
            if ((into ?? DesktopFolderPath()) is not { } destination || HostFileClipboard.Instance.Read() is not { } content)
            {
                return;
            }

            var surface = at?.Surface ?? KeyboardSurface ?? DesktopKeysSurface ?? _windows.FirstOrDefault(w => w.IsAlive && w.Monitor.IsPrimary);
            if (into is null && surface is not null)
            {
                var spot = at ?? (_emptyPress is { } press && ReferenceEquals(press.Surface, surface) ? press : null);
                ExpectLanding(new DesktopLandingHint(
                    surface.CurrentViewId,
                    spot?.X,
                    spot?.Y,
                    content.Paths.Count > 0 ? [.. content.Paths.Select(Path.GetFileName).OfType<string>()] : []));
            }

            if (content.Paths.Count > 0)
            {
                if (content.Effect == DropEffect.Move)
                {
                    if (await HostFileOps.Instance.MoveAsync(content.Paths, destination).ConfigureAwait(true) == FileOpOutcome.Done)
                    {
                        HostFileClipboard.Instance.CompleteCut();
                    }
                }
                else
                {
                    await HostFileOps.Instance.CopyAsync(content.Paths, destination).ConfigureAwait(true);
                }
            }
            else if (content.VirtualFiles > 0)
            {
                HostFileClipboard.Instance.SaveVirtualFiles(destination);
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
    /// [S-5543] [S-5584] F2: the focused icon's name becomes a box on its widget (the one the keys came
    /// from, else the one on the display with the keyboard, else the first). A desktop item is renamed
    /// through the shell, an own icon by its label; shell items are not renamed in place.
    /// </summary>
    private bool BeginRename(string? key, string? widgetId)
    {
        var how = DesktopCommands.Rename(DeskIconOf(key));
        if (key is null || how == DeskRename.None
            || (how == DeskRename.Shell && (HostDesktopItems.Instance.Find(key) is not { } item || !DesktopRename.CanRename(item))))
        {
            return false;
        }

        var widget = widgetId is not null && WidgetShows(widgetId, key) ? widgetId
            : KeyboardHolder?.Model is { Config: IDesktopItemConfig held } holder && DesktopItemKeys.Comparer.Equals(DesktopItemKeys.Of(held), key) ? holder.Id
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
        => State.Views.SelectMany(v => v.Widgets).Any(w => w.Id == widgetId && w.Config is IDesktopItemConfig c && DesktopItemKeys.Comparer.Equals(DesktopItemKeys.Of(c), key));

    public string? CommitRename(string key, string text)
    {
        if (!DesktopItemKeys.Comparer.Equals(_renamingKey, key))
        {
            CancelRename();
            return null;
        }

        if (DesktopItemKeys.IsOwn(key))
        {
            CancelRename();
            RenameOwnIcon(key, text);
            return null;
        }

        if (HostDesktopItems.Instance.Find(key) is not { } item)
        {
            CancelRename();
            return null;
        }

        return Rename(item, text, closeBox: true);
    }

    /// <summary>
    /// [S-5570] A screen reader's value pattern sets the name: F2's rules without the box ([S-5543],
    /// [S-5584]); shell items are not renamed.
    /// </summary>
    public string? RenameTo(string key, string name)
    {
        LayoutDesk();
        if (DesktopItemKeys.IsOwn(key))
        {
            RenameOwnIcon(key, name);
            return null;
        }

        return HostDesktopItems.Instance.Find(key) is { } item && DesktopRename.CanRename(item)
            ? Rename(item, name, closeBox: false)
            : null;
    }

    /// <summary>
    /// [S-5584] An own icon's label is set to <paramref name="typed"/> (the target's own name, or
    /// nothing, clears it): one undo step, and none when the name is the one it has.
    /// </summary>
    private void RenameOwnIcon(string key, string typed)
    {
        if (!_deskWidgets.TryGetValue(key, out var at) || at.Widget.Config is not IDesktopItemConfig config
            || at.Widget.Config.Clone() is not IDesktopItemConfig probe || !probe.RenameOwn(typed))
        {
            return;
        }

        Mutate(_ => config.RenameOwn(typed), undo: UndoAction.Change);
    }

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
    /// [S-5549] [S-5586] The drag of the selection (the held icon alone when it is not selected) begins:
    /// the keys and widgets in it, the surface it leaves and the pointer's place are kept until
    /// <see cref="EndDrag"/>, and what it offers another target is the desktop's one rule
    /// (<see cref="DesktopCommands.Drag"/>: the desktop items' files and each own icon's path target;
    /// with no file at all only the drag's marker, so everything but the desktop refuses it). The view
    /// starts it as XAML's drag in the same pointer handler (<see cref="Themia.Sdk.WinUI.ItemDrag"/>).
    /// Onto Explorer, an app, the Recycle Bin or a folder icon the target does what it does; onto empty
    /// desktop of any display the widgets move ([S-5546]). A drag that never ended is replaced.
    /// </summary>
    public ItemDragOffer? BeginDrag(string key, nint window)
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
            return null;
        }

        if (_deskDrag is { } leaked)
        {
            // A real leak now: the view ends its drag on XAML's completion, on DropCompleted and when it
            // leaves the tree (ItemDrag.StartAsync). What is known of the one that did not is said.
            Log.Warn(Scope, "a drag of desktop icons begins while an earlier one never ended; the earlier one is dropped "
                + $"(begun {Environment.TickCount64 - leaked.Began} ms ago, {leaked.Keys.Count} icon(s), "
                + $"{(leaked.Dropped ? "dropped on the desktop" : "no drop on the desktop seen")}, its surface {(leaked.Source is { IsAlive: true } ? "alive" : "gone")})");
        }

        var source = _windows.FirstOrDefault(w => w.IsAlive && w.Hwnd == window) ?? (_deskWidgets.TryGetValue(key, out var at) ? at.Surface : null);
        var widgets = new HashSet<string>(keys.Select(k => _deskWidgets.TryGetValue(k, out var held) ? held.Widget.Id : null).OfType<string>(), StringComparer.Ordinal);
        var drag = DesktopCommands.Drag(DeskIcons(keys));
        var offer = new ItemDragOffer(drag.Paths, DesktopCommands.DragEffects(drag));
        _deskDrag = new DeskDragState(offer, new HashSet<string>(keys, DesktopItemKeys.Comparer), widgets, source, key, Win32.GetCursorPos(out var pointer) ? pointer : (Win32.Point?)null);
        _dropTargetWidget = null;
        RepaintDesk();
        Log.Debug(Scope, $"{keys.Count} desktop icon(s) dragged ({offer.Paths.Count} file(s), {offer.Effects})");
        return offer;
    }

    /// <summary>
    /// [S-5549] [S-5564] The ghost of the dragged icons (<see cref="DesktopWindow.DeskDragPicture"/>): the
    /// held icon in front, the badge counting every icon in the drag, the pointer where the drag began.
    /// </summary>
    public DragPicture? DragPicture(ItemDragOffer drag)
    {
        if (_deskDrag is not { } running || !ReferenceEquals(running.Offer, drag) || running.Source is not { } source || running.Pointer is not { } pointer
            || !_deskWidgets.TryGetValue(running.Held, out var held))
        {
            return null;
        }

        return source.DeskDragPicture(running.Widgets, held.Widget.Id, running.Keys.Count, pointer.X, pointer.Y);
    }

    /// <summary>[S-5549] The drag is over, however it ended: its state goes and the icons are drawn at rest. A drag that was replaced meanwhile clears nothing.</summary>
    public void EndDrag(ItemDragOffer drag)
    {
        if (_deskDrag is not { } running || !ReferenceEquals(running.Offer, drag))
        {
            return;
        }

        _deskDrag = null;
        _dropTargetWidget = null;
        RepaintDesk();
    }

    public bool Dragging(string key) => _deskDrag is { } drag && drag.Keys.Contains(key);

    /// <summary>[S-5549] Whether a drag from the desktop is running (a drop on empty desktop then moves widgets).</summary>
    public bool DeskDragRunning => _deskDrag is not null;

    /// <summary>[S-5546] Whether the widget <paramref name="widgetId"/> is one of the icons in the drag running from the desktop.</summary>
    public bool DeskDragHolds(string widgetId) => _deskDrag is { } drag && drag.Widgets.Contains(widgetId);

    public void SetDropTarget(string? widgetId)
    {
        if (!string.Equals(_dropTargetWidget, widgetId, StringComparison.Ordinal))
        {
            _dropTargetWidget = widgetId;
            RepaintDesk();
        }
    }

    /// <summary>
    /// [S-5546] [S-11878] The shell's drag of desktop icons (a move that left the desktop, [S-11878]) let go
    /// back on empty desktop of <paramref name="target"/> at <paramref name="dip"/>: the dragged widgets of
    /// the display it started on (icons of both sources, and any card selected with them) move there as one
    /// block, the held icon where its picture is, snapped to the widget grid as a move is
    /// (<see cref="DesktopGrid.Drop"/>), to another display's view too (one widget per desktop item in a view:
    /// one already there gives way). One undo step, never a file operation.
    /// </summary>
    public void MoveDeskDrag(DesktopWindow target, Point dip)
    {
        if (_deskDrag is not { } drag || ItemKind is null)
        {
            return;
        }

        drag.Dropped = true;
        var to = target.Metrics;
        var sourceSurface = drag.Source ?? target;
        var from = sourceSurface.Metrics;
        if (!to.IsUsable || !from.IsUsable)
        {
            return;
        }

        var sourceView = State.ViewForMonitor(sourceSurface.Monitor.Id);
        var targetView = State.ViewForMonitor(target.Monitor.Id);
        var moving = sourceView.Widgets.Where(w => DeskKey(w) is { } key && drag.Keys.Contains(key)).ToList();
        var held = moving.FindIndex(w => DesktopItemKeys.Comparer.Equals(DeskKey(w), drag.Held));
        if (moving.Count == 0 || held < 0)
        {
            return;
        }

        // The moved rects in the target display's fractions, through DIPs.
        FracRect Into(FracRect r) => new(r.X * from.Width / to.Width, r.Y * from.Height / to.Height, r.W * from.Width / to.Width, r.H * from.Height / to.Height);
        // A desktop item's own key (an own icon has none): the one a view holds once.
        var movingKeys = new HashSet<string>(moving.Select(w => w.Config is IDesktopItemConfig c ? c.Item : string.Empty).Where(k => k.Length > 0), DesktopItemKeys.Comparer);

        // Where the held icon's corner is now: the pointer less where the icon was grabbed, so it lands
        // where its picture is. Without the press's place (a drag begun by the keys of a screen reader), its middle.
        var heldRect = moving[held].Rect;
        var (heldW, heldH) = (heldRect.W * from.Width, heldRect.H * from.Height);
        var grabX = heldW / 2;
        var grabY = heldH / 2;
        if (drag.Pointer is { } pressed && sourceSurface.Monitor.Bounds.Width > 0)
        {
            var scale = sourceSurface.Monitor.Bounds.Width / from.Width;
            grabX = Math.Clamp(((pressed.X - sourceSurface.Monitor.Bounds.Left) / scale) - (heldRect.X * from.Width), 0, heldW);
            grabY = Math.Clamp(((pressed.Y - sourceSurface.Monitor.Bounds.Top) / scale) - (heldRect.Y * from.Height), 0, heldH);
        }

        FracRect[] dragged = [.. moving.Select(w => Into(w.Rect))];
        var x = (dip.X - grabX) / to.Width;
        var y = (dip.Y - grabY) / to.Height;

        // [S-11878] The block lands as a move does: snapped to the grid, kept in reach; nothing is pushed aside.
        var spots = DesktopGrid.Drop(dragged, held, x, y, to);

        Mutate(state =>
        {
            for (var i = 0; i < moving.Count; i++)
            {
                var widget = moving[i];
                widget.Rect = spots[i];
                if (!ReferenceEquals(sourceView, targetView))
                {
                    var itemKey = widget.Config is IDesktopItemConfig item ? item.Item : string.Empty;
                    sourceView.Widgets.Remove(widget);
                    if (itemKey.Length > 0)
                    {
                        targetView.Widgets.RemoveAll(w => w.Config is IDesktopItemConfig c && DesktopItemKeys.Comparer.Equals(c.Item, itemKey));
                    }

                    targetView.Widgets.Add(widget);
                }
            }
        }, undo: UndoAction.Move);
    }

    /// <summary>
    /// [S-5554] [S-5556] Sort by: the desktop icons of the view <paramref name="surface"/> shows, of both
    /// sources (an own icon with the files, by its name as drawn), go back
    /// into the grid from the work area's top-left corner in the order of <paramref name="by"/>, around
    /// the view's other widgets, which stay. One undo step of the arrangement, never a file operation;
    /// an item with no free cell left stays where it was.
    /// </summary>
    private void SortDesk(DesktopWindow surface, DesktopSortBy by)
    {
        var metrics = surface.Metrics;
        if (!metrics.IsUsable || ItemKind is null)
        {
            return;
        }

        var view = State.ViewForMonitor(surface.Monitor.Id);
        var widgets = new Dictionary<string, WidgetInstance>(DesktopItemKeys.Comparer);
        foreach (var widget in view.Widgets)
        {
            if (widget.Config is IDesktopItemConfig config)
            {
                widgets.TryAdd(DesktopItemKeys.Of(config), widget);
            }
        }

        if (widgets.Count == 0)
        {
            // [S-5596] The sort is remembered even with nothing to arrange: the dot follows the choice.
            Mutate(state => state.Desktop.Sort = by, undo: null);
            return;
        }

        // An own icon sorts with the files, by its name as drawn.
        var items = widgets.Select(pair => HostDesktopItems.Instance.Find(pair.Key)
            ?? (pair.Value.Config is IDesktopItemConfig { Item.Length: 0 } own
                ? new DesktopItem(pair.Key, own.OwnName, DesktopItemKind.File)
                : new DesktopItem(pair.Key, DesktopItemKeys.FallbackName(pair.Key), DesktopItemKind.File)));
        var ordered = DesktopSort.Order(items, by, item => SortFacts(item, widgets));
        var others = view.Widgets.Where(w => w.Config is not IDesktopItemConfig).Select(w => w.Rect).ToList();
        var cells = DesktopSort.Arrange(ordered.Count, others, DeskIconSize(metrics), metrics);

        Mutate(state =>
        {
            // [S-5596] The sort is remembered: its line carries the dot.
            state.Desktop.Sort = by;
            for (var i = 0; i < ordered.Count; i++)
            {
                if (cells[i] is { } spot && widgets.TryGetValue(ordered[i].Key, out var widget))
                {
                    widget.Rect = spot;
                }
            }
        }, undo: UndoAction.Move);
    }

    /// <summary>
    /// [S-5555] What a sort reads of one item: a file's size, extension and last write, a folder's last
    /// write. An own icon has its target's while that is a local file or folder that is there, else none.
    /// An item that cannot be read (gone a moment ago, no access) has none, logged without its name.
    /// </summary>
    private static DesktopSortFacts SortFacts(DesktopItem item, Dictionary<string, WidgetInstance> widgets)
    {
        try
        {
            var path = item.Key;
            if (DesktopItemKeys.IsOwn(path))
            {
                if (!widgets.TryGetValue(path, out var widget) || widget.Config is not IDesktopItemConfig own
                    || DeskIcon.Of(own, PathExists).FilePath is not { } target)
                {
                    return DesktopSortFacts.None;
                }

                path = target;
            }

            var folder = new DirectoryInfo(path);
            if (item.Kind == DesktopItemKind.Folder || folder.Exists)
            {
                return folder.Exists ? new DesktopSortFacts(0, string.Empty, folder.LastWriteTimeUtc) : DesktopSortFacts.None;
            }

            var file = new FileInfo(path);
            return file.Exists ? new DesktopSortFacts(file.Length, file.Extension.ToLowerInvariant(), file.LastWriteTimeUtc) : DesktopSortFacts.None;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            Log.Warn(Scope, "could not read a desktop item's size and date for the sort", ex);
            return DesktopSortFacts.None;
        }
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
            ExpectLanding(new DesktopLandingHint(surface.CurrentViewId, dip.X / metrics.Width, dip.Y / metrics.Height, names));
        }
    }

    /// <summary>
    /// [S-5591] A gesture expects arrivals.
    /// </summary>
    private void ExpectLanding(DesktopLandingHint hint) => _landings.Expect(hint);

    /// <summary>[S-5591] Where one reconcile's arrivals land, and each spot's display.</summary>
    private IReadOnlyList<DesktopLandingSpot?> LandingSpots(IReadOnlyList<DesktopItem> arrivals) => _landings.Place(arrivals);

    private ScreenMetrics? ScreenOfView(string viewId)
        => _windows.FirstOrDefault(w => w.IsAlive && string.Equals(w.CurrentViewId, viewId, StringComparison.Ordinal))?.Metrics;
}
