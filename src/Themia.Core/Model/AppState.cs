using System.Text.Json.Serialization;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>One placed widget. Geometry is fractional ([S-01]); config is typed ([S-13]).</summary>
public sealed class WidgetInstance
{
    public required string Id { get; set; }

    /// <summary>
    /// [S-6908] Which widget this is: its config's id ([S-6903]). Not stored on its own: the config's
    /// discriminator is the kind, so the two cannot disagree.
    /// </summary>
    [JsonIgnore]
    public WidgetId Kind => Config.Kind;

    public double X { get; set; }

    public double Y { get; set; }

    public double W { get; set; }

    public double H { get; set; }

    /// <summary>Optional user-set title shown in the widget header. Null means "use the catalogue label".</summary>
    public string? Label { get; set; }

    /// <summary>
    /// [S-6562] This widget's own background ([S-6560]): Default follows the app-wide one
    /// (<see cref="DesignState.Background"/>), Transparent drops the card ([S-32]), a colour is that
    /// colour at its opacity. Set on the properties panel ([S-6520]).
    /// </summary>
    public FillChoice Background { get; set; } = new();

    /// <summary>
    /// [S-6590] This widget's own background image: a picture file's full path, or null (the default)
    /// for none, which is not written. It covers the card's body behind the content; <see cref="Background"/> then tints it
    /// ([S-6592]). Set on the properties panel ([S-6593]).
    /// </summary>
    public string? BackgroundImage { get; set; }

    /// <summary>Where the widget's header sits. A per-widget choice, like the background.</summary>
    public HeaderPlacement Header { get; set; } = HeaderPlacement.Top;

    /// <summary>
    /// [S-6512] This widget's own item background ([S-6560]): Default (the default) follows the
    /// app-wide one (<see cref="DesignState.ItemBackground"/>). Set on the properties panel ([S-6520]).
    /// </summary>
    public FillChoice ItemBackground { get; set; } = new();

    /// <summary>
    /// [S-6540] The colour of the text and glyphs drawn on this widget's items, as <c>#RRGGBB</c>, or
    /// empty (the default) to follow the app-wide one (<see cref="DesignState.ItemTextColor"/>). Apart
    /// from <see cref="ItemBackground"/>: each resolves on its own ([S-6541]). Set on the properties
    /// panel ([S-6544]).
    /// </summary>
    public string ItemTextColor { get; set; } = string.Empty;

    /// <summary>
    /// [S-6530] This widget's own content scale, a factor (1 is 100 %): 0.5..2 on a 5 % grain
    /// (<see cref="WidgetScale"/>). It multiplies the display's scale ([S-6532]); like it, it magnifies
    /// what is inside the card, never the card's cell.
    /// </summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// [S-6580] Where this widget's content sits across its card, or null (the default) for where the
    /// kind draws it (<see cref="Themia.Core.Widgets.ContentAlignment.Default"/>), so a widget that was
    /// never given one does not move. Set on the properties panel ([S-6584]); read through
    /// <see cref="Themia.Core.Widgets.ContentAlignment.Shown"/>.
    /// </summary>
    public ContentAlign? Align { get; set; }

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

    public static WidgetInstance Create(WidgetId kind, FracRect rect) => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Header = DefaultHeader(kind),
        Config = WidgetConfig.CreateDefault(kind),
        X = rect.X,
        Y = rect.Y,
        W = rect.W,
        H = rect.H,
    };

    /// <summary>
    /// [S-700] Where a new widget's header goes: Top, unless its descriptor says it starts headerless
    /// or can never have one ([S-6905]). Only the default: the user can still give it a header, and a
    /// saved widget keeps the placement it was saved with.
    /// </summary>
    public static HeaderPlacement DefaultHeader(WidgetId kind)
        => CanHaveHeader(kind) && !ModuleCatalog.Of(kind).Traits.HasFlag(WidgetTraits.StartsHeaderless)
            ? HeaderPlacement.Top
            : HeaderPlacement.Hidden;

    /// <summary>[S-3711] Whether a widget of <paramref name="kind"/> may have a header at all: its descriptor's <see cref="WidgetTraits.CanHaveHeader"/>.</summary>
    public static bool CanHaveHeader(WidgetId kind) => ModuleCatalog.Of(kind).Traits.HasFlag(WidgetTraits.CanHaveHeader);

    /// <summary>
    /// [S-3711]/[S-3712] Where the header is drawn: the stored placement, or hidden for a kind that
    /// can never have one, whatever the file says.
    /// </summary>
    [JsonIgnore]
    public HeaderPlacement ShownHeader => CanHaveHeader(Kind) ? Header : HeaderPlacement.Hidden;

    public WidgetInstance Clone() => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Label = Label,
        Background = Background.Clone(),
        BackgroundImage = BackgroundImage,
        Header = Header,
        ItemBackground = ItemBackground.Clone(),
        ItemTextColor = ItemTextColor,
        Scale = Scale,
        Align = Align,
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
    /// the name's initials. [S-903] Chosen in the View menu.
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

    /// <summary>
    /// Corner radius of a widget card, in device-independent pixels. [S-6482] 4 by the user's
    /// decision, not Fluent's 8px OverlayCornerRadius.
    /// </summary>
    public double CornerRadiusPx { get; set; } = DefaultCornerRadiusPx;

    /// <summary>[S-6482] The default card corner radius.</summary>
    public const double DefaultCornerRadiusPx = 4;

    /// <summary>
    /// [S-823] [S-6560] The app-wide widget background: Default is the Windows card (Fluent base and
    /// card tint), Transparent no card at all, a colour exactly that colour at its opacity.
    /// </summary>
    public FillChoice Background { get; set; } = new();

    /// <summary>
    /// [S-6510] The app-wide item background ([S-6560]): what the items inside every card are filled
    /// with, apart from the card's own fill. Default keeps each item's Fluent fill.
    /// </summary>
    public FillChoice ItemBackground { get; set; } = new();

    /// <summary>
    /// [S-6540] The app-wide colour of the text and glyphs drawn on items, as <c>#RRGGBB</c>. Empty (the
    /// default) keeps the theme's text colours.
    /// </summary>
    public string ItemTextColor { get; set; } = string.Empty;

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

    /// <summary>
    /// [S-6329] Live values move smoothly: readouts and their colours glide to each reading ([S-6323])
    /// and history charts scroll ([S-1826]). Off, both jump as with Windows' animation effects off.
    /// </summary>
    public bool SmoothValues { get; set; } = true;

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
        Background = Background.Clone(),
        ItemBackground = ItemBackground.Clone(),
        ItemTextColor = ItemTextColor,
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
        SmoothValues = SmoothValues,
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
    /// [S-893] A fresh profile: the maker's own Home and Work views, so switching views is
    /// discoverable from the start and the desktop looks lived-in (<see cref="FirstRunDesktop"/>, the
    /// seed as data). Nothing in it is anyone's data: the weather waits for a place, calendar and
    /// tasks for an account ([S-1860]), the note is made on first show, and every id is new on each
    /// call.
    /// </summary>
    public static AppState CreateDefault()
    {
        var home = FirstRunDesktop.View(Str.Shell_View_HomeName.Text(), FirstRunDesktop.Home);
        var work = FirstRunDesktop.View(Str.Shell_View_WorkName.Text(), FirstRunDesktop.Work);
        return new AppState
        {
            Views = [home, work],
            ActiveViewId = home.Id,
        };
    }

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
