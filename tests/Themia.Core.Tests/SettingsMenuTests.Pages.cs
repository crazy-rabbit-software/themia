using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2300]..[S-2306] The edit bar's pages: the settings' lines grouped, every setting at one home.</summary>
public sealed partial class SettingsMenuTests
{
    [Fact]
    public void S2300_the_bar_has_five_pages_in_order_each_with_its_glyph()
    {
        Assert.Equal([SettingsPageId.General, SettingsPageId.Accounts, SettingsPageId.Display, SettingsPageId.Views, SettingsPageId.Appearance], SettingsPages.Order);
        Assert.Equal(Glyphs.Settings, SettingsPages.Glyph(SettingsPageId.General));
        Assert.Equal(5, SettingsPages.Order.Select(SettingsPages.Glyph).Distinct().Count());
        Assert.Equal(5, SettingsPages.Order.Select(SettingsPages.Name).Distinct().Count());
    }

    [Fact]
    public void S2300_general_is_general_then_language_online_motion_backup_pro_about_and_quit_last()
    {
        var page = SettingsPages.General(new FakeHost());

        Assert.Equal(["page", "language", "online", "motion", "backup", "pro", "about", "-about"], page.Sections.Select(s => s.Key));
        Assert.Equal(Str.Common_General.Text(), page.Sections[0].Heading);
        Assert.Equal(["startup", "desktop", "gameMode", "snap"], page.Sections[0].Lines.Select(l => l.Key));
        Assert.Equal(SettingsMenu.Keys.Quit, Assert.Single(page.Sections[^1].Lines).Key);
    }

    [Fact]
    public void S2300_appearance_is_the_style_then_its_groups_each_a_section_and_motion_is_not_there()
    {
        var page = SettingsPages.Appearance(new FakeHost());

        Assert.Equal(["page", "appearance/theme", "appearance/items", "appearance/cards", "appearance/text"], page.Sections.Select(s => s.Key));
        Assert.Null(page.Sections[0].Heading);
        Assert.DoesNotContain(SettingsPages.Lines(page), l => l.Key.StartsWith("motion", StringComparison.Ordinal));
    }

    [Fact]
    public void S2300_accounts_lists_every_account_as_a_page_of_its_own_then_add_account()
    {
        var page = SettingsPages.Accounts(new FakeHost());
        var lines = page.Sections.SelectMany(s => s.Lines).ToList();

        var add = Assert.IsType<SettingsSubmenu>(lines.Single(l => l.Key == SettingsMenu.Keys.AddAccount));
        Assert.Equal(SettingsRowKind.SubPage, SettingsRows.KindOf(add));
        Assert.All(lines.OfType<SettingsSubmenu>(), s => Assert.Equal(SettingsRowKind.SubPage, SettingsRows.KindOf(s)));
    }

    [Fact]
    public void S2300_display_and_views_are_their_menus_lines_with_their_groups_as_sections()
    {
        var world = new World();
        var display = SettingsPages.Display(world.Display);
        var views = SettingsPages.Views(world.Views);

        Assert.Equal(["page", DisplayMenu.Keys.Views, DisplayMenu.Keys.ViewBar, DisplayMenu.Keys.All, DisplayMenu.Keys.ShowOn], display.Sections.Select(s => s.Key));
        Assert.Equal(["page", ViewsMenu.Keys.Manage], views.Sections.Select(s => s.Key));
        Assert.All(views.Sections[1].Lines.OfType<SettingsItem>(), i => Assert.NotNull(i.Order));
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
        Assert.Equal(SettingsRowKind.Text, SettingsRows.KindOf(new SettingsAsk("a", "A", null, "T", string.Empty, "OK", string.Empty, _ => null, _ => { })));
        Assert.Equal(SettingsRowKind.Action, SettingsRows.KindOf(new SettingsCommand("c", "C", null, Nothing)));
        Assert.Equal(SettingsRowKind.Link, SettingsRows.KindOf(new SettingsLink("l", "L", null, "Owner", Nothing)));
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
                .. SettingsMenu.General(host), SettingsMenu.Appearance(host), SettingsMenu.Accounts(host), SettingsMenu.Language(host),
                SettingsMenu.OnlineUpdates(host), SettingsMenu.Motion(host), SettingsMenu.Assistants(host)!, SettingsMenu.Backup(host), SettingsMenu.Pro(host), SettingsMenu.About(host),
            ];
            return roots.SelectMany(r => r is SettingsSubmenu s && !s.Page ? s.Lines : [r]);
        }

        var settingKinds = new[] { typeof(SettingsToggle), typeof(SettingsChoice), typeof(SettingsSlider), typeof(SettingsColour), typeof(SettingsFill), typeof(SettingsFont), typeof(SettingsAsk), typeof(SettingsIcon), typeof(SettingsItem) };
        var everyKey = Builders().Concat(DisplayMenu.Root(world.Display)).Concat(ViewsMenu.Root(world.Views))
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
        var clock = SettingsMenu.Keys.Format(Sdk.OwnedSetting.TimeFormat);

        var path = SettingsPages.PathTo(page, clock);

        Assert.Equal([SettingsMenu.Keys.Language, clock], path);
        Assert.Equal(clock, SettingsPages.Find(page, path!)!.Key);
        Assert.Null(SettingsPages.PathTo(page, "nowhere"));
        Assert.Equal(["page", SettingsMenu.Keys.Snap], SettingsPages.PathTo(page, SettingsMenu.Keys.Snap));
    }
}
