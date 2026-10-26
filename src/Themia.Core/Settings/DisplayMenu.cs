using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3543] A connected display as the Display menu names it: its stable id ([S-842]), its short
/// name ("DELL U2720Q (primary)"), its long form with its resolution, and what the display pass knows
/// of it ([S-845]: whether it is the primary).
/// </summary>
public sealed record MenuDisplay(string Id, string Name, string Description, DisplayInfo Info);

/// <summary>
/// [S-3543] What the edit bar's Display menu reads and writes on one display: the state, the
/// connected displays, and the host's switch and scale, which re-run the display pass.
/// </summary>
public interface IDisplayMenuHost
{
    /// <summary>The live state, read as it is now.</summary>
    AppState State { get; }

    /// <summary>[S-842] The display the menu is open on: "this display".</summary>
    string DisplayId { get; }

    /// <summary>[S-853] Every connected display, switched off or not, in Windows' order.</summary>
    IReadOnlyList<MenuDisplay> Displays { get; }

    /// <summary>One change of the state: saved, one undo step.</summary>
    void Mutate(Action<AppState> change, bool relayout = true);

    /// <summary>[S-845] Show Themia on a display or not; false (and nothing changed) for the last display on.</summary>
    bool SetEnabled(string displayId, bool on);

    /// <summary>[S-850] This display's widget scale (1 = 100 %).</summary>
    void SetScale(double scale);
}

/// <summary>
/// [S-3543]..[S-3545] The edit bar's Display menu (Display ▾) as data, after Karpo's (karpo-app,
/// docs/widgets.md §348): this display's own settings under its name — its widget scale and its view
/// bar — then, under "All displays", what every display shares: the view a display Themia has not
/// seen before shows, and which displays Themia is on. Which view this display shows is the View
/// menu's ([S-3541]), not here again.
/// </summary>
public static class DisplayMenu
{
    /// <summary>[S-3543] The first level: this display's name, its lines, a divider, then every display's.</summary>
    public static IReadOnlyList<SettingsLine> Root(IDisplayMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var state = host.State;
        var id = host.DisplayId;
        var lines = new List<SettingsLine>();

        // The name says which display "this" is: two identical screens are told apart by their size.
        if (host.Displays.FirstOrDefault(d => d.Id == id) is { } here)
        {
            lines.Add(new SettingsNote(Keys.Heading, here.Description));
        }

        // [S-850] This display's magnification of the widgets' content, 50–200 % in fives.
        var scale = state.MonitorScales.TryGetValue(id, out var stored) ? stored : 1.0;
        lines.Add(new SettingsSlider(Keys.Scale, Str.Display_WidgetScale_Label.Text(), Glyphs.Scale, scale * 100,
            AppStateStore.MinMonitorScale * 100, AppStateStore.MaxMonitorScale * 100, 5, v => SettingsMenu.Percent(v / 100), v => host.SetScale(v / 100)));
        lines.Add(ViewBar(host));

        lines.Add(new SettingsSeparator("-" + Keys.All));
        lines.Add(new SettingsHeading(Keys.All, Str.BarMenus_Display_All.Text()));
        lines.Add(NewDisplays(host));
        lines.Add(ShowOn(host));
        return lines;
    }

    /// <summary>
    /// [S-3544] This display's view bar ([S-906], [S-916]): shown or not, its style ([S-5400]), where
    /// it sits (the eight anchors), its size, its names, and the views it offers; its value where it
    /// sits, or Hidden.
    /// </summary>
    private static SettingsSubmenu ViewBar(IDisplayMenuHost host)
    {
        var id = host.DisplayId;
        var state = host.State;
        var bar = ViewBarRules.For(state, id);
        void Change(Action<ViewBarSettings> change) => host.Mutate(s => ViewBarRules.Update(s, id, change));

        (ViewBarAnchor Value, string Text)[] anchors =
        [
            (ViewBarAnchor.TopLeft, Str.Shell_ViewBar_TopLeft.Text()),
            (ViewBarAnchor.TopCenter, Str.Shell_ViewBar_TopCentre.Text()),
            (ViewBarAnchor.TopRight, Str.Shell_ViewBar_TopRight.Text()),
            (ViewBarAnchor.LeftCenter, Str.Shell_ViewBar_LeftVertical.Text()),
            (ViewBarAnchor.RightCenter, Str.Shell_ViewBar_RightVertical.Text()),
            (ViewBarAnchor.BottomLeft, Str.Shell_ViewBar_BottomLeft.Text()),
            (ViewBarAnchor.BottomCenter, Str.Shell_ViewBar_BottomCentre.Text()),
            (ViewBarAnchor.BottomRight, Str.Shell_ViewBar_BottomRight.Text()),
        ];
        (ViewBarStyle Value, string Text)[] styles =
        [
            (ViewBarStyle.Chips, Str.Shell_ViewBar_StyleChips.Text()),
            (ViewBarStyle.Dropdown, Str.Shell_ViewBar_StyleDropdown.Text()),
        ];
        (ViewBarSize Value, string Text)[] sizes =
        [
            (ViewBarSize.Medium, Str.Shell_Size_Medium.Text()),
            (ViewBarSize.Large, Str.Shell_Size_Large.Text()),
            (ViewBarSize.ExtraLarge, Str.Shell_Size_ExtraLarge.Text()),
        ];

        var key = Keys.ViewBar;
        List<SettingsLine> lines =
        [
            new SettingsToggle(key + "/show", Str.Shell_ViewBar_Show.Text(), Glyphs.View, !bar.Hidden, on => Change(b => b.Hidden = !on)),
            Choice(key + "/style", Str.BarMenus_ViewBar_Style.Text(), Glyphs.ListLayout, styles, bar.Style, v => Change(b => b.Style = v)),
            Choice(key + "/position", Str.BarMenus_ViewBar_Position.Text(), Glyphs.Display, anchors, bar.Anchor, v => Change(b => b.Anchor = v)),
            Choice(key + "/size", Str.BarMenus_ViewBar_Size.Text(), Glyphs.Scale, sizes, bar.Size, v => Change(b => b.Size = v)),
            new SettingsToggle(key + "/names", Str.Shell_ViewBar_ShowNames.Text(), Glyphs.Rename, !bar.IconOnly, on => Change(b => b.IconOnly = !on)),
            new SettingsSeparator(key + "/-views"),
            ViewsOnTheBar(host, bar),
        ];

        return new SettingsSubmenu(key, Str.BarMenus_ViewBar.Text(), Glyphs.View, lines)
        {
            Value = bar.Hidden ? Str.BarMenus_ViewBar_Hidden.Text() : anchors.First(a => a.Value == bar.Anchor).Text,
        };
    }

    /// <summary>
    /// [S-3544] Views on the bar ([S-908]): All views (on while the bar offers every view; a click on
    /// it then keeps only the view this display shows, else offers all, [S-1708]'s rule), a divider,
    /// then one switch per view in the user's order, each with its mark ([S-3810]). Every switch is
    /// read back from what was stored: the last view on the bar cannot be switched off.
    /// </summary>
    private static SettingsSubmenu ViewsOnTheBar(IDisplayMenuHost host, ViewBarSettings bar)
    {
        var id = host.DisplayId;
        var state = host.State;
        var offer = ViewBarRules.Offer(state.Views, bar);
        var picked = ViewBarRules.VisibleViews(state.Views, bar).Select(v => v.Id).ToHashSet(StringComparer.Ordinal);
        var key = Keys.ViewBar + "/views";

        var lines = new List<SettingsLine>
        {
            new SettingsToggle(key + "/all", Str.Shell_ViewBar_AllViews.Text(), null, offer.All,
                on => host.Mutate(s => ViewBarRules.Update(s, id, b => b.VisibleViews = ViewBarRules.SetAllVisible(s.Views, on, ViewsMenu.ShownOn(s, id))))),
            new SettingsSeparator(key + "/-each"),
        };

        foreach (var view in state.Views)
        {
            var viewId = view.Id;
            lines.Add(new SettingsToggle(key + "/" + viewId, view.Name, string.IsNullOrEmpty(view.Icon) ? Glyphs.View : view.Icon, picked.Contains(viewId),
                on => host.Mutate(s => ViewBarRules.Update(s, id, b => b.VisibleViews = ViewBarRules.ToggleVisible(s.Views, b, viewId, on))))
            {
                GlyphColour = view.Color,
            });
        }

        // In the view bar's own submenu: "Views", not "Views on the bar", so its value has room.
        return new SettingsSubmenu(key, Str.BarMenus_ViewBar_Views.Text(), Glyphs.View, lines)
        {
            Value = offer.All
                ? Str.Shell_ViewBar_AllViews.Text()
                : offer.Single ?? Str.Shell_ViewBar_SomeViews.Format(("count", LocaleFormat.Integer(offer.Count)), ("total", LocaleFormat.Integer(offer.Total))),
        };
    }

    /// <summary>
    /// [S-3545] [S-847] New displays show: The last view shown, or one of the views (each with its
    /// mark): what a display Themia has not seen before shows. Stored with no relayout.
    /// </summary>
    private static SettingsChoice NewDisplays(IDisplayMenuHost host)
    {
        var state = host.State;
        var chosen = state.DefaultViewForNewDisplays is { } id && state.FindView(id) is not null ? id : null;
        void Set(string? viewId) => host.Mutate(s => s.DefaultViewForNewDisplays = viewId, relayout: false);

        var options = new List<SettingsRadio>
        {
            Radio(Keys.NewDisplays + "/last", Str.Shell_Settings_General_NewDisplayLastView.Text(), chosen is null, () => Set(null)),
        };

        foreach (var view in state.Views)
        {
            var viewId = view.Id;
            options.Add(Radio(Keys.NewDisplays + "/" + viewId, view.Name, chosen == viewId, () => Set(viewId)) with
            {
                Glyph = string.IsNullOrEmpty(view.Icon) ? Glyphs.View : view.Icon,
                GlyphColour = view.Color,
            });
        }

        return new SettingsChoice(Keys.NewDisplays, Str.Shell_Settings_General_NewDisplays.Text(), Glyphs.NewWindow, options);
    }

    /// <summary>
    /// [S-3545] [S-845] Show Themia on: one switch per connected display, switched off or not (this
    /// one says so), so a display switched off is switched back on from another's menu. The last
    /// display on is greyed, with why under the list.
    /// </summary>
    private static SettingsSubmenu ShowOn(IDisplayMenuHost host)
    {
        var state = host.State;
        var connected = host.Displays.Select(d => d.Info).ToList();
        var lines = new List<SettingsLine>();
        var locked = false;

        foreach (var display in host.Displays)
        {
            var displayId = display.Id;
            var on = !state.DisabledMonitors.Contains(displayId);
            var canSwitch = !on || DisplayPlan.CanDisable(connected, state.DisabledMonitors, displayId);
            locked |= !canSwitch;

            lines.Add(new SettingsToggle(Keys.ShowOn + "/" + displayId, display.Name, Glyphs.Display, on, value => host.SetEnabled(displayId, value))
            {
                Value = displayId == host.DisplayId ? Str.BarMenus_Display_This.Text() : null,
                Disabled = !canSwitch,
            });
        }

        if (locked)
        {
            lines.Add(new SettingsNote(Keys.ShowOn + "/last", Str.Shell_Display_ShowLast.Text()));
        }

        return new SettingsSubmenu(Keys.ShowOn, Str.BarMenus_Display_ShowOn.Text(), Glyphs.Display, lines);
    }

    private static SettingsChoice Choice<T>(string key, string text, string glyph, (T Value, string Text)[] options, T holds, Action<T> pick)
        where T : struct, Enum
        => new(key, text, glyph, [.. options.Select(o => Radio(key + "/" + o.Value, o.Text, EqualityComparer<T>.Default.Equals(o.Value, holds), () => pick(o.Value)))]);

    /// <summary>[S-3718] An option; picking the one that holds changes nothing.</summary>
    private static SettingsRadio Radio(string key, string text, bool holds, Action pick)
        => new(key, text, holds, () =>
        {
            if (!holds)
            {
                pick();
            }
        });

    /// <summary>The menu's keys that something outside it opens (a render, a test).</summary>
    public static class Keys
    {
        public const string Heading = "display";
        public const string Scale = "scale";
        public const string ViewBar = "viewBar";
        public const string All = "all";
        public const string NewDisplays = "newDisplays";
        public const string ShowOn = "showOn";
    }
}
