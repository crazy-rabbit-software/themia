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
    public void S1388_the_first_level_is_open_with_then_add()
        => Assert.Equal(
            [ShortcutsStr.WidgetsC_Shortcuts_Settings_OpenWith_Label.Text(), Str.Common_Add.Text()],
            Build(new ShortcutsConfig()).Lines.Lines.Select(l => l.Text));

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
