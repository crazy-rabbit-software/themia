using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3543]..[S-3545] The edit bar's Display menu as data: this display's name, scale and view bar,
/// then what every display shares (the view new displays show, which displays Themia is on). Each
/// line writes the field the Displays page wrote, through the host.
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

        public List<double> Scales { get; } = [];

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

        public void SetScale(double scale) => Scales.Add(scale);

        public List<string> Shown { get; } = [];

        public void Show(string viewId) => Shown.Add(viewId);

        public static MenuDisplay Display(string id, int x, bool primary)
            => new(id, "Screen " + id, "Screen " + id + " — 1920 × 1080", new DisplayInfo(id, true, new PixelRect(x, 0, 1920, 1080), new PixelRect(x, 0, 1920, 1040), primary));
    }

    private static T At<T>(FakeHost host, params string[] path)
        where T : SettingsLine
        => Assert.IsType<T>(SettingsMenu.Find(DisplayMenu.Root(host), path));

    private const string Bar = DisplayMenu.Keys.ViewBar;

    [Fact]
    public void S3543_this_displays_name_the_views_it_may_show_its_scale_and_view_bar_then_every_displays_lines()
    {
        var host = new FakeHost();
        var keys = DisplayMenu.Root(host).Select(l => l is SettingsSeparator ? "-" : l.Key).ToArray();

        Assert.Equal(
            [
                DisplayMenu.Keys.Heading, DisplayMenu.Keys.Views, DisplayMenu.Keys.Show("home"), DisplayMenu.Keys.Show("work"), "-",
                DisplayMenu.Keys.Scale, Bar, "-", DisplayMenu.Keys.All, DisplayMenu.Keys.NewDisplays, DisplayMenu.Keys.ShowOn,
            ],
            keys);
        Assert.Equal("Screen a — 1920 × 1080", At<SettingsNote>(host, DisplayMenu.Keys.Heading).Text);
        Assert.Equal(Str.BarMenus_Display_All.Text(), At<SettingsHeading>(host, DisplayMenu.Keys.All).Text);
    }

    [Fact]
    public void S3543_the_view_this_display_shows_is_ticked_and_a_pick_of_another_shows_it_here()
    {
        var host = new FakeHost();
        var home = At<SettingsRadio>(host, DisplayMenu.Keys.Show("home"));
        var work = At<SettingsRadio>(host, DisplayMenu.Keys.Show("work"));

        Assert.Equal((true, false), (home.Holds, work.Holds));
        Assert.Equal("Work", work.Text);
        home.Pick();
        Assert.Empty(host.Shown);
        work.Pick();
        Assert.Equal(["work"], host.Shown);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3543_a_display_with_no_view_of_its_own_ticks_the_active_one_with_its_mark()
    {
        var host = new FakeHost();
        host.State.MonitorViews.Remove("a");
        host.State.Views[0].Icon = Themia.App.Shell.Glyphs.Mail;
        host.State.Views[0].Color = "#0099BC";

        var home = At<SettingsRadio>(host, DisplayMenu.Keys.Show("home"));
        Assert.True(home.Holds);
        Assert.Equal((Themia.App.Shell.Glyphs.Mail, "#0099BC"), (home.Glyph, home.GlyphColour));
        Assert.Equal(Themia.App.Shell.Glyphs.View, At<SettingsRadio>(host, DisplayMenu.Keys.Show("work")).Glyph);
    }

    [Fact]
    public void S850_the_scale_is_this_displays_50_to_200_in_fives_and_asks_the_host()
    {
        var host = new FakeHost();
        host.State.MonitorScales["a"] = 1.25;
        host.State.MonitorScales["b"] = 2;
        var scale = At<SettingsSlider>(host, DisplayMenu.Keys.Scale);

        Assert.Equal((125d, 50d, 200d, 5d), (scale.Value, scale.Min, scale.Max, scale.Step));
        Assert.Equal(LocaleFormat.Percent(Strings.Current, 125), scale.Format(125));

        scale.Set(150);
        Assert.Equal([1.5], host.Scales);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3544_the_view_bar_says_where_it_sits_or_hidden_and_writes_this_displays_bar()
    {
        var host = new FakeHost();
        var bar = At<SettingsSubmenu>(host, Bar);

        Assert.Equal(Str.Shell_ViewBar_BottomLeft.Text(), bar.Value);
        Assert.Equal(
            [Bar + "/show", Bar + "/style", Bar + "/position", Bar + "/size", Bar + "/names", "-", Bar + "/views"],
            bar.Lines.Select(l => l is SettingsSeparator ? "-" : l.Key));

        At<SettingsChoice>(host, Bar, Bar + "/position").Options.Single(o => o.Key.EndsWith("/" + ViewBarAnchor.TopRight, StringComparison.Ordinal)).Pick();
        At<SettingsChoice>(host, Bar, Bar + "/style").Options.Single(o => o.Key.EndsWith("/" + ViewBarStyle.Dropdown, StringComparison.Ordinal)).Pick();
        At<SettingsChoice>(host, Bar, Bar + "/size").Options.Single(o => o.Key.EndsWith("/" + ViewBarSize.Large, StringComparison.Ordinal)).Pick();
        At<SettingsToggle>(host, Bar, Bar + "/names").Set(false);
        At<SettingsToggle>(host, Bar, Bar + "/show").Set(false);

        var stored = ViewBarRules.For(host.State, "a");
        Assert.Equal((ViewBarAnchor.TopRight, ViewBarStyle.Dropdown, ViewBarSize.Large, true, true), (stored.Anchor, stored.Style, stored.Size, stored.IconOnly, stored.Hidden));
        Assert.False(host.State.ViewBars.ContainsKey("b"));
        Assert.Equal(Str.BarMenus_ViewBar_Hidden.Text(), At<SettingsSubmenu>(host, Bar).Value);
    }

    [Fact]
    public void S3718_picking_the_option_that_holds_changes_nothing()
    {
        var host = new FakeHost();
        At<SettingsChoice>(host, Bar, Bar + "/position").Options.Single(o => o.Holds).Pick();

        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3544_views_on_the_bar_switch_views_off_but_never_the_last_and_all_brings_every_view_back()
    {
        var host = new FakeHost();
        string views = Bar + "/views";

        Assert.Equal(Str.Shell_ViewBar_AllViews.Text(), At<SettingsSubmenu>(host, Bar, views).Value);
        At<SettingsToggle>(host, Bar, views, views + "/work").Set(false);
        Assert.Equal(["home"], ViewBarRules.VisibleViews(host.State.Views, ViewBarRules.For(host.State, "a")).Select(v => v.Id));
        Assert.Equal("Home", At<SettingsSubmenu>(host, Bar, views).Value);

        // [S-908] The last view on the bar stays on.
        At<SettingsToggle>(host, Bar, views, views + "/home").Set(false);
        Assert.Equal(["home"], ViewBarRules.VisibleViews(host.State.Views, ViewBarRules.For(host.State, "a")).Select(v => v.Id));

        At<SettingsToggle>(host, Bar, views, views + "/all").Set(true);
        Assert.True(At<SettingsToggle>(host, Bar, views, views + "/all").On);
    }

    [Fact]
    public void S3545_new_displays_show_the_last_view_shown_or_a_view_stored_without_a_relayout()
    {
        var host = new FakeHost();
        var choice = At<SettingsChoice>(host, DisplayMenu.Keys.NewDisplays);

        Assert.Equal(["newDisplays/last", "newDisplays/home", "newDisplays/work"], choice.Options.Select(o => o.Key));
        Assert.Equal(Str.Shell_Settings_General_NewDisplayLastView.Text(), choice.Value);

        choice.Options[2].Pick();
        Assert.Equal("work", host.State.DefaultViewForNewDisplays);
        Assert.Equal([false], host.Relayouts);
        Assert.Equal("Work", At<SettingsChoice>(host, DisplayMenu.Keys.NewDisplays).Value);

        At<SettingsChoice>(host, DisplayMenu.Keys.NewDisplays).Options[0].Pick();
        Assert.Null(host.State.DefaultViewForNewDisplays);
    }

    [Fact]
    public void S3545_show_themia_on_lists_every_display_this_one_said_and_a_switched_off_one_can_come_back()
    {
        var host = new FakeHost();
        host.State.DisabledMonitors.Add("c");
        var lines = At<SettingsSubmenu>(host, DisplayMenu.Keys.ShowOn).Lines.OfType<SettingsToggle>().ToList();

        Assert.Equal([true, true, false], lines.Select(l => l.On));
        Assert.Equal([Str.BarMenus_Display_This.Text(), null, null], lines.Select(l => l.Value));
        Assert.All(lines, l => Assert.False(l.Disabled));

        lines[2].Set(true);
        lines[1].Set(false);
        Assert.Equal(["c=True", "b=False"], host.Switched);
    }

    [Fact]
    public void S845_the_last_display_on_is_greyed_with_why_under_the_list()
    {
        var host = new FakeHost();
        host.State.DisabledMonitors.AddRange(["b", "c"]);
        var level = At<SettingsSubmenu>(host, DisplayMenu.Keys.ShowOn).Lines;

        Assert.True(level.OfType<SettingsToggle>().First().Disabled);
        Assert.Equal(Str.Shell_Display_ShowLast.Text(), Assert.IsType<SettingsNote>(level[^1]).Text);
    }
}
