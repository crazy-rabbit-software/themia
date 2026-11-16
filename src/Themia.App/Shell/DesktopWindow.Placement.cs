using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Foundation;
using Windows.Graphics;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Themia.App.Shell;

/// <summary>
/// Window plumbing: the borderless, transparent, never-activated HWND, where it sits on its
/// display, its backdrop and theme, and showing and closing it.
/// </summary>
internal sealed partial class DesktopWindow
{
    private void ConfigureWindow()
    {
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        _appWindow = appWindow;
        _presenter = appWindow.Presenter as OverlappedPresenter;
        ApplyChromeless();

        appWindow.IsShownInSwitchers = false;
        AppIcon.Apply(appWindow);
        _ = _appWindow; // retained for placement and future presenter work

        ApplyPlacement(appWindow);

        // [S-851] Cut to the work area from the start, before the surface is ever shown.
        ApplyHitRegion();

        // A fully transparent backdrop is what lets the wallpaper show between the cards. Without
        // it the window paints its own opaque surface over the whole monitor.
        TryApplyTransparentBackdrop();

        ApplyTheme();

        // Hand the surface to the z-order controller for good: edit mode makes the window
        // activatable, and an activatable window is raised above its neighbours when clicked unless
        // something says otherwise.
        DesktopZOrder.Track(_hwnd);
        DesktopLayer.Apply(_hwnd, interactive: false);
    }

    /// <summary>
    /// [S-23] Put the window over its monitor — the <b>whole</b> monitor, taskbar included ([S-840]).
    /// <para>
    /// Not the work area: the grid and every stored fraction resolve against this window, so a window
    /// that followed the work area would move and rescale every widget each time the taskbar was
    /// resized, moved or set to auto-hide. The taskbar stays usable because it is topmost and because
    /// the hit region never covers it ([S-851]).
    /// </para>
    /// <para>
    /// <c>AppWindow.MoveAndResize</c> takes <b>physical</b> pixels, which is what
    /// <c>GetMonitorInfo</c> reports, so the bounds go across unconverted. The display scale only
    /// matters inside the window, where XAML lays out in DIPs — see <see cref="Metrics"/>.
    /// </para>
    /// </summary>
    private void ApplyPlacement(AppWindow appWindow)
    {
        var area = SurfaceWindow();
        appWindow.MoveAndResize(new RectInt32((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height));

        // [S-852] And said to the shell in its own words: this window is never a full-screen ("rude") app.
        if (!Win32.SetProp(_hwnd, "NonRudeHWND", 1))
        {
            Log.Warn(Scope, $"{_monitor.GdiName}: could not mark the surface as no full-screen app (win32 {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()})");
        }
    }

    /// <summary>[S-852] Where this surface's window stands: its display, a pixel short of covering it (<see cref="GridGeometry.SurfaceWindow"/>).</summary>
    private PixelRect SurfaceWindow()
    {
        var bounds = _monitor.Bounds;
        return GridGeometry.SurfaceWindow(new PixelRect(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
    }

    /// <summary>
    /// [S-844] The display pass found this surface's monitor changed: take the new bounds and work
    /// area. The cards are kept; only the window moves (when the monitor did) and the layout, hit
    /// region and chrome insets are redone. A work-area change alone moves no widget ([S-840]).
    /// </summary>
    public void UpdateMonitor(MonitorTarget next)
    {
        if (_closed)
        {
            return;
        }

        var moved = next.Info.Bounds != _monitor.Info.Bounds;
        _monitor = next;
        _window.Title = $"Themia ({next.GdiName})";

        // [S-3507] The Display crumb names the display as the pass found it.
        ShowToolbar();

        if (moved && _appWindow is not null)
        {
            Log.Info(Scope, $"{next.GdiName} moved or changed size; re-placing its surface");
            ApplyPlacement(_appWindow);
        }

        // [S-851] The region is the work area relative to the window: either may have changed.
        ApplyHitRegion();
        Relayout();
    }

    /// <summary>
    /// [S-849] Put the window back over its monitor if anything moved it. A display-scale change makes
    /// Windows resize every window on that monitor to a "suggested" rect scaled from the old one,
    /// which for a full-monitor surface spills onto the neighbouring display.
    /// </summary>
    public void EnsurePlacement()
    {
        if (_closed || _appWindow is null || !Win32.GetWindowRect(_hwnd, out var actual))
        {
            return;
        }

        var wanted = SurfaceWindow();
        if (actual.Left == (int)wanted.X && actual.Top == (int)wanted.Y && actual.Width == (int)wanted.Width && actual.Height == (int)wanted.Height)
        {
            return;
        }

        Log.Info(Scope, $"surface for {_monitor.GdiName} was at {actual.Left},{actual.Top} {actual.Width}x{actual.Height}; " +
            $"re-placing over the monitor at {wanted.X},{wanted.Y} {wanted.Width}x{wanted.Height}");
        ApplyPlacement(_appWindow);
        ApplyHitRegion();
        Relayout();
    }

    /// <summary>
    /// Strip the border and title bar.
    /// <para>
    /// Re-applied whenever the window's styles are touched, not just at creation: toggling edit mode
    /// changes the extended styles and activation, and the presenter restores its default frame in
    /// response — which is how the surface grew a title bar the moment you started editing.
    /// </para>
    /// </summary>
    private void ApplyChromeless()
    {
        if (_presenter is not null)
        {
            _presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            _presenter.IsResizable = false;
            _presenter.IsMinimizable = false;
            _presenter.IsMaximizable = false;
            _presenter.IsAlwaysOnTop = false;
        }

        // The presenter alone leaves WS_CAPTION in place, so finish the job at the Win32 level.
        DesktopLayer.MakeChromeless(_hwnd);
    }

    private void TryApplyTransparentBackdrop()
    {
        try
        {
            _window.SystemBackdrop = new TransparentBackdrop();
        }
        catch (Exception ex)
        {
            // Without a transparent backdrop the surface would be an opaque sheet over the desktop,
            // which is worse than no widgets at all — so say so loudly rather than ship a black screen.
            Log.Error(Scope, "transparent backdrop unavailable; the widget surface will not be see-through", ex);
        }
    }

    private void ApplyTheme()
    {
        // [S-822] The engine's effective theme, so the controls' theme resources and the brushes
        // code hands out agree even under "use system setting".
        _root.RequestedTheme = DesignApplier.Theme;
    }

    public void ShowSurface()
    {
        // Shown without activation: starting the app must not pull focus away from whatever the
        // user is doing, and the surface belongs behind their windows anyway.
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        AppWindow.GetFromWindowId(windowId).Show(activateWindow: false);

        DesktopZOrder.Reconcile();
        Relayout();
    }

    public void CloseSurface()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;

        // [S-5544] A band this surface holds the pointer for ends with it, on every display.
        EndDesktopBand(null);

        // [S-802] A surface going away takes a lease one of its widgets held with it.
        _host.KeyboardEndOn(this);
        _window.Activated -= OnWindowActivated;

        // [S-1021] Before the cards go: a drag in flight may be talking to one of their views.
        DetachDropTarget();

        // [S-1017] A show in flight ends (its overlay and tiles go) before the cards do.
        _show?.Stop();

        DesktopZOrder.Forget(_hwnd);
        _host.StateChanged -= OnStateChanged;
        _host.EditModeChanged -= OnEditModeChanged;
        _host.SelectionChanged -= OnSelectionChanged;
        _host.HistoryChanged -= OnHistoryChanged;
        _host.PlacementChanged -= OnPlacementChanged;
        _host.Clipboard.Changed -= OnClipboardChanged;
        _host.SettingsOutdated -= SyncBarMenu;
        _host.SuspendedChanged -= OnSuspendedForSettings;

        // [S-3557] A settings page open here closes with the surface: with the last one the accounts' forms empty.
        CloseFlyout(focusEntry: false);

        Strings.Changed -= OnLanguageChanged;

        foreach (var card in _cards)
        {
            card.Detach();
        }

        _cards.Clear();
        _window.Close();
    }
}
