using Themia.Core.Desktop;
using Themia.Core.Files;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5580]..[S-5587] The desktop's commands on a selection that may mix both sources of desktop icon:
/// what an icon is, what opens, which menu, Delete, F2, the clipboard, what a drag carries and what a
/// drop onto an icon is forwarded to.
/// </summary>
public sealed class DesktopCommandsTests
{
    private const string Desk = @"C:\Users\a\Desktop\";
    private const string ThisPcItem = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    private const string App = @"C:\Tools\app.exe";
    private const string Share = @"\\server\share\x";

    private static DeskIconFake Config(string target = "", string item = "", string id = "id1", LayoutNode? layout = null, ItemClickMode click = ItemClickMode.Double)
        => new() { Item = item, ItemId = id, Target = target, Layout = layout, ClickMode = click };

    private static LayoutNode Pane(string? target)
    {
        var pane = LayoutNode.NewPane();
        if (target is not null)
        {
            pane.Assignment = new LayoutAssignment { Target = target };
        }

        return pane;
    }

    private static DeskIcon Item(string name) => DeskIcon.Of(Config(item: Desk + name), _ => false);

    private static DeskIcon Shell(string key) => DeskIcon.Of(Config(item: key), _ => false);

    private static DeskIcon Own(string target, string id = "id1", bool there = true) => DeskIcon.Of(Config(target, id: id), _ => there);

    private static DeskIcon Layout(string? paneTarget, string id = "lay") => DeskIcon.Of(Config(id: id, layout: Pane(paneTarget)), _ => false);

    private static DeskIcon Waiting(string id = "wait") => Own(string.Empty, id);

    // ---- [S-5580] what an icon is ---------------------------------------------------------------

    [Fact]
    public void S5580_a_desktop_item_is_its_key_opens_and_is_never_probed()
    {
        var asked = new List<string>();
        var icon = DeskIcon.Of(Config(item: Desk + "a.txt", click: ItemClickMode.Single), path =>
        {
            asked.Add(path);
            return false;
        });

        Assert.Equal(new DeskIcon(Desk + "a.txt", DeskIconKind.Item, Desk + "a.txt", string.Empty, Opens: true, PathExists: true, ItemClickMode.Double), icon);
        Assert.False(icon.Own);
        Assert.Equal(Desk + "a.txt", icon.FilePath);
        Assert.Equal(Desk + "a.txt", icon.PathText);
        Assert.Empty(asked);
    }

    [Fact]
    public void S5580_a_shell_item_is_no_file_but_has_its_key_to_say()
    {
        var icon = Shell(ThisPcItem);

        Assert.Equal(DeskIconKind.ShellItem, icon.Kind);
        Assert.Equal(ThisPcItem, icon.Key);
        Assert.Equal(ThisPcItem, icon.Value);
        Assert.True(icon.Opens);
        Assert.False(icon.Own);
        Assert.Null(icon.FilePath);
        Assert.Null(icon.PathText);
    }

    [Fact]
    public void S5580_a_desktop_item_that_also_holds_a_target_is_still_the_item()
    {
        var config = Config(App, item: Desk + "a.txt");
        config.Layout = Pane(App);

        Assert.Equal(DeskIconKind.Item, DeskIcon.Of(config, _ => true).Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void S5580_an_own_icon_with_no_target_waits_and_opens_nothing(string target)
    {
        var icon = DeskIcon.Of(Config(target, id: "abc", click: ItemClickMode.Single), _ => throw new InvalidOperationException("probed"));

        Assert.Equal(new DeskIcon("*icon:abc", DeskIconKind.OwnWaiting, string.Empty, string.Empty, Opens: false, PathExists: false, ItemClickMode.Single), icon);
        Assert.True(icon.Own);
        Assert.Null(icon.FilePath);
        Assert.Null(icon.PathText);
    }

    [Fact]
    public void S5580_a_layout_opens_only_with_an_assigned_pane_and_wins_over_a_target()
    {
        var empty = Layout(null);
        var blank = Layout("  ");
        var assigned = Layout(App);
        var config = Config(App, id: "both", layout: Pane(App));
        var both = DeskIcon.Of(config, _ => throw new InvalidOperationException("probed"));

        Assert.Equal(new DeskIcon("*icon:lay", DeskIconKind.OwnLayout, string.Empty, string.Empty, Opens: false, PathExists: false, ItemClickMode.Double), empty);
        Assert.False(blank.Opens);
        Assert.True(assigned.Opens);
        Assert.Equal(DeskIconKind.OwnLayout, both.Kind);
        Assert.Equal(string.Empty, both.Target);
        Assert.Null(assigned.FilePath);
        Assert.Null(assigned.PathText);
    }

    [Fact]
    public void S5580_a_split_layout_opens_when_any_pane_is_assigned()
    {
        var split = new LayoutNode { Id = "root", Kind = LayoutNodeKind.Split, A = Pane(null), B = Pane(App) };
        var none = new LayoutNode { Id = "root", Kind = LayoutNodeKind.Split, A = Pane(null), B = Pane(null) };

        Assert.True(DeskIcon.Of(Config(layout: split), _ => false).Opens);
        Assert.False(DeskIcon.Of(Config(layout: none), _ => false).Opens);
    }

    [Theory]
    [InlineData("https://example.com/page", DeskIconKind.OwnWeb)]
    [InlineData("ms-settings:display", DeskIconKind.OwnSettings)]
    [InlineData("shell:Downloads", DeskIconKind.OwnLocation)]
    [InlineData("shell:RecycleBinFolder", DeskIconKind.OwnBin)]
    [InlineData("SHELL:recyclebinfolder", DeskIconKind.OwnBin)]
    public void S5580_an_own_icon_is_what_its_target_classifies_as_and_only_a_path_is_probed(string target, DeskIconKind kind)
    {
        var icon = DeskIcon.Of(Config(target), _ => throw new InvalidOperationException("probed"));

        Assert.Equal(kind, icon.Kind);
        Assert.Equal("*icon:id1", icon.Key);
        Assert.Equal(LaunchTargets.Classify(target).Value, icon.Value);
        Assert.Equal(target, icon.Target);
        Assert.True(icon.Opens);
        Assert.True(icon.Own);
        Assert.False(icon.PathExists);
        Assert.Null(icon.FilePath);
    }

    [Fact]
    public void S5580_what_an_own_icon_says_as_its_path()
    {
        Assert.Null(Own("https://example.com/page").PathText);
        Assert.Null(Own("ms-settings:display").PathText);
        Assert.Equal("shell:Downloads", Own("shell:Downloads").PathText);
        Assert.Equal("shell:RecycleBinFolder", Own("shell:RecycleBinFolder").PathText);
        Assert.Equal(App, Own(App, there: false).PathText);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void S5580_an_own_path_is_asked_whether_it_is_there(bool there)
    {
        var asked = new List<string>();
        var icon = DeskIcon.Of(Config("  " + App + " ", click: ItemClickMode.Single), path =>
        {
            asked.Add(path);
            return there;
        });

        Assert.Equal([App], asked);
        Assert.Equal(new DeskIcon("*icon:id1", DeskIconKind.OwnPath, App, "  " + App + " ", Opens: true, there, ItemClickMode.Single), icon);
        Assert.Equal(there ? App : null, icon.FilePath);
        Assert.Equal(App, icon.PathText);
    }

    [Theory]
    [InlineData(@"\\server\share\x")]
    [InlineData("//server/share/x")]
    public void S5580_a_network_path_is_never_probed(string target)
    {
        var icon = DeskIcon.Of(Config(target), _ => throw new InvalidOperationException("a network path was probed"));

        Assert.Equal(DeskIconKind.OwnPath, icon.Kind);
        Assert.True(icon.Opens);
        Assert.False(icon.PathExists);
        Assert.Null(icon.FilePath);
        Assert.NotNull(icon.PathText);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/x.exe")]
    [InlineData(@"relative\path.exe")]
    [InlineData("C:\\a\u0000b.exe")]
    [InlineData("shell:")]
    public void S5580_a_refused_target_opens_nothing_and_keeps_what_was_stored(string target)
    {
        var icon = DeskIcon.Of(Config(target), _ => throw new InvalidOperationException("probed"));

        Assert.Equal(new DeskIcon("*icon:id1", DeskIconKind.OwnRefused, string.Empty, target, Opens: false, PathExists: false, ItemClickMode.Double), icon);
        Assert.True(icon.Own);
        Assert.Null(icon.FilePath);
        Assert.Null(icon.PathText);
    }

    // ---- [S-5581] Open --------------------------------------------------------------------------

    [Fact]
    public void S5581_opens_each_selected_icon_its_own_way()
    {
        var (open, skipped) = DesktopCommands.Open([Item("a.txt"), Shell(ThisPcItem), Own("  https://example.com/page", "web"), Own(App, "app", there: false), Layout(App)]);

        Assert.Equal(0, skipped);
        Assert.Equal(
            [
                new DeskOpen(Desk + "a.txt", Desk + "a.txt", false),
                new DeskOpen(ThisPcItem, "shell:" + ThisPcItem, false),
                new DeskOpen("*icon:web", "  https://example.com/page", false),
                new DeskOpen("*icon:app", App, false),
                new DeskOpen("*icon:lay", null, true),
            ],
            open);
    }

    [Fact]
    public void S5581_icons_that_open_nothing_are_left_out_and_not_counted_as_skipped()
    {
        var (open, skipped) = DesktopCommands.Open([Waiting(), Layout(null, "empty"), Own("javascript:alert(1)", "bad"), Item("a.txt")]);

        Assert.Equal([new DeskOpen(Desk + "a.txt", Desk + "a.txt", false)], open);
        Assert.Equal(0, skipped);
        Assert.Equal((0, 0), (DesktopCommands.Open([Waiting()]).Open.Count, DesktopCommands.Open([Waiting()]).Skipped));
        Assert.Equal((0, 0), (DesktopCommands.Open([]).Open.Count, DesktopCommands.Open([]).Skipped));
    }

    [Theory]
    [InlineData(15, 15, 0)]
    [InlineData(16, 15, 1)]
    [InlineData(40, 15, 25)]
    public void S5581_opens_at_most_fifteen_and_says_how_many_were_left(int selected, int opened, int left)
    {
        // Waiting icons in between do not use up the fifteen.
        var selection = Enumerable.Range(0, selected)
            .SelectMany(i => new[] { Waiting("w" + i), i % 2 == 0 ? Item(i + ".txt") : Own(App, "o" + i) })
            .ToList();

        var (open, skipped) = DesktopCommands.Open(selection);

        Assert.Equal(15, DesktopCommands.OpenAtMost);
        Assert.Equal(opened, open.Count);
        Assert.Equal(left, skipped);
        Assert.Equal(selection.Where(icon => icon.Opens).Take(15).Select(icon => icon.Key), open.Select(o => o.Key));
    }

    // ---- [S-5582] the menu ----------------------------------------------------------------------

    private static readonly DeskMenuFacts AllOn = new(CanEmptyBin: true, CanEditLayout: true, ClipboardHasFiles: true);

    private static DeskMenu Menu(DeskIcon[] selection, DeskMenuFacts? facts = null, params string[] folders)
        => DesktopCommands.Menu(selection, facts ?? AllOn, key => key.StartsWith(Desk, StringComparison.Ordinal) && folders.Contains(key[Desk.Length..]));

    /// <summary>The icon row a menu's lines make ([S-1740], [S-1741]: fewer than two quick commands is no row).</summary>
    private static QuickCommand[] Row(DeskMenu menu)
    {
        var split = ContextMenuLayout.Split([.. menu.Lines.Select(l => l.IsSeparator ? MenuLine.Separator : MenuLine.Entry(FileMenu.Quick(l.Command)))]);
        return [.. split.Row.Select(i => FileMenu.Quick(menu.Lines[i].Command)!.Value)];
    }

    /// <summary>The labelled list a menu's lines make, separators as <see cref="FileCommand.Separator"/>.</summary>
    private static FileCommand[] List(DeskMenu menu)
    {
        var split = ContextMenuLayout.Split([.. menu.Lines.Select(l => l.IsSeparator ? MenuLine.Separator : MenuLine.Entry(FileMenu.Quick(l.Command)))]);
        return [.. split.List.Select(i => i == MenuSplit.SeparatorIndex ? FileCommand.Separator : menu.Lines[i].Command)];
    }

    private static bool Enabled(DeskMenu menu, FileCommand command) => menu.Lines.Single(l => l.Command == command).Enabled;

    private const FileCommand Sep = FileCommand.Separator;

    [Fact]
    public void S5582_nothing_selected_has_no_menu()
    {
        var menu = Menu([]);

        Assert.Empty(menu.Lines);
        Assert.Empty(menu.Classic);
    }

    [Fact]
    public void S5582_one_desktop_file_gets_explorers_row_and_list_and_the_classic_menu_behind_show_more_options()
    {
        var menu = Menu([Item("a.txt")]);

        Assert.Equal([QuickCommand.Cut, QuickCommand.Copy, QuickCommand.Rename, QuickCommand.Share, QuickCommand.Delete], Row(menu));
        Assert.Equal(
            [FileCommand.Open, FileCommand.OpenWith, FileCommand.CompressToZip, FileCommand.CopyAsPath, FileCommand.Properties, Sep, FileCommand.HideFromView, FileCommand.EditMode, FileCommand.LockWidgets, Sep, FileCommand.ShowMoreOptions],
            List(menu));
        Assert.All(menu.Lines, line => Assert.True(line.Enabled));
        Assert.Equal([Desk + "a.txt"], menu.Classic);
    }

    [Fact]
    public void S5582_a_desktop_link_adds_open_file_location_and_pin_to_start()
    {
        var menu = Menu([Item("Themia.lnk")]);

        Assert.Equal([QuickCommand.Cut, QuickCommand.Copy, QuickCommand.Rename, QuickCommand.Share, QuickCommand.Delete], Row(menu));
        Assert.Equal(
            [
                FileCommand.Open, FileCommand.OpenWith, FileCommand.OpenFileLocation, FileCommand.PinToStart, FileCommand.CompressToZip,
                FileCommand.CopyAsPath, FileCommand.Properties, Sep, FileCommand.HideFromView, FileCommand.EditMode, FileCommand.LockWidgets, Sep, FileCommand.ShowMoreOptions,
            ],
            List(menu));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void S5582_a_desktop_folder_pastes_into_itself_and_pins_but_has_no_share_or_open_with(bool clipboardHasFiles)
    {
        var menu = Menu([Item("Docs")], AllOn with { ClipboardHasFiles = clipboardHasFiles }, "Docs");

        Assert.Equal([QuickCommand.Cut, QuickCommand.Copy, QuickCommand.Paste, QuickCommand.Rename, QuickCommand.Delete], Row(menu));
        Assert.Equal(clipboardHasFiles, Enabled(menu, FileCommand.Paste));
        Assert.Equal(
            [
                FileCommand.Open, FileCommand.PinToQuickAccess, FileCommand.PinToStart, FileCommand.CompressToZip, FileCommand.CopyAsPath,
                FileCommand.Properties, Sep, FileCommand.HideFromView, FileCommand.EditMode, FileCommand.LockWidgets, Sep, FileCommand.ShowMoreOptions,
            ],
            List(menu));

        // Open stays Explorer's on the desktop: there is no "Open in new window" beside it.
        Assert.DoesNotContain(menu.Lines, line => line.Command == FileCommand.OpenInNewWindow);
    }

    [Fact]
    public void S5582_several_desktop_files_lose_the_one_item_commands()
    {
        var files = Menu([Item("a.txt"), Item("b.txt")]);
        var withFolder = Menu([Item("a.txt"), Item("Docs")], null, "Docs");

        Assert.Equal([QuickCommand.Cut, QuickCommand.Copy, QuickCommand.Share, QuickCommand.Delete], Row(files));
        Assert.Equal([FileCommand.Open, FileCommand.CompressToZip, FileCommand.CopyAsPath, FileCommand.Properties, Sep, FileCommand.HideFromView, FileCommand.EditMode, FileCommand.LockWidgets, Sep, FileCommand.ShowMoreOptions], List(files));
        Assert.Equal([Desk + "a.txt", Desk + "b.txt"], files.Classic);
        Assert.Equal([QuickCommand.Cut, QuickCommand.Copy, QuickCommand.Delete], Row(withFolder));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void S5582_shell_items_never_get_cut_rename_or_delete_and_the_bin_offers_emptying_it(bool canEmpty)
    {
        var pc = Menu([Shell(ThisPcItem)]);
        var bin = Menu([Shell(DesktopCommands.RecycleBinItem)], AllOn with { CanEmptyBin = canEmpty });
        var withFile = Menu([Item("a.txt"), Shell(DesktopCommands.RecycleBinItem)]);

        Assert.Empty(Row(pc));
        Assert.Equal([FileCommand.Open, FileCommand.Properties, Sep, FileCommand.HideFromView, FileCommand.EditMode, FileCommand.LockWidgets, Sep, FileCommand.ShowMoreOptions], List(pc));
        Assert.Equal([ThisPcItem], pc.Classic);

        Assert.Empty(Row(bin));
        Assert.Equal([FileCommand.Open, FileCommand.EmptyRecycleBin, FileCommand.Properties, Sep, FileCommand.HideFromView, FileCommand.EditMode, FileCommand.LockWidgets, Sep, FileCommand.ShowMoreOptions], List(bin));
        Assert.Equal(canEmpty, Enabled(bin, FileCommand.EmptyRecycleBin));

        // With a file beside it the bin's own line goes, and still nothing cuts, renames or deletes.
        Assert.Empty(Row(withFile));
        Assert.Equal([FileCommand.Open, FileCommand.Properties, Sep, FileCommand.HideFromView, FileCommand.EditMode, FileCommand.LockWidgets, Sep, FileCommand.ShowMoreOptions], List(withFile));
        Assert.Equal([Desk + "a.txt", DesktopCommands.RecycleBinItem], withFile.Classic);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void S5582_one_own_path_offers_its_location_and_its_path_and_the_shells_menu_only_while_it_is_there(bool there)
    {
        var menu = Menu([Own(App, there: there)], new DeskMenuFacts(false, false, false));

        Assert.Equal([QuickCommand.Rename, QuickCommand.Delete], Row(menu));
        FileCommand[] expected = [FileCommand.Open, FileCommand.OpenFileLocation, FileCommand.CopyPath, FileCommand.IconProperties, Sep, FileCommand.EditMode, FileCommand.LockWidgets];
        Assert.Equal(there ? [.. expected, Sep, FileCommand.ShowMoreOptions] : expected, List(menu));
        Assert.Equal(there ? new[] { App } : [], menu.Classic);
    }

    [Theory]
    [InlineData("https://example.com/page")]
    [InlineData("ms-settings:display")]
    public void S5582_one_own_address_offers_its_address_to_copy_and_has_no_classic_menu(string target)
    {
        var menu = Menu([Own(target)]);

        Assert.Equal([QuickCommand.Rename, QuickCommand.Delete], Row(menu));
        Assert.Equal([FileCommand.Open, FileCommand.CopyUrl, FileCommand.IconProperties, Sep, FileCommand.EditMode, FileCommand.LockWidgets], List(menu));
        Assert.Empty(menu.Classic);
    }

    [Fact]
    public void S5582_one_own_location_offers_its_path()
        => Assert.Equal(
            [FileCommand.Open, FileCommand.CopyPath, FileCommand.IconProperties, Sep, FileCommand.EditMode, FileCommand.LockWidgets],
            List(Menu([Own("shell:Downloads")])));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void S5582_an_own_recycle_bin_offers_emptying_it_only_while_it_can_be_emptied(bool canEmpty)
    {
        var menu = Menu([Own("shell:RecycleBinFolder")], new DeskMenuFacts(canEmpty, !canEmpty, false));

        Assert.Equal([FileCommand.Open, FileCommand.EmptyRecycleBin, FileCommand.CopyPath, FileCommand.IconProperties, Sep, FileCommand.EditMode, FileCommand.LockWidgets], List(menu));
        Assert.Equal(canEmpty, Enabled(menu, FileCommand.EmptyRecycleBin));
        Assert.Empty(menu.Classic);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void S5582_an_own_layout_opens_as_a_layout_and_offers_its_editor(bool assigned, bool canEdit)
    {
        var menu = Menu([Layout(assigned ? App : null)], new DeskMenuFacts(true, canEdit, false));

        Assert.Equal([QuickCommand.Rename, QuickCommand.Delete], Row(menu));
        Assert.Equal([FileCommand.OpenLayout, FileCommand.EditLayout, FileCommand.IconProperties, Sep, FileCommand.EditMode, FileCommand.LockWidgets], List(menu));
        Assert.Equal(assigned, Enabled(menu, FileCommand.OpenLayout));
        Assert.Equal(canEdit, Enabled(menu, FileCommand.EditLayout));
    }

    [Theory]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    public void S5582_an_own_icon_that_opens_nothing_has_open_off(string target)
    {
        var menu = Menu([Own(target)]);

        Assert.Equal([QuickCommand.Rename, QuickCommand.Delete], Row(menu));
        Assert.Equal([FileCommand.Open, FileCommand.IconProperties, Sep, FileCommand.EditMode, FileCommand.LockWidgets], List(menu));
        Assert.False(Enabled(menu, FileCommand.Open));
    }

    [Fact]
    public void S5582_several_own_icons_open_and_delete_and_copy_paths_only_when_every_one_has_one()
    {
        var paths = Menu([Own(App, "a"), Own("shell:Downloads", "b"), Own("shell:RecycleBinFolder", "c")]);
        var withWeb = Menu([Own(App, "a"), Own("https://example.com/page", "b")]);
        var nothingOpens = Menu([Waiting("a"), Layout(null, "b")]);

        // One row command is no row ([S-1741]): Delete stays a labelled line.
        Assert.Empty(Row(paths));
        Assert.Equal([FileCommand.Open, FileCommand.CopyPath, Sep, FileCommand.Delete, Sep, FileCommand.EditMode, FileCommand.LockWidgets], List(paths));
        Assert.Equal([FileCommand.Open, Sep, FileCommand.Delete, Sep, FileCommand.EditMode, FileCommand.LockWidgets], List(withWeb));
        Assert.Equal([FileCommand.Open, Sep, FileCommand.Delete, Sep, FileCommand.EditMode, FileCommand.LockWidgets], List(nothingOpens));
        Assert.False(Enabled(nothingOpens, FileCommand.Open));
        Assert.Empty(paths.Classic);
    }

    [Fact]
    public void S5582_a_mixed_selection_opens_and_copies_paths_but_never_deletes_or_renames()
    {
        var paths = Menu([Item("a.txt"), Own(App)]);
        var withShellItem = Menu([Shell(ThisPcItem), Own(App)]);
        var withWeb = Menu([Item("a.txt"), Own("https://example.com/page")]);
        var withWaiting = Menu([Item("a.txt"), Waiting()]);

        Assert.Equal([FileCommand.Open, FileCommand.CopyPath, Sep, FileCommand.EditMode, FileCommand.LockWidgets], List(paths));
        Assert.Equal([FileCommand.Open, Sep, FileCommand.EditMode, FileCommand.LockWidgets], List(withShellItem));
        Assert.Equal([FileCommand.Open, Sep, FileCommand.EditMode, FileCommand.LockWidgets], List(withWeb));
        Assert.Equal([FileCommand.Open, Sep, FileCommand.EditMode, FileCommand.LockWidgets], List(withWaiting));
        foreach (var menu in new[] { paths, withShellItem, withWeb, withWaiting })
        {
            Assert.Empty(Row(menu));
            Assert.Empty(menu.Classic);
        }
    }

    [Fact]
    public void S5582_every_menu_ends_its_groups_cleanly_and_show_more_options_is_there_exactly_when_the_shell_has_a_menu()
    {
        DeskIcon[][] selections =
        [
            [Item("a.txt")], [Item("Themia.lnk")], [Item("Docs")], [Item("a.txt"), Item("b.txt")], [Shell(ThisPcItem)],
            [Shell(DesktopCommands.RecycleBinItem)], [Own(App)], [Own(App, there: false)], [Own("https://example.com/")],
            [Layout(App)], [Waiting()], [Own(App, "a"), Own(App, "b")], [Item("a.txt"), Own(App)],
        ];

        foreach (var selection in selections)
        {
            var menu = Menu(selection, null, "Docs");
            var list = List(menu);

            Assert.NotEqual(Sep, list[0]);
            Assert.NotEqual(Sep, list[^1]);
            Assert.DoesNotContain(Enumerable.Range(1, list.Length - 1), i => list[i] == Sep && list[i - 1] == Sep);
            Assert.Equal(menu.Classic.Count > 0, menu.Lines.Any(l => l.Command == FileCommand.ShowMoreOptions));
            Assert.Equal(menu.Classic.Count > 0 ? FileCommand.ShowMoreOptions : FileCommand.LockWidgets, list[^1]);

            // [S-65] The way into edit mode is on every desktop icon menu, once.
            Assert.Single(menu.Lines, l => l.Command == FileCommand.EditMode);

            // [S-12951] Lock widgets right under it, once.
            Assert.Single(menu.Lines, l => l.Command == FileCommand.LockWidgets);
            Assert.Equal(FileCommand.LockWidgets, list[Array.IndexOf(list, FileCommand.EditMode) + 1]);
        }
    }

    [Fact]
    public void S5582_the_keys_beside_the_lines_are_only_ones_the_desktop_answers()
    {
        // [S-5545] Enter, Ctrl+X/C, F2, Delete, Alt+Enter and Shift+F10; the desktop's Ctrl+V pastes onto
        // the desktop, never into a folder icon, and it has no Ctrl+Shift+C.
        Assert.Equal(KeyChord.Plain(ChordKey.Enter), FileMenu.Accelerator(FileCommand.Open, FileMenuPlace.Desktop));
        Assert.Equal(KeyChord.Plain(ChordKey.Enter), FileMenu.Accelerator(FileCommand.OpenLayout, FileMenuPlace.Desktop));
        Assert.Equal(KeyChord.Ctrl(ChordKey.X), FileMenu.Accelerator(FileCommand.Cut, FileMenuPlace.Desktop));
        Assert.Equal(KeyChord.Ctrl(ChordKey.C), FileMenu.Accelerator(FileCommand.Copy, FileMenuPlace.Desktop));
        Assert.Equal(KeyChord.Plain(ChordKey.F2), FileMenu.Accelerator(FileCommand.Rename, FileMenuPlace.Desktop));
        Assert.Equal(KeyChord.Plain(ChordKey.Delete), FileMenu.Accelerator(FileCommand.Delete, FileMenuPlace.Desktop));
        Assert.Equal(KeyChord.Alt(ChordKey.Enter), FileMenu.Accelerator(FileCommand.Properties, FileMenuPlace.Desktop));
        Assert.Equal(KeyChord.Alt(ChordKey.Enter), FileMenu.Accelerator(FileCommand.IconProperties, FileMenuPlace.Desktop));
        Assert.Equal(KeyChord.Shift(ChordKey.F10), FileMenu.Accelerator(FileCommand.ShowMoreOptions, FileMenuPlace.Desktop));
        Assert.Null(FileMenu.Accelerator(FileCommand.Paste, FileMenuPlace.Desktop));
        Assert.Null(FileMenu.Accelerator(FileCommand.CopyAsPath, FileMenuPlace.Desktop));
        Assert.Null(FileMenu.Accelerator(FileCommand.EditMode, FileMenuPlace.Desktop));
    }

    [Fact]
    public void S6416_copy_as_path_quotes_each_path_on_its_own_line()
        => Assert.Equal("\"C:\\a b.txt\"\r\n\"C:\\c\"", FileMenu.QuotedPaths([@"C:\a b.txt", @"C:\c"]));

    [Fact]
    public void S5582_copy_text_is_each_path_or_address_on_its_own_line()
    {
        var text = DesktopCommands.CopyText([Item("a.txt"), Own(App, "a", there: false), Own("https://example.com/page", "b"), Waiting(), Layout(App), Own("shell:Downloads", "c"), Own("ms-settings:display", "d")]);

        Assert.Equal(
            string.Join(Environment.NewLine, Desk + "a.txt", App, LaunchTargets.Classify("https://example.com/page").Value, "shell:Downloads", "ms-settings:display"),
            text);
        Assert.Equal(string.Empty, DesktopCommands.CopyText([Waiting(), Own("javascript:alert(1)", "bad")]));
        Assert.Equal(string.Empty, DesktopCommands.CopyText([]));
    }

    // ---- [S-5583] Delete ------------------------------------------------------------------------

    [Fact]
    public void S5583_delete_hands_the_items_files_to_the_shell_and_removes_the_own_icons_widgets()
    {
        var delete = DesktopCommands.Delete([Item("a.txt"), Own(App, "app"), Shell(ThisPcItem), Waiting("w"), Item("b.txt"), Layout(App)]);

        Assert.Equal([Desk + "a.txt", Desk + "b.txt"], delete.Files);
        Assert.Equal(["*icon:app", "*icon:w", "*icon:lay"], delete.OwnKeys);
    }

    [Fact]
    public void S5583_an_own_icons_target_is_never_among_the_files_to_delete()
    {
        var delete = DesktopCommands.Delete([Own(App, "app", there: true), Own("shell:RecycleBinFolder", "bin")]);

        Assert.Empty(delete.Files);
        Assert.Equal(["*icon:app", "*icon:bin"], delete.OwnKeys);
        Assert.Equal((0, 0), (DesktopCommands.Delete([Shell(ThisPcItem)]).Files.Count, DesktopCommands.Delete([Shell(ThisPcItem)]).OwnKeys.Count));
    }

    // ---- [S-5584] F2 ----------------------------------------------------------------------------

    [Fact]
    public void S5584_f2_renames_an_items_file_an_own_icons_label_and_no_shell_item()
    {
        Assert.Equal(DeskRename.None, DesktopCommands.Rename(null));
        Assert.Equal(DeskRename.None, DesktopCommands.Rename(Shell(ThisPcItem)));
        Assert.Equal(DeskRename.Shell, DesktopCommands.Rename(Item("a.txt")));
        foreach (var own in new[] { Own(App), Own("https://example.com/page"), Own("shell:RecycleBinFolder"), Waiting(), Layout(null), Own("javascript:alert(1)") })
        {
            Assert.Equal(DeskRename.Label, DesktopCommands.Rename(own));
        }
    }

    // ---- [S-5585] the clipboard -----------------------------------------------------------------

    [Fact]
    public void S5585_copy_takes_the_items_files_and_the_own_paths_that_are_there()
    {
        var copy = DesktopCommands.Clipboard(
            [Item("a.txt"), Own(App, "app"), Own(@"C:\gone.txt", "gone", there: false), Shell(ThisPcItem), Own("https://example.com/page", "web"), Own("shell:Downloads", "loc"), Waiting(), Layout(App)],
            cut: false);

        Assert.False(copy.Cut);
        Assert.Equal([Desk + "a.txt", App], copy.Paths);
    }

    [Fact]
    public void S5585_cut_takes_the_items_files_only_never_an_own_icons_target()
    {
        var cut = DesktopCommands.Clipboard([Item("a.txt"), Own(App, "app"), Shell(ThisPcItem), Item("b.txt")], cut: true);

        Assert.True(cut.Cut);
        Assert.Equal([Desk + "a.txt", Desk + "b.txt"], cut.Paths);
        Assert.Empty(DesktopCommands.Clipboard([Own(App)], cut: true).Paths);
    }

    [Fact]
    public void S5585_a_file_selected_as_an_item_and_as_an_own_target_is_copied_once()
    {
        var copy = DesktopCommands.Clipboard([Item("a.txt"), Own((Desk + "A.TXT").ToUpperInvariant(), "same")], cut: false);

        Assert.Equal([Desk + "a.txt"], copy.Paths);
    }

    [Fact]
    public void S5585_a_network_target_is_never_copied_as_a_file()
        => Assert.Empty(DesktopCommands.Clipboard([Own(Share)], cut: false).Paths);

    // ---- [S-5586] what a drag carries -----------------------------------------------------------

    [Fact]
    public void S5586_a_drag_of_items_carries_their_files_never_a_shell_item_and_allows_every_effect()
    {
        var drag = DesktopCommands.Drag([Item("a.txt"), Shell(ThisPcItem), Item("b.txt")]);

        Assert.Equal([Desk + "a.txt", Desk + "b.txt"], drag.Paths);
        Assert.Equal(DropEffect.Copy | DropEffect.Move | DropEffect.Link, drag.Allowed);
        Assert.Equal(drag.Allowed, DesktopCommands.DragEffects(drag));
    }

    [Fact]
    public void S5586_a_drag_of_shell_items_alone_carries_nothing_and_starts_as_a_move()
    {
        var drag = DesktopCommands.Drag([Shell(ThisPcItem), Shell(DesktopCommands.RecycleBinItem)]);

        Assert.Empty(drag.Paths);
        Assert.Equal(DropEffect.None, drag.Allowed);
        Assert.Equal(DropEffect.Move, DesktopCommands.DragEffects(drag));
    }

    [Fact]
    public void S5586_with_an_own_icons_path_among_them_the_drag_never_allows_a_move()
    {
        var mixed = DesktopCommands.Drag([Item("a.txt"), Own(App, "app"), Own("https://example.com/page", "web")]);
        var ownOnly = DesktopCommands.Drag([Own(App, "app")]);

        Assert.Equal([Desk + "a.txt", App], mixed.Paths);
        Assert.Equal(DropEffect.Copy | DropEffect.Link, mixed.Allowed);
        Assert.Equal(DropEffect.Copy | DropEffect.Link, DesktopCommands.DragEffects(mixed));
        Assert.Equal([App], ownOnly.Paths);
        Assert.Equal(DropEffect.Copy | DropEffect.Link, ownOnly.Allowed);
    }

    [Fact]
    public void S5586_own_icons_that_carry_no_file_leave_the_items_drag_as_it_is()
    {
        var drag = DesktopCommands.Drag([Item("a.txt"), Own(@"C:\gone.txt", "gone", there: false), Own(Share, "net"), Waiting(), Layout(App)]);

        Assert.Equal([Desk + "a.txt"], drag.Paths);
        Assert.Equal(DropEffect.Copy | DropEffect.Move | DropEffect.Link, drag.Allowed);
    }

    [Fact]
    public void S5586_a_drag_with_no_file_carries_nothing_allows_others_nothing_and_starts_as_a_move()
    {
        var drag = DesktopCommands.Drag([Own("https://example.com/page", "web"), Own("ms-settings:display", "set"), Own("shell:Downloads", "loc"), Own("shell:RecycleBinFolder", "bin"), Waiting(), Layout(App), Own(@"C:\gone.txt", "gone", there: false)]);

        Assert.Empty(drag.Paths);
        Assert.Equal(DropEffect.None, drag.Allowed);
        Assert.Equal(DropEffect.Move, DesktopCommands.DragEffects(drag));
        Assert.Equal(DropEffect.None, DesktopCommands.Drag([]).Allowed);
    }

    [Fact]
    public void S5586_a_file_dragged_as_an_item_and_as_an_own_target_is_carried_once()
    {
        var drag = DesktopCommands.Drag([Item("a.txt"), Own((Desk + "a.txt").ToUpperInvariant(), "same")]);

        Assert.Equal([Desk + "a.txt"], drag.Paths);
        Assert.Equal(DropEffect.Copy | DropEffect.Link, drag.Allowed);
    }

    [Theory]
    [InlineData(DropEffect.Copy | DropEffect.Move | DropEffect.Link, DropEffect.Move)]
    [InlineData(DropEffect.Move, DropEffect.Move)]
    [InlineData(DropEffect.Copy | DropEffect.Link, DropEffect.Link)]
    [InlineData(DropEffect.Link, DropEffect.Link)]
    [InlineData(DropEffect.Copy, DropEffect.Copy)]
    [InlineData(DropEffect.None, DropEffect.None)]
    public void S5546_the_desktop_answers_its_own_drag_with_a_move_else_the_first_effect_allowed(DropEffect allowed, DropEffect answer)
        => Assert.Equal(answer, DesktopCommands.MoveEffect(allowed));

    [Theory]
    [InlineData(true, false, SurfaceHit.Empty, false, true)]
    [InlineData(true, false, SurfaceHit.Empty, true, true)]
    [InlineData(true, false, SurfaceHit.Widget, true, true)]
    [InlineData(true, false, SurfaceHit.Widget, false, false)]
    [InlineData(true, false, SurfaceHit.Chrome, false, false)]
    [InlineData(true, false, SurfaceHit.Chrome, true, false)]
    [InlineData(true, true, SurfaceHit.Empty, false, false)]
    [InlineData(true, true, SurfaceHit.Widget, true, false)]
    [InlineData(false, false, SurfaceHit.Empty, false, false)]
    [InlineData(false, false, SurfaceHit.Widget, true, false)]
    public void S5546_the_desktops_own_drag_moves_its_icons_over_empty_desktop_or_one_of_them_outside_edit_mode(bool deskDrag, bool editMode, SurfaceHit hit, bool overDragged, bool moves)
        => Assert.Equal(moves, DesktopCommands.MovesIcons(deskDrag, editMode, hit, overDragged));

    // ---- [S-5587] what a drop onto an icon is forwarded to --------------------------------------

    [Fact]
    public void S5587_a_drop_onto_a_desktop_item_goes_to_the_item_the_recycle_bin_included()
    {
        Assert.Equal(Desk + "Folder", DesktopCommands.DropTarget(Item("Folder")));
        Assert.Equal(DesktopCommands.RecycleBinItem, DesktopCommands.DropTarget(Shell(DesktopCommands.RecycleBinItem)));
        Assert.Equal(ThisPcItem, DesktopCommands.DropTarget(Shell(ThisPcItem)));
        Assert.Equal("::{645FF040-5081-101B-9F08-00AA002F954E}", DesktopCommands.RecycleBinItem);
    }

    [Fact]
    public void S5587_a_drop_onto_an_own_path_goes_to_that_path_and_onto_an_own_bin_to_the_recycle_bin()
    {
        Assert.Equal(App, DesktopCommands.DropTarget(Own(App)));
        Assert.Equal(App, DesktopCommands.DropTarget(Own(App, there: false)));
        Assert.Equal(DesktopCommands.RecycleBinItem, DesktopCommands.DropTarget(Own("shell:RecycleBinFolder")));
    }

    [Theory]
    [InlineData(@"\\server\share\x")]
    [InlineData("//server/share/x")]
    [InlineData("https://example.com/page")]
    [InlineData("ms-settings:display")]
    [InlineData("shell:Downloads")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    public void S5587_a_drop_onto_anything_else_is_forwarded_nowhere(string target)
        => Assert.Null(DesktopCommands.DropTarget(Own(target)));

    [Fact]
    public void S5587_a_layout_takes_no_drop()
        => Assert.Null(DesktopCommands.DropTarget(Layout(App)));
}
