using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2314] [S-3543]..[S-3545] The edit bar's Display page as data: every display its own card (its name, resolution,
/// scale and view under it), opened the view it shows, Show Themia here, its scale and its view bar; then what every
/// display shares. Each line names its display and writes through the host.
/// </summary>
[Collection(GlobalLocaleCollection.Name)]
public sealed class DisplayMenuTests
{
    /// <summary>A host over two views and three displays (the first is this one), recording what was asked.</summary>
    private sealed class FakeHost : IDisplayMenuHost
    {
        public FakeHost()
        {
            foreach (var (id, name) in new[] { ("home", "Home"), ("work", "Work") })
            {
                var view = ViewState.Create(name);
                view.Id = id;
                State.Views.Add(view);
            }

            State.ActiveViewId = "home";
            State.MonitorViews["a"] = "home";
        }

        public AppState State { get; } = new() { Views = [] };

        public string DisplayId { get; set; } = "a";

        public IReadOnlyList<MenuDisplay> Displays { get; set; } = [Display("a", 0, primary: true), Display("b", 1920, primary: false), Display("c", 3840, primary: false)];

        public int Mutations { get; private set; }

        public List<bool> Relayouts { get; } = [];

        public List<string> Switched { get; } = [];

        public List<string> Scales { get; } = [];

        public void Mutate(Action<AppState> change, bool relayout = true)
        {
            change(State);
            Mutations++;
            Relayouts.Add(relayout);
        }

        public bool SetEnabled(string displayId, bool on)
        {
            Switched.Add($"{displayId}={on}");
            return true;
        }

        public void SetScale(string displayId, double scale) => Scales.Add(displayId + "=" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public List<string> Shown { get; } = [];

        public void Show(string displayId, string viewId) => Shown.Add(displayId + ":" + viewId);

        public static MenuDisplay Display(string id, int x, bool primary)
            => new(id, "Screen " + id, "Screen " + id + " — 1920 × 1080", new DisplayInfo(id, true, new PixelRect(x, 0, 1920, 1080), new PixelRect(x, 0, 1920, 1040), primary));
    }

    private static SettingsSubmenu Card(FakeHost host, string id)
        => Assert.IsType<SettingsSubmenu>(DisplayMenu.Root(host).Single(l => l.Key == DisplayMenu.Keys.Display(id)));

    private static T In<T>(FakeHost host, string id, string key)
        where T : SettingsLine
        => Assert.IsType<T>(Card(host, id).Lines.Single(l => l.Key == key));

    private static string Bar(string id) => DisplayMenu.Keys.ViewBar(id);

    [Fact]
    public void S2314_the_page_is_every_display_a_card_in_windows_order_then_every_displays_lines()
    {
        var host = new FakeHost();
        var keys = DisplayMenu.Root(host).Select(l => l.Key).ToArray();

        Assert.Equal(
            [DisplayMenu.Keys.Heading, DisplayMenu.Keys.Display("a"), DisplayMenu.Keys.Display("b"), DisplayMenu.Keys.Display("c"), DisplayMenu.Keys.All, DisplayMenu.Keys.NewDisplays],
            keys);
        Assert.All(DisplayMenu.Root(host).OfType<SettingsSubmenu>(), c => Assert.Equal(SettingsRowKind.Expander, SettingsRows.KindOf(c)));
        Assert.Equal([DisplayMenu.Keys.Heading, DisplayMenu.Keys.All], SettingsPages.Display(host).Sections.Select(s => s.Key));

        // The page open on another display lists them in the same order: none is moved first.
        host.DisplayId = "c";
        Assert.Equal(keys, DisplayMenu.Root(host).Select(l => l.Key));
    }

    [Fact]
    public void S2314_a_displays_head_is_its_name_resolution_scale_and_view_and_this_display_at_its_end()
    {
        var host = new FakeHost();
        host.State.MonitorScales["b"] = 1.25;
        host.State.DisabledMonitors.Add("c");

        var a = Card(host, "a");
        Assert.Equal(("Screen a", Str.BarMenus_Display_This.Text()), (a.Text, a.Value));
        Assert.Equal(Str.SettingsPages_Display_Caption.Format(("width", "1,920"), ("height", "1,080"), ("scale", SettingsMenu.Percent(1))) + " · Home", a.Caption);
        Assert.Null(Card(host, "b").Value);
        Assert.Contains(SettingsMenu.Percent(1.25), Card(host, "b").Caption, StringComparison.Ordinal);
        Assert.EndsWith(Str.SettingsPages_Display_Off.Text(), Card(host, "c").Caption, StringComparison.Ordinal);
    }

    [Fact]
    public void S2314_no_line_acts_on_the_selected_display_every_line_names_its_display()
    {
        var host = new FakeHost();
        foreach (var card in DisplayMenu.Root(host).OfType<SettingsSubmenu>())
        {
            Assert.All(card.Lines.Where(l => l is not SettingsSeparator), l => Assert.StartsWith(card.Key + "/", l.Key, StringComparison.Ordinal));
            Assert.DoesNotContain(card.Lines, l => l is SettingsSubmenu);
        }
    }

    [Fact]
    public void S19_each_display_ticks_the_view_it_shows_and_a_pick_shows_another_there()
    {
        var host = new FakeHost();
        host.State.MonitorViews["b"] = "work";
        var a = In<SettingsChoice>(host, "a", DisplayMenu.Keys.View("a"));
        var b = In<SettingsChoice>(host, "b", DisplayMenu.Keys.View("b"));

        Assert.Equal([true, false], a.Options.Select(o => o.Holds));
        Assert.Equal([false, true], b.Options.Select(o => o.Holds));

        a.Options[0].Pick();
        Assert.Empty(host.Shown);
        b.Options[0].Pick();
        a.Options[1].Pick();
        Assert.Equal(["b:home", "a:work"], host.Shown);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3543_a_display_with_no_view_of_its_own_ticks_the_active_one_with_its_mark()
    {
        var host = new FakeHost();
        host.State.MonitorViews.Remove("a");
        host.State.Views[0].Icon = Themia.App.Shell.Glyphs.Mail;
        host.State.Views[0].Color = "#0099BC";

        var options = In<SettingsChoice>(host, "a", DisplayMenu.Keys.View("a")).Options;
        Assert.True(options[0].Holds);
        Assert.Equal((Themia.App.Shell.Glyphs.Mail, "#0099BC"), (options[0].Glyph, options[0].GlyphColour));
        Assert.Equal(Themia.App.Shell.Glyphs.View, options[1].Glyph);
    }

    [Fact]
    public void S850_each_displays_scale_is_its_own_50_to_200_in_fives_and_asks_the_host()
    {
        var host = new FakeHost();
        host.State.MonitorScales["a"] = 1.25;
        host.State.MonitorScales["b"] = 2;
        var a = In<SettingsSlider>(host, "a", DisplayMenu.Keys.Scale("a"));

        Assert.Equal((125d, 50d, 200d, 5d), (a.Value, a.Min, a.Max, a.Step));
        Assert.Equal(200d, In<SettingsSlider>(host, "b", DisplayMenu.Keys.Scale("b")).Value);
        Assert.Equal(LocaleFormat.Percent(Strings.Current, 125), a.Format(125));

        In<SettingsSlider>(host, "b", DisplayMenu.Keys.Scale("b")).Set(150);
        Assert.Equal(["b=1.5"], host.Scales);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3544_each_displays_view_bar_is_in_its_card_and_writes_that_displays_bar()
    {
        var host = new FakeHost();
        var b = Bar("b");

        Assert.Equal(
            [b + "/-", b, b + "/show", b + "/type", b + "/labels", b + "/position", b + "/size", b + "/views/-", b + "/views", b + "/views/all", b + "/views/home", b + "/views/work"],
            Card(host, "b").Lines.SkipWhile(l => l.Key != b + "/-").Select(l => l.Key));

        In<SettingsChoice>(host, "b", b + "/position").Options.Single(o => o.Key.EndsWith("/" + ViewBarAnchor.TopRight, StringComparison.Ordinal)).Pick();
        In<SettingsChoice>(host, "b", b + "/type").Options.Single(o => o.Key.EndsWith("/" + ViewBarType.Select, StringComparison.Ordinal)).Pick();
        In<SettingsChoice>(host, "b", b + "/size").Options.Single(o => o.Key.EndsWith("/" + ViewBarSize.Large, StringComparison.Ordinal)).Pick();
        In<SettingsChoice>(host, "b", b + "/labels").Options.Single(o => o.Key.EndsWith("/" + ViewBarLabels.TextOnly, StringComparison.Ordinal)).Pick();
        In<SettingsToggle>(host, "b", b + "/show").Set(false);

        var stored = ViewBarRules.For(host.State, "b");
        Assert.Equal((ViewBarAnchor.TopRight, ViewBarType.Select, ViewBarSize.Large, ViewBarLabels.TextOnly, true), (stored.Anchor, stored.Type, stored.Size, stored.Labels, stored.Hidden));
        Assert.False(host.State.ViewBars.ContainsKey("a"));
    }

    [Fact]
    public void S2349_a_displays_card_is_its_own_lines_then_the_bar_and_its_views_each_a_group_under_a_glyphs_head()
    {
        var host = new FakeHost();
        var lines = Card(host, "a").Lines;
        var b = Bar("a");

        // Its own lines first, no divider nor heading between them: the view, Show Themia here (and why not), the scale.
        Assert.Equal(
            [DisplayMenu.Keys.View("a"), DisplayMenu.Keys.On("a"), DisplayMenu.Keys.On("a") + "/last", DisplayMenu.Keys.Scale("a")],
            lines.TakeWhile(l => l is not SettingsSeparator).Select(l => l.Key));

        // Then two groups, each a divider and a heading with a glyph (Karpo's §685).
        var heads = lines.OfType<SettingsHeading>().ToList();
        Assert.Equal([b, b + "/views"], heads.Select(h => h.Key));
        Assert.All(heads, h => Assert.NotNull(h.Glyph));
        Assert.All(lines.OfType<SettingsToggle>().Where(t => t.Key.StartsWith(b + "/views/", StringComparison.Ordinal) && t.Key != b + "/views/all"), t => Assert.True(t.Marked));
    }

    [Fact]
    public void S2349_what_only_a_shown_bar_draws_stands_greyed_while_it_is_hidden()
    {
        var host = new FakeHost();
        var b = Bar("a");
        string[] keys = [b + "/type", b + "/labels", b + "/position", b + "/size", b + "/views/all", b + "/views/home", b + "/views/work"];
        Assert.All(keys, k => Assert.False(Card(host, "a").Lines.Single(l => l.Key == k).Disabled));

        In<SettingsToggle>(host, "a", b + "/show").Set(false);

        Assert.All(keys, k => Assert.True(Card(host, "a").Lines.Single(l => l.Key == k).Disabled));
        Assert.False(In<SettingsToggle>(host, "a", b + "/show").Disabled);
    }

    [Fact]
    public void S13400_type_offers_every_style_in_order_and_picking_one_stores_it()
    {
        var host = new FakeHost();
        var type = In<SettingsChoice>(host, "b", Bar("b") + "/type");

        Assert.Equal(
            [ViewBarType.Inline, ViewBarType.Select, ViewBarType.Dots, ViewBarType.Arrows, ViewBarType.Cycle],
            type.Options.Select(o => Enum.Parse<ViewBarType>(o.Key[(o.Key.LastIndexOf('/') + 1)..])));
        Assert.Equal(Enum.GetValues<ViewBarType>().Length, type.Options.Count);

        type.Options.Single(o => o.Key.EndsWith("/" + ViewBarType.Cycle, StringComparison.Ordinal)).Pick();

        Assert.Equal(ViewBarType.Cycle, ViewBarRules.For(host.State, "b").Type);
    }

    [Fact]
    public void S13401_labels_stand_greyed_while_the_bar_is_dots()
    {
        var host = new FakeHost();
        Assert.False(In<SettingsChoice>(host, "b", Bar("b") + "/labels").Disabled);

        In<SettingsChoice>(host, "b", Bar("b") + "/type").Options.Single(o => o.Key.EndsWith("/" + ViewBarType.Dots, StringComparison.Ordinal)).Pick();

        Assert.True(In<SettingsChoice>(host, "b", Bar("b") + "/labels").Disabled);
        Assert.False(In<SettingsChoice>(host, "a", Bar("a") + "/labels").Disabled);
    }

    [Fact]
    public void S3718_picking_the_option_that_holds_changes_nothing()
    {
        var host = new FakeHost();
        In<SettingsChoice>(host, "a", Bar("a") + "/position").Options.Single(o => o.Holds).Pick();

        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3544_views_on_the_bar_switch_views_off_but_never_the_last_and_all_brings_every_view_back()
    {
        var host = new FakeHost();
        var views = Bar("a") + "/views";

        In<SettingsToggle>(host, "a", views + "/work").Set(false);
        Assert.Equal(["home"], ViewBarRules.VisibleViews(host.State.Views, ViewBarRules.For(host.State, "a")).Select(v => v.Id));
        Assert.False(In<SettingsToggle>(host, "a", views + "/all").On);

        // [S-908] The last view on the bar stays on.
        In<SettingsToggle>(host, "a", views + "/home").Set(false);
        Assert.Equal(["home"], ViewBarRules.VisibleViews(host.State.Views, ViewBarRules.For(host.State, "a")).Select(v => v.Id));

        In<SettingsToggle>(host, "a", views + "/all").Set(true);
        Assert.True(In<SettingsToggle>(host, "a", views + "/all").On);
    }

    [Fact]
    public void S3545_new_displays_show_the_last_view_shown_or_a_view_stored_without_a_relayout()
    {
        var host = new FakeHost();
        var choice = Assert.IsType<SettingsChoice>(DisplayMenu.Root(host).Single(l => l.Key == DisplayMenu.Keys.NewDisplays));

        Assert.Equal(["newDisplays/last", "newDisplays/home", "newDisplays/work"], choice.Options.Select(o => o.Key));
        Assert.Equal(Str.Shell_Settings_General_NewDisplayLastView.Text(), choice.Value);

        choice.Options[2].Pick();
        Assert.Equal("work", host.State.DefaultViewForNewDisplays);
        Assert.Equal([false], host.Relayouts);
    }

    [Fact]
    public void S845_show_themia_here_is_each_displays_and_a_switched_off_one_can_come_back()
    {
        var host = new FakeHost();
        host.State.DisabledMonitors.Add("c");

        Assert.Equal([true, true, false], new[] { "a", "b", "c" }.Select(id => In<SettingsToggle>(host, id, DisplayMenu.Keys.On(id)).On));
        Assert.All(new[] { "a", "b", "c" }, id => Assert.False(In<SettingsToggle>(host, id, DisplayMenu.Keys.On(id)).Disabled));

        In<SettingsToggle>(host, "c", DisplayMenu.Keys.On("c")).Set(true);
        In<SettingsToggle>(host, "b", DisplayMenu.Keys.On("b")).Set(false);
        Assert.Equal(["c=True", "b=False"], host.Switched);
    }

    [Fact]
    public void S845_the_last_display_on_is_greyed_with_why_under_it()
    {
        var host = new FakeHost();
        host.State.DisabledMonitors.AddRange(["b", "c"]);

        Assert.True(In<SettingsToggle>(host, "a", DisplayMenu.Keys.On("a")).Disabled);
        Assert.Equal(Str.Shell_Display_ShowLast.Text(), In<SettingsNote>(host, "a", DisplayMenu.Keys.On("a") + "/last").Text);
        Assert.Equal(string.Empty, In<SettingsNote>(host, "b", DisplayMenu.Keys.On("b") + "/last").Text);
    }
}
