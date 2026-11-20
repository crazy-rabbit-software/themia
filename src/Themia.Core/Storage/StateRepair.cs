using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Storage;

/// <summary>
/// [S-1050] The walk that brings a read state back inside its invariants, reporting what had to change. Pure and
/// total: it repairs rather than rejects, because a half-valid layout is still the user's layout.
/// </summary>
public static class StateRepair
{
    /// <summary>Bring a deserialised document back inside its invariants, reporting what had to change.</summary>
    public static List<string> Repair(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var repairs = new List<string>();

        state.Design ??= new DesignState();
        state.Views ??= [];
        state.MonitorViews ??= [];

        var design = state.Design;
        design.CornerRadiusPx = Math.Clamp(Sanitise(design.CornerRadiusPx, DesignState.DefaultCornerRadiusPx), 0, 32);
        design.FontScale = Math.Clamp(Sanitise(design.FontScale, 1), 0.5, 2.0);
        design.BorderWidthPx = Math.Clamp(Sanitise(design.BorderWidthPx, 1), 0, 8);
        design.BorderOpacity = Math.Clamp(Sanitise(design.BorderOpacity, 1), 0, 1);
        design.IntroAnimationMs = Motion.IntroMotion.ClampMs(Sanitise(design.IntroAnimationMs, Motion.IntroMotion.DefaultMs));

        // A malformed colour becomes empty, which means "use the theme's own" - the one value that
        // is always renderable. Keeping the bad string would leave the widget with no fill at all.
        design.PrimaryColor = Widgets.HexColor.Normalise(design.PrimaryColor);
        design.BorderColor = Widgets.HexColor.Normalise(design.BorderColor);

        // [S-6561] The two fill choices: an unknown kind or a colour that is not one is the default,
        // the opacity 0..1, loudly. A missing one is the default, silently.
        design.Background ??= new FillChoice();
        repairs.AddRange(design.Background.Repair("widget background"));
        design.ItemBackground ??= new FillChoice();
        repairs.AddRange(design.ItemBackground.Repair("item background"));

        // [S-6543] The item text colour: a colour or empty (the theme's text colours), loudly.
        var (itemText, itemTextRepair) = Widgets.ItemLook.RepairTextColour(design.ItemTextColor);
        design.ItemTextColor = itemText;
        if (itemTextRepair is not null)
        {
            repairs.Add(itemTextRepair);
        }

        if (string.IsNullOrWhiteSpace(design.FontFamily))
        {
            design.FontFamily = "system";
        }

        if (!Enum.IsDefined(design.BorderStyle))
        {
            design.BorderStyle = Model.WidgetBorderStyle.Solid;
        }

        if (!Enum.IsDefined(design.Material))
        {
            design.Material = Model.CardMaterial.Solid;
        }

        // [S-2130] The look tokens: a missing set is a fresh one, silently; a value out of its range, loudly.
        design.Tokens ??= new LookTokens();
        repairs.AddRange(design.Tokens.Repair());

        if (!Enum.IsDefined(design.IntroAnimation))
        {
            design.IntroAnimation = Model.IntroAnimation.Scale;
        }

        if (!Enum.IsDefined(design.Theme))
        {
            design.Theme = Model.AppTheme.System;
        }

        // [S-704] The transition/unlock animations, on the same terms as the rest.
        design.TransitionAnimationMs = Motion.IntroMotion.ClampMs(Sanitise(design.TransitionAnimationMs, Motion.IntroMotion.DefaultMs));
        design.UnlockAnimationMs = Motion.IntroMotion.ClampMs(Sanitise(design.UnlockAnimationMs, Motion.IntroMotion.DefaultMs));

        if (!Enum.IsDefined(design.TransitionAnimation))
        {
            design.TransitionAnimation = Model.IntroAnimation.Scale;
        }

        if (!Enum.IsDefined(design.UnlockAnimation))
        {
            design.UnlockAnimation = Model.IntroAnimation.Scale;
        }

        repairs.AddRange(ValidatePreferences(state));

        if (state.Views.Count == 0)
        {
            state.Views.Add(ViewState.Create("Desktop"));
            repairs.Add("no views; seeded one");
        }

        var seenViewIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var view in state.Views)
        {
            if (string.IsNullOrWhiteSpace(view.Id) || !seenViewIds.Add(view.Id))
            {
                view.Id = Guid.NewGuid().ToString("n");
                seenViewIds.Add(view.Id);
                repairs.Add("view had a missing or duplicate id");
            }

            if (string.IsNullOrWhiteSpace(view.Name))
            {
                view.Name = "View";
                repairs.Add("view had no name");
            }

            if (view.Icon is not null && !IsGlyph(view.Icon))
            {
                // [S-703] Fall back to initials rather than draw whatever text got onto disk.
                view.Icon = null;
                repairs.Add($"view '{view.Id}' had an icon that is not a single glyph");
            }

            // [S-3811] A colour that does not parse draws the icon in the theme's colour, loudly.
            if (ViewOps.CleanColor(view.Color) is var color && color != view.Color)
            {
                if (color is null && !string.IsNullOrWhiteSpace(view.Color))
                {
                    repairs.Add($"view '{view.Id}' had a colour that is not #RRGGBB; it uses the theme's");
                }

                view.Color = color;
            }

            view.Widgets ??= [];
            repairs.AddRange(ValidateWidgets(view));

            // [S-5525] Which desktop icons it shows: an unknown value is All; a None view holds no item widget.
            repairs.AddRange(Desktop.DesktopViews.Repair(view));
        }

        if (state.FindView(state.ActiveViewId) is null)
        {
            state.ActiveViewId = state.Views[0].Id;
            repairs.Add("active view id was dangling");
        }

        foreach (var monitorId in state.MonitorViews.Keys.ToList())
        {
            if (state.FindView(state.MonitorViews[monitorId]) is null)
            {
                state.MonitorViews.Remove(monitorId);
                repairs.Add($"monitor '{monitorId}' pointed at a deleted view");
            }
        }

        repairs.AddRange(ValidateDisplays(state));

        state.SchemaVersion = AppState.CurrentSchemaVersion;
        return repairs;
    }

    /// <summary>[S-702] The behaviour switches: the language, the regional formats and the update cadences can be out of range.</summary>
    private static List<string> ValidatePreferences(AppState state)
    {
        var repairs = new List<string>();

        if (state.Preferences is null)
        {
            // A hand-edited null: repair to the defaults, loudly.
            state.Preferences = new AppPreferences();
            repairs.Add("preferences were null; reset to defaults");
            return repairs;
        }

        if (!Enum.IsDefined(state.Preferences.Language))
        {
            state.Preferences.Language = AppLanguage.System;
            repairs.Add("unknown language; following Windows");
        }

        // [S-5593] A number that names no icon size (the names are read strictly) is Explorer's default.
        if (!Enum.IsDefined(state.Preferences.DesktopIconSize))
        {
            state.Preferences.DesktopIconSize = Themia.Sdk.DesktopIconSize.Medium;
            repairs.Add("unknown desktop icon size; medium icons");
        }

        // [S-13970] A number that names no icon layout (the names are read strictly) is text below, Explorer's.
        if (!Enum.IsDefined(state.Preferences.DesktopIconLayout))
        {
            state.Preferences.DesktopIconLayout = Themia.Sdk.DesktopIconLayout.Below;
            repairs.Add("unknown desktop icon layout; text below the icons");
        }

        // [S-1575] The recent colours: what is no colour dropped, each once, one line at most.
        var recent = Themia.Core.Settings.ColourPicker.Recent(state.Preferences.RecentColours);
        if (state.Preferences.RecentColours is null || !recent.SequenceEqual(state.Preferences.RecentColours))
        {
            state.Preferences.RecentColours = [.. recent];
            repairs.Add("recent colours that are no colours, repeated or too many; kept the newest valid ones");
        }

        // [S-3772] [S-3781] The shared formats and cadences: a hand-edited null is the defaults.
        if (state.Preferences.Region is null)
        {
            state.Preferences.Region = new RegionPreferences();
            repairs.Add("region formats were null; following Windows");
        }

        if (state.Preferences.Refresh is null)
        {
            state.Preferences.Refresh = RefreshPreferences.WithDefaults();
            repairs.Add("refresh cadences were null; reset to defaults");
        }

        repairs.AddRange(state.Preferences.Region.Repair());
        repairs.AddRange(Sync.UpdateCadence.Repair(state.Preferences.Refresh));
        return repairs;
    }

    /// <summary>
    /// [S-705] The per-display settings. Runs after the views are repaired, because the default view
    /// for new displays has to name one that still exists.
    /// </summary>
    private static List<string> ValidateDisplays(AppState state)
    {
        var repairs = new List<string>();

        if (state.DefaultViewForNewDisplays is not null && state.FindView(state.DefaultViewForNewDisplays) is null)
        {
            state.DefaultViewForNewDisplays = null;
            repairs.Add("the default view for new displays was deleted; new displays show the active view");
        }

        state.DisabledMonitors ??= [];
        var disabled = state.DisabledMonitors
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (disabled.Count != state.DisabledMonitors.Count)
        {
            repairs.Add("the disabled displays held blank or repeated ids");
        }

        state.DisabledMonitors = disabled;

        state.MonitorScales ??= [];
        foreach (var monitorId in state.MonitorScales.Keys.ToList())
        {
            var scale = state.MonitorScales[monitorId];
            if (string.IsNullOrWhiteSpace(monitorId) || !double.IsFinite(scale))
            {
                state.MonitorScales.Remove(monitorId);
                repairs.Add("dropped an unreadable display scale");
                continue;
            }

            var clamped = Math.Clamp(scale, MinMonitorScale, MaxMonitorScale);
            if (clamped != scale)
            {
                state.MonitorScales[monitorId] = clamped;
                repairs.Add($"display scale for '{monitorId}' was outside {MinMonitorScale:0.#}–{MaxMonitorScale:0.#}");
            }
        }

        repairs.AddRange(ValidateViewBars(state));
        repairs.AddRange(Desktop.DesktopReconcile.RepairKnown(state));

        // [S-13002] Every fence's items whole, and no item in two places of one view.
        repairs.AddRange(Desktop.DesktopFences.Repair(state));

        // [S-5596] A number that names no sort is the order new items are laid out in.
        if (!Enum.IsDefined(state.Desktop.Sort))
        {
            state.Desktop.Sort = Desktop.DesktopSortBy.Name;
            repairs.Add("unknown desktop sort; by name");
        }

        repairs.AddRange(Desktop.DesktopDisplays.RepairFallbacks(state));
        return repairs;
    }

    /// <summary>
    /// [S-906] The per-display view bars: unreadable entries are dropped (the display falls back to
    /// the defaults), unknown anchors, sizes, types and labels reset, and each whitelist keeps only views that
    /// exist, once each. A whitelist that ends up empty means "all views", never an empty bar.
    /// </summary>
    private static List<string> ValidateViewBars(AppState state)
    {
        var repairs = new List<string>();
        state.ViewBars ??= [];

        foreach (var monitorId in state.ViewBars.Keys.ToList())
        {
            var bar = state.ViewBars[monitorId];
            if (string.IsNullOrWhiteSpace(monitorId) || bar is null)
            {
                state.ViewBars.Remove(monitorId);
                repairs.Add("dropped an unreadable view bar entry");
                continue;
            }

            if (!Enum.IsDefined(bar.Anchor))
            {
                bar.Anchor = ViewBarAnchor.BottomLeft;
                repairs.Add($"view bar on '{monitorId}' had an unknown position");
            }

            if (!Enum.IsDefined(bar.Size))
            {
                bar.Size = ViewBarSize.Medium;
                repairs.Add($"view bar on '{monitorId}' had an unknown size");
            }

            if (!Enum.IsDefined(bar.Type))
            {
                // [S-5400] Inline, the bar as it always was.
                bar.Type = ViewBarType.Inline;
                repairs.Add($"view bar on '{monitorId}' had an unknown type");
            }

            if (!Enum.IsDefined(bar.Labels))
            {
                // [S-5405] Icon and text.
                bar.Labels = ViewBarLabels.IconAndText;
                repairs.Add($"view bar on '{monitorId}' had unknown labels");
            }

            var visible = (bar.VisibleViews ?? [])
                .Where(id => state.FindView(id) is not null)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (bar.VisibleViews is null || visible.Count != bar.VisibleViews.Count)
            {
                repairs.Add($"view bar on '{monitorId}' listed missing or repeated views");
            }

            bar.VisibleViews = visible;
        }

        return repairs;
    }

    /// <summary>The per-display scale range, as in the original's display settings (50 % to 200 %).</summary>
    public const double MinMonitorScale = 0.5;

    public const double MaxMonitorScale = 2.0;

    /// <summary>One visible glyph: a single UTF-16 unit, or one surrogate pair.</summary>
    private static bool IsGlyph(string text)
        => text.Length == 1 ? !char.IsWhiteSpace(text[0]) && !char.IsControl(text[0])
            : text.Length == 2 && char.IsSurrogatePair(text[0], text[1]);

    private static List<string> ValidateWidgets(ViewState view)
    {
        var repairs = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Geometry is validated against a unit screen: the stored model is fractional, so the
        // invariants (on-screen, non-inverted, finite) hold independently of any real monitor.
        var unit = new ScreenMetrics(1920, 1080);

        for (var i = view.Widgets.Count - 1; i >= 0; i--)
        {
            var widget = view.Widgets[i];

            // [S-6908] A widget is its config: one without a config is no widget this build knows.
            // The read never makes one (the store drops it before the typed read); this is for a
            // state built in memory.
            if (widget.Config is null)
            {
                view.Widgets.RemoveAt(i);
                repairs.Add($"dropped widget '{widget.Id}' without a config from view '{view.Name}'");
                continue;
            }

            if (string.IsNullOrWhiteSpace(widget.Id) || !seen.Add(widget.Id))
            {
                widget.Id = Guid.NewGuid().ToString("n");
                seen.Add(widget.Id);
                repairs.Add("widget had a missing or duplicate id");
            }

            // The config's own invariants: ranges and values a hand edit made unusable.
            foreach (var repair in widget.Config.Repair())
            {
                repairs.Add($"widget '{widget.Id}': {repair}");
            }

            // [S-6004] A style the kind does not have: its first style. One rule for every styled kind.
            if (WidgetStyles.Repair(widget.Config) is { } style)
            {
                repairs.Add($"widget '{widget.Id}': {style}");
            }

            // [S-6561] A widget's own fill choices: repaired like the app-wide ones; absent follows them.
            widget.Background ??= new FillChoice();
            widget.ItemBackground ??= new FillChoice();
            foreach (var repair in widget.Background.Repair("background").Concat(widget.ItemBackground.Repair("item background")))
            {
                repairs.Add($"widget '{widget.Id}': {repair}");
            }

            // [S-6591] A widget's background image: a picture file's full path, or none. A file that is
            // not there now is kept: the card draws no image until it is back ([S-6590]).
            var (image, imageRepair) = BackgroundImage.Repair(widget.BackgroundImage);
            widget.BackgroundImage = image.Length == 0 ? null : image;
            if (imageRepair is not null)
            {
                repairs.Add($"widget '{widget.Id}': {imageRepair}");
            }

            // [S-6543] A widget's own item text colour: a colour, or empty to follow the app-wide one.
            var (ownText, ownTextRepair) = ItemLook.RepairTextColour(widget.ItemTextColor);
            widget.ItemTextColor = ownText;
            if (ownTextRepair is not null)
            {
                repairs.Add($"widget '{widget.Id}': {ownTextRepair}");
            }

            // [S-6533] A scale outside 50–200 % or off the 5 % grain: the nearest one it may hold.
            if (WidgetScale.Repair(widget) is { } scale)
            {
                repairs.Add($"widget '{widget.Id}': {scale}");
            }

            // [S-6585] An alignment outside the set, or on a kind with nothing to align: the kind's default.
            if (ContentAlignment.Repair(widget) is { } align)
            {
                repairs.Add($"widget '{widget.Id}': {align}");
            }

            var settled = GridGeometry.Clamp(widget.Rect, unit);
            if (settled != widget.Rect)
            {
                widget.Rect = settled;
                repairs.Add($"widget '{widget.Id}' was off-screen or below the size floor");
            }
        }

        return repairs;
    }

    private static double Sanitise(double value, double fallback)
        => double.IsFinite(value) ? value : fallback;
}
