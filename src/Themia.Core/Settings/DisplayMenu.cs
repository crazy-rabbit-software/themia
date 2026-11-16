using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3543] A connected display as the Display page names it: its stable id ([S-842]), its short
/// name ("DELL U2720Q (primary)"), its long form with its resolution, and what the display pass knows
/// of it ([S-845]: whether it is the primary, its bounds).
/// </summary>
public sealed record MenuDisplay(string Id, string Name, string Description, DisplayInfo Info);

/// <summary>
/// [S-3543] What the edit bar's Display page reads and writes: the state, the connected displays, and the
/// host's switch, scale and view, which re-run the display pass. Every write names its display.
/// </summary>
public interface IDisplayMenuHost
{
    /// <summary>The live state, read as it is now.</summary>
    AppState State { get; }

    /// <summary>[S-842] The display the page is open on: "this display".</summary>
    string DisplayId { get; }

    /// <summary>[S-853] Every connected display, switched off or not, in Windows' order.</summary>
    IReadOnlyList<MenuDisplay> Displays { get; }

    /// <summary>One change of the state: saved, one undo step.</summary>
    void Mutate(Action<AppState> change, bool relayout = true);

    /// <summary>[S-845] Show Themia on a display or not; false (and nothing changed) for the last display on.</summary>
    bool SetEnabled(string displayId, bool on);

    /// <summary>[S-850] A display's widget scale (1 = 100 %).</summary>
    void SetScale(string displayId, double scale);

    /// <summary>[S-19] Show a view on a display.</summary>
    void Show(string displayId, string viewId);
}

/// <summary>
/// [S-2314] The edit bar's Display page as data, after Karpo's Displays (karpo-app, Karpo.Core/BarLooks.cs
/// <c>DisplayThing</c>, docs/widgets.md §585): under "Displays" every connected display in Windows' order, never
/// moved, each a card that opens: its name, its resolution, its widget scale and the view it shows under it, "This
/// display" at its head's end on the one the page is open on; opened, everything that is that display's — the view
/// it shows, whether Themia is on it, its widget scale and its view bar. Then under "All displays" what they share:
/// what a display Themia has not seen before shows. Nothing acts on "the selected display": every line names its
/// display. What a view is (its name, icon, colour, order) is the Views page's ([S-2313]).
/// </summary>
public static class DisplayMenu
{
    /// <summary>[S-2314] The page's lines: every display's card, then what every display shares.</summary>
    public static IReadOnlyList<SettingsLine> Root(IDisplayMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var lines = new List<SettingsLine> { new SettingsHeading(Keys.Heading, Str.SettingsPages_Display_Displays.Text()) };
        var connected = host.Displays.Select(d => d.Info).ToList();
        foreach (var display in host.Displays)
        {
            lines.Add(Display(host, display, connected));
        }

        lines.Add(new SettingsHeading(Keys.All, Str.BarMenus_Display_All.Text()));
        lines.Add(NewDisplays(host));
        return lines;
    }

    /// <summary>
    /// [S-2314] One display's card: its head (name; resolution, scale and view under it; "This display" at its end)
    /// and, opened, the view it shows ([S-19]), Show Themia here ([S-845]), its widget scale ([S-850]) and its view
    /// bar ([S-3544]).
    /// </summary>
    private static SettingsSubmenu Display(IDisplayMenuHost host, MenuDisplay display, IReadOnlyList<DisplayInfo> connected)
    {
        var state = host.State;
        var id = display.Id;
        var key = Keys.Display(id);
        var on = !state.DisabledMonitors.Contains(id);
        var shown = ViewsMenu.ShownOn(state, id);
        var scale = state.MonitorScales.TryGetValue(id, out var stored) ? stored : 1.0;

        // [S-19] The view it shows: one option per view, its mark in its colour.
        var view = new SettingsChoice(Keys.View(id), Str.SettingsPages_Display_View.Text(), Glyphs.View,
        [
            .. state.Views.Select(v =>
            {
                var viewId = v.Id;
                return Radio(Keys.View(id) + "/" + viewId, v.Name, viewId == shown, () => host.Show(id, viewId)) with
                {
                    Glyph = ViewsMenu.Mark(v),
                    GlyphColour = v.Color,
                };
            }),
        ]);

        // [S-845] Switched off from any display's page, so it is switched back on from another's; the last one on is greyed.
        var canSwitch = !on || DisplayPlan.CanDisable(connected, state.DisabledMonitors, id);
        List<SettingsLine> lines =
        [
            view,
            new SettingsSeparator(key + "/-on"),
            new SettingsToggle(Keys.On(id), Str.SettingsPages_Display_ShowThemia.Text(), Glyphs.Display, on, value => host.SetEnabled(id, value))
            {
                Disabled = !canSwitch,
            },
            new SettingsNote(Keys.On(id) + "/last", canSwitch ? string.Empty : Str.Shell_Display_ShowLast.Text()),

            // [S-850] Its magnification of the widgets' content, 50–200 % in fives.
            new SettingsSlider(Keys.Scale(id), Str.Display_WidgetScale_Label.Text(), Glyphs.Scale, scale * 100,
                AppStateStore.MinMonitorScale * 100, AppStateStore.MaxMonitorScale * 100, 5, v => SettingsMenu.Percent(v / 100), v => host.SetScale(id, v / 100)),
            .. ViewBar(host, id),
        ];

        var head = Str.SettingsPages_Display_Caption.Format(
            ("width", LocaleFormat.Integer((long)Math.Round(display.Info.Bounds.Width))),
            ("height", LocaleFormat.Integer((long)Math.Round(display.Info.Bounds.Height))),
            ("scale", SettingsMenu.Percent(scale)));
        return new SettingsSubmenu(key, display.Name, Glyphs.Display, lines)
        {
            Card = true,
            Caption = on
                ? head + " · " + (state.FindView(shown)?.Name ?? string.Empty)
                : head + " · " + Str.SettingsPages_Display_Off.Text(),
            Value = id == host.DisplayId ? Str.BarMenus_Display_This.Text() : null,
        };
    }

    /// <summary>
    /// [S-3544] A display's view bar ([S-906], [S-916]) in its card under its heading: shown or not, its type
    /// ([S-5400]) and labels ([S-5405]), where it sits (the eight anchors), its size; then the views it offers
    /// under theirs ([S-908]): All views (on while the bar offers every view; a click on it then keeps only the view
    /// the display shows, else offers all, [S-1708]'s rule) and one switch per view, each with its mark ([S-3810]).
    /// Every switch is read back from what was stored: the last view on the bar cannot be switched off.
    /// </summary>
    private static IEnumerable<SettingsLine> ViewBar(IDisplayMenuHost host, string id)
    {
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
        (ViewBarType Value, string Text)[] types =
        [
            (ViewBarType.Inline, Str.Shell_ViewBar_TypeInline.Text()),
            (ViewBarType.Select, Str.Shell_ViewBar_TypeSelect.Text()),
        ];
        (ViewBarLabels Value, string Text)[] labels =
        [
            (ViewBarLabels.IconAndText, Str.Shell_ViewBar_LabelsIconAndText.Text()),
            (ViewBarLabels.IconOnly, Str.Shell_ViewBar_LabelsIconOnly.Text()),
            (ViewBarLabels.TextOnly, Str.Shell_ViewBar_LabelsTextOnly.Text()),
        ];
        (ViewBarSize Value, string Text)[] sizes =
        [
            (ViewBarSize.Medium, Str.Shell_Size_Medium.Text()),
            (ViewBarSize.Large, Str.Shell_Size_Large.Text()),
            (ViewBarSize.ExtraLarge, Str.Shell_Size_ExtraLarge.Text()),
        ];

        var key = Keys.ViewBar(id);
        yield return new SettingsSeparator(key + "/-");
        yield return new SettingsHeading(key, Str.BarMenus_ViewBar.Text());
        yield return new SettingsToggle(key + "/show", Str.Shell_ViewBar_Show.Text(), Glyphs.View, !bar.Hidden, on => Change(b => b.Hidden = !on));
        yield return Choice(key + "/type", Str.BarMenus_ViewBar_Type.Text(), Glyphs.ListLayout, types, bar.Type, v => Change(b => b.Type = v));
        yield return Choice(key + "/labels", Str.BarMenus_ViewBar_Labels.Text(), Glyphs.Tag, labels, bar.Labels, v => Change(b => b.Labels = v));
        yield return Choice(key + "/position", Str.BarMenus_ViewBar_Position.Text(), Glyphs.Display, anchors, bar.Anchor, v => Change(b => b.Anchor = v));
        yield return Choice(key + "/size", Str.BarMenus_ViewBar_Size.Text(), Glyphs.Scale, sizes, bar.Size, v => Change(b => b.Size = v));

        var offer = ViewBarRules.Offer(state.Views, bar);
        var picked = ViewBarRules.VisibleViews(state.Views, bar).Select(v => v.Id).ToHashSet(StringComparer.Ordinal);
        var views = key + "/views";
        yield return new SettingsSeparator(views + "/-");
        yield return new SettingsHeading(views, Str.BarMenus_ViewBar_Views.Text());
        yield return new SettingsToggle(views + "/all", Str.Shell_ViewBar_AllViews.Text(), null, offer.All,
            on => host.Mutate(s => ViewBarRules.Update(s, id, b => b.VisibleViews = ViewBarRules.SetAllVisible(s.Views, on, ViewsMenu.ShownOn(s, id)))));
        foreach (var view in state.Views)
        {
            var viewId = view.Id;
            yield return new SettingsToggle(views + "/" + viewId, view.Name, ViewsMenu.Mark(view), picked.Contains(viewId),
                on => host.Mutate(s => ViewBarRules.Update(s, id, b => b.VisibleViews = ViewBarRules.ToggleVisible(s.Views, b, viewId, on))))
            {
                GlyphColour = view.Color,
            };
        }
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
                Glyph = ViewsMenu.Mark(view),
                GlyphColour = view.Color,
            });
        }

        return new SettingsChoice(Keys.NewDisplays, Str.Shell_Settings_General_NewDisplays.Text(), Glyphs.NewWindow, options);
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

    /// <summary>The page's keys that something outside it opens (a deep link, a render, a test).</summary>
    public static class Keys
    {
        /// <summary>"Displays": the section every display's card stands in.</summary>
        public const string Heading = "displays";
        public const string All = "all";
        public const string NewDisplays = "newDisplays";

        /// <summary>[S-2314] A display's card, by its stable id ([S-842]).</summary>
        public static string Display(string displayId) => "display:" + displayId;

        public static string View(string displayId) => Display(displayId) + "/view";

        public static string On(string displayId) => Display(displayId) + "/on";

        public static string Scale(string displayId) => Display(displayId) + "/scale";

        public static string ViewBar(string displayId) => Display(displayId) + "/viewBar";
    }
}
