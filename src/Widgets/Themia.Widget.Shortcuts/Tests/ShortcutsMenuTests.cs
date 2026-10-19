using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Shortcuts.Tests;

/// <summary>[S-1388] Shortcuts' options on its properties menu ([S-3719]): what each look offers, and what a line does.</summary>
public sealed class ShortcutsMenuTests
{
    private sealed class Layouts(bool canEdit) : IAppLayouts
    {
        public List<string> Edited { get; } = [];

        public bool CanEdit => canEdit;

        public void Edit(string itemId) => Edited.Add(itemId);

        public void Launch(LayoutNode? layout, nint anchor)
        {
        }
    }

    private sealed class Built
    {
        public int Saves { get; set; }

        public WidgetTextAsk? Asked { get; set; }

        public WidgetMenuLines Lines { get; set; } = null!;
    }

    private static Built Build(ShortcutsConfig config, IAppLayouts? layouts = null)
    {
        var built = new Built();
        built.Lines = new WidgetMenuLines(() => built.Saves++, ask => built.Asked = ask);
        ShortcutsMenu.Lines(built.Lines, config, layouts ?? new Layouts(canEdit: true));
        return built;
    }

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text)
        => Assert.Single(lines, l => l.Text == text);

    private static ItemMenuEntry Add(Built built) => Line(built.Lines.Lines, Str.Common_Add.Text());

    [Fact]
    public void S1388_tiles_offer_their_size_icons_only_and_stretch_under_style()
    {
        var look = Build(new ShortcutsConfig { Style = ShortcutsStyle.Grid }).Lines.StyleLines.Select(l => l.Text).ToList();

        Assert.Equal(
            [
                ShortcutsStr.WidgetsC_Shortcuts_Settings_TileSize_Label.Text(),
                ShortcutsStr.CtxMenu_IconOnly.Text(),
                ShortcutsStr.WidgetsC_Shortcuts_Settings_Stretch_Label.Text(),
                Str.Folder_ShowExtMenu.Text(),
            ],
            look);
    }

    [Theory]
    [InlineData(ShortcutsStyle.List)]
    [InlineData(ShortcutsStyle.Details)]
    public void S1388_a_list_and_details_offer_the_row_size_and_no_tile_options(ShortcutsStyle style)
    {
        var look = Build(new ShortcutsConfig { Style = style }).Lines.StyleLines.Select(l => l.Text).ToList();

        Assert.Contains(ShortcutsStr.WidgetsC_Shortcuts_Settings_RowSize_Label.Text(), look);
        Assert.DoesNotContain(ShortcutsStr.WidgetsC_Shortcuts_Settings_TileSize_Label.Text(), look);
        Assert.DoesNotContain(ShortcutsStr.CtxMenu_IconOnly.Text(), look);
    }

    [Fact]
    public void S1388_the_first_level_is_appearance_then_open_with_add_and_the_shortcuts()
    {
        var lines = Build(new ShortcutsConfig()).Lines;

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [ShortcutsStr.WidgetsC_Shortcuts_Settings_OpenWith_Label.Text(), Str.Common_Add.Text(), ShortcutsStr.WidgetsA_Catalog_Shortcuts_Label.Text()],
            lines.Lines.Select(l => l.Text));
    }

    private static ShortcutsConfig Three() => new()
    {
        Items =
        [
            new ShortcutItem { Id = "a", Target = "https://a.example/" },
            new ShortcutItem { Id = "b", Target = "https://b.example/", Label = "Bee" },
            new ShortcutItem { Id = "c", Target = "https://c.example/" },
        ],
    };

    private static IReadOnlyList<ItemMenuEntry> ItemLines(Built built)
        => Line(built.Lines.Lines, ShortcutsStr.WidgetsA_Catalog_Shortcuts_Label.Text()).Children!;

    [Fact]
    public void S1388_the_shortcuts_are_listed_in_their_order_by_the_names_they_show()
        => Assert.Equal(["a.example", "Bee", "c.example"], ItemLines(Build(Three())).Select(l => l.Text));

    [Fact]
    public void S1388_with_no_shortcut_the_list_says_so_and_offers_nothing()
    {
        var only = Assert.Single(ItemLines(Build(new ShortcutsConfig())));
        Assert.True(only.Disabled);
    }

    [Fact]
    public void S1388_an_item_moves_up_and_down_and_the_ends_are_greyed()
    {
        var config = Three();
        var built = Build(config);
        var first = ItemLines(built)[0].Children!;
        var last = ItemLines(built)[2].Children!;

        Assert.True(Line(first, ShortcutsStr.WidgetsC_Shortcuts_Menu_MoveUp.Text()).Disabled);
        Assert.True(Line(last, ShortcutsStr.WidgetsC_Shortcuts_Menu_MoveDown.Text()).Disabled);

        Line(first, ShortcutsStr.WidgetsC_Shortcuts_Menu_MoveDown.Text()).Action!();
        Assert.Equal(["b", "a", "c"], config.Items.Select(i => i.Id));
        Assert.Equal(1, built.Saves);
    }

    [Fact]
    public void S1388_remove_takes_the_item_away_and_saves()
    {
        var config = Three();
        var built = Build(config);

        Line(ItemLines(built)[1].Children!, Str.Common_Remove.Text()).Action!();

        Assert.Equal(["a", "c"], config.Items.Select(i => i.Id));
        Assert.Equal(1, built.Saves);
    }

    [Fact]
    public void S1388_rename_starts_from_the_name_shown_and_the_derived_name_clears_the_label()
    {
        var config = Three();
        var built = Build(config);

        Line(ItemLines(built)[1].Children!, ShortcutsStr.WidgetsC_Shortcuts_Menu_Rename.Text()).Action!();
        var ask = Assert.IsType<WidgetTextAsk>(built.Asked);
        Assert.Equal("Bee", ask.Initial);
        Assert.True(ask.AllowEmpty);

        ask.Done("b.example");
        Assert.Equal(string.Empty, config.Items[1].Label);

        ask.Done("Honey");
        Assert.Equal("Honey", config.Items[1].Label);
    }

    [Fact]
    public void S1388_change_target_is_checked_and_keeps_the_label()
    {
        var config = Three();
        var built = Build(config);

        Line(ItemLines(built)[1].Children!, ShortcutsStr.WidgetsC_Shortcuts_Menu_ChangeTarget.Text()).Action!();
        var ask = Assert.IsType<WidgetTextAsk>(built.Asked);
        Assert.Equal("https://b.example/", ask.Initial);
        Assert.NotNull(ask.Refuse("not a target"));

        ask.Done("https://bee.example/");
        Assert.Equal("https://bee.example/", config.Items[1].Target);
        Assert.Equal("Bee", config.Items[1].Label);
    }

    [Fact]
    public void S1388_a_switch_shows_its_state_and_a_click_turns_it_and_saves_once()
    {
        var config = new ShortcutsConfig { Style = ShortcutsStyle.Grid };
        var built = Build(config);
        var iconsOnly = Line(built.Lines.StyleLines, ShortcutsStr.CtxMenu_IconOnly.Text());

        Assert.False(iconsOnly.Checked);
        iconsOnly.Action!();

        Assert.True(config.GridIconOnly);
        Assert.Equal(1, built.Saves);
        Assert.True(Line(Build(config).Lines.StyleLines, ShortcutsStr.CtxMenu_IconOnly.Text()).Checked);
    }

    [Fact]
    public void S1388_open_with_is_a_choice_of_one_and_picking_the_current_stores_nothing()
    {
        var config = new ShortcutsConfig { ClickMode = ItemClickMode.Double };
        var built = Build(config);
        var options = Line(built.Lines.Lines, ShortcutsStr.WidgetsC_Shortcuts_Settings_OpenWith_Label.Text()).Children!;

        Assert.All(options, o => Assert.NotNull(o.Group));
        Assert.Equal([true, false], options.Select(o => o.Checked == true));

        options[0].Action!();
        Assert.Equal(0, built.Saves);

        options[1].Action!();
        Assert.Equal(ItemClickMode.Single, config.ClickMode);
        Assert.Equal(1, built.Saves);
    }

    [Fact]
    public void S1388_a_location_switch_adds_its_shortcut_and_takes_every_one_pointing_there_away()
    {
        var config = new ShortcutsConfig();
        var name = LaunchTargets.WellKnownShellNames[0];
        var label = LaunchTargets.ShellDisplayName(LaunchTargets.ShellTarget(name));

        ItemMenuEntry Location(Built built)
            => Line(Line(Add(built).Children!, ShortcutsStr.WidgetsC_Shortcuts_Settings_Locations.Text()).Children!, label);

        var first = Build(config);
        Assert.False(Location(first).Checked);
        Location(first).Action!();
        Assert.True(ShortcutRules.HasLocation(config.Items, name));
        Assert.Equal(1, first.Saves);

        var second = Build(config);
        Assert.True(Location(second).Checked);
        Location(second).Action!();
        Assert.False(ShortcutRules.HasLocation(config.Items, name));
    }

    [Fact]
    public void S1388_a_typed_address_is_refused_until_it_is_one_and_then_added()
    {
        var config = new ShortcutsConfig();
        var built = Build(config);

        Line(Add(built).Children!, ShortcutsStr.WidgetsC_Shortcuts_Menu_AddLink.Text()).Action!();
        var ask = Assert.IsType<WidgetTextAsk>(built.Asked);

        Assert.NotNull(ask.Refuse("example"));
        Assert.NotNull(ask.Refuse("relative\\path"));
        Assert.Null(ask.Refuse("https://example.com/"));

        ask.Done("https://example.com/");
        Assert.Equal("https://example.com/", Assert.Single(config.Items).Target);
        Assert.Equal(1, built.Saves);
    }

    [Fact]
    public void S1388_a_new_app_layout_is_added_and_opens_in_its_editor()
    {
        var config = new ShortcutsConfig();
        var layouts = new Layouts(canEdit: true);
        var built = Build(config, layouts);

        Line(Add(built).Children!, ShortcutsStr.WidgetsC_Shortcuts_Menu_AddLayout.Text()).Action!();

        var item = Assert.Single(config.Items);
        Assert.True(item.IsLayout);
        Assert.Equal([item.Id], layouts.Edited);
    }

    [Fact]
    public void S1388_without_an_editor_the_app_layout_line_is_greyed()
        => Assert.True(Line(Add(Build(new ShortcutsConfig(), new Layouts(canEdit: false))).Children!, ShortcutsStr.WidgetsC_Shortcuts_Menu_AddLayout.Text()).Disabled);
}
