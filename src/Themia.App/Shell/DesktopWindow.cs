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
using Themia.Core.Shell;
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

    /// <summary>[S-11874] The local grid of a move on the locked desktop, under the cards.</summary>
    private readonly LocalGridOverlay _localGrid = new();
    private readonly EditBar _editBar;


    private readonly Border _placementHint;
    private readonly TextBlock _placementHintText;
    private readonly Canvas _ghostLayer;

    /// <summary>
    /// [S-13026] [S-13713] The drag's picture under the pointer, over every other layer (the bar's flyouts too); never hit-testable,
    /// so the drop is found under it.
    /// </summary>
    private readonly Canvas _pictureLayer = new() { IsHitTestVisible = false };

    /// <summary>[S-1585] The ghost's outlines, one per widget being placed; pooled, grown on demand.</summary>
    private readonly List<GhostCard> _ghosts = [];

    private readonly DesktopRoot _root;

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

    /// <summary>[S-31b] The cards' edit markers, drawn above all of them so neighbours' marks meet as one line.</summary>
    private readonly EditMarkerLayer _markerLayer;

    public DesktopWindow(AppHost host, MonitorTarget monitor)
    {
        _host = host;
        _monitor = monitor;

        _widgetLayer = new Canvas();

        // [S-31b] Every card's edit markers over every card: halos, then bars.
        _markerLayer = new EditMarkerLayer();

        _gridOverlay = new SnapGridOverlay { Visibility = Visibility.Collapsed };

        _editBar = BuildEditBar();

        // [S-906] The view bar sits on its own layer, over the widgets and under the edit bar.
        _viewBar = new ViewBar(this) { Visibility = Visibility.Collapsed };
        _viewBar.SizeChanged += OnViewBarSizeChanged;
        _barLayer.Children.Add(_viewBar);

        _placementHintText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _placementHint = BuildPlacementHint(_placementHintText);

        // Never hit-testable: the ghost shows where a click will land, so it must not be the
        // thing that catches the click. The rubber band ([S-1581]) is drawn here too, for the same reason.
        _ghostLayer = new Canvas { IsHitTestVisible = false };

        // [S-5571] A screen reader sees the desktop's list outside edit mode.
        _root = new DesktopRoot(() => !_closed && !EditMode)
        {
            // Transparent, not null: the root still has to receive the pointer input that edit mode
            // needs, while the window itself shows the wallpaper straight through.
            Background = Themia.Sdk.WinUI.Kit.Family.Brush(Themia.Sdk.Kit.ColourRole.Clear),
            Children = { _menuAnchor, _gridOverlay, _localGrid, _widgetLayer, _markerLayer.Overhangs, _markerLayer.Halos, _markerLayer.Inks, _markerLayer.Lifts, _barLayer, _ghostLayer, _galleryLayer, _editBar, _flyoutLayer, _pictureLayer, _placementHint },

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

        // [S-3521] A press anywhere outside the edit bar's open menu closes it, the presses the cards take too.
        _root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPressForBarMenu), handledEventsToo: true);
        AttachShellCleanup();
        AttachSession();
        _root.PointerMoved += OnRootPointerMoved;
        _root.PointerExited += OnRootPointerExited;
        _root.PointerReleased += OnRootPointerReleased;
        _root.PointerCaptureLost += OnRootPointerCaptureLost;
        _root.RightTapped += OnRootRightTapped;

        // [S-13017] The foreground a press asked for is taken when it lets go, whoever handled the release; a shell
        // drag that took the press over ends with no release here, so the next move with no button down counts.
        _root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => RunForegroundAfterPress()), handledEventsToo: true);
        _root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, _) =>
        {
            RunForegroundAfterPress();

            // [S-13026] A drag that ended where no leave reached this surface (cancelled away from it) leaves no picture.
            if (_dragImageOf is not null && Themia.Sdk.WinUI.ItemDrag.Running is null)
            {
                HideDragPicture();
            }
        }), handledEventsToo: true);

        // [S-5593] Ctrl + wheel on the desktop steps its icon size.
        _root.PointerWheelChanged += OnDesktopWheel;

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
        _host.SettingsOutdated += SyncBarMenu;
        _host.SuspendedChanged += OnSuspendedForSettings;
        _host.DesktopListed += OnDesktopListedForUnplaced;

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
    /// The coordinate space fractional rects resolve into: the whole monitor in DIPs ([S-840]), with the work area as
    /// the region new geometry may use ([S-841]).
    /// <para>
    /// [S-05] Derived from the monitor's real bounds divided by the real rasterization scale, <b>not</b> from
    /// <c>ActualWidth</c>. On a scaled display the root's ActualWidth was observed still holding the
    /// pre-DPI size (physical pixels treated as DIPs) because changing scale alone resizes the
    /// content without changing the client's pixel size, so no SizeChanged ever corrected it — and
    /// every widget rendered 1.5x too far right and down, with the far edge off-screen. [S-11892] Nor from the
    /// window's client rect: the window is a pixel short of its display ([S-852]).
    /// </para>
    /// <para>
    /// [S-855] Its allowed area is the part of the screen every display showing this view holds in its work area: a
    /// view on a display whose taskbar is on the left and on one whose taskbar is on the right takes new geometry
    /// only where both show it. This display's own chrome stands in its own work area (<see cref="WorkArea"/>).
    /// </para>
    /// <para>
    /// [S-15668] Kept between reads (a drag step reads them many times): made again when the scale this surface renders at,
    /// its display or the view it shows is another, and whenever the host forgets them (<see cref="ForgetMetrics"/>: every
    /// change of the state, an undo step, a display pass), since another display's view or work area decides the allowed area.
    /// </para>
    /// </summary>
    public ScreenMetrics Metrics
    {
        get
        {
            var scale = RasterScale;
            var view = CurrentViewId;
            if (_metrics is { } kept && kept.Scale.Equals(scale) && kept.Monitor == _monitor && string.Equals(kept.View, view, StringComparison.Ordinal))
            {
                return kept.Metrics;
            }

            var metrics = OwnMetrics with { Allowed = _host.SharedAllowed(view, OwnAllowed) };
            _metrics = (scale, _monitor, view, metrics);
            return metrics;
        }
    }

    /// <summary>[S-15668] The metrics as last made, and what they were made for; null when forgotten.</summary>
    private (double Scale, MonitorTarget Monitor, string View, ScreenMetrics Metrics)? _metrics;

    /// <summary>[S-15668] Forget the kept metrics: the next read makes them again.</summary>
    internal void ForgetMetrics() => _metrics = null;

    /// <summary>
    /// [S-841] This display's metrics with its own work area as the allowed area. [S-11892] The display's bounds, never
    /// the window's client rect: the window is a pixel short at the foot ([S-852]), and fractions resolved against it
    /// put the grid's rows off whole DIPs (<see cref="GridGeometry.DisplayScreen"/>).
    /// </summary>
    private ScreenMetrics OwnMetrics
    {
        get
        {
            var bounds = _monitor.Bounds;
            return GridGeometry.DisplayScreen(new DeviceRect(bounds.Left, bounds.Top, bounds.Width, bounds.Height), RasterScale) with { Allowed = OwnAllowed };
        }
    }

    /// <summary>[S-841] This display's work area as a fraction of its whole monitor.</summary>
    internal FracRect OwnAllowed => GridGeometry.AllowedArea(_monitor.Info.Bounds, _monitor.Info.WorkArea);

    /// <summary>
    /// [S-852] [S-855] This display's own work area in its DIPs: where its chrome stands (the edit bar, its flyouts and the
    /// add page, the view bar, the notices, the placement line), whatever another display showing the view reserves.
    /// </summary>
    private DipRect WorkArea => GridGeometry.AllowedDips(OwnMetrics);

    public DesignState Design => _host.State.Design;

    public Ticker Ticker => _host.Ticker;

    public bool EditMode => _host.EditMode;

    /// <summary>The display this surface covers.</summary>
    public MonitorTarget Monitor => _monitor;

    /// <summary>[S-2118] This display's blurred wallpaper, shared by its cards.</summary>
    public Backdrop? Backdrop => AppServices.Backdrops?.For(_monitor.Info.Bounds);

    /// <summary>False once the surface is closed or its window was destroyed under it ([S-844] rebuilds it).</summary>
    public bool IsAlive => !_closed && Win32.IsWindow(_hwnd);

    /// <summary>
    /// [S-850]/[S-6532] How much a widget's content is magnified on this display: the display's own
    /// scale times the widget's own; a desktop icon none ([S-5593]: <see cref="WidgetScale.For"/>). Content only —
    /// a widget's cell stays where it is and the same size.
    /// </summary>
    private double ContentScale(WidgetInstance widget, double? previewed = null)
    {
        var display = _host.State.MonitorScales.TryGetValue(_monitor.Id, out var own) ? own : 1.0;
        return WidgetScale.For(widget, display, previewed);
    }

    /// <summary>[S-2444] The content scale a widget of this display is drawn at: what its sizing is multiplied by.</summary>
    internal double ContentScaleOf(WidgetInstance widget) => ContentScale(widget);

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

        // [S-13916] A container resized: its parts and days follow at once, as the drop will lay them.
        PreviewContainerResize(card);

        // [S-2244] The card being resized is free; its neighbours get their corners back.
        JoinCards();

        // [S-2343] [S-3710] Nothing hangs over a widget that is being resized.
        ClearForDrag();
    }

    /// <summary>A resize ended: landed where the card shows it, as one undo step ([S-884], [S-15662]). A resize never leaves its screen ([S-78]).</summary>
    public void OnGeometryCommitted(WidgetCard card)
    {
        var rect = card.LiveRect;
        PositionCard(card, rect);
        JoinCards();

        // [S-13040] A fence resized is laid again in the step.
        _host.Land(this, [new Landing(card.Model.Id, rect)], UndoAction.Resize);
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

    /// <summary>
    /// [S-1862] Set up goes where the missing piece is set: the Accounts page when an account is all
    /// the widget lacks ([S-3558]); otherwise edit mode, the widget selected alone and its page opened
    /// at the row its module names, or the page as it stands where it names none.
    /// </summary>
    public void RequestSetup(WidgetCard card)
    {
        var id = card.Model.Id;
        var signedIn = WidgetSetup.Offered(AppServices.Accounts?.Accounts.Select(a => a.Provider) ?? []);
        var offerable = WidgetSetup.Offered(Themia.Sdk.ProviderCatalog.Offered.Select(p => p.Id));
        if (WidgetSetup.Goes(card.Model.Config, signedIn, offerable) == SetupGoes.Accounts)
        {
            RequestAccounts();
            return;
        }

        _host.SetEditMode(true);
        _host.Select(id);
        SyncProperties();
        OpenSelectionPage(focus: false);
        if (_flyout is not { } flyout || !flyout.Page.OpenSetup())
        {
            Log.Debug(Scope, $"widget '{id}' ({card.Model.Kind}) names no setup line; its menu stands as it is");
        }
    }

    /// <summary>[S-15102] The card menu's Widget settings: <see cref="RequestSettingsFor"/> its widget.</summary>
    public void RequestSettings(WidgetCard card) => RequestSettingsFor(card.Model);

    /// <summary>
    /// [S-15102] A widget's settings from the locked desktop (its card menu's Widget settings, Alt+Enter on its focused card):
    /// edit mode on, the widget selected (with the several the desktop's selection holds here when it is one of them) and the
    /// selection's page opened beside it, as the Set up button opens it ([S-1862]).
    /// </summary>
    public void RequestSettingsFor(WidgetInstance widget)
    {
        if (_closed)
        {
            return;
        }

        _host.ShowWidgetSettings(this, widget);
        SyncProperties();
        OpenSelectionPage(focus: false);
    }

    /// <summary>
    /// [S-1592] Delete the selection (the right-clicked card is in it): in edit mode its own; [S-14320] outside it the desktop's
    /// one selection, as Del there does.
    /// </summary>
    public void RequestDelete(WidgetCard card)
    {
        if (EditMode)
        {
            _host.DeleteSelected();
        }
        else
        {
            _host.DeleteSelection(permanent: false);
        }
    }

    public void RequestEditMode(bool enabled) => _host.SetEditMode(enabled);

    /// <summary>[S-12950] Lock widgets, as the state holds it.</summary>
    public bool WidgetsLocked => _host.State.Preferences.LockWidgets;

    /// <summary>[S-12951] The menus' Lock widgets line.</summary>
    public void RequestLockWidgets(bool on) => _host.SetLockWidgets(on);

    /// <summary>[S-12950] Whether Lock widgets keeps <paramref name="card"/> where it is.</summary>
    public bool Pins(WidgetCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return Core.Desktop.WidgetLock.Pins(ViewForThisMonitor(_host.State), Stand(card).Model.Id, WidgetsLocked, EditMode);
    }

    /// <inheritdoc />
    public ViewState? ViewForCards => _closed ? null : ViewForThisMonitor(_host.State);

    /// <summary>A widget edited its own config. Persist it, but do not re-lay-out: see [S-128].</summary>
    public void RequestSaveConfig() => _host.SaveSoon();

    /// <summary>[S-11877] In edit mode its selection's; on the locked desktop the desktop's one selection (icons and cards).</summary>
    public bool IsSelected(WidgetCard card) => !IsCopy(card) && (EditMode ? _host.Selection.Contains(card.Model.Id) : _host.DeskSelects(card.Model));

    public int SelectionCount => _host.Selection.Count;

    /// <summary>[S-15103] The desktop's one selection's count (icons and cards) when it holds the card, else 1: what the card menu acts on outside edit mode.</summary>
    public int DeskSelectionCount(WidgetCard card) => _host.DeskSelects(card.Model) ? _host.DeskSelectedCount : 1;

    /// <summary>[S-5524] The selection's Delete (the card alone when it is not selected) on desktop items only takes them off this view.</summary>
    public bool DeleteRemovesFromView(WidgetCard card)
        => !IsSelected(card) || ViewForThisMonitor(_host.State) is not { } view
            ? Themia.Core.Desktop.DesktopBackground.RemovesFromView([card.Model.Config])
            : Themia.Core.Desktop.DesktopBackground.RemovesFromView(view.Widgets.Where(w => _host.Selection.Contains(w.Id)).Select(w => w.Config));

    /// <summary>
    /// [S-13917] Whether <paramref name="stands"/> is the container whose layout is being edited (a press on its empty room or
    /// on another day): not picked, moved or resized; a press there lets go of the part selected.
    /// </summary>
    private bool HeldStill(WidgetCard stands) => LayoutEditing.HeldStill(_composing, stands.Model.Id);

    public void RequestSelect(WidgetCard card)
    {
        if (HeldStill(Stand(card)))
        {
            _host.StepOutOfSelection();
            return;
        }

        // [S-15526] A copied row stands for its repeater.
        _host.Select(Stand(card).Model.Id);

        // [S-2343] A click on the widget again opens its page again; [S-13917] not while a layout is being edited.
        if (_composing is null)
        {
            ReopenSelectionPage();
        }
    }

    public void RequestToggleSelect(WidgetCard card)
    {
        if (!HeldStill(Stand(card)))
        {
            _host.ToggleSelect(Stand(card).Model.Id);
        }
    }

    /// <inheritdoc />
    public bool RequestPress(WidgetCard card, bool onHandle)
    {
        // [S-2343] The pointer holds the widget from here to its release: the selection this press makes opens its page then.
        HoldProperties(true);
        var stands = Stand(card);
        if (HeldStill(stands))
        {
            _host.StepOutOfSelection();
            return true;
        }

        // [S-13917] A container selected alone before the press: a click on it starts its layout editing.
        _pressedSelected = LayoutEditing.Enters(ViewForThisMonitor(_host.State), stands.Model.Id, EditMode, _composing, _host.Selection.ViewId, _host.Selection.Ids);
        return _host.PressSelect(stands.Model.Id, onHandle);
    }

    /// <inheritdoc />
    public void RequestClick(WidgetCard card)
    {
        var stands = Stand(card);
        if (HeldStill(stands))
        {
            return;
        }

        _host.ClickSelect(stands.Model.Id);

        // [S-13917] A click on a container already selected: its layout editing starts, its page closes.
        if (_pressedSelected && stands.Model.Config is IRepeater)
        {
            _pressedSelected = false;
            Compose(stands.Model.Id);
        }
    }

    public void RequestCopy(WidgetCard card) => _host.CopySelected();

    public void RequestCut(WidgetCard card) => _host.CutSelected();

    public void RequestPaste() => _host.Paste();

    public bool CanPaste => _host.Clipboard.HasContent;

#if DEBUG
    /// <summary>Debug only: the surface's content, for the switch check's picture ([S-1883]).</summary>
    internal FrameworkElement DebugRoot => _root;

    /// <summary>Debug only: the selection's frame, for the desk select check's [S-14310] steps.</summary>
    internal SelectionFrame DebugFrame => _markerLayer.Frame;

    /// <summary>Debug checks: the container whose layout is edited, the bento, the smoke, an open flyout, what a press stands for.</summary>
    internal string? DebugComposing => _composing;

    internal bool DebugPaletteShown => _palette is { Visibility: Visibility.Visible };

    internal bool DebugDimmed => _dim is not null;

    internal bool DebugFlyoutOpen => BarMenuOpen;

    internal string DebugStands(WidgetCard card) => Stand(card).Model.Id;

    /// <summary>Debug renders and checks: the surface's window.</summary>
    internal Window DebugWindow => _window;
#endif
}
