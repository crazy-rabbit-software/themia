using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.DesktopItem.Tests;

/// <summary>[S-9509] The shortcut's options on its properties page ([S-3719]): what each line does.</summary>
public sealed class OwnIconMenuTests
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

        public WidgetMenuLines Lines { get; set; } = null!;
    }

    private static Built Build(DesktopItemConfig config, IAppLayouts? layouts = null)
    {
        var built = new Built();
        built.Lines = new WidgetMenuLines(() => built.Saves++);
        OwnIconMenu.Lines(built.Lines, config, layouts ?? new Layouts(canEdit: true));
        return built;
    }

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text)
        => Assert.Single(lines, l => l.Text == text);

    private static ItemMenuEntry Target(Built built) => Line(built.Lines.Lines, DesktopItemStr.WidgetsC_Shortcuts_Column_Target.Text());

    [Fact]
    public void S9509_the_page_is_target_name_icon_then_name_lines_and_open_with()
    {
        var lines = Build(new DesktopItemConfig()).Lines;

        Assert.Equal(
            [
                DesktopItemStr.WidgetsC_Shortcuts_Column_Target.Text(), Str.Folder_ColName.Text(), DesktopItemStr.WidgetsC_Shortcuts_Icon_Label.Text(), "-",
                DesktopItemStr.DesktopItem_NameLines.Text(), DesktopItemStr.WidgetsC_Shortcuts_Settings_OpenWith_Label.Text(),
            ],
            lines.Lines.Select(l => l.IsSeparator ? "-" : l.Text));
        Assert.Empty(lines.StyleLines);
    }

    [Fact]
    public void S9509_set_up_opens_the_target_line_and_nothing_else()
    {
        // [S-1862] A shortcut that opens nothing yet: its Set up button opens its page at Target.
        var built = Build(new DesktopItemConfig());

        Assert.True(Target(built).SetsUp);
        Assert.Single(built.Lines.Lines.Concat(built.Lines.StyleLines), l => l.SetsUp);
    }

    [Fact]
    public void S9509_the_target_offers_a_file_a_folder_a_typed_address_the_windows_locations_and_an_app_layout()
        => Assert.Equal(
            [
                DesktopItemStr.WidgetsC_Shortcuts_Settings_Pick_File.Text(), DesktopItemStr.WidgetsC_Shortcuts_Settings_Pick_Folder.Text(),
                DesktopItemStr.WidgetsC_Shortcuts_Menu_AddLink.Text(), DesktopItemStr.WidgetsC_Shortcuts_Settings_Locations.Text(), "-",
                DesktopItemStr.WidgetsC_Shortcuts_Menu_AddLayout.Text(),
            ],
            Target(Build(new DesktopItemConfig())).Children!.Select(l => l.IsSeparator ? "-" : l.Text));

    [Fact]
    public void S9509_the_target_line_says_what_kind_of_thing_it_opens_now()
    {
        Assert.Null(Target(Build(new DesktopItemConfig())).Value);
        Assert.Equal("Web address", Target(Build(new DesktopItemConfig { Target = "https://example.com/" })).Value);
        Assert.Equal("Settings page", Target(Build(new DesktopItemConfig { Target = "ms-settings:display" })).Value);

        var layout = new DesktopItemConfig();
        layout.MakeLayout();
        Assert.Equal("App layout", Target(Build(layout)).Value);
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData(@"C:\Users\Public")]
    [InlineData("shell:Downloads")]
    [InlineData("ms-settings:display")]
    public void S9509_a_typed_target_is_refused_until_it_is_one_and_then_set(string typed)
    {
        var config = new DesktopItemConfig { Label = "Bee" };
        var built = Build(config);

        var line = Line(Target(built).Children!, DesktopItemStr.WidgetsC_Shortcuts_Menu_AddLink.Text());
        Assert.True(line.Asks);
        Assert.Null(line.Action);
        var ask = Assert.IsType<WidgetTextAsk>(line.Ask);

        Assert.NotNull(ask.Refuse("example"));
        Assert.NotNull(ask.Refuse("relative\\path"));
        Assert.NotNull(ask.Refuse("ms-windows-store://home"));
        Assert.Null(ask.Refuse(typed));

        ask.Done(typed);
        Assert.Equal((typed, "Bee"), (config.Target, config.Label));
        Assert.Equal(1, built.Saves);
    }

    [Fact]
    public void S9509_changing_the_target_starts_from_the_one_it_has_and_the_same_one_stores_nothing()
    {
        var config = new DesktopItemConfig { Target = "https://b.example/" };
        var built = Build(config);

        var ask = Assert.IsType<WidgetTextAsk>(Line(Target(built).Children!, DesktopItemStr.WidgetsC_Shortcuts_Menu_AddLink.Text()).Ask);
        Assert.Equal("https://b.example/", ask.Initial);

        ask.Done("https://b.example/");
        Assert.Equal(0, built.Saves);
    }

    [Fact]
    public void S9509_a_windows_location_is_a_choice_of_one_and_picking_the_current_stores_nothing()
    {
        var config = new DesktopItemConfig();
        var name = LaunchTargets.WellKnownShellNames[0];
        var label = LaunchTargets.ShellDisplayName(LaunchTargets.ShellTarget(name));

        IReadOnlyList<ItemMenuEntry> Locations(Built built)
            => Line(Target(built).Children!, DesktopItemStr.WidgetsC_Shortcuts_Settings_Locations.Text()).Children!;

        var first = Build(config);
        Assert.Equal(LaunchTargets.WellKnownShellNames.Count, Locations(first).Count);
        Assert.All(Locations(first), l => Assert.False(l.Checked));
        Assert.All(Locations(first), l => Assert.NotNull(l.Group));
        Line(Locations(first), label).Action!();
        Assert.Equal(LaunchTargets.ShellTarget(name), config.Target);
        Assert.Equal(1, first.Saves);

        var second = Build(config);
        Assert.True(Line(Locations(second), label).Checked);
        Line(Locations(second), label).Action!();
        Assert.Equal(0, second.Saves);
    }

    [Fact]
    public void S9509_app_layout_makes_it_a_layout_and_opens_the_editor_and_a_layout_offers_its_editor()
    {
        var config = new DesktopItemConfig { Target = "https://example.com/" };
        var layouts = new Layouts(canEdit: true);
        var built = Build(config, layouts);

        Line(Target(built).Children!, DesktopItemStr.WidgetsC_Shortcuts_Menu_AddLayout.Text()).Action!();

        Assert.True(config.IsLayout);
        Assert.Equal(string.Empty, config.Target);
        Assert.Equal([config.ItemId], layouts.Edited);
        Assert.Equal(1, built.Saves);

        // An existing layout is never made anew: its line opens the editor on it.
        var tree = config.Layout;
        var again = Build(config, layouts);
        Assert.DoesNotContain(Target(again).Children!, l => l.Text == DesktopItemStr.WidgetsC_Shortcuts_Menu_AddLayout.Text());
        Line(Target(again).Children!, Str.Shortcut_EditLayoutMenu.Text()).Action!();
        Assert.Same(tree, config.Layout);
        Assert.Equal([config.ItemId, config.ItemId], layouts.Edited);
        Assert.Equal(0, again.Saves);
    }

    [Fact]
    public void S9509_without_an_editor_the_app_layout_line_is_greyed()
        => Assert.True(Line(Target(Build(new DesktopItemConfig(), new Layouts(canEdit: false))).Children!, DesktopItemStr.WidgetsC_Shortcuts_Menu_AddLayout.Text()).Disabled);

    [Fact]
    public void S9507_the_name_line_starts_from_the_name_shown_and_the_derived_name_clears_the_label()
    {
        var config = new DesktopItemConfig { Target = "https://b.example/", Label = "Bee" };
        var built = Build(config);
        var line = Line(built.Lines.Lines, Str.Folder_ColName.Text());

        Assert.Equal("Bee", line.Value);
        Assert.True(line.Asks);
        Assert.Null(line.Action);
        var ask = Assert.IsType<WidgetTextAsk>(line.Ask);
        Assert.Equal("Bee", ask.Initial);
        Assert.Equal("b.example", ask.Placeholder);
        Assert.True(ask.AllowEmpty);

        ask.Done("b.example");
        Assert.Equal(string.Empty, config.Label);

        ask.Done("Honey");
        Assert.Equal("Honey", config.Label);
        Assert.Equal(2, built.Saves);
    }

    [Fact]
    public void S11204_name_lines_is_one_or_two()
    {
        var config = new DesktopItemConfig();
        var built = Build(config);
        var options = Line(built.Lines.Lines, DesktopItemStr.DesktopItem_NameLines.Text()).Children!;

        Assert.Equal([false, true], options.Select(o => o.Checked == true));
        options[0].Action!();
        Assert.Equal(1, config.LabelLines);
        Assert.Equal(1, built.Saves);
    }

    [Fact]
    public void S9505_open_with_is_a_choice_of_one_and_picking_the_current_stores_nothing()
    {
        var config = new DesktopItemConfig { ClickMode = ItemClickMode.Double };
        var built = Build(config);
        var options = Line(built.Lines.Lines, DesktopItemStr.WidgetsC_Shortcuts_Settings_OpenWith_Label.Text()).Children!;

        Assert.All(options, o => Assert.NotNull(o.Group));
        Assert.Equal([true, false], options.Select(o => o.Checked == true));

        options[0].Action!();
        Assert.Equal(0, built.Saves);

        options[1].Action!();
        Assert.Equal(ItemClickMode.Single, config.ClickMode);
        Assert.Equal(1, built.Saves);
    }
}
