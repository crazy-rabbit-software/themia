using System.Text.Json.Serialization;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>One placed widget. Geometry is fractional ([S-01]); config is typed ([S-13]).</summary>
public sealed partial class WidgetInstance
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

    /// <summary>
    /// [S-6562] This widget's own background ([S-6560]): Default follows the app-wide one
    /// (<see cref="DesignState.Background"/>), Transparent drops the card ([S-32]), a colour is that
    /// colour at its opacity. Set on the selection's page ([S-6520]).
    /// </summary>
    public FillChoice Background { get; set; } = new();

    /// <summary>
    /// [S-6590] This widget's own background image: a picture file's full path, or null (the default)
    /// for none, which is not written. It covers the card's body behind the content; <see cref="Background"/> then tints it
    /// ([S-6592]). Set on the selection's page ([S-6593]).
    /// </summary>
    public string? BackgroundImage { get; set; }

    /// <summary>
    /// [S-6512] This widget's own item background ([S-6560]): Default (the default) follows the
    /// app-wide one (<see cref="DesignState.ItemBackground"/>). Set on the selection's page ([S-6520]).
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
    /// never given one does not move. Set on the selection's page ([S-6584]); read through
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
        Background = DefaultBackground(kind),
        Config = WidgetConfig.CreateDefault(kind),
        X = rect.X,
        Y = rect.Y,
        W = rect.W,
        H = rect.H,
    };

    /// <summary>
    /// [S-5600] A new widget's background: the app-wide one, or none at all for a kind drawn as a desktop
    /// icon (<see cref="WidgetTraits.StartsTransparent"/>). Only the default: the user can still give it a card.
    /// </summary>
    public static FillChoice DefaultBackground(WidgetId kind)
        => ModuleCatalog.Of(kind).Traits.HasFlag(WidgetTraits.StartsTransparent) ? FillChoice.Transparent() : new();

    /// <summary>A copy under a new id, its config copied too: a paste, a duplicate.</summary>
    public WidgetInstance Clone()
    {
        var copy = new WidgetInstance { Id = Guid.NewGuid().ToString("n"), Config = Config.Clone() };
        copy.CopyArrangementFrom(this);
        return copy;
    }

    /// <summary>
    /// [S-886] Take every stored property of <paramref name="source"/> but its id and its config: the widget's
    /// whole arrangement (its place and size and its own look). The one hand-written copy of a widget:
    /// <see cref="Clone"/>, the undo snapshot and its restore all go through it, and a guard test fails on a stored
    /// property it leaves out.
    /// </summary>
    public void CopyArrangementFrom(WidgetInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);

        X = source.X;
        Y = source.Y;
        W = source.W;
        H = source.H;
        Background = source.Background.Clone();
        BackgroundImage = source.BackgroundImage;
        ItemBackground = source.ItemBackground.Clone();
        ItemTextColor = source.ItemTextColor;
        Scale = source.Scale;
        Align = source.Align;
    }

    /// <summary>[S-886] Whether the two are arranged alike: every property <see cref="CopyArrangementFrom"/> copies is equal.</summary>
    public bool SameArrangement(WidgetInstance other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return X.Equals(other.X) && Y.Equals(other.Y) && W.Equals(other.W) && H.Equals(other.H)
            && Background.SameAs(other.Background)
            && string.Equals(BackgroundImage, other.BackgroundImage, StringComparison.Ordinal)
            && ItemBackground.SameAs(other.ItemBackground)
            && string.Equals(ItemTextColor, other.ItemTextColor, StringComparison.Ordinal)
            && Scale.Equals(other.Scale)
            && Align == other.Align;
    }
}

/// <summary>A named desktop layout. Views are switched as a whole; each holds its own widgets.</summary>
public sealed partial class ViewState
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

    /// <summary>
    /// [S-5525] Which desktop items this view shows: All (the default, left out of the file), Chosen or None.
    /// Set in the view's card on the Views page (<see cref="Desktop.DesktopViews"/> holds the rules).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public ViewDesktopIcons DesktopIcons { get; set; }

    /// <summary>
    /// [S-855] The area its widgets were last settled on: the part of the screen every display showing it held in its
    /// work area then (<see cref="Geometry.GridGeometry.SharedAllowed"/>), as a fraction of the monitor; null until the
    /// view has been shown on a settled display. Kept by <see cref="Geometry.WorkAreaSettle"/>: when the area the view
    /// is shown in is another (a second display, a taskbar moved or grown), a widget that lay clear of a side there and
    /// would now be behind a taskbar moves clear, while one the user hung over a taskbar's edge stays over it ([S-6608]).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FracRect? WorkArea { get; set; }

    /// <summary>
    /// [S-889] Take every stored property of <paramref name="source"/> but its id and its widgets (the caller lays those):
    /// a guard test fails on a stored property it leaves out.
    /// </summary>
    public void CopyFrom(ViewState source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Name = source.Name;
        Icon = source.Icon;
        Color = source.Color;
        DesktopIcons = source.DesktopIcons;
        WorkArea = source.WorkArea;
    }

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
public sealed partial class DesignState
{
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>A font-family preset, or the name of any installed family.</summary>
    public string FontFamily { get; set; } = "system";

    /// <summary>Multiplies the computed font sizes.</summary>
    public double FontScale { get; set; } = 1.0;

    /// <summary>
    /// Corner radius of a widget card, in device-independent pixels. [S-6482] 8: Windows' own radius
    /// for a top-level surface, the same as Themia's menus (<c>ShellMetrics.OverlayRadius</c>).
    /// </summary>
    public double CornerRadiusPx { get; set; } = DefaultCornerRadiusPx;

    /// <summary>[S-6482] The default card corner radius.</summary>
    public const double DefaultCornerRadiusPx = 8;

    /// <summary>
    /// [S-823] [S-6560] The app-wide widget background: Default is Themia's neutral translucent card
    /// for the theme ([S-2100]), Transparent no card at all, a colour exactly that colour at its opacity.
    /// </summary>
    public FillChoice Background { get; set; } = new();

    /// <summary>
    /// [S-2110] Blur behind widgets: the display's wallpaper, blurred, under every card whose fill is
    /// translucent. On by default; a preset owns it.
    /// </summary>
    public bool BlurBehind { get; set; } = true;

    /// <summary>[S-2123] What the cards are made of: a solid fill, or Liquid glass. A style owns it; left out of the file while Solid.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public CardMaterial Material { get; set; } = CardMaterial.Solid;

    /// <summary>
    /// [S-2130] The look tokens past the fill, border and font: the frost, the glass, the text levels and type, the
    /// items' surfaces, the rows' dividers and the rendering mode (<see cref="LookTokens"/>). A style owns them.
    /// Stored as <see cref="StoredTokens"/>: left out of the file while they are a fresh set's.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public LookTokens Tokens { get; set; } = new();

    /// <summary>[S-2130] <see cref="Tokens"/> as the file holds them: null (left out) while they are a fresh set's, as <see cref="Material"/> is while Solid.</summary>
    [System.Text.Json.Serialization.JsonInclude]
    [System.Text.Json.Serialization.JsonPropertyName("tokens")]
    internal LookTokens? StoredTokens
    {
        get => Tokens is null || Tokens.SameAs(new LookTokens()) ? null : Tokens;
        set => Tokens = value ?? new LookTokens();
    }

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

    /// <summary>[S-704] The animation widgets play when the user switches views.</summary>
    public IntroAnimation TransitionAnimation { get; set; } = IntroAnimation.Scale;

    public double TransitionAnimationMs { get; set; } = 500;

    /// <summary>[S-704] The animation replayed when the session is unlocked. None turns the replay off.</summary>
    public IntroAnimation UnlockAnimation { get; set; } = IntroAnimation.Scale;

    public double UnlockAnimationMs { get; set; } = 500;

    public DesignState Clone() => new()
    {
        Theme = Theme,
        FontFamily = FontFamily,
        FontScale = FontScale,
        CornerRadiusPx = CornerRadiusPx,
        Background = Background.Clone(),
        BlurBehind = BlurBehind,
        Material = Material,
        Tokens = Tokens.Clone(),
        ItemBackground = ItemBackground.Clone(),
        ItemTextColor = ItemTextColor,
        PrimaryColor = PrimaryColor,
        BorderStyle = BorderStyle,
        BorderWidthPx = BorderWidthPx,
        BorderColor = BorderColor,
        BorderOpacity = BorderOpacity,
        IntroAnimation = IntroAnimation,
        IntroAnimationMs = IntroAnimationMs,
        TransitionAnimation = TransitionAnimation,
        TransitionAnimationMs = TransitionAnimationMs,
        UnlockAnimation = UnlockAnimation,
        UnlockAnimationMs = UnlockAnimationMs,
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

    /// <summary>[S-5522] The desktop layer's memory: the desktop items the reconcile has seen.</summary>
    public DesktopState Desktop { get; set; } = new();

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

    /// <summary>
    /// [S-498] Make this state <paramref name="source"/>'s, in place, so everything holding the live state sees it:
    /// every stored property taken over (by reference: <paramref name="source"/> is given up). The one hand-written
    /// copy of the whole state; a guard test fails on a stored property it leaves out.
    /// </summary>
    public void CopyFrom(AppState source)
    {
        ArgumentNullException.ThrowIfNull(source);

        SchemaVersion = source.SchemaVersion;
        Views = source.Views;
        ActiveViewId = source.ActiveViewId;
        MonitorViews = source.MonitorViews;
        Design = source.Design;
        Preferences = source.Preferences;
        DefaultViewForNewDisplays = source.DefaultViewForNewDisplays;
        DisabledMonitors = source.DisabledMonitors;
        MonitorScales = source.MonitorScales;
        ViewBars = source.ViewBars;
        Desktop = source.Desktop;
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
