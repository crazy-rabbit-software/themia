using Themia.Core.Settings;
using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2303] [S-2307] [S-2345] [S-2351] [S-3534] Where a page stands, as the page's view draws it.</summary>
public sealed class PageNavigationTests
{
    private static NavRow Row(string key, SettingsRowKind kind, params NavRow[] rows) => new(key, kind, rows);

    /// <summary>
    /// A page of two tabs: General (a card "views" holding a choice "views/sort", a line that asks "views/name" and a sub-page
    /// "views/icons" with a choice of its own), and Look (a choice "look/theme", a line that asks "look/reset").
    /// </summary>
    private static NavPage Page() => new(
        [
            new NavSection("general",
            [
                Row("views", SettingsRowKind.Expander,
                    Row("views/sort", SettingsRowKind.Choice, Row("views/sort/name", SettingsRowKind.Radio)),
                    Row("views/name", SettingsRowKind.Ask),
                    Row("views/icons", SettingsRowKind.SubPage, Row("views/icons/size", SettingsRowKind.Choice))) with { Tools = ["views#delete"] },
                Row("startup", SettingsRowKind.Toggle),
            ]),
            new NavSection("look",
            [
                Row("look/theme", SettingsRowKind.Choice, Row("look/theme/dark", SettingsRowKind.Radio)),
                Row("look/reset", SettingsRowKind.Ask),
            ]),
        ],
        [new NavTab("first", ["general"]), new NavTab("second", ["look"])]);

    [Fact]
    public void S3534_a_way_in_opens_the_card_on_the_way_and_unfolds_the_picker_inside_it()
    {
        var nav = new PageNavigation();

        var target = nav.Enter(Page(), ["general", "views", "views/sort"]);

        Assert.Equal("views/sort", target);
        Assert.Equal("views", nav.Open);
        Assert.Equal(["views/sort"], nav.Unfolded);
        Assert.True(nav.IsOpen("views/sort", inside: true));
        Assert.Empty(nav.Steps);
    }

    [Fact]
    public void S3534_a_way_in_chooses_the_tab_its_section_stands_on_and_opens_a_picker_of_the_page_as_the_card()
    {
        var nav = new PageNavigation();

        Assert.Equal("look/theme", nav.Enter(Page(), ["look", "look/theme"]));
        Assert.Equal("second", nav.Tab);
        Assert.Equal("look/theme", nav.Open);
        Assert.Empty(nav.Unfolded);
    }

    [Fact]
    public void S3534_a_way_in_through_a_sub_page_steps_into_it_and_opens_its_picker_alone()
    {
        var nav = new PageNavigation();

        nav.Enter(Page(), ["general", "views", "views/icons", "views/icons/size"]);

        Assert.Equal(["views/icons"], nav.Steps);
        Assert.Equal("views/icons", nav.Deepest);
        Assert.Equal("views/icons/size", nav.Open);
        Assert.Empty(nav.Unfolded);
    }

    [Fact]
    public void S2351_a_way_in_to_a_line_that_asks_asks_it_in_its_card()
    {
        var nav = new PageNavigation();

        nav.Enter(Page(), ["general", "views", "views/name"]);

        Assert.Equal("views", nav.Open);
        Assert.True(nav.Asks.IsOpen("views/name"));
    }

    [Fact]
    public void S3534_a_way_in_starts_from_nothing_and_stops_where_the_path_leaves_the_page()
    {
        var nav = new PageNavigation();
        nav.Enter(Page(), ["general", "views", "views/icons"]);
        nav.Asks.Show("look/reset");

        Assert.Equal("views", nav.Enter(Page(), ["general", "views", "gone", "views/sort"]));
        Assert.Empty(nav.Steps);
        Assert.Equal("views", nav.Open);
        Assert.Null(nav.Asks.Open);
        Assert.Equal("general", nav.Enter(Page(), ["general"]));
        Assert.Null(nav.Enter(Page(), []));
    }

    [Fact]
    public void S2303_a_cards_head_opens_it_alone_and_a_picker_in_it_unfolds_without_closing_it()
    {
        var nav = new PageNavigation();
        nav.Asks.Show("look/reset");

        Assert.True(nav.ToggleCard("views", inside: false));
        Assert.Null(nav.Asks.Open);
        Assert.True(nav.ToggleCard("views/sort", inside: true));
        Assert.Equal("views", nav.Open);
        Assert.False(nav.ToggleCard("views/sort", inside: true));
        Assert.Empty(nav.Unfolded);

        nav.ToggleCard("views/sort", inside: true);
        Assert.True(nav.ToggleCard("look/theme", inside: false));
        Assert.Equal("look/theme", nav.Open);
        Assert.Empty(nav.Unfolded);
        Assert.False(nav.ToggleCard("look/theme", inside: false));
        Assert.Null(nav.Open);
    }

    [Fact]
    public void S2327_back_closes_the_sub_page_then_the_open_card_then_nothing()
    {
        var nav = new PageNavigation();
        nav.ToggleCard("views", inside: false);
        nav.Push("views/icons");

        Assert.Null(nav.Open);
        Assert.Equal(new NavBack(NavBackKind.SubPage, null), nav.Back());
        nav.ToggleCard("views", inside: false);
        nav.ToggleCard("views/sort", inside: true);
        Assert.Equal(new NavBack(NavBackKind.Card, "views"), nav.Back());
        Assert.Empty(nav.Unfolded);
        Assert.Equal(NavBackKind.None, nav.Back().Kind);
    }

    [Fact]
    public void S2345_a_tab_closes_the_card_and_what_is_asked_and_a_tab_gone_shows_the_first()
    {
        var nav = new PageNavigation();
        nav.ToggleCard("views", inside: false);
        nav.Asks.Show("views/name");

        nav.ChooseTab("second");
        Assert.Null(nav.Open);
        Assert.Null(nav.Asks.Open);
        Assert.Equal("second", nav.ShownTab(Page())!.Key);

        var oneTab = Page() with { Tabs = [new NavTab("only", ["general", "look"])] };
        Assert.Equal("only", nav.ShownTab(oneTab)!.Key);
        Assert.Equal("only", nav.Tab);
        Assert.Null(nav.ShownTab(Page() with { Tabs = [] }));
    }

    [Fact]
    public void S2361_a_page_built_again_without_the_sub_pages_row_or_the_asking_row_drops_them()
    {
        var nav = new PageNavigation();
        nav.Push("views/icons");
        nav.Asks.Show("views#delete");

        nav.Prune(Page());
        Assert.Equal(["views/icons"], nav.Steps);
        Assert.True(nav.Asks.IsOpen("views#delete"));

        var without = new NavPage([new NavSection("general", [Row("startup", SettingsRowKind.Toggle)])], []);
        Assert.True(nav.Prune(without));
        Assert.Empty(nav.Steps);
        Assert.Null(nav.Asks.Open);
    }

    [Fact]
    public void S2351_an_ask_opens_in_its_holders_card_and_closes_another_card_of_the_page()
    {
        var nav = new PageNavigation();
        nav.ToggleCard("look/theme", inside: false);

        Assert.True(nav.ToggleAsk("views#delete", "views", inside: false));
        Assert.Null(nav.Open);
        Assert.False(nav.ToggleAsk("views#delete", "views", inside: false));

        nav.ToggleCard("views", inside: false);
        nav.ToggleCard("views/sort", inside: true);
        Assert.True(nav.ToggleAsk("views/name", "views/name", inside: true));
        Assert.Equal("views", nav.Open);
        Assert.Equal(["views/sort"], nav.Unfolded);
    }

    [Fact]
    public void S3727_a_question_asked_after_a_step_opens_the_cards_it_stands_in()
    {
        var nav = new PageNavigation();
        nav.ToggleCard("views", inside: false);
        nav.ToggleCard("views/sort", inside: true);

        nav.AskAt(Page(), "views/name");
        Assert.Equal("views", nav.Open);
        Assert.Equal(["views/sort"], nav.Unfolded);
        Assert.True(nav.Asks.IsOpen("views/name"));

        nav.AskAt(Page(), "look/theme/dark");
        Assert.Equal("look/theme", nav.Open);
        Assert.Empty(nav.Unfolded);

        nav.AskAt(Page(), "startup");
        Assert.Null(nav.Open);
    }

    [Fact]
    public void S2328_a_click_reaching_a_picker_opens_its_card_and_unfolds_it_and_one_of_the_page_alone()
    {
        var nav = new PageNavigation();

        nav.Reach(Page(), "views/sort");
        Assert.Equal("views", nav.Open);
        Assert.Equal(["views/sort"], nav.Unfolded);

        nav.Reach(Page(), "look/theme");
        Assert.Equal("look/theme", nav.Open);
        Assert.Empty(nav.Unfolded);

        Assert.True(nav.OpenAlong(Page(), "views/name"));
        Assert.Equal("views", nav.Open);
        Assert.False(nav.OpenAlong(Page(), "startup"));
    }

    [Fact]
    public void S2302_a_pages_rows_are_found_with_their_path_their_holder_and_their_card()
    {
        var page = Page();

        Assert.Equal(["views", "views/icons", "views/icons/size"], page.PathTo("views/icons/size"));
        Assert.Empty(page.PathTo("nowhere"));
        Assert.Equal("views", page.HolderOf("views#delete")!.Key);
        Assert.Equal("views", page.CardOf("views/sort")!.Key);
        Assert.Null(page.CardOf("views"));
        Assert.Null(page.CardOf("look/theme"));
        Assert.Equal(SettingsRowKind.Radio, page.Find("look/theme/dark")!.Kind);
        Assert.True(PageNavigation.Opens(SettingsRowKind.Icon));
        Assert.False(PageNavigation.Opens(SettingsRowKind.Ask));
    }
}
