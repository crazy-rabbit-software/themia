using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Themia.App.Interop;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-1744] The menu of empty surface in edit mode: what a right-click on the desktop between
/// widgets offers while arranging them, the edit bar's way in first (Add widget, View, Display,
/// Settings: each the bar's own menu on this display). Same look as every other menu ([S-1740]);
/// which commands it has is <see cref="ShellMenus.ForSurface"/>'s.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>
    /// The right press that is about to raise this right-tap cancelled a placement ([S-1587]); that
    /// press was the whole gesture, so no menu follows it.
    /// </summary>
    private bool _rightPressCancelledPlacement;

    private void OnRootRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        try
        {
            var cancelled = _rightPressCancelledPlacement;
            _rightPressCancelledPlacement = false;

            // Only the surface itself: a card, the edit bar and the view bar have menus of their own,
            // or none, and a right-click on them is not a right-click on the desktop.
            if (e.Handled || cancelled || _closed || !ReferenceEquals(e.OriginalSource, _root))
            {
                return;
            }

            // [S-5548] Outside edit mode empty desktop's menu is the desktop's own.
            if (OpenDesktopBackgroundMenu(e.GetPosition(_root)) || OpenSurfaceMenu(e.GetPosition(_root)))
            {
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not open the surface menu", ex);
        }
    }

    /// <summary>[S-1744] The surface menu at a point of the surface (a right-click, or [S-1974] the Menu key).</summary>
    private bool OpenSurfaceMenu(Point at)
    {
        var view = ViewForThisMonitor(_host.State);
        var facts = new SurfaceMenuFacts(
            EditMode: EditMode,
            Placing: _host.Placing is not null,
            CanPaste: _host.Clipboard.HasContent,
            HasWidgets: SelectionRules.All(view, HiddenIds()).Count > 0);

        return ItemMenu.ShowAt(_menuAnchor, OnMenuAnchor(at), SurfaceMenu(facts));
    }

    /// <summary>
    /// [S-5548] [S-5582] One of the desktop's own menus (an icon's, empty desktop's) at a point of the
    /// surface: the same <see cref="ItemMenu"/> every Themia menu is, opened as its own popup window at
    /// the pointer and kept inside the display's work area by the framework ([S-1500], [S-1748]).
    /// </summary>
    internal bool OpenMenuAt(Point at, IEnumerable<ItemMenuEntry> entries, Action? closed = null)
        => !_closed && ItemMenu.ShowAt(_menuAnchor, OnMenuAnchor(at), entries, closed);

    /// <summary>
    /// [S-14321] [S-1743] [S-1758] A widget card's menu at <paramref name="at"/> (a point of the card; null: from the keys, at
    /// its centre), in and outside edit mode, opened as the desktop's own menus are (<see cref="OpenMenuAt"/>: this surface's
    /// menu anchor at the pointer, its own popup), never on the card (owner, 2026-11-20: the card's menu should "look (in terms
    /// of style) the same as the one on the desktop"). Outside edit mode the card is the desktop's first: its one selection and
    /// the keys, as a right-click on an icon or on empty desktop gives them ([S-5550]).
    /// </summary>
    public bool OpenCardMenu(WidgetCard card, Point? at)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (_closed)
        {
            return false;
        }

        // [S-15526] A copied row's menu is its repeater's, opened where it was asked for.
        if (IsCopy(card) && Stand(card) is var stands && !ReferenceEquals(stands, card))
        {
            at = at is { } p ? card.TransformToVisual(stands).TransformPoint(p) : null;
            card = stands;
        }

        var point = at is { } on ? card.TransformToVisual(_root).TransformPoint(on) : CentreOf(card);
        if (!EditMode)
        {
            _host.CardMenuOpening(this, card.Model);
        }

        return OpenMenuAt(point, card.CardMenu(point));
    }

    /// <summary>[S-14321] Where a menu from the keys stands at <paramref name="card"/>: its centre, on this surface.</summary>
    internal Point CentreOf(WidgetCard card)
        => card.TransformToVisual(_root).TransformPoint(new Point(card.ActualWidth / 2, card.ActualHeight / 2));

    /// <summary>[S-14321] Outside edit mode: how the card menu's Delete line reads (what Del does to the desktop's one selection).</summary>
    public (Core.Desktop.DeleteWords Words, int Count) DeskDeleteLine(WidgetCard card)
        => EditMode || _closed ? (Core.Desktop.DeleteWords.None, 0) : _host.DeskDeleteLine();

    /// <summary>[S-14321] Outside edit mode: the desktop's background menu's lines for a card's menu at <paramref name="at"/> on this surface.</summary>
    public IReadOnlyList<ItemMenuEntry> DesktopLines(WidgetCard card, Point at)
        => EditMode || _closed ? [] : _host.DesktopMenuLines(this, at);

    /// <summary>[S-5582] <see cref="OpenMenuAt"/> for a screen point (physical pixels) over this surface.</summary>
    internal bool OpenMenuAtScreen(int screenX, int screenY, IEnumerable<ItemMenuEntry> entries)
    {
        var metrics = Metrics;
        if (!metrics.IsUsable || Monitor.Bounds.Width <= 0)
        {
            return false;
        }

        var scale = Monitor.Bounds.Width / metrics.Width;
        var (left, top) = Services.Files.ShellContextMenu.ToScreen(_hwnd, 0, 0);
        return OpenMenuAt(new Point((screenX - left) / scale, (screenY - top) / scale), entries);
    }

    /// <summary>[S-1744] The entries for this surface; [S-11707] its Add widget opens the add page under the bar.</summary>
    private List<ItemMenuEntry> SurfaceMenu(SurfaceMenuFacts facts)
        => SurfaceEntries(facts, new SurfaceMenuActions(
            AddWidget: OpenAddPage,
            View: () => OpenPage(Core.Settings.SettingsPageId.Views, []),
            Display: () => OpenPage(Core.Settings.SettingsPageId.Display, []),
            Settings: () => OpenPage(Core.Settings.SettingsPageId.General, []),
            Paste: _host.Paste,
            SelectAll: SelectAll,
            Done: () => _host.SetEditMode(false)));

    /// <summary>[S-1744] The surface menu's entries, worded; what each does is the caller's (the menu render draws it too).</summary>
    internal static List<ItemMenuEntry> SurfaceEntries(SurfaceMenuFacts facts, SurfaceMenuActions actions)
    {
        var entries = new List<ItemMenuEntry>();

        foreach (var item in ShellMenus.ForSurface(facts))
        {
            var disabled = !item.Enabled;
            entries.Add(item.Command switch
            {
                ShellCommand.AddWidget => new(Glyphs.Add, Str.Shell_Toolbar_AddWidget.Text(), actions.AddWidget,
                    Shortcut: KeyChord.Ctrl(ChordKey.N)),
                ShellCommand.View => new(Glyphs.View, Str.CtxMenu_View.Text(), actions.View),
                ShellCommand.Display => new(Glyphs.Display, Str.BarMenus_Display.Text(), actions.Display),
                ShellCommand.Settings => new(Glyphs.Settings, Str.Common_Settings.Text(), actions.Settings),
                ShellCommand.Paste => new(Glyphs.Paste, Str.CtxMenu_Paste.Text(), actions.Paste, Disabled: disabled, Quick: item.Quick,
                    Shortcut: KeyChord.Ctrl(ChordKey.V)),
                ShellCommand.SelectAll => new(Glyphs.SelectAll, Str.Shell_Menu_SelectAll.Text(), actions.SelectAll, Disabled: disabled,
                    Shortcut: KeyChord.Ctrl(ChordKey.A)),
                // [S-65] Edit mode, checked: this menu is edit mode's; the line leaves it.
                ShellCommand.ToggleEditMode => new(Glyphs.Edit, Str.Shell_Menu_EditMode.Text(), actions.Done, Checked: true),
                _ => ItemMenuEntry.Separator,
            });
        }

        return entries;
    }

    /// <summary>
    /// [S-1998] The add page from a key (the global Ctrl+Alt+Shift+W, or Ctrl+N in edit mode), under this
    /// display's bar. The surface takes the foreground first — a hotkey press is what lets it — or the
    /// page's search box would get none of the keys typed into it ([S-11708]).
    /// </summary>
    internal void OpenAddMenuFromKeys()
    {
        if (_closed || !EditMode)
        {
            return;
        }

        try
        {
            if (!TakeForeground())
            {
                Log.Info(Scope, "could not take the foreground for the add page; typing will not reach its search");
            }

            OpenAddPage();
        }
        catch (Exception ex)
        {
            // Reached from a hotkey or a key press: nothing above may see it.
            Log.Error(Scope, "could not open the add menu from the keyboard", ex);
        }
    }

}

/// <summary>[S-1744] What each command of the surface menu does.</summary>
internal readonly record struct SurfaceMenuActions(
    Action AddWidget, Action View, Action Display, Action Settings, Action Paste, Action SelectAll, Action Done);
