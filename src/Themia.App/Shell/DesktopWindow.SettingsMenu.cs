using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell.Motion;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Motion;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-3520]..[S-3523] The edit bar's settings menu on this display: opened under ⚙, closed by ⚙
/// again, Escape, a press elsewhere on the surface, another edit-bar menu, a placement or leaving
/// edit mode; the selection's properties menu is away while it is open.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-3525] The desktop icons followed while the menu is open.</summary>
    private IDisposable? _settingsIcons;

    /// <summary>[S-3520] Whether the settings menu is open on this display.</summary>
    internal bool SettingsMenuOpen => _settingsMenu.Visibility == Visibility.Visible;

    private SettingsMenuView BuildSettingsMenu()
    {
        var menu = new SettingsMenuView(new SurfaceSettingsHost(this), () => CloseSettingsMenu(focusButton: true));

        // Its real height arrives with layout (a submenu's words in the current language): place it again.
        menu.SizeChanged += (_, _) => PlaceSettingsMenu();
        return menu;
    }

    /// <summary>[S-3521] ⚙ opens the menu, or closes it again.</summary>
    private void ToggleSettingsMenu()
    {
        if (SettingsMenuOpen)
        {
            CloseSettingsMenu(focusButton: false);
        }
        else
        {
            OpenSettingsMenu([]);
        }
    }

    /// <summary>
    /// [S-3520] [S-3534] Open the settings menu under ⚙ on this display, at <paramref name="path"/>
    /// (the lines' keys; empty for its first level), the keyboard in it. Only in edit mode and not
    /// while placing: the bar is not there otherwise.
    /// </summary>
    internal void OpenSettingsMenu(IReadOnlyList<string> path)
    {
        if (_closed || !EditMode || _host.Placing is not null)
        {
            return;
        }

        if (!SettingsMenuOpen)
        {
            // [S-3523] One menu at a time: the selection's goes away (the selection stays) and comes back on close.
            _properties.CloseSubmenus();
            _settingsMenu.Visibility = Visibility.Visible;
            _toolbar.SettingsOpen(true);
            PlaceProperties();

            // [S-3525] What Windows owns is read now and followed while the menu is open.
            _settingsIcons ??= _host.WatchDesktopIcons();
            _host.ReadStartup();
        }

        _settingsMenu.Show();
        _root.UpdateLayout();
        PlaceSettingsMenu();

        // [S-3520] The keyboard goes into it, on its first line or the deepest level opened.
        if (path.Count > 0 ? !_settingsMenu.Open(path, focus: true) : !_settingsMenu.FocusFirst())
        {
            Core.Diagnostics.Log.Debug(Scope, $"the settings menu opened, but not at '{string.Join('/', path)}'");
        }
    }

    /// <summary>[S-3521] Close the settings menu; the keyboard back on ⚙ where it was closed from the keyboard.</summary>
    private void CloseSettingsMenu(bool focusButton)
    {
        if (!SettingsMenuOpen)
        {
            return;
        }

        _settingsMenu.Visibility = Visibility.Collapsed;
        _settingsIcons?.Dispose();
        _settingsIcons = null;
        _toolbar.SettingsOpen(false);
        PlaceProperties();

        if (focusButton)
        {
            _toolbar.FocusSettings();
        }
    }

    /// <summary>[S-3531] The open menu shows the state as it is now (an undo, an import, another display's change).</summary>
    private void SyncSettingsMenu()
    {
        if (!_closed && SettingsMenuOpen)
        {
            _settingsMenu.Show();
            PlaceSettingsMenu();
        }
    }

    private void OnSuspendedForSettings(bool suspended) => SyncSettingsMenu();

    /// <summary>
    /// [S-3521] A press anywhere on the surface outside the menu (and not on ⚙, whose click toggles
    /// it) closes it, and the press goes on to do what it does: a click on a widget selects it.
    /// Seen with the presses the cards and the bars handle too.
    /// </summary>
    private void OnPressForSettingsMenu(object sender, PointerRoutedEventArgs e)
    {
        if (!SettingsMenuOpen || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, _settingsMenu) || _toolbar.IsSettingsButton(node))
            {
                return;
            }
        }

        CloseSettingsMenu(focusButton: false);
    }

    /// <summary>
    /// [S-3522] Under ⚙: its start edge level with the button's, 4 DIPs under the bar, inside the
    /// work area, clear of the view bar (<see cref="DropDownPlacement"/>); its submenus clear of the bars.
    /// </summary>
    private void PlaceSettingsMenu()
    {
        if (_closed || !SettingsMenuOpen)
        {
            return;
        }

        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var area = GridGeometry.AllowedPixels(metrics);
        var bar = ToolbarRect(area);
        var bars = new List<PixelRect> { bar };
        if (ViewBarBox() is { } viewBar)
        {
            bars.Add(viewBar);
        }

        var button = _toolbar.SettingsBox(_propertiesLayer) ?? new PixelRect(bar.X + bar.Width - (2 * EditBar.ButtonSize), bar.Y, EditBar.ButtonSize, bar.Height);
        _settingsMenu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _settingsMenu.DesiredSize;

        var (x, y) = DropDownPlacement.Place(button, bar, size.Width, size.Height, area, bars, UiLanguage.Flow == FlowDirection.RightToLeft);
        Canvas.SetLeft(_settingsMenu, Math.Round(x));
        Canvas.SetTop(_settingsMenu, Math.Round(y));

        // [S-3733] Its submenus keep clear of the bars and the levels above them.
        _settingsMenu.Room = new MenuRoom(area, null, bars);
    }

    /// <summary>[S-3524] What this display's settings menu reads and writes: the app's state and what Windows owns, through the host.</summary>
    private sealed class SurfaceSettingsHost(DesktopWindow surface) : ISettingsMenuHost
    {
        private AppHost Host => surface._host;

        public AppState State => Host.State;

        public AutostartStatus? Startup => Host.StartupStatus;

        public bool DesktopIconsShown => Host.DesktopIconsShown;

        public bool Suspended => Host.Suspended;

        public bool AnimationsEnabled => SystemMotion.AnimationsEnabled;

        public void Mutate(Action<AppState> change, bool relayout = true) => Host.Mutate(change, relayout);

        public void SetStartup(bool on) => Host.SetStartup(on);

        public void SetDesktopIcons(bool shown) => Host.SetDesktopIcons(shown);

        public void SetGameModeSuspend(bool on) => Host.SetGameModeSuspend(on);

        public void ReplayIntro(IntroMode mode) => Host.ReplayIntro(mode);

        public void Confirm(SettingsConfirm confirm) => surface._settingsMenu.Confirm(confirm);

        public void ShowAllSettings() => Host.ShowSettings();

        public void Quit() => Host.RequestExit();
    }
}
