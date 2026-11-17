using Themia.App.Shell;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2313] [S-2310] The edit bar's Views page as data: under "Your views" every view its own card (its mark, its name,
/// where it is shown and its widgets under it, Duplicate and Delete at its head's end, dragged into order), opened its
/// Name, Icon, Colour and Show on this display; then Add a view. No line acts on "the selected view". Every write goes
/// through the host.
/// </summary>
[Collection(GlobalLocaleCollection.Name)]
public sealed class ViewsMenuTests
{
    private const string Here = "display-here";

    private sealed class AnyConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new AnyConfig();
    }

    /// <summary>A host over three views of its own (Work shown here, Home on the other display), recording what was asked of it.</summary>
    private sealed class FakeHost : IViewsMenuHost
    {
        public FakeHost()
        {
            foreach (var (id, name) in new[] { ("home", "Home"), ("work", "Work"), ("evening", "Evening") })
            {
                var view = ViewState.Create(name);
                view.Id = id;
                State.Views.Add(view);
            }

            State.ActiveViewId = "home";
            State.MonitorViews[Here] = "work";
            State.MonitorViews["other"] = "home";
            State.Views[1].Icon = Glyphs.Mail;
            State.Views[1].Color = "#0099BC";
            State.Views[1].Widgets.Add(new WidgetInstance { Id = "w", Config = new AnyConfig(), Rect = new FracRect(0.1, 0.1, 0.1, 0.1) });
        }

        public AppState State { get; } = new() { Views = [] };

        public string DisplayId => Here;

        public IReadOnlyList<MenuDisplay> Displays { get; } =
        [
            new(Here, "Screen here", "Screen here — 1920 × 1080", new DisplayInfo(Here, true, new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true)),
            new("other", "LG HDR WQHD", "LG HDR WQHD — 2560 × 1440", new DisplayInfo("other", true, new PixelRect(1920, 0, 2560, 1440), new PixelRect(1920, 0, 2560, 1400), false)),
        ];

        public int Mutations { get; private set; }

        public List<string> Log { get; } = [];

        public List<SettingsConfirm> Asked { get; } = [];

        public void Mutate(Action<AppState> change, bool relayout = true)
        {
            change(State);
            Mutations++;
        }

        public void Add() => Log.Add("add");

        public void Duplicate(string viewId) => Log.Add("duplicate " + viewId);

        public void Delete(string viewId) => Log.Add("delete " + viewId);

        public void Reorder(string viewId, string targetId) => Log.Add($"reorder {viewId} {targetId}");

        public void Show(string viewId) => Log.Add("show " + viewId);

        public void Confirm(SettingsConfirm confirm) => Asked.Add(confirm);

        public IReadOnlyList<DesktopItem> DesktopItems { get; set; } = [];

        public void SetDesktopIcons(string viewId, ViewDesktopIcons value) => Log.Add($"icons {viewId} {value}");

        public void ShowDesktopItem(string viewId, string key, bool shown) => Log.Add($"{(shown ? "show" : "hide")} {key} on {viewId}");
    }

    private static readonly string Work = ViewsMenu.Keys.View("work");

    private static SettingsSubmenu Card(FakeHost host, string id)
        => Assert.IsType<SettingsSubmenu>(ViewsMenu.Root(host).Single(l => l.Key == ViewsMenu.Keys.View(id)));

    private static T In<T>(FakeHost host, string id, string key)
        where T : SettingsLine
        => Assert.IsType<T>(Card(host, id).Lines.Single(l => l.Key == key));

    private static List<SettingsSubmenu> Cards(FakeHost host) => [.. ViewsMenu.Root(host).OfType<SettingsSubmenu>()];

    [Fact]
    public void S2313_the_page_is_your_views_each_a_card_then_add_a_view()
    {
        var lines = ViewsMenu.Root(new FakeHost());

        Assert.Equal(
            [ViewsMenu.Keys.Heading, ViewsMenu.Keys.View("home"), Work, ViewsMenu.Keys.View("evening"), "-", ViewsMenu.Keys.Add],
            lines.Select(l => l is SettingsSeparator ? "-" : l.Key));
        Assert.Equal(Str.SettingsPages_Views_Yours.Text(), Assert.IsType<SettingsHeading>(lines[0]).Text);
        Assert.All(Cards(new FakeHost()), c => Assert.Equal(SettingsRowKind.Expander, SettingsRows.KindOf(c)));
        Assert.All(Cards(new FakeHost()), c => Assert.True(c.Card));

        var page = SettingsPages.Views(new FakeHost());
        Assert.Equal([ViewsMenu.Keys.Heading], page.Sections.Select(s => s.Key));
    }

    [Fact]
    public void S2313_no_line_acts_on_the_selected_view_every_line_is_one_views()
    {
        var host = new FakeHost();
        var loose = ViewsMenu.Root(host).Where(l => l is not SettingsSubmenu and not SettingsHeading and not SettingsSeparator).ToList();

        // Only the truly global line stands outside a card.
        Assert.Equal([ViewsMenu.Keys.Add], loose.Select(l => l.Key));
        foreach (var card in Cards(host))
        {
            Assert.All(card.Lines.Where(l => l is not SettingsSeparator), l => Assert.StartsWith(card.Key + "/", l.Key, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void S2313_a_views_head_is_its_mark_its_name_where_it_is_shown_and_its_widgets()
    {
        var host = new FakeHost();
        var work = Card(host, "work");

        Assert.Equal(("Work", Glyphs.Mail, "#0099BC"), (work.Text, work.Glyph, work.GlyphColour));
        Assert.Equal(Str.SettingsPages_Views_ShownOn.Format(("displays", Str.BarMenus_Display_This.Text().ToLowerInvariant())) + " · " + Str.Shell_Views_WidgetCount.Plural(1), work.Caption);
        Assert.Equal(Str.SettingsPages_Views_ShownOn.Format(("displays", "LG HDR WQHD")) + " · " + Str.Shell_Views_WidgetCount.Plural(0), Card(host, "home").Caption);
        Assert.StartsWith(Str.SettingsPages_Views_NotShown.Text(), Card(host, "evening").Caption, StringComparison.Ordinal);

        // A display Themia is off on shows nothing.
        host.State.DisabledMonitors.Add("other");
        Assert.StartsWith(Str.SettingsPages_Views_NotShown.Text(), Card(host, "home").Caption, StringComparison.Ordinal);
    }

    [Fact]
    public void S2313_opened_a_view_holds_name_icon_colour_and_show_on_this_display()
    {
        var keys = Card(new FakeHost(), "work").Lines.Select(l => l is SettingsSeparator ? "-" : l.Key);

        Assert.Equal([ViewsMenu.Keys.Name("work"), ViewsMenu.Keys.Icon("work"), ViewsMenu.Keys.Colour("work"), "-", ViewsMenu.Keys.DesktopIcons("work"), "-", ViewsMenu.Keys.Show("work")], keys);
        Assert.DoesNotContain(Card(new FakeHost(), "work").Lines, l => l is SettingsSubmenu);
    }

    [Fact]
    public void S1707_name_holds_the_name_and_stores_it_trimmed()
    {
        var host = new FakeHost();
        var rename = In<SettingsAsk>(host, "work", ViewsMenu.Keys.Name("work"));

        Assert.Equal("Work", rename.Initial);
        rename.Done("  Office  ");

        Assert.Equal("Office", host.State.FindView("work")!.Name);
        Assert.Equal(1, host.Mutations);
    }

    [Fact]
    public void S903_the_icon_line_offers_the_initials_as_its_default_and_stores_a_glyph_or_none()
    {
        var host = new FakeHost();
        var icon = In<SettingsIcon>(host, "work", ViewsMenu.Keys.Icon("work"));

        Assert.Equal(Glyphs.Mail, icon.Current);
        Assert.Equal("WO", icon.DefaultText);

        icon.Set(Glyphs.Home);
        Assert.Equal(Glyphs.Home, host.State.FindView("work")!.Icon);

        In<SettingsIcon>(host, "work", ViewsMenu.Keys.Icon("work")).Set(null);
        Assert.Null(host.State.FindView("work")!.Icon);
        Assert.Equal(Glyphs.View, Card(host, "work").Glyph);
    }

    [Fact]
    public void S3810_the_colour_line_stores_rrggbb_and_its_default_clears_it()
    {
        var host = new FakeHost();
        var colour = In<SettingsColour>(host, "work", ViewsMenu.Keys.Colour("work"));

        Assert.Equal("#0099BC", colour.Current);
        colour.Set("#e74856");
        Assert.Equal("#E74856", host.State.FindView("work")!.Color);

        // [S-3812] The page follows: the view's card and its icon wear the new colour.
        Assert.Equal("#E74856", Card(host, "work").GlyphColour);
        Assert.Equal("#E74856", In<SettingsIcon>(host, "work", ViewsMenu.Keys.Icon("work")).GlyphColour);

        In<SettingsColour>(host, "work", ViewsMenu.Keys.Colour("work")).Set(string.Empty);
        Assert.Null(host.State.FindView("work")!.Color);
    }

    [Fact]
    public void S2313_show_on_this_display_asks_the_host_and_is_greyed_where_it_is_shown()
    {
        var host = new FakeHost();

        Assert.True(In<SettingsCommand>(host, "work", ViewsMenu.Keys.Show("work")).Disabled);
        var home = In<SettingsCommand>(host, "home", ViewsMenu.Keys.Show("home"));
        Assert.False(home.Disabled);
        home.Run();

        Assert.Equal(["show home"], host.Log);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S2313_duplicate_and_delete_are_the_heads_icon_buttons_delete_asked_first()
    {
        var host = new FakeHost();
        var tools = Card(host, "work").Tools;

        Assert.Equal([Glyphs.Duplicate, Glyphs.Delete], tools.Select(t => t.Glyph));
        Assert.Equal([SettingsToolKey.None, SettingsToolKey.Delete], tools.Select(t => t.Shortcut));

        tools[0].Run!();
        Assert.Equal(["duplicate work"], host.Log);

        tools[1].Run!();
        var asked = Assert.Single(host.Asked);
        Assert.True(asked.Danger);
        Assert.Contains("Work", asked.Question, StringComparison.Ordinal);
        Assert.Equal(["duplicate work"], host.Log);

        asked.Done();
        Assert.Equal(["duplicate work", "delete work"], host.Log);
    }

    [Fact]
    public void S12_the_last_view_cannot_be_deleted()
    {
        var host = new FakeHost();
        host.State.Views.RemoveAll(v => v.Id != "work");

        Assert.True(Card(host, "work").Tools[1].Disabled);
    }

    [Fact]
    public void S1065_add_a_view_asks_the_host()
    {
        var host = new FakeHost();

        Assert.IsType<SettingsCommand>(ViewsMenu.Root(host).Single(l => l.Key == ViewsMenu.Keys.Add)).Run();

        Assert.Equal(["add"], host.Log);
    }

    [Fact]
    public void S3732_every_views_card_is_one_ordered_list_and_a_drop_moves_by_id()
    {
        // Regression: each line carried a move of its own, so the engine saw three lists of one and
        // a dragged view had nowhere to go.
        var host = new FakeHost();
        var cards = Cards(host);

        Assert.All(cards, l => Assert.NotNull(l.Order));
        Assert.Equal([0, 1, 2], cards.Select(l => l.Order!.Index));
        Assert.All(cards, l => Assert.Equal(3, l.Order!.Count));
        Assert.Single(cards.Select(l => l.Order!.Move).Distinct());

        cards[0].Order!.Move(0, 2);
        cards[0].Order!.Move(1, 1);
        Assert.Equal(["reorder home evening"], host.Log);
    }

    [Fact]
    public void S3802_a_move_past_either_end_moves_nothing()
    {
        var host = new FakeHost();
        var order = Cards(host)[0].Order!;

        order.Move(0, -1);
        order.Move(2, 3);
        Assert.Empty(host.Log);
    }

    [Fact]
    public void S2313_keys_hold_the_ids_so_a_rename_or_a_language_switch_keeps_the_shape()
    {
        var host = new FakeHost();
        string[] Keys() => [.. ViewsMenu.Root(host).Select(l => l.Key), .. Cards(host).SelectMany(c => c.Lines.Select(l => l.Key))];
        var before = Keys();

        host.State.FindView("work")!.Name = "Office";
        try
        {
            Strings.SetLocale(Locale.De);
            Assert.Equal(before, Keys());
        }
        finally
        {
            Strings.SetLocale(Locale.En);
        }
    }

    [Fact]
    public void S1706_the_view_a_display_shows_is_its_assignment_else_the_active_one()
    {
        var host = new FakeHost();
        Assert.Equal("work", ViewsMenu.ShownOn(host.State, Here));

        host.State.MonitorViews.Remove(Here);
        Assert.Equal("home", ViewsMenu.ShownOn(host.State, Here));
    }

    [Fact]
    public void S5529_a_views_card_chooses_its_desktop_icons_all_chosen_or_none()
    {
        var host = new FakeHost();
        var choice = In<SettingsChoice>(host, "work", ViewsMenu.Keys.DesktopIcons("work"));

        Assert.Equal(Str.Desktop_ViewIcons_Label.Text(), choice.Text);
        Assert.Equal([Str.Desktop_ViewIcons_All.Text(), Str.Desktop_ViewIcons_Chosen.Text(), Str.Desktop_ViewIcons_None.Text()], choice.Options.Select(o => o.Text));
        Assert.Equal(Str.Desktop_ViewIcons_All.Text(), choice.Value);

        choice.Options[2].Pick();
        Assert.Equal(["icons work None"], host.Log);

        // While All (or None) there is no list.
        Assert.DoesNotContain(Card(host, "work").Lines, l => l.Key == ViewsMenu.Keys.ChosenIcons("work"));
    }

    [Fact]
    public void S5529_a_chosen_view_lists_the_desktops_items_to_tick_on_a_page_of_its_own()
    {
        const string Desk = @"C:\Users\a\Desktop\";
        var host = new FakeHost
        {
            DesktopItems =
            [
                new(Desk + "z.txt", "z.txt", DesktopItemKind.File),
                new(Desk + "Projects", "Projects", DesktopItemKind.Folder),
                new("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "This PC", DesktopItemKind.ShellItem),
            ],
        };
        var work = host.State.FindView("work")!;
        work.DesktopIcons = ViewDesktopIcons.Chosen;
        work.Widgets.Add(new WidgetInstance { Id = "z", Config = new DeskIconFake { Item = Desk + "z.txt" }, Rect = new FracRect(0, 0, 0.1, 0.1) });

        var list = In<SettingsSubmenu>(host, "work", ViewsMenu.Keys.ChosenIcons("work"));

        Assert.True(list.Page);
        Assert.Equal(Str.Desktop_ViewIcons_Count.Format(("shown", "1"), ("all", "3")), list.Value);
        var ticks = list.Lines.Cast<SettingsToggle>().ToList();
        Assert.Equal(["This PC", "Projects", "z.txt"], ticks.Select(t => t.Text));
        Assert.Equal([false, false, true], ticks.Select(t => t.On));
        Assert.Equal(ViewsMenu.Keys.ChosenIcon("work", Desk + "Projects"), ticks[1].Key);

        ticks[1].Set(true);
        ticks[2].Set(false);
        Assert.Equal([$"show {Desk}Projects on work", $"hide {Desk}z.txt on work"], host.Log);

        // Nothing on the desktop: one note says so.
        host.DesktopItems = [];
        Assert.IsType<SettingsNote>(Assert.Single(In<SettingsSubmenu>(host, "work", ViewsMenu.Keys.ChosenIcons("work")).Lines));
    }
}
