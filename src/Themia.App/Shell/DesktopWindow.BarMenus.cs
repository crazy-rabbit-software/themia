using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Geometry;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-3520]..[S-3523] The edit bar's drop-down menus on this display, one mechanism for every one of
/// them (<see cref="BarMenu"/>): each opens under its button, at most one at a time, and closes on its
/// button again, Escape, a press elsewhere on the surface, another of the bar's menus opening, a
/// placement or leaving edit mode; the selection's properties menu is away while one is open.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-3520] The bar's menus, built once, on the properties' layer: over the cards, under the edit bar.</summary>
    private readonly Dictionary<BarMenu, BarMenuView> _barMenus = [];

    /// <summary>[S-3521] The bar's menu open on this display, if any: one at a time.</summary>
    private BarMenu? _openBarMenu;

    /// <summary>[S-3520] Whether one of the edit bar's menus is open on this display.</summary>
    internal bool BarMenuOpen => _openBarMenu is not null;

    /// <summary>The open menu, if any.</summary>
    private BarMenuView? OpenBarMenuView => _openBarMenu is { } open ? _barMenus[open] : null;

    /// <summary>Build the bar's menus and put them on the properties' layer, hidden.</summary>
    private void BuildBarMenus()
    {
        Add(BarMenu.Settings, SettingsMenuLines());

        void Add(BarMenu which, Func<IReadOnlyList<Core.Settings.SettingsLine>> lines)
        {
            var menu = new BarMenuView(which.ToString(), lines, () => CloseBarMenu(focusButton: true));

            // Its real height arrives with layout (a submenu's words in the current language): place it again.
            menu.SizeChanged += (_, _) => PlaceBarMenu();
            _barMenus[which] = menu;
            _propertiesLayer.Children.Add(menu);
        }
    }

    /// <summary>[S-3521] A menu's button opens it, or closes it again; another menu open closes first.</summary>
    private void ToggleBarMenu(BarMenu which)
    {
        if (_openBarMenu == which)
        {
            CloseBarMenu(focusButton: false);
        }
        else
        {
            OpenBarMenu(which, []);
        }
    }

    /// <summary>
    /// [S-3520] [S-3534] Open one of the bar's menus under its button on this display, at
    /// <paramref name="path"/> (the lines' keys; empty for its first level), the keyboard in it. Only
    /// in edit mode and not while placing: the bar is not there otherwise. Another menu open closes.
    /// </summary>
    internal void OpenBarMenu(BarMenu which, IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_closed || !EditMode || _host.Placing is not null)
        {
            return;
        }

        if (_openBarMenu is { } other && other != which)
        {
            CloseBarMenu(focusButton: false);
        }

        var menu = _barMenus[which];
        if (_openBarMenu is null)
        {
            // [S-3523] One menu at a time: the selection's goes away (the selection stays) and comes back on close.
            _properties.CloseSubmenus();
            _openBarMenu = which;
            menu.Visibility = Visibility.Visible;
            _toolbar.MenuOpen(which);
            PlaceProperties();
            BarMenuOpened(which);
        }

        menu.Show();
        _root.UpdateLayout();
        PlaceBarMenu();

        // [S-3520] The keyboard goes into it, on its first line or the deepest level opened.
        if (path.Count > 0 ? !menu.Open(path, focus: true) : !menu.FocusFirst())
        {
            Core.Diagnostics.Log.Debug(Scope, $"the {which} menu opened, but not at '{string.Join('/', path)}'");
        }
    }

    /// <summary>[S-3521] Close the open menu; the keyboard back on its button where it was closed from the keyboard.</summary>
    private void CloseBarMenu(bool focusButton)
    {
        if (_openBarMenu is not { } which)
        {
            return;
        }

        _openBarMenu = null;
        _barMenus[which].Visibility = Visibility.Collapsed;
        BarMenuClosed(which);
        _toolbar.MenuOpen(null);
        PlaceProperties();

        if (focusButton)
        {
            _toolbar.FocusMenu(which);
        }
    }

    /// <summary>What a menu follows only while it is open starts (the settings menu: what Windows owns).</summary>
    private void BarMenuOpened(BarMenu which)
    {
        if (which == BarMenu.Settings)
        {
            SettingsMenuOpened();
        }
    }

    /// <summary>What a menu followed while it was open stops.</summary>
    private void BarMenuClosed(BarMenu which)
    {
        if (which == BarMenu.Settings)
        {
            SettingsMenuClosed();
        }
    }

    /// <summary>[S-3531] The open menu shows the state as it is now (an undo, an import, another display's change).</summary>
    private void SyncBarMenu()
    {
        if (!_closed && OpenBarMenuView is { } menu)
        {
            menu.Show();
            PlaceBarMenu();
        }
    }

    /// <summary>[S-3531] A language switch: the open menu's words in the new language; the keys stay, so what is open stays open.</summary>
    private void RebuildBarMenu()
    {
        if (OpenBarMenuView is { } menu)
        {
            menu.Show(rebuild: true);
            PlaceBarMenu();
        }
    }

    /// <summary>
    /// [S-3521] A press anywhere on the surface outside the open menu (and not on one of the bar's
    /// menu buttons, whose click toggles or swaps it) closes it, and the press goes on to do what it
    /// does: a click on a widget selects it. Seen with the presses the cards and the bars handle too.
    /// </summary>
    private void OnPressForBarMenu(object sender, PointerRoutedEventArgs e)
    {
        if (OpenBarMenuView is not { } menu || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, menu) || _toolbar.IsMenuButton(node))
            {
                return;
            }
        }

        CloseBarMenu(focusButton: false);
    }

    /// <summary>
    /// [S-3522] Under its button: its start edge level with the button's, 4 DIPs under the bar, inside
    /// the work area, clear of the view bar (<see cref="DropDownPlacement"/>); its submenus clear of the bars.
    /// </summary>
    private void PlaceBarMenu()
    {
        if (_closed || _openBarMenu is not { } which)
        {
            return;
        }

        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var menu = _barMenus[which];
        var area = GridGeometry.AllowedPixels(metrics);
        var bar = ToolbarRect(area);
        var bars = new List<PixelRect> { bar };
        if (ViewBarBox() is { } viewBar)
        {
            bars.Add(viewBar);
        }

        var button = _toolbar.MenuButtonBox(which, _propertiesLayer) ?? new PixelRect(bar.X + bar.Width - (2 * EditBar.ButtonSize), bar.Y, EditBar.ButtonSize, bar.Height);
        menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = menu.DesiredSize;

        var (x, y) = DropDownPlacement.Place(button, bar, size.Width, size.Height, area, bars, UiLanguage.Flow == FlowDirection.RightToLeft);
        Canvas.SetLeft(menu, Math.Round(x));
        Canvas.SetTop(menu, Math.Round(y));

        // [S-3733] Its submenus keep clear of the bars and the levels above them.
        menu.Room = new MenuRoom(area, null, bars);
    }
}
