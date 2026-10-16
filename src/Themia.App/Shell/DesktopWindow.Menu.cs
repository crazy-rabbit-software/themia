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
/// widgets offers while arranging them. Same look as every other menu ([S-1740]); which commands it
/// has is <see cref="ShellMenus.ForSurface"/>'s.
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

            // Only the surface itself: a card, the toolbar and the view bar have menus of their own,
            // or none, and a right-click on them is not a right-click on the desktop.
            if (e.Handled || cancelled || _closed || !ReferenceEquals(e.OriginalSource, _root))
            {
                return;
            }

            if (OpenSurfaceMenu(e.GetPosition(_root)))
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
            HasWidgets: SelectionRules.All(view, HiddenIds()).Count > 0,
            GridSnap: Design.GridSnap);

        return ItemMenu.ShowAt(_menuAnchor, OnMenuAnchor(at), SurfaceMenu(facts, at));
    }

    /// <summary>[S-1744] The entries for this surface. <paramref name="at"/> is where the picker opens.</summary>
    private List<ItemMenuEntry> SurfaceMenu(SurfaceMenuFacts facts, Point at)
        => SurfaceEntries(facts, new SurfaceMenuActions(
            Paste: _host.Paste,
            Settings: _host.ShowSettings,
            AddWidget: () => OpenPickerAt(at),
            SelectAll: SelectAll,
            ToggleSnap: ToggleSnap,
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
                ShellCommand.Paste => new(Glyphs.Paste, Str.CtxMenu_Paste.Text(), actions.Paste, Disabled: disabled, Quick: item.Quick,
                    Shortcut: KeyChord.Ctrl(ChordKey.V)),
                ShellCommand.Settings => new(Glyphs.Settings, Str.Common_Settings.Text(), actions.Settings, Quick: item.Quick),
                ShellCommand.AddWidget => new(Glyphs.Add, Str.Shell_Toolbar_AddWidget.Text(), actions.AddWidget,
                    Shortcut: KeyChord.Ctrl(ChordKey.N)),
                ShellCommand.SelectAll => new(Glyphs.SelectAll, Str.Shell_Menu_SelectAll.Text(), actions.SelectAll, Disabled: disabled,
                    Shortcut: KeyChord.Ctrl(ChordKey.A)),
                ShellCommand.SnapToGrid => new(Glyphs.Grid, Str.Shell_Toolbar_SnapName.Text(), actions.ToggleSnap, Checked: item.Checked == true),
                ShellCommand.ToggleEditMode => new(Glyphs.Done, Str.Shell_Card_DoneEditing.Text(), actions.Done),
                _ => ItemMenuEntry.Separator,
            });
        }

        return entries;
    }

    /// <summary>
    /// [S-1744] The Add-widget picker, opened where the right-click was rather than under the
    /// toolbar: choosing a widget then starts the placement that follows the pointer ([S-1587]).
    /// </summary>
    private void OpenPickerAt(Point at)
    {
        if (_closed || !EditMode)
        {
            return;
        }

        BuildAddFlyout().ShowAt(_menuAnchor, new FlyoutShowOptions { Position = OnMenuAnchor(at), Placement = FlyoutPlacementMode.Bottom, ShowMode = FlyoutShowMode.Standard });
    }

    /// <summary>
    /// [S-1998] The add menu from a key (the global Ctrl+Alt+Shift+W, or Ctrl+N in edit mode): at the
    /// pointer when it is on this display, else in the middle of the work area, as the Menu key opens
    /// the surface menu. The surface takes the foreground first — a hotkey press is what lets it —
    /// or the picker's search box would get none of the keys typed into it ([S-3514]).
    /// </summary>
    internal void OpenAddMenuFromKeys()
    {
        if (_closed || !EditMode)
        {
            return;
        }

        try
        {
            if (Win32.GetForegroundWindow() != _hwnd && !Win32.SetForegroundWindow(_hwnd))
            {
                Log.Info(Scope, "could not take the foreground for the add menu; typing will not reach its search");
            }

            Point at;
            var metrics = Metrics;

            if (IsUnderCursor && Win32.GetCursorPos(out var cursor))
            {
                var (x, y) = FractionOf(cursor);
                at = new Point(x * metrics.Width, y * metrics.Height);
            }
            else
            {
                var area = GridGeometry.AllowedPixels(metrics);
                at = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));
            }

            OpenPickerAt(at);
        }
        catch (Exception ex)
        {
            // Reached from a hotkey or a key press: nothing above may see it.
            Log.Error(Scope, "could not open the add menu from the keyboard", ex);
        }
    }

    /// <summary>[S-06] The grid snap aid, flipped as the toolbar's switch does; <see cref="SyncToolbar"/> follows.</summary>
    private void ToggleSnap()
    {
        var on = !Design.GridSnap;
        _host.Mutate(state => state.Design.GridSnap = on);
    }
}

/// <summary>[S-1744] What each command of the surface menu does.</summary>
internal readonly record struct SurfaceMenuActions(Action Paste, Action Settings, Action AddWidget, Action SelectAll, Action ToggleSnap, Action Done);
