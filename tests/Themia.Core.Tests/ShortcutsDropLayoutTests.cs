using System.Text.Json;
using Themia.Core.Files;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// Shortcuts B, the host's part: the addresses in dropped text (S-1402), the app layout tree, its walk,
/// placement and window matching (S-1403..S-1409). The widget's drops and reorder are its module's
/// (ShortcutDropTests), its layout items' config the module host's (ShortcutsHostTests).
/// </summary>
public sealed class ShortcutsDropLayoutTests
{
    // ---- drops ([S-1400]..[S-1402]) ----------------------------------------------------------------

    [Fact]
    public void S1402_urls_in_dropped_text_are_http_lines_only_once_each()
    {
        var urls = DropUrls.InText("# comment\r\nhttps://a.example/x\r\nftp://b.example\r\nhttps://a.example/x\nhttp://c.example\n");

        Assert.Equal(["https://a.example/x", "http://c.example/"], urls); // normalised by the rule
        Assert.Empty(DropUrls.InText("   "));
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

}
