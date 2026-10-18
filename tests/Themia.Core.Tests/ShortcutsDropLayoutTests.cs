using System.Text.Json;
using Themia.Core.Files;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>Shortcuts B: drops, reorder and app layouts (S-1400..S-1410).</summary>
public sealed class ShortcutsDropLayoutTests
{
    private static string? NoLink(string _) => null;

    private static string? NoText(string _) => null;

    // ---- drops ([S-1400]..[S-1402]) ----------------------------------------------------------------

    [Fact]
    public void S1400_each_dropped_path_becomes_an_item_by_reference()
    {
        var result = ShortcutDrop.FromDrop([@"C:\Work\report.docx", @"D:\Photos"], [], [], NoLink, NoText);

        Assert.Equal([@"C:\Work\report.docx", @"D:\Photos"], result.Items.Select(i => i.Target));
        Assert.All(result.Items, item => Assert.Equal(string.Empty, item.Label));
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void S1400_urls_count_only_when_no_files_came_with_the_drop()
    {
        var withFiles = ShortcutDrop.FromDrop([@"C:\a.txt"], ["https://example.com"], [], NoLink, NoText);
        var urlsOnly = ShortcutDrop.FromDrop([], ["https://example.com/page", "javascript:alert(1)"], [], NoLink, NoText);

        Assert.Equal([@"C:\a.txt"], withFiles.Items.Select(i => i.Target));
        Assert.Equal(["https://example.com/page"], urlsOnly.Items.Select(i => i.Target));
        Assert.Equal(1, urlsOnly.Skipped);
    }

    [Fact]
    public void S1400_refused_and_already_held_targets_are_skipped_and_counted()
    {
        var existing = new[] { ShortcutItem.Create(@"C:\Work\report.docx") };

        var result = ShortcutDrop.FromDrop(
            [@"c:\work\REPORT.docx", "relative\\path.txt", @"C:\b.txt", @"C:\b.txt"],
            [],
            existing,
            NoLink,
            NoText);

        Assert.Equal([@"C:\b.txt"], result.Items.Select(i => i.Target));
        Assert.Equal(3, result.Skipped);
    }

    [Fact]
    public void S1401_a_url_file_becomes_its_web_address_named_after_the_file()
    {
        var item = ShortcutDrop.FromPath(@"C:\Users\me\Desktop\Docs.url", NoLink,
            _ => "[InternetShortcut]\r\nURL=https://learn.microsoft.com/\r\nIconFile=C:\\icons\\docs.ico\r\n");

        Assert.NotNull(item);
        Assert.Equal("https://learn.microsoft.com/", item.Target);
        Assert.Equal("Docs", item.Label);
        Assert.Null(item.IconPath);
    }

    [Fact]
    public void S1401_a_custom_protocol_url_file_is_kept_as_the_file_with_its_icon()
    {
        var item = ShortcutDrop.FromPath(@"C:\Users\me\Desktop\Half-Life.url", NoLink,
            _ => "[InternetShortcut]\nIDList=\nURL=steam://rungameid/70\nIconIndex=0\nIconFile=C:\\Steam\\steam\\games\\hl.ico\n");

        Assert.NotNull(item);
        Assert.Equal(@"C:\Users\me\Desktop\Half-Life.url", item.Target);
        Assert.Equal("Half-Life", item.Label);
        Assert.Equal(@"C:\Steam\steam\games\hl.ico", item.IconPath);
    }

    [Fact]
    public void S1401_an_unreadable_url_file_is_a_plain_file_item()
    {
        var item = ShortcutDrop.FromPath(@"C:\x\Broken.url", NoLink, NoText);

        Assert.NotNull(item);
        Assert.Equal(@"C:\x\Broken.url", item.Target);
        Assert.Null(item.IconPath);
    }

    [Fact]
    public void S1401_url_file_parsing_reads_only_the_internet_shortcut_section()
    {
        var parsed = ShortcutDrop.ParseUrlFile("[Other]\nURL=https://wrong.example\n[internetshortcut]\nurl = https://right.example \nIconFile=\n");

        Assert.Equal("https://right.example", parsed.Url);
        Assert.Null(parsed.IconFile);
        Assert.Equal(default, ShortcutDrop.ParseUrlFile(null));
    }

    [Fact]
    public void S1401_a_link_is_kept_by_its_target_and_named_as_the_link()
    {
        var resolved = ShortcutDrop.FromPath(@"C:\Start\Visual Studio Code.lnk", _ => @"C:\Apps\Code\Code.exe", NoText);
        var unresolved = ShortcutDrop.FromPath(@"C:\Start\Chrome (Work).lnk", NoLink, NoText);
        var refused = ShortcutDrop.FromPath(@"C:\Start\Odd.lnk", _ => "http://not-a-path", NoText);

        Assert.Equal(@"C:\Apps\Code\Code.exe", resolved!.Target);
        Assert.Equal("Visual Studio Code", resolved.Label);
        Assert.Equal(@"C:\Start\Chrome (Work).lnk", unresolved!.Target);
        Assert.Equal(@"C:\Start\Odd.lnk", refused!.Target);
    }

    [Fact]
    public void S1402_urls_in_dropped_text_are_http_lines_only_once_each()
    {
        var urls = DropUrls.InText("# comment\r\nhttps://a.example/x\r\nftp://b.example\r\nhttps://a.example/x\nhttp://c.example\n");

        Assert.Equal(["https://a.example/x", "http://c.example/"], urls); // normalised by the rule
        Assert.Empty(DropUrls.InText("   "));
    }

    // ---- reorder ([S-1410]) -------------------------------------------------------------------------

    private static List<ShortcutItem> Items(params string[] ids)
        => ids.Select(id => new ShortcutItem { Id = id, Target = @"C:\" + id }).ToList();

    private static string Order(List<ShortcutItem> items) => string.Concat(items.Select(i => i.Id));

    [Fact]
    public void S1410_an_item_moves_before_the_one_it_is_dropped_on_or_to_the_end()
    {
        var items = Items("a", "b", "c", "d");

        Assert.True(ShortcutDrop.Reorder(items, ["d"], "b"));
        Assert.Equal("adbc", Order(items));

        Assert.True(ShortcutDrop.Reorder(items, ["a"], null));
        Assert.Equal("dbca", Order(items));
    }

    [Fact]
    public void S1410_a_drop_that_changes_nothing_writes_nothing()
    {
        var items = Items("a", "b", "c");

        Assert.False(ShortcutDrop.Reorder(items, ["b"], "b"));
        Assert.False(ShortcutDrop.Reorder(items, ["b"], "c"));
        Assert.False(ShortcutDrop.Reorder(items, ["c"], null));
        Assert.False(ShortcutDrop.Reorder(items, [], "a"));
        Assert.False(ShortcutDrop.Reorder(items, ["a"], "gone"));
        Assert.Equal("abc", Order(items));
    }

    [Fact]
    public void S1410_a_multi_selection_moves_together_in_its_own_order()
    {
        var items = Items("a", "b", "c", "d", "e");

        Assert.True(ShortcutDrop.Reorder(items, ["d", "b"], "a"));
        Assert.Equal("bdace", Order(items));

        // Before a dragged item = before the next one that is not dragged.
        items = Items("a", "b", "c", "d", "e");
        Assert.True(ShortcutDrop.Reorder(items, ["b", "d"], "d"));
        Assert.Equal("acbde", Order(items));
    }

    [Fact]
    public void S1410_the_insert_point_is_the_half_of_the_item_under_the_pointer()
    {
        string[] order = ["a", "b", "c"];

        Assert.Equal("b", ShortcutDrop.InsertBefore(order, 1, 0.2));
        Assert.Equal("c", ShortcutDrop.InsertBefore(order, 1, 0.7));
        Assert.Null(ShortcutDrop.InsertBefore(order, 2, 0.9));
        Assert.Null(ShortcutDrop.InsertBefore(order, -1, 0));
    }

    // ---- layout tree ([S-1403], [S-1404]) ----------------------------------------------------------

    [Fact]
    public void S1403_splitting_a_pane_keeps_it_as_the_first_half_and_adds_an_empty_second()
    {
        var root = LayoutNode.NewPane();
        LayoutTree.Assign(root, root.Id, new LayoutAssignment { Source = LayoutSource.Url, Target = "https://a.example" });
        var paneId = root.Id;

        var (split, changed) = LayoutTree.Split(root, paneId, SplitDirection.Vertical);

        Assert.True(changed);
        Assert.Equal(LayoutNodeKind.Split, split.Kind);
        Assert.Equal(0.5, split.Ratio);
        Assert.Equal(paneId, split.A!.Id);
        Assert.Equal("https://a.example", split.A.Assignment!.Target);
        Assert.Null(split.B!.Assignment);
        Assert.Equal(2, LayoutTree.Panes(split).Count);
    }

    [Fact]
    public void S1403_removing_a_pane_gives_its_space_to_the_sibling_and_never_leaves_nothing()
    {
        var root = LayoutNode.NewPane();
        var first = root.Id;
        (root, _) = LayoutTree.Split(root, first, SplitDirection.Vertical);
        var second = root.B!.Id;
        (root, _) = LayoutTree.Split(root, second, SplitDirection.Horizontal);
        var third = root.B!.B!.Id;

        (root, var changed) = LayoutTree.Remove(root, second);
        Assert.True(changed);
        Assert.Equal([first, third], LayoutTree.Panes(root).Select(p => p.Id));
        Assert.Equal(third, root.B!.Id);

        (root, _) = LayoutTree.Remove(root, first);
        Assert.Equal(third, root.Id);
        Assert.Equal(LayoutNodeKind.Pane, root.Kind);

        (root, changed) = LayoutTree.Remove(root, third);
        Assert.True(changed);
        Assert.Equal(LayoutNodeKind.Pane, root.Kind);
        Assert.NotEqual(third, root.Id);
    }

    [Fact]
    public void S1403_a_divider_is_held_between_five_and_ninety_five_percent()
    {
        var (root, _) = LayoutTree.Split(LayoutNode.NewPane(), "nope", SplitDirection.Vertical);
        Assert.Equal(LayoutNodeKind.Pane, root.Kind);

        (root, _) = LayoutTree.Split(root, root.Id, SplitDirection.Vertical);

        Assert.True(LayoutTree.SetRatio(root, root.Id, 0.01));
        Assert.Equal(LayoutTree.MinRatio, root.Ratio);
        Assert.True(LayoutTree.SetRatio(root, root.Id, 2));
        Assert.Equal(LayoutTree.MaxRatio, root.Ratio);
        Assert.False(LayoutTree.SetRatio(root, root.Id, 0.95));
        Assert.False(LayoutTree.SetRatio(root, root.Id, double.NaN));
        Assert.False(LayoutTree.SetRatio(root, root.A!.Id, 0.3));
    }

    [Fact]
    public void S1403_a_pane_at_the_depth_limit_does_not_split()
    {
        var root = LayoutNode.NewPane();
        var pane = root.Id;

        for (var i = 0; i < LayoutTree.MaxDepth; i++)
        {
            (root, var changed) = LayoutTree.Split(root, pane, SplitDirection.Vertical);
            Assert.True(changed);
        }

        Assert.False(LayoutTree.Split(root, pane, SplitDirection.Vertical).Changed);
    }

    [Fact]
    public void S1404_a_blank_target_clears_the_assignment_and_nothing_changed_reports_false()
    {
        var root = LayoutNode.NewPane();
        var app = new LayoutAssignment { Source = LayoutSource.App, Target = @"  C:\Start\Word.lnk ", Label = "Word" };

        Assert.True(LayoutTree.Assign(root, root.Id, app));
        Assert.Equal(@"C:\Start\Word.lnk", root.Assignment!.Target);
        Assert.False(LayoutTree.Assign(root, root.Id, new LayoutAssignment { Source = LayoutSource.App, Target = @"C:\Start\Word.lnk", Label = "Word" }));
        Assert.True(LayoutTree.Assign(root, root.Id, new LayoutAssignment { Source = LayoutSource.Url, Target = "  " }));
        Assert.Null(root.Assignment);
        Assert.False(LayoutTree.Assign(root, root.Id, null));
    }

    // ---- the walk ([S-1407]) -----------------------------------------------------------------------

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
    public void S1407_a_half_and_half_split_puts_the_panes_at_0_to_50_and_50_to_100()
    {
        var entries = LayoutPlan.Walk(SideBySide(@"C:\a.lnk", "https://b.example"));

        Assert.Equal(2, entries.Count);
        Assert.Equal(new PercentRect(0, 0, 50, 100), entries[0].Area);
        Assert.Equal(new PercentRect(50, 0, 50, 100), entries[1].Area);
        Assert.Equal(LayoutSource.App, entries[0].Source);
        Assert.True(entries[0].ReuseIfRunning);
        Assert.False(entries[1].ReuseIfRunning);
    }

    [Fact]
    public void S1407_stacked_splits_nest_their_rectangles()
    {
        var root = LayoutNode.NewPane();
        (root, _) = LayoutTree.Split(root, root.Id, SplitDirection.Horizontal);
        LayoutTree.SetRatio(root, root.Id, 0.25);
        (root, _) = LayoutTree.Split(root, root.B!.Id, SplitDirection.Vertical);
        LayoutTree.Assign(root, root.A!.Id, new LayoutAssignment { Source = LayoutSource.File, Target = @"C:\top.txt" });
        LayoutTree.Assign(root, root.B!.B!.Id, new LayoutAssignment { Source = LayoutSource.Folder, Target = @"C:\Work" });

        var entries = LayoutPlan.Walk(root);

        Assert.Equal(new PercentRect(0, 0, 100, 25), entries[0].Area);
        Assert.Equal(new PercentRect(50, 25, 50, 75), entries[1].Area);
        Assert.False(entries[1].ReuseIfRunning);
    }

    [Fact]
    public void S1407_an_empty_layout_walks_to_nothing_and_a_repeated_target_opens_once()
    {
        Assert.Empty(LayoutPlan.Walk(LayoutNode.NewPane()));
        Assert.Empty(LayoutPlan.Walk(null));

        var root = SideBySide(@"C:\a.lnk", "https://b.example");
        root.B!.Assignment = new LayoutAssignment { Source = LayoutSource.App, Target = @"c:\A.LNK" };

        var entries = LayoutPlan.Walk(root);

        Assert.Single(entries);
        Assert.Equal(new PercentRect(0, 0, 50, 100), entries[0].Area);
    }

    [Fact]
    public void S1407_a_pane_is_placed_in_pixels_on_the_work_area_and_never_tiny()
    {
        var work = new ScreenRect(1920, 0, 2560, 1400);

        Assert.Equal(new ScreenRect(1920, 0, 1280, 1400), LayoutPlan.Place(new PercentRect(0, 0, 50, 100), work));
        Assert.Equal(new ScreenRect(3200, 700, 1280, 700), LayoutPlan.Place(new PercentRect(50, 50, 50, 50), work));
        Assert.Equal(new ScreenRect(1920, 0, LayoutPlan.MinWidth, LayoutPlan.MinHeight), LayoutPlan.Place(new PercentRect(-5, double.NaN, 1, 1), work));
    }

    [Fact]
    public void S1408_invisible_borders_widen_the_window_so_the_frame_fills_the_pane()
    {
        var pane = new ScreenRect(0, 0, 960, 1040);

        Assert.Equal(new ScreenRect(-7, 0, 974, 1047), LayoutPlan.WithInvisibleBorders(pane, 7, 0, 7, 7));
        Assert.Equal(pane, LayoutPlan.WithInvisibleBorders(pane, -3, 0, 0, 0));
    }

    [Fact]
    public void S1408_drift_is_beyond_eight_pixels_of_place_or_twenty_four_of_size()
    {
        var pane = new ScreenRect(100, 100, 800, 600);

        Assert.False(LayoutPlan.Drifted(pane, new ScreenRect(108, 92, 824, 576)));
        Assert.True(LayoutPlan.Drifted(pane, new ScreenRect(109, 100, 800, 600)));
        Assert.True(LayoutPlan.Drifted(pane, new ScreenRect(100, 100, 800, 625)));
    }

    [Fact]
    public void S1405_preview_and_summary_count_assigned_panes_in_tree_order()
    {
        var root = SideBySide(@"C:\a.lnk", "https://b.example");

        Assert.Equal([@"C:\a.lnk", "https://b.example"], LayoutPlan.Preview(root).Select(a => a.Target));
        Assert.Single(LayoutPlan.Preview(root, 1));
        Assert.Equal("2 apps", LayoutPlan.Summary(Locale.En, root));
        Assert.Equal("No apps yet", LayoutPlan.Summary(Locale.En, LayoutNode.NewPane()));
    }

    [Fact]
    public void S1405_summary_follows_the_language()
    {
        Assert.Equal("2 Apps", LayoutPlan.Summary(Locale.De, SideBySide(@"C:\a.lnk", "https://b.example")));
        Assert.Equal("Noch keine Apps", LayoutPlan.Summary(Locale.De, LayoutNode.NewPane()));
    }

    // ---- window matching ([S-1409]) -----------------------------------------------------------------

    private static WindowFacts Window(nint handle, int pid, string exe, string cls = "AppWindow", int title = 5, int w = 1200, int h = 800)
        => new(handle, pid, exe, cls, Visible: true, Owned: false, ToolWindow: false, title, w, h);

    private static readonly HashSet<nint> Before = [(nint)1, (nint)2];

    [Fact]
    public void S1409_a_folder_pane_takes_only_a_new_or_activated_explorer_folder_window()
    {
        var watch = new WindowWatch(LayoutSource.Folder, 0, null, Before, OwnProcessId: 99);

        Assert.Equal(WindowMatch.Main, LayoutWindows.Judge(watch, Window(10, 5, "explorer.exe", LayoutWindows.ExplorerClass), false));
        Assert.Equal(WindowMatch.None, LayoutWindows.Judge(watch, Window(1, 5, "explorer.exe", LayoutWindows.ExplorerClass), false));
        Assert.Equal(WindowMatch.Main, LayoutWindows.Judge(watch, Window(1, 5, "explorer.exe", LayoutWindows.ExplorerClass), true));
        Assert.Equal(WindowMatch.None, LayoutWindows.Judge(watch, Window(11, 5, "explorer.exe", "Shell_TrayWnd"), true));
        Assert.Equal(WindowMatch.None, LayoutWindows.Judge(watch, Window(12, 5, "explorer.exe", "Progman"), true));
    }

    [Fact]
    public void S1409_an_app_pane_takes_its_process_or_a_new_window_of_its_exe()
    {
        var watch = new WindowWatch(LayoutSource.App, 42, "WINWORD.EXE", Before, OwnProcessId: 99);

        Assert.Equal(WindowMatch.Main, LayoutWindows.Judge(watch, Window(20, 42, "WINWORD.EXE"), false));
        Assert.Equal(WindowMatch.Main, LayoutWindows.Judge(watch, Window(21, 7, "winword.exe"), false));
        Assert.Equal(WindowMatch.None, LayoutWindows.Judge(watch, Window(2, 7, "winword.exe"), false));
        Assert.Equal(WindowMatch.Main, LayoutWindows.Judge(watch, Window(2, 7, "winword.exe"), true));
        Assert.Equal(WindowMatch.None, LayoutWindows.Judge(watch, Window(22, 8, "notepad.exe"), true));
    }

    [Fact]
    public void S1409_a_splash_is_only_a_fallback_and_hidden_owned_or_own_windows_never_match()
    {
        var watch = new WindowWatch(LayoutSource.App, 42, "app.exe", Before, OwnProcessId: 99);

        Assert.Equal(WindowMatch.Fallback, LayoutWindows.Judge(watch, Window(30, 42, "app.exe", title: 0), false));
        Assert.Equal(WindowMatch.Fallback, LayoutWindows.Judge(watch, Window(31, 42, "app.exe", w: 300), false));
        Assert.Equal(WindowMatch.None, LayoutWindows.Judge(watch, Window(32, 42, "app.exe") with { Owned = true }, false));
        Assert.Equal(WindowMatch.None, LayoutWindows.Judge(watch, Window(33, 42, "app.exe") with { Visible = false }, false));
        Assert.Equal(WindowMatch.None, LayoutWindows.Judge(watch, Window(34, 99, "app.exe"), true));
    }

    [Fact]
    public void S1409_an_explorer_hint_never_moves_the_desktop_or_the_taskbar()
    {
        var watch = new WindowWatch(LayoutSource.File, 0, "explorer.exe", Before, OwnProcessId: 99);

        Assert.Equal(WindowMatch.None, LayoutWindows.Judge(watch, Window(40, 5, "explorer.exe", "Shell_TrayWnd"), true));
        Assert.Null(LayoutWindows.Running([Window(41, 5, "explorer.exe", LayoutWindows.ExplorerClass)], LayoutSource.App, "explorer.exe", 99));
    }

    [Fact]
    public void S1409_a_running_app_is_its_first_main_window_and_never_for_urls_or_folders()
    {
        WindowFacts[] windows = [Window(50, 7, "code.exe", title: 0), Window(51, 7, "code.exe"), Window(52, 7, "code.exe")];

        Assert.Equal((nint)51, LayoutWindows.Running(windows, LayoutSource.App, "Code.exe", 99)?.Handle);
        Assert.Null(LayoutWindows.Running(windows, LayoutSource.Url, "code.exe", 99));
        Assert.Null(LayoutWindows.Running(windows, LayoutSource.Folder, "code.exe", 99));
        Assert.Null(LayoutWindows.Running(windows, LayoutSource.App, null, 99));
    }

    // ---- config ([S-1405], [S-1406]) -------------------------------------------------------------------

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
    public void S1406_a_sound_layout_repairs_to_itself_and_too_deep_trees_are_cut()
    {
        var sound = SideBySide(@"C:\a.lnk", "https://b.example");
        var repairs = new List<string>();
        Assert.Same(sound, LayoutTree.Repair(sound, repairs));
        Assert.Empty(repairs);

        var deep = new LayoutNode { Id = "0", Kind = LayoutNodeKind.Pane };

        for (var i = 1; i <= LayoutTree.MaxDepth + 3; i++)
        {
            deep = new LayoutNode { Id = i.ToString(System.Globalization.CultureInfo.InvariantCulture), Kind = LayoutNodeKind.Split, A = deep, B = new LayoutNode { Id = "b" + i } };
        }

        var cut = LayoutTree.Repair(deep, repairs);
        Assert.Contains("App layout was nested too deeply", repairs);
        Assert.True(LayoutTree.Panes(cut).Count <= 1 << LayoutTree.MaxDepth);
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
