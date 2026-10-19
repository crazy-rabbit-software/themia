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
/// The widget surface for one display: a borderless, transparent, never-focused window covering the
/// <b>whole monitor</b> ([S-840]), holding every card in that monitor's view.
/// <para>
/// One window per monitor rather than one per widget. A widget is a card on a shared pane, so the
/// process pays for one HWND, one composition target and one render thread per display no matter
/// how many widgets the user places.
/// </para>
/// </summary>
internal sealed partial class DesktopWindow : IWidgetCardHost
{
    private const string Scope = "surface";

    private readonly AppHost _host;
    private readonly Window _window;
    private readonly Canvas _widgetLayer;
    private readonly SnapGridOverlay _gridOverlay;
    private readonly EditBar _toolbar;

    /// <summary>[S-3707] The selected widgets' properties strip, on its own layer over the cards and under the edit bar.</summary>
    private readonly PropertiesStrip _properties;

    private readonly Canvas _propertiesLayer = new();
    private readonly Border _placementHint;
    private readonly TextBlock _placementHintText;
    private readonly Canvas _ghostLayer;

    /// <summary>[S-1585] The ghost's outlines, one per widget being placed; pooled, grown on demand.</summary>
    private readonly List<GhostCard> _ghosts = [];

    private readonly Grid _root;

    /// <summary>
    /// [S-1113] What menus opened on the bare surface hang from: a full-size, never hit-tested layer
    /// that flows with the language, so those menus mirror while the surface's own layers (screen
    /// geometry) do not. Flyouts take their direction from the element they open on.
    /// </summary>
    private readonly Grid _menuAnchor = new() { IsHitTestVisible = false };
    private readonly List<WidgetCard> _cards = [];

    private MonitorTarget _monitor;
    private nint _hwnd;
    private AppWindow? _appWindow;
    private OverlappedPresenter? _presenter;
    private bool _closed;

    public DesktopWindow(AppHost host, MonitorTarget monitor)
    {
        _host = host;
        _monitor = monitor;

        _widgetLayer = new Canvas();

        _gridOverlay = new SnapGridOverlay { Visibility = Visibility.Collapsed };

        _toolbar = BuildToolbar();
        _properties = BuildProperties();
        _propertiesLayer.Children.Add(_properties);

        // [S-906] The view bar sits on its own layer, over the widgets and under the toolbar.
        _viewBar = new ViewBar(this) { Visibility = Visibility.Collapsed };
        _viewBar.SizeChanged += OnViewBarSizeChanged;
        _barLayer.Children.Add(_viewBar);

        _placementHintText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _placementHint = BuildPlacementHint(_placementHintText);

        // Never hit-testable: the ghost shows where a click will land, so it must not be the
        // thing that catches the click. The rubber band ([S-1581]) is drawn here too, for the same reason.
        _ghostLayer = new Canvas { IsHitTestVisible = false };

        _root = new Grid
        {
            // Transparent, not null: the root still has to receive the pointer input that edit mode
            // needs, while the window itself shows the wallpaper straight through.
            Background = new SolidColorBrush(Colors.Transparent),
            Children = { _menuAnchor, _gridOverlay, _widgetLayer, _barLayer, _ghostLayer, _propertiesLayer, _toolbar, _placementHint },

            // Focusable so edit-mode keyboard commands (copy / cut / paste / delete) and the keys of
            // a widget holding the keyboard lease ([S-800]) land here.
            IsTabStop = true,
        };

        // [S-860] Lease keys tunnel to the holder before a ScrollViewer inside its card can take
        // them; the bubble is for edit-mode shortcuts and the lease's Escape.
        _root.PreviewKeyDown += OnPreviewKeyDown;
        _root.KeyDown += OnKeyDown;
        _root.KeyUp += OnKeyUp;

        // [S-1997] Typed characters for the holder's type-ahead.
        _root.CharacterReceived += OnCharacterReceived;

        _root.PointerPressed += OnRootPointerPressed;
        AttachShellCleanup();
        AttachSession();
        _root.PointerMoved += OnRootPointerMoved;
        _root.PointerExited += OnRootPointerExited;
        _root.PointerReleased += OnRootPointerReleased;
        _root.PointerCaptureLost += OnRootPointerCaptureLost;
        _root.RightTapped += OnRootRightTapped;

        _window = new Window
        {
            Title = $"Themia ({monitor.GdiName})",
            Content = _root,
        };

        ConfigureWindow();

        // [S-802] Losing activation is how a click on the desktop or another app ends the lease.
        _window.Activated += OnWindowActivated;

        _root.SizeChanged += (_, _) => Relayout();
        _root.Loaded += OnRootLoaded;
        _root.Loaded += OnRootLoadedForDrops;
        _host.StateChanged += OnStateChanged;
        _host.EditModeChanged += OnEditModeChanged;
        _host.SelectionChanged += OnSelectionChanged;
        _host.PlacementChanged += OnPlacementChanged;
        _host.Clipboard.Changed += OnClipboardChanged;

        // [S-1110] One central redraw when the language changes; no card follows it on its own.
        ApplyFlow();
        Strings.Changed += OnLanguageChanged;
    }

    // -----------------------------------------------------------------------------------------
    // IWidgetCardHost
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// The DIP-to-physical factor XAML is actually rendering at.
    /// <para>
    /// Read from the XamlRoot rather than assumed from the monitor: <c>GetDpiForMonitor</c> answers
    /// per the calling process's DPI awareness, and at startup it reported 96 for a 144-DPI display,
    /// which would put every widget in the wrong place.
    /// </para>
    /// </summary>
    private double RasterScale
    {
        get
        {
            var scale = _root.XamlRoot?.RasterizationScale ?? 0;
            return scale > 0 ? scale : 1.0;
        }
    }

    /// <summary>
    /// The coordinate space fractional rects resolve into: the window's client area in DIPs, which is
    /// the whole monitor ([S-840]), with the work area as the region new geometry may use ([S-841]).
    /// <para>
    /// Derived from the real client rect divided by the real rasterization scale, <b>not</b> from
    /// <c>ActualWidth</c>. On a scaled display the root's ActualWidth was observed still holding the
    /// pre-DPI size (physical pixels treated as DIPs) because changing scale alone resizes the
    /// content without changing the client's pixel size, so no SizeChanged ever corrected it — and
    /// every widget rendered 1.5x too far right and down, with the far edge off-screen.
    /// </para>
    /// </summary>
    public ScreenMetrics Metrics
    {
        get
        {
            var info = _monitor.Info;
            var allowed = GridGeometry.AllowedArea(info.Bounds, info.WorkArea);
            var scale = RasterScale;

            if (Win32.GetClientRect(_hwnd, out var client) && client.Width > 0 && client.Height > 0)
            {
                return new ScreenMetrics(client.Width / scale, client.Height / scale) { Allowed = allowed };
            }

            return new ScreenMetrics(_monitor.Bounds.Width / scale, _monitor.Bounds.Height / scale) { Allowed = allowed };
        }
    }

    public DesignState Design => _host.State.Design;

    public Ticker Ticker => _host.Ticker;

    public bool EditMode => _host.EditMode;

    /// <summary>The display this surface covers.</summary>
    public MonitorTarget Monitor => _monitor;

    /// <summary>False once the surface is closed or its window was destroyed under it ([S-844] rebuilds it).</summary>
    public bool IsAlive => !_closed && Win32.IsWindow(_hwnd);

    /// <summary>
    /// [S-850]/[S-6532] How much a widget's content is magnified on this display: the display's own
    /// scale times the app-wide widget scale times the widget's own. Content only — a widget's cell
    /// stays where it is and the same size.
    /// </summary>
    private double ContentScale(WidgetInstance widget)
    {
        var display = _host.State.MonitorScales.TryGetValue(_monitor.Id, out var own) ? own : 1.0;
        return WidgetScale.Effective(display, Design.UiScale, widget.Scale);
    }

    /// <summary>The id of the view this surface is currently showing.</summary>
    public string CurrentViewId => ViewForThisMonitor(_host.State).Id;

    public bool IsUnderCursor
    {
        get
        {
            return Win32.GetCursorPos(out var cursor) && _monitor.Contains(cursor.X, cursor.Y);
        }
    }

    /// <summary>A resize in progress: follow it. Moves go through <see cref="PreviewMove"/>.</summary>
    public void OnGeometryPreview(WidgetCard card)
    {
        PositionCard(card, card.LiveRect);

        // [S-3710] The strip steps aside while the widget is resized.
        HoldProperties(true);
    }

    /// <summary>A resize ended: persist it, as one undo step ([S-884]). A resize never leaves its screen ([S-78]).</summary>
    public void OnGeometryCommitted(WidgetCard card)
    {
        PositionCard(card, card.LiveRect);
        _host.CommitGeometry(UndoAction.Resize);
        HoldProperties(false);
    }

    /// <summary>Where a physical screen point falls on this display, as a fraction of the whole monitor.</summary>
    private (double X, double Y) FractionOf(Win32.Point point)
    {
        var area = _monitor.Bounds;
        if (area.Width <= 0 || area.Height <= 0)
        {
            return (0.5, 0.5);
        }

        return (
            Math.Clamp((point.X - area.Left) / (double)area.Width, 0, 1),
            Math.Clamp((point.Y - area.Top) / (double)area.Height, 0, 1));
    }

    public void RequestConfigure(WidgetCard card)
        => _host.ShowConfig(new ConfigTarget.Widget(card.Model.Id));

    /// <inheritdoc />
    public bool SettingsOpen => _host.ConfigOpen;

    /// <inheritdoc />
    public void RequestFollowInSettings(WidgetCard card)
        => _host.FollowConfig(new ConfigTarget.Widget(card.Model.Id));

    /// <summary>[S-1592] Delete the selection (the right-clicked card is in it).</summary>
    public void RequestDelete(WidgetCard card) => _host.DeleteSelected();

    public void RequestEditMode(bool enabled) => _host.SetEditMode(enabled);

    /// <inheritdoc />
    public ViewState? ViewForCards => _closed ? null : ViewForThisMonitor(_host.State);

    /// <summary>A widget edited its own config. Persist it, but do not re-lay-out: see [S-128].</summary>
    public void RequestSaveConfig() => _host.SaveGeometryOnly();

    public bool IsSelected(WidgetCard card) => _host.Selection.Contains(card.Model.Id);

    public int SelectionCount => _host.Selection.Count;

    public void RequestSelect(WidgetCard card) => _host.Select(card.Model.Id);

    public void RequestToggleSelect(WidgetCard card) => _host.ToggleSelect(card.Model.Id);

    public void RequestCopy(WidgetCard card) => _host.CopySelected();

    public void RequestCut(WidgetCard card) => _host.CutSelected();

    public void RequestPaste() => _host.Paste();

    public bool CanPaste => _host.Clipboard.HasContent;

#if DEBUG
    /// <summary>Debug only: the surface's content, for the switch check's picture ([S-1883]).</summary>
    internal FrameworkElement DebugRoot => _root;
#endif
}
