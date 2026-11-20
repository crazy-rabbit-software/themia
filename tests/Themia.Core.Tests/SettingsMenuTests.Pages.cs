using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2300]..[S-2306] The edit bar's pages: the settings' lines grouped, every setting at one home.</summary>
public sealed partial class SettingsMenuTests
{
    [Fact]
    public void S2300_the_bar_has_six_pages_in_order_each_with_its_glyph()
    {
        // [S-13700] Unplaced icons has its own entry, after Views (owner, 2026-11-19: "own entry in the topbar").
        Assert.Equal([SettingsPageId.General, SettingsPageId.Accounts, SettingsPageId.Display, SettingsPageId.Views, SettingsPageId.DesktopItems, SettingsPageId.Appearance], SettingsPages.Order);
        Assert.Equal(Glyphs.Settings, SettingsPages.Glyph(SettingsPageId.General));
        Assert.Equal(6, SettingsPages.Order.Select(SettingsPages.Glyph).Distinct().Count());
        Assert.Equal(6, SettingsPages.Order.Select(SettingsPages.Name).Distinct().Count());
    }

    [Fact]
    public void S13700_unplaced_icons_is_one_live_part_that_sets_nothing()
    {
        var page = SettingsPages.DesktopItems();

        Assert.Equal(SettingsPageId.DesktopItems, page.Id);
        var line = Assert.IsType<SettingsPreview>(Assert.Single(Assert.Single(page.Sections).Lines));
        Assert.Equal(SettingsPages.UnplacedKey, line.Key);
        Assert.Equal(Str.Unplaced_Page_Name.Text(), line.Name);
        Assert.Equal(SettingsRowKind.Preview, SettingsRows.KindOf(line));
        Assert.Null(SettingsPages.Appearance(new FakeHost()).Sections[0].Lines.OfType<SettingsPreview>().Single().Name);
    }

    [Fact]
    public void S2315_general_is_karpos_sections_general_desktop_services_themia_and_about()
    {
        var page = SettingsPages.General(new FakeHost());

        Assert.Equal(["page", SettingsMenu.Keys.DesktopHeading, SettingsMenu.Keys.ServicesHeading, SettingsMenu.Keys.ThemiaHeading, SettingsMenu.Keys.About], page.Sections.Select(s => s.Key));
        Assert.Equal(Str.Common_General.Text(), page.Sections[0].Heading);

        // Karpo's General: Start with Windows, the language and its formats, then Themia's own switches; no Quit.
        Assert.Equal(["startup", SettingsMenu.Keys.Language + "/display", SettingsMenu.Keys.Formats, "gameMode"], page.Sections[0].Lines.Select(l => l.Key));
        Assert.Equal(["desktopIconSize", "desktopIconLayout", "lockWidgets"], page.Sections[1].Lines.Select(l => l.Key));
        Assert.Equal([SettingsMenu.Keys.Online], page.Sections[2].Lines.Select(l => l.Key));
        Assert.Equal([SettingsMenu.Keys.Pro, SettingsMenu.Keys.Backup, SettingsMenu.Keys.Uninstall], page.Sections[3].Lines.Select(l => l.Key));

        // Karpo's levels are cards that open in place, one level, never a page of their own.
        foreach (var card in page.Sections.Take(4).SelectMany(s => s.Lines).OfType<SettingsSubmenu>())
        {
            Assert.True(card.Card, card.Key);
            Assert.Equal(SettingsRowKind.Expander, SettingsRows.KindOf(card));
        }

        Assert.DoesNotContain(SettingsPages.Lines(page), l => l.Key.StartsWith(SettingsMenu.Keys.Motion, StringComparison.Ordinal));
    }

    [Fact]
    public void S2316_appearance_is_the_preview_bare_then_the_style_its_groups_and_motion()
    {
        var page = SettingsPages.Appearance(new FakeHost());

        Assert.Equal(["page", "appearance/theme", "appearance/items", "appearance/cards", "appearance/material", "appearance/edge", "appearance/text", "appearance/surfaces", "appearance/layout", SettingsMenu.Keys.Motion], page.Sections.Select(s => s.Key));
        Assert.Null(page.Sections[0].Heading);
        Assert.IsType<SettingsPreview>(page.Sections[0].Lines[0]);
        Assert.Equal(SettingsRowKind.Preview, SettingsRows.KindOf(page.Sections[0].Lines[0]));
        Assert.Equal(SettingsMenu.Keys.Appearance + "/style", page.Sections[0].Lines[1].Key);

        // [S-2311] The preview is a card of its own and sets nothing.
        Assert.Equal([[SettingsMenu.Keys.Preview]], SettingsRows.Cards(page.Sections[0].Lines).Take(1).Select(c => c.Select(l => l.Key).ToArray()));
    }

    [Fact]
    public void S2312_accounts_is_your_accounts_each_a_card_then_add_an_account_nothing_a_page_of_its_own()
    {
        var page = SettingsPages.Accounts(new FakeHost());
        var lines = page.Sections.SelectMany(s => s.Lines).ToList();

        Assert.Equal([SettingsMenu.Keys.YourAccounts, SettingsMenu.Keys.AddAccount], page.Sections.Select(s => s.Key));
        Assert.Equal([Str.SettingsPages_Accounts_Yours.Text(), Str.SettingsPages_Accounts_Add.Text()], page.Sections.Select(s => s.Heading));
        Assert.All(lines.OfType<SettingsSubmenu>(), s => Assert.Equal(SettingsRowKind.Expander, SettingsRows.KindOf(s)));
        Assert.All(lines.OfType<SettingsSubmenu>(), s => Assert.True(s.Card, s.Key));
    }

    [Fact]
    public void S2300_display_and_views_are_their_cards_under_their_headings()
    {
        var world = new World();
        var display = SettingsPages.Display(world.Display);
        var views = SettingsPages.Views(world.Views);

        Assert.Equal([DisplayMenu.Keys.Heading, DisplayMenu.Keys.All], display.Sections.Select(s => s.Key));
        Assert.Equal([ViewsMenu.Keys.Heading], views.Sections.Select(s => s.Key));
        Assert.All(views.Sections[0].Lines.OfType<SettingsSubmenu>(), i => Assert.NotNull(i.Order));
    }

    [Fact]
    public void S2310_a_thing_is_a_card_that_opens_on_its_section_never_a_section_of_its_own()
    {
        SettingsLine[] lines =
        [
            new SettingsHeading("h", "H"),
            new SettingsSubmenu("t1", "T1", null, [new SettingsNote("t1/x", "x")]) { Card = true, Caption = "under" },
            new SettingsSubmenu("t2", "T2", null, [new SettingsNote("t2/x", "x")]) { Card = true },
            new SettingsSubmenu("s", "S", null, [new SettingsNote("s/x", "x")]),
        ];

        var sections = SettingsPages.Sectioned(lines, null);

        Assert.Equal(["h", "s"], sections.Select(s => s.Key));
        Assert.Equal(["t1", "t2"], sections[0].Lines.Select(l => l.Key));
        Assert.Equal([["t1"], ["t2"]], SettingsRows.Cards(sections[0].Lines).Select(c => c.Select(l => l.Key).ToArray()));
    }

    [Fact]
    public void S2301_a_heading_or_a_submenu_starts_a_section_and_no_section_is_empty()
    {
        SettingsLine[] lines =
        [
            new SettingsNote("a", "a"),
            new SettingsSeparator("-"),
            new SettingsSubmenu("b", "B", null, [new SettingsNote("b/x", "x")]),
            new SettingsSeparator("-b"),
            new SettingsHeading("c", "C"),
            new SettingsNote("c/x", "x"),
            new SettingsSubmenu("d", "D", null, [new SettingsNote("d/x", "x")]) { Page = true },
        ];

        var sections = SettingsPages.Sectioned(lines, "First");

        Assert.Equal(["page", "b", "c"], sections.Select(s => s.Key));
        Assert.Equal(["First", "B", "C"], sections.Select(s => s.Heading));
        Assert.Equal(["c/x", "d"], sections[2].Lines.Select(l => l.Key));
    }

    [Fact]
    public void S2302_every_line_kind_is_a_row_kind()
    {
        static void Nothing()
        {
        }

        Assert.Equal(SettingsRowKind.Toggle, SettingsRows.KindOf(new SettingsToggle("t", "T", null, true, _ => { })));
        Assert.Equal(SettingsRowKind.Number, SettingsRows.KindOf(new SettingsSlider("n", "N", null, 1, 0, 2, 1, v => v.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => { })));
        Assert.Equal(SettingsRowKind.Text, SettingsRows.KindOf(new SettingsField("f", "F", null, string.Empty, _ => { })));
        Assert.Equal(SettingsRowKind.Text, SettingsRows.KindOf(new SettingsAsk("a", "A", null, string.Empty, string.Empty, _ => null, _ => { })));
        Assert.Equal(SettingsRowKind.Action, SettingsRows.KindOf(new SettingsCommand("c", "C", null, Nothing)));
        Assert.Equal(SettingsRowKind.Ask, SettingsRows.KindOf(SettingsCommand.Asking("q", "Q", null, new SettingsConfirm("Sure?", "Do", Nothing))));
        Assert.Equal(SettingsRowKind.Item, SettingsRows.KindOf(new SettingsItem("i", "I", null, [])));
        Assert.Equal(SettingsRowKind.Note, SettingsRows.KindOf(new SettingsNote("n", "N")));
        Assert.Equal(SettingsRowKind.Note, SettingsRows.KindOf(new SettingsReading("r", "R", null, "1")));
        Assert.Equal(SettingsRowKind.Choice, SettingsRows.KindOf(new SettingsChoice("ch", "Ch", null, [])));
        Assert.Equal(SettingsRowKind.Lookup, SettingsRows.KindOf(new SettingsChoice("lk", "Lk", null, []) { Filter = "Find" }));
        Assert.Equal(SettingsRowKind.Colour, SettingsRows.KindOf(new SettingsColour("co", "Co", string.Empty, _ => { }, "Default", [])));
        Assert.Equal(SettingsRowKind.Font, SettingsRows.KindOf(new SettingsFont("fo", "Fo", null, string.Empty, _ => { })));
        Assert.Equal(SettingsRowKind.Break, SettingsRows.KindOf(new SettingsSeparator("-")));
    }

    [Fact]
    public void S2303_a_card_opens_one_level_inside_it_and_never_a_third()
    {
        var flat = new SettingsSubmenu("e", "E", null, [new SettingsNote("e/x", "x")]);
        var deep = new SettingsSubmenu("p", "P", null, [flat]);
        var marked = flat with { Page = true };

        Assert.Equal(SettingsRowKind.Expander, SettingsRows.KindOf(flat));
        Assert.Equal(SettingsRowKind.SubPage, SettingsRows.KindOf(deep));
        Assert.Equal(SettingsRowKind.SubPage, SettingsRows.KindOf(marked));
    }

    [Fact]
    public void S2304_each_row_is_a_card_with_its_notes_a_choices_options_and_a_lists_things_share_one()
    {
        static void Nothing()
        {
        }

        SettingsLine[] lines =
        [
            new SettingsSeparator("-0"),
            new SettingsToggle("a", "a", null, true, _ => { }),
            new SettingsNote("a/why", "why"),
            new SettingsCommand("b", "b", null, Nothing),
            new SettingsRadio("r1", "1", true, Nothing),
            new SettingsRadio("r2", "2", false, Nothing),
            new SettingsItem("i1", "1", null, []),
            new SettingsItem("i2", "2", null, []),
            new SettingsSeparator("-1"),
            new SettingsHeading("h", "H"),
            new SettingsCommand("c", "c", null, Nothing),
            new SettingsCommand("d", "d", null, Nothing),
            new SettingsSeparator("-2"),
        ];

        var cards = SettingsRows.Cards(lines);

        Assert.Equal([["a", "a/why"], ["b"], ["r1", "r2"], ["i1", "i2"], ["h", "c", "d"]], cards.Select(c => c.Select(l => l.Key).ToArray()));
    }

    [Fact]
    public void S2306_every_setting_of_the_menus_has_exactly_one_page()
    {
        var world = new World();
        SettingsPage[] pages =
        [
            SettingsPages.General(world.Settings), SettingsPages.Accounts(world.Settings), SettingsPages.Display(world.Display),
            SettingsPages.Views(world.Views), SettingsPages.Appearance(world.Settings),
        ];

        // Every section builder's lines, and Display ▾'s and View ▾'s: each on one page, and once on it.
        IEnumerable<SettingsLine> Builders()
        {
            var host = world.Settings;
            SettingsLine[] roots =
            [
                .. SettingsMenu.General(host), .. SettingsMenu.Desktop(host), SettingsMenu.Appearance(host), SettingsMenu.Accounts(host), SettingsMenu.Language(host),
                SettingsMenu.OnlineUpdates(host), SettingsMenu.Motion(host), SettingsMenu.Backup(host), SettingsMenu.Pro(host), SettingsMenu.About(host),
            ];
            return roots.SelectMany(r => r is SettingsSubmenu s && !s.Page ? s.Lines : [r]).SelectMany(l => l is SettingsSubmenu { Card: true } c ? c.Lines : [l]);
        }

        var settingKinds = new[] { typeof(SettingsToggle), typeof(SettingsChoice), typeof(SettingsSlider), typeof(SettingsColour), typeof(SettingsFill), typeof(SettingsFont), typeof(SettingsAsk), typeof(SettingsIcon), typeof(SettingsItem) };
        static IEnumerable<SettingsLine> Opened(IEnumerable<SettingsLine> lines) => lines.SelectMany(l => l is SettingsSubmenu { Card: true } c ? c.Lines : [l]);
        var everyKey = Builders().Concat(Opened(DisplayMenu.Root(world.Display))).Concat(Opened(ViewsMenu.Root(world.Views)))
            .Where(l => settingKinds.Contains(l.GetType())).Select(l => l.Key).ToList();

        Assert.NotEmpty(everyKey);
        foreach (var key in everyKey)
        {
            var homes = pages.Where(p => SettingsPages.Lines(p).Count(l => l.Key == key && settingKinds.Contains(l.GetType())) > 0).ToList();
            Assert.True(homes.Count == 1, $"{key}: on {homes.Count} pages");
            Assert.Equal(1, SettingsPages.Lines(homes[0]).Count(l => l.Key == key));
        }
    }

    [Fact]
    public void S2306_a_deep_link_finds_its_row_by_its_key_through_its_section()
    {
        var page = SettingsPages.General(new FakeHost());
        var clock = SettingsMenu.Keys.TimeFormat;

        var path = SettingsPages.PathTo(page, clock);

        Assert.Equal(["page", SettingsMenu.Keys.Formats, clock], path);
        Assert.Equal(clock, SettingsPages.Find(page, path!)!.Key);
        Assert.Null(SettingsPages.PathTo(page, "nowhere"));
    }
}
