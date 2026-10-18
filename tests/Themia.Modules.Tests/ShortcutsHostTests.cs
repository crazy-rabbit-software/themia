using System.Text.Json;
using Themia.Core.Files;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>The Shortcuts config and rules under the root's tags (the keyboard move, [S-3802]; the layout item's menu, clone, repair and round trip, [S-1405], [S-1406]): the config is its module's, so they run here. Moved from Core's ShortcutsTests and ShortcutsDropLayoutTests, unchanged in what they assert.</summary>
public sealed class ShortcutsHostTests
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
    public void S3802_move_reorders_by_id_one_place_and_a_no_op_changes_nothing()
    {
        var items = new List<ShortcutItem>
        {
            new() { Id = "a" }, new() { Id = "b" }, new() { Id = "c" },
        };

        Assert.True(ShortcutRules.Move(items, "c", -1));
        Assert.Equal(["a", "c", "b"], items.Select(i => i.Id));
        Assert.True(ShortcutRules.Move(items, "a", +1));
        Assert.Equal(["c", "a", "b"], items.Select(i => i.Id));
        Assert.False(ShortcutRules.Move(items, "c", -1));
        Assert.False(ShortcutRules.Move(items, "b", +1));
        Assert.False(ShortcutRules.Move(items, "zz", -1));
        Assert.False(ShortcutRules.Move(items, "a", 0));
        Assert.Equal(["c", "a", "b"], items.Select(i => i.Id));
    }

    [Fact]
    public void S1405_the_layout_menu_launches_edits_moves_renames_and_removes()
    {
        var layout = SideBySide(@"C:\a.lnk", "https://b.example");
        var menu = ShortcutRules.Menu(LaunchTargets.Classify(string.Empty), 1, layout);
        var commands = menu.Where(e => e.Command is not null).Select(e => e.Command!.Value).ToList();

        Assert.Equal(ShortcutCommand.Open, commands[0]);
        Assert.False(menu[0].Disabled);
        Assert.Contains(ShortcutCommand.EditLayout, commands);
        Assert.DoesNotContain(ShortcutCommand.CopyPath, commands);
        Assert.Contains(ShortcutCommand.Rename, commands);

        var empty = ShortcutRules.Menu(LaunchTargets.Classify(string.Empty), 1, LayoutNode.NewPane());
        Assert.True(empty[0].Disabled);
    }

    [Fact]
    public void S1406_repair_mends_a_hand_edited_layout_and_blanks_its_target()
    {
        var config = new ShortcutsConfig
        {
            Items =
            [
                new ShortcutItem
                {
                    Id = "l",
                    Target = @"C:\evil.exe",
                    Layout = new LayoutNode
                    {
                        Id = "s",
                        Kind = LayoutNodeKind.Split,
                        Ratio = double.NaN,
                        A = new LayoutNode { Id = "p", Assignment = new LayoutAssignment { Target = null! } },
                        B = null,
                    },
                },
                new ShortcutItem { Id = "i", Target = @"C:\x.url", IconPath = "https://tracker.example/i.ico" },
            ],
        };

        var repairs = config.Repair();

        var layout = config.Items[0].Layout!;
        Assert.Equal(LayoutNodeKind.Pane, layout.Kind);
        Assert.Null(layout.Assignment);
        Assert.Equal(string.Empty, config.Items[0].Target);
        Assert.Null(config.Items[1].IconPath);
        Assert.NotEmpty(repairs);
    }

    [Fact]
    public void S1406_a_layout_and_an_icon_round_trip_through_the_state_file()
    {
        var config = new ShortcutsConfig
        {
            Items =
            [
                ShortcutItem.CreateLayout("Morning"),
                new ShortcutItem { Id = "u", Target = @"C:\g.url", Label = "Game", IconPath = @"C:\g.ico" },
            ],
        };

        config.Items[0].Layout = SideBySide(@"C:\a.lnk", "https://b.example", 0.3);

        var json = JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig);
        var back = Assert.IsType<ShortcutsConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));

        Assert.Contains("\"Vertical\"", json, StringComparison.Ordinal);
        Assert.True(back.Items[0].IsLayout);
        Assert.Equal(0.3, back.Items[0].Layout!.Ratio, 6);
        Assert.Equal(LayoutSource.Url, back.Items[0].Layout!.B!.Assignment!.Source);
        Assert.Equal(@"C:\g.ico", back.Items[1].IconPath);
        Assert.Empty(back.Repair());
    }

    [Fact]
    public void S1405_a_cloned_layout_is_a_separate_tree()
    {
        var item = ShortcutItem.CreateLayout("x");
        var copy = item.Clone();

        LayoutTree.Assign(copy.Layout!, copy.Layout!.Id, new LayoutAssignment { Source = LayoutSource.Url, Target = "https://a.example" });

        Assert.Null(item.Layout!.Assignment);
        Assert.NotEqual(item.Id, copy.Id);
    }
}
