using System.Text.Json.Serialization;
using Themia.Core.Geometry;
using Themia.Core.Localization;

namespace Themia.Core.Model;

/// <summary>One placed widget. Geometry is fractional ([S-01]); config is typed ([S-13]).</summary>
public sealed class WidgetInstance
{
    public required string Id { get; set; }

    public required WidgetKind Kind { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public double W { get; set; }

    public double H { get; set; }

    /// <summary>Optional user-set title shown in the widget header. Null means "use the catalogue label".</summary>
    public string? Label { get; set; }

    public WidgetSurface Surface { get; set; } = WidgetSurface.Card;

    /// <summary>Where the widget's header sits. A per-widget choice, like the surface.</summary>
    public HeaderPlacement Header { get; set; } = HeaderPlacement.Top;

    /// <summary>Never null after a successful load; a missing or mismatched config is repaired to the kind's default.</summary>
    public required WidgetConfig Config { get; set; }

    [JsonIgnore]
    public FracRect Rect
    {
        get => new(X, Y, W, H);
        set
        {
            X = value.X;
            Y = value.Y;
            W = value.W;
            H = value.H;
        }
    }

    public static WidgetInstance Create(WidgetKind kind, FracRect rect) => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Kind = kind,
        Header = DefaultHeader(kind),
        Config = WidgetConfig.CreateDefault(kind),
        X = rect.X,
        Y = rect.Y,
        W = rect.W,
        H = rect.H,
    };

    /// <summary>
    /// [S-700] Where a new widget's header goes. Top for everything except the clock, which starts
    /// headerless as it did in the original: a single readout says what it is, and at the small
    /// sizes it is usually placed at, a header would take a third of the card. Only the default:
    /// the user can still give it a header, and a saved widget keeps the placement it was saved with.
    /// <para>
    /// [S-1911] The power widgets start headerless too: the whole tile is the button, and its name
    /// is already under the icon.
    /// </para>
    /// <para>
    /// [S-3901] The quick-setting toggles and [S-3200] every audio visualizer never have one
    /// ([S-3711]).
    /// </para>
    /// <para>
    /// [S-5300] The Date widget starts headerless like the clock: it is made for the wallpaper.
    /// </para>
    /// <para>
    /// The sun, the moon and the disk meter start headerless too (user requirement): the sun's path
    /// and the moon's face say what they are, and the disk meter names its drive beside the reading
    /// ([S-1161]).
    /// </para>
    /// </summary>
    public static HeaderPlacement DefaultHeader(WidgetKind kind) => kind switch
    {
        _ when !CanHaveHeader(kind) => HeaderPlacement.Hidden,
        WidgetKind.Clock or WidgetKind.Sun or WidgetKind.Moon or WidgetKind.Disk
            or WidgetKind.Drive => HeaderPlacement.Hidden,
        _ when Themia.Core.Widgets.PowerActionRules.ForKind(kind) is not null => HeaderPlacement.Hidden,
        _ => HeaderPlacement.Top,
    };

    /// <summary>
    /// [S-3711] Whether a widget of <paramref name="kind"/> may have a header at all. The
    /// quick-setting toggles never do ([S-3901]): the whole tile is the switch and its name is
    /// under the icon, so a header would only repeat it and take a third of a 120-DIP tile. Nor
    /// does any audio visualizer ([S-3200]): the spectrum is the widget, edge to edge, and a title
    /// over it says nothing the bars do not.
    /// </summary>
    public static bool CanHaveHeader(WidgetKind kind)
        => Themia.Core.Widgets.QuickToggleRules.ForKind(kind) is null
            && kind != WidgetKind.AudioVisualizer;

    /// <summary>
    /// [S-3711]/[S-3712] Where the header is drawn: the stored placement, or hidden for a kind that
    /// can never have one, whatever the file says.
    /// </summary>
    [JsonIgnore]
    public HeaderPlacement ShownHeader => CanHaveHeader(Kind) ? Header : HeaderPlacement.Hidden;

    public WidgetInstance Clone() => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Kind = Kind,
        Label = Label,
        Surface = Surface,
        Header = Header,
        Config = Config.Clone(),
        X = X,
        Y = Y,
        W = W,
        H = H,
    };
}

/// <summary>A named desktop layout. Views are switched as a whole; each holds its own widgets.</summary>
public sealed class ViewState
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// [S-703] A Segoe Fluent glyph shown for the view in switchers and the view bar, or null for
    /// the name's initials. [S-903] Chosen on the view's settings page; the settings pane shows it too.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// [S-3810] The colour the view's icon (or initials) is drawn in, as <c>#RRGGBB</c>, or null for
    /// the theme's text colour. Set with <see cref="ViewOps.SetColor"/>.
    /// </summary>
    public string? Color { get; set; }

    public List<WidgetInstance> Widgets { get; set; } = [];

    /// <summary>Find a widget in this view by id, or null.</summary>
    public WidgetInstance? Find(string? id)
        => string.IsNullOrEmpty(id) ? null : Widgets.Find(w => string.Equals(w.Id, id, StringComparison.Ordinal));

    public static ViewState Create(string name) => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Name = name,
    };
}

/// <summary>
/// Global look-and-feel, mirroring the original app's design options minus anything tied to an
/// account. Colours are hex strings; <b>empty means "use the theme's own"</b>, which is what lets a
/// user override one colour without pinning all the others.
/// </summary>
public sealed class DesignState
{
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Scales every widget's content. The user's accessibility choice, not a look.</summary>
    public double UiScale { get; set; } = 1.0;

    /// <summary>A font-family preset, or the name of any installed family.</summary>
    public string FontFamily { get; set; } = "system";

    /// <summary>Multiplies the computed font sizes.</summary>
    public double FontScale { get; set; } = 1.0;

    /// <summary>[S-04] Gap between widgets, in device-independent pixels.</summary>
    public double WidgetGapPx { get; set; } = 8;

    /// <summary>[S-06] Snap-to-grid aid for drag, resize and placement.</summary>
    public bool GridSnap { get; set; } = true;

    /// <summary>Corner radius of a widget card, in device-independent pixels.</summary>
    public double CornerRadiusPx { get; set; } = 8;

    /// <summary>Widget card fill, as <c>#RRGGBB</c>. Empty uses the theme's surface colour.</summary>
    public string WidgetBackgroundColor { get; set; } = string.Empty;

    /// <summary>Opacity applied to every widget card, 0.2 to 1.0.</summary>
    public double SurfaceOpacity { get; set; } = 1.0;

    /// <summary>Accent override, as <c>#RRGGBB</c>. Empty follows the Windows accent colour.</summary>
    public string PrimaryColor { get; set; } = string.Empty;

    public WidgetBorderStyle BorderStyle { get; set; } = WidgetBorderStyle.Solid;

    /// <summary>Border thickness in device-independent pixels, 0 to 8.</summary>
    public double BorderWidthPx { get; set; } = 1;

    /// <summary>Border colour, as <c>#RRGGBB</c>. Empty uses the theme's card stroke.</summary>
    public string BorderColor { get; set; } = string.Empty;

    public double BorderOpacity { get; set; } = 1.0;

    /// <summary>The one-shot animation widgets play when a view appears.</summary>
    public IntroAnimation IntroAnimation { get; set; } = IntroAnimation.Scale;

    /// <summary>Duration of that animation, in milliseconds.</summary>
    public double IntroAnimationMs { get; set; } = 500;

    /// <summary>[S-704] The header strip's fill, as <c>#RRGGBB</c>. Empty uses the widget's own fill.</summary>
    public string HeaderBackgroundColor { get; set; } = string.Empty;

    /// <summary>Opacity of the header strip, 0 to 1. Independent of the body, as in the original.</summary>
    public double HeaderOpacity { get; set; } = 1.0;

    /// <summary>[S-704] The animation widgets play when the user switches views.</summary>
    public IntroAnimation TransitionAnimation { get; set; } = IntroAnimation.Scale;

    public double TransitionAnimationMs { get; set; } = 500;

    /// <summary>[S-704] The animation replayed when the session is unlocked. None turns the replay off.</summary>
    public IntroAnimation UnlockAnimation { get; set; } = IntroAnimation.Scale;

    public double UnlockAnimationMs { get; set; } = 500;

    /// <summary>Launch with Windows, via the per-user Run key.</summary>
    public bool RunAtStartup { get; set; }

    public DesignState Clone() => new()
    {
        Theme = Theme,
        UiScale = UiScale,
        FontFamily = FontFamily,
        FontScale = FontScale,
        WidgetGapPx = WidgetGapPx,
        GridSnap = GridSnap,
        CornerRadiusPx = CornerRadiusPx,
        WidgetBackgroundColor = WidgetBackgroundColor,
        SurfaceOpacity = SurfaceOpacity,
        PrimaryColor = PrimaryColor,
        BorderStyle = BorderStyle,
        BorderWidthPx = BorderWidthPx,
        BorderColor = BorderColor,
        BorderOpacity = BorderOpacity,
        IntroAnimation = IntroAnimation,
        IntroAnimationMs = IntroAnimationMs,
        HeaderBackgroundColor = HeaderBackgroundColor,
        HeaderOpacity = HeaderOpacity,
        TransitionAnimation = TransitionAnimation,
        TransitionAnimationMs = TransitionAnimationMs,
        UnlockAnimation = UnlockAnimation,
        UnlockAnimationMs = UnlockAnimationMs,
        RunAtStartup = RunAtStartup,
    };
}

/// <summary>
/// The whole persisted document. <see cref="SchemaVersion"/> is the load-time trust boundary: a
/// document from the future is refused rather than half-read.
/// </summary>
public sealed class AppState
{
    /// <summary>Bumped whenever the shape changes. Only this version is read; there is no upgrade path.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<ViewState> Views { get; set; } = [];

    public string? ActiveViewId { get; set; }

    /// <summary>Per-monitor view assignment: monitor stable id to view id. Missing means "use the active view".</summary>
    public Dictionary<string, string> MonitorViews { get; set; } = [];

    public DesignState Design { get; set; } = new();

    /// <summary>[S-702] How the app behaves: language, game mode, network guard, developer switches.</summary>
    public AppPreferences Preferences { get; set; } = new();

    /// <summary>
    /// [S-705] The view a display shows the first time it is seen, or null for the active view.
    /// Repaired to null when the view is deleted.
    /// </summary>
    public string? DefaultViewForNewDisplays { get; set; }

    /// <summary>[S-705] Displays the user turned Themia off on, keyed like <see cref="MonitorViews"/>. No surface is built there.</summary>
    public List<string> DisabledMonitors { get; set; } = [];

    /// <summary>
    /// [S-705] Per-display widget scale, keyed like <see cref="MonitorViews"/>, as a factor from 0.5 to 2. Missing means 1, so
    /// only displays the user changed are stored.
    /// </summary>
    public Dictionary<string, double> MonitorScales { get; set; } = [];

    /// <summary>
    /// [S-906] Per-display view bar settings, keyed like <see cref="MonitorViews"/>. Missing means the
    /// defaults (<see cref="ViewBarRules.For"/>), so only displays the user changed are stored.
    /// </summary>
    public Dictionary<string, ViewBarSettings> ViewBars { get; set; } = [];

    /// <summary>
    /// [S-893] A fresh profile, seeded as the original's first run with two views so switching views
    /// is discoverable from the start. Home: a clock top right, the Desktop folder top left, and the
    /// everyday Windows places bottom right. Work: calendar, tasks, notes and a clock. The clock stays
    /// first in each, so it is the one a reset or a recovery shows on top. Shortcut labels are left
    /// empty so the names come from the target, translated with the app.
    /// </summary>
    public static AppState CreateDefault()
    {
        var home = CreateHomeView();
        var work = CreateWorkView();
        return new AppState
        {
            Views = [home, work],
            ActiveViewId = home.Id,
        };
    }

    private static ViewState CreateHomeView()
    {
        var view = ViewState.Create(Str.Shell_View_HomeName.Text());
        view.Widgets.Add(WidgetInstance.Create(WidgetKind.Clock, new FracRect(0.78, 0.04, 0.20, 0.16)));

        var folder = WidgetInstance.Create(WidgetKind.Folder, new FracRect(0.02, 0.04, 0.24, 0.50));
        if (folder.Config is FolderConfig listing)
        {
            // Empty means the profile folder; the original's first-run folder is the Desktop.
            listing.Path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        view.Widgets.Add(folder);

        var places = WidgetInstance.Create(WidgetKind.Shortcuts, new FracRect(0.80, 0.80, 0.18, 0.16));
        if (places.Config is ShortcutsConfig shortcuts)
        {
            // The kind's own default holds two places; the first run holds the original's six.
            shortcuts.Items = [.. FirstRunPlaces.Select(name => ShortcutItem.Create(Themia.Core.Widgets.LaunchTargets.ShellTarget(name)))];
        }

        view.Widgets.Add(places);
        return view;
    }

    /// <summary>
    /// [S-893] The original's Work showcase: the calendar takes the left half, tasks and notes share a
    /// column beside it, the clock sits top right as on Home. Calendar and tasks show their Set up
    /// button until an account exists ([S-1860]); notes work at once.
    /// </summary>
    private static ViewState CreateWorkView()
    {
        var view = ViewState.Create(Str.Shell_View_WorkName.Text());
        view.Widgets.Add(WidgetInstance.Create(WidgetKind.Clock, new FracRect(0.78, 0.04, 0.20, 0.16)));
        view.Widgets.Add(WidgetInstance.Create(WidgetKind.Calendar, new FracRect(0.02, 0.04, 0.50, 0.72)));

        view.Widgets.Add(WidgetInstance.Create(WidgetKind.Tasks, new FracRect(0.54, 0.04, 0.20, 0.58)));
        view.Widgets.Add(WidgetInstance.Create(WidgetKind.Notes, new FracRect(0.54, 0.64, 0.20, 0.28)));
        return view;
    }

    /// <summary>[S-893] The Windows places the first-run Shortcuts widget holds, in the original's order.</summary>
    public static IReadOnlyList<string> FirstRunPlaces { get; } =
        ["RecycleBinFolder", "MyComputerFolder", "Downloads", "Personal", "My Pictures", "UsersFilesFolder"];

    public ViewState? FindView(string? id)
        => id is null ? null : Views.FirstOrDefault(v => v.Id == id);

    /// <summary>The active view, repairing a dangling or missing id to the first view.</summary>
    public ViewState ActiveView()
    {
        var found = FindView(ActiveViewId);
        if (found is not null)
        {
            return found;
        }

        if (Views.Count == 0)
        {
            var seeded = ViewState.Create(Str.Shell_View_DefaultName.Text());
            Views.Add(seeded);
        }

        ActiveViewId = Views[0].Id;
        return Views[0];
    }

    /// <summary>The view a given monitor shows, falling back to the active view.</summary>
    public ViewState ViewForMonitor(string monitorId)
    {
        if (MonitorViews.TryGetValue(monitorId, out var viewId))
        {
            var view = FindView(viewId);
            if (view is not null)
            {
                return view;
            }

            // Dangling assignment (the view was deleted): drop it rather than keep resolving it.
            MonitorViews.Remove(monitorId);
        }

        return ActiveView();
    }

    /// <summary>
    /// [S-19] Show a view on one display. Returns false, changing nothing, when the view does not exist.
    /// <para>
    /// The display's own assignment is what is changed, not just the active view. Every display is
    /// given an assignment at startup ([S-18]), and an assignment always wins over the active view,
    /// so switching only the active view — which is what switching did — changed nothing on screen.
    /// The active view follows too, so a display that has no assignment of its own, such as one
    /// plugged in later, starts on the view the user last chose.
    /// </para>
    /// </summary>
    public bool ShowViewOn(string monitorId, string viewId)
    {
        ArgumentNullException.ThrowIfNull(monitorId);

        if (FindView(viewId) is null)
        {
            return false;
        }

        MonitorViews[monitorId] = viewId;
        ActiveViewId = viewId;
        return true;
    }
}
