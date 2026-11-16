using System.Text.Json;
using Themia.Core.Desktop;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.DesktopItem;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>An own desktop icon's config under the root's tags (an app layout's menu, clone, repair and round trip, [S-1405], [S-1406]): the config is its module's, so they run here, over the state's own JSON.</summary>
public sealed class OwnIconHostTests
{
    private static LayoutNode SideBySide(string left, string right, double ratio = 0.5)
    {
        var root = LayoutNode.NewPane();
        (root, _) = LayoutTree.Split(root, root.Id, SplitDirection.Vertical);
        LayoutTree.SetRatio(root, root.Id, ratio);
        LayoutTree.Assign(root, root.A!.Id, new LayoutAssignment { Source = LayoutSource.App, Target = left });
        LayoutTree.Assign(root, root.B!.Id, new LayoutAssignment { Source = LayoutSource.Url, Target = right });
        return root;
    }

    [Fact]
    public void S1405_the_layout_menu_opens_the_layout_edits_it_and_renames()
    {
        var config = new DesktopItemConfig { Label = "Morning", Layout = SideBySide(@"C:\a.lnk", "https://b.example") };
        var icon = DeskIcon.Of(config, _ => false);
        var menu = DesktopCommands.Menu([icon], new DeskMenuFacts(CanEmptyBin: false, CanEditLayout: true, ClipboardHasFiles: false), _ => false);

        Assert.Equal(DeskIconKind.OwnLayout, icon.Kind);
        Assert.Equal(DesktopItemKeys.Own(config.ItemId), icon.Key);
        Assert.Equal(
            [
                new FileMenuLine(FileCommand.Rename),
                new FileMenuLine(FileCommand.Delete),
                new FileMenuLine(FileCommand.OpenLayout),
                new FileMenuLine(FileCommand.EditLayout),
                new FileMenuLine(FileCommand.IconProperties),
                FileMenuLine.Separator,
                new FileMenuLine(FileCommand.UnlockWidgets),
            ],
            menu.Lines);
        Assert.Empty(menu.Classic);

        // The editor is off while it cannot be opened; a layout with no assigned pane opens nothing.
        Assert.False(DesktopCommands.Menu([icon], new DeskMenuFacts(false, CanEditLayout: false, false), _ => false).Lines[3].Enabled);
        var empty = new DesktopItemConfig();
        empty.MakeLayout();
        var emptyMenu = DesktopCommands.Menu([DeskIcon.Of(empty, _ => false)], new DeskMenuFacts(false, CanEditLayout: true, false), _ => false);
        Assert.Equal(new FileMenuLine(FileCommand.OpenLayout, Enabled: false), emptyMenu.Lines[2]);
        Assert.Equal(new FileMenuLine(FileCommand.EditLayout), emptyMenu.Lines[3]);
    }

    [Fact]
    public void S5582_every_line_a_desktop_icon_menu_holds_has_its_icon_and_words_in_the_file_menus_builder()
    {
        const string Desk = @"C:\Users\a\Desktop\";
        var layout = new DesktopItemConfig { Label = "Morning", Layout = SideBySide(@"C:\a.lnk", "https://b.example") };
        DeskIcon[][] selections =
        [
            [DeskIcon.Of(new DesktopItemConfig { Item = Desk + "a.lnk" }, _ => false)],
            [DeskIcon.Of(new DesktopItemConfig { Item = Desk + "Docs" }, _ => false)],
            [DeskIcon.Of(new DesktopItemConfig { Item = DesktopCommands.RecycleBinItem }, _ => false)],
            [DeskIcon.Of(layout, _ => false)],
            [DeskIcon.Of(new DesktopItemConfig { Target = @"C:\Tools\app.exe" }, _ => true)],
            [DeskIcon.Of(new DesktopItemConfig { Target = "https://example.com/" }, _ => false)],
            [DeskIcon.Of(new DesktopItemConfig { Target = "shell:RecycleBinFolder" }, _ => false)],
        ];
        var ran = new List<FileCommand>();
        var target = new Themia.App.Widgets.FileMenuTarget(FileMenuPlace.Desktop, ran.Add, _ => [new(null, "child", static () => { })]);

        foreach (var selection in selections)
        {
            var menu = DesktopCommands.Menu(selection, new DeskMenuFacts(true, true, true), key => key.EndsWith("Docs", StringComparison.Ordinal));
            var entries = Themia.App.Widgets.FileMenuEntries.Of(menu.Lines, target);

            Assert.Equal(menu.Lines.Count, entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var line = menu.Lines[i];
                Assert.Equal(line.IsSeparator, entries[i].IsSeparator);
                if (line.IsSeparator)
                {
                    continue;
                }

                Assert.False(string.IsNullOrWhiteSpace(entries[i].Text));
                Assert.NotNull(entries[i].Glyph);
                Assert.Equal(!line.Enabled, entries[i].Disabled);
                Assert.Equal(FileMenu.Accelerator(line.Command, FileMenuPlace.Desktop), entries[i].Shortcut);
            }
        }
    }

    [Fact]
    public void S1406_repair_mends_a_hand_edited_layout_and_blanks_its_target()
    {
        var config = new DesktopItemConfig
        {
            Target = @"C:\evil.exe",
            Layout = new LayoutNode
            {
                Id = "s",
                Kind = LayoutNodeKind.Split,
                Ratio = double.NaN,
                A = new LayoutNode { Id = "p", Assignment = new LayoutAssignment { Target = null! } },
                B = null,
            },
        };

        var repairs = config.Repair();

        var layout = config.Layout!;
        Assert.Equal(LayoutNodeKind.Pane, layout.Kind);
        Assert.Null(layout.Assignment);
        Assert.Equal(string.Empty, config.Target);
        Assert.NotEmpty(repairs);

        var icon = new DesktopItemConfig { Target = @"C:\x.url", IconPath = "https://tracker.example/i.ico" };
        Assert.NotEmpty(icon.Repair());
        Assert.Null(icon.IconPath);
    }

    [Fact]
    public void S1406_a_layout_has_no_target_after_repair()
    {
        var config = new DesktopItemConfig();
        config.MakeLayout();
        config.Target = @"C:\Windows\notepad.exe";

        Assert.Contains(config.Repair(), r => r.Contains("app layout", StringComparison.Ordinal));
        Assert.Equal(string.Empty, config.Target);
        Assert.True(config.IsLayout);
    }

    [Fact]
    public void S1406_a_layout_and_an_icon_round_trip_through_the_state_file()
    {
        var morning = new DesktopItemConfig { Label = "Morning", Layout = SideBySide(@"C:\a.lnk", "https://b.example", 0.3) };

        var json = JsonSerializer.Serialize<WidgetConfig>(morning, StateJson.WidgetConfig);
        var back = Assert.IsType<DesktopItemConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));

        Assert.Contains("\"Vertical\"", json, StringComparison.Ordinal);
        Assert.True(back.IsLayout);
        Assert.Equal(0.3, back.Layout!.Ratio, 6);
        Assert.Equal(LayoutSource.Url, back.Layout!.B!.Assignment!.Source);
        Assert.Equal((morning.ItemId, "Morning"), (back.ItemId, back.Label));
        Assert.Empty(back.Repair());

        var game = new DesktopItemConfig { Target = @"C:\g.url", Label = "Game", IconPath = @"C:\g.ico" };
        var gameBack = Assert.IsType<DesktopItemConfig>(JsonSerializer.Deserialize(JsonSerializer.Serialize<WidgetConfig>(game, StateJson.WidgetConfig), StateJson.WidgetConfig));
        Assert.Equal(@"C:\g.ico", gameBack.IconPath);
        Assert.Empty(gameBack.Repair());
    }

    [Fact]
    public void S1405_a_cloned_layout_is_a_separate_tree_with_an_id_of_its_own()
    {
        var item = new DesktopItemConfig { Label = "x" };
        item.MakeLayout();
        var copy = (DesktopItemConfig)item.Clone();

        LayoutTree.Assign(copy.Layout!, copy.Layout!.Id, new LayoutAssignment { Source = LayoutSource.Url, Target = "https://a.example" });

        Assert.Null(item.Layout!.Assignment);
        Assert.NotEqual(item.ItemId, copy.ItemId);
    }

    [Fact]
    public void S1411_the_layout_editor_finds_a_layout_by_its_id_and_nothing_else()
    {
        var layout = new DesktopItemConfig { Label = "Work" };
        layout.MakeLayout();
        var plain = new DesktopItemConfig { Target = "https://example.com/" };

        Assert.Same(layout, layout.FindLayout(layout.ItemId));
        Assert.Null(layout.FindLayout("other"));
        Assert.Null(plain.FindLayout(plain.ItemId));
    }

    [Fact]
    public void S1411_the_host_finds_a_layout_through_the_holder_contract_alone()
    {
        var layout = new DesktopItemConfig { Label = "Work" };
        layout.MakeLayout();
        WidgetConfig[] configs = [new DesktopItemConfig { Target = "https://example.com/" }, layout];

        var found = configs.OfType<IHoldsAppLayouts>().Select(holder => holder.FindLayout(layout.ItemId)).Single(item => item is not null);

        Assert.Same(layout, found);
        Assert.Equal("Work", found!.Label);
    }
}
