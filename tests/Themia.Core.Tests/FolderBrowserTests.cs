using System.Buffers.Binary;
using System.Text;
using Themia.Core.Files;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The folder widget's file browser rules ([S-1320]..[S-1339]); no test touches a real file.</summary>
public sealed class FolderBrowserTests
{
    private static FolderEntry File(string name, bool dir = false, int attributes = 0, long size = 0)
        => new(name, $@"C:\Root\{name}", dir, size, new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero), false, attributes);

    // ---- config ---------------------------------------------------------------------------------

    [Fact]
    public void S1320_defaults_match_the_original()
    {
        var config = new FolderConfig();

        Assert.Equal(FolderStyle.List, config.Style);
        Assert.Equal(FolderItemSize.Medium, config.ItemSize);
        Assert.Equal(FolderNavigation.Explorer, config.Navigation);
        Assert.Equal(ItemClickMode.Double, config.ClickMode);
        Assert.False(config.ShowExtensions);
        Assert.Equal([FolderColumn.Size, FolderColumn.Modified, FolderColumn.Type], config.Columns);
        Assert.Equal(FolderTileSize.Medium, config.TileSize);
    }

    [Fact]
    public void S1320_repair_drops_unknown_and_repeated_columns_and_says_so()
    {
        var config = new FolderConfig { Style = FolderStyle.Details, Columns = [FolderColumn.Git, (FolderColumn)42, FolderColumn.Git, FolderColumn.Size] };

        var notes = config.Repair();

        Assert.Equal([FolderColumn.Git, FolderColumn.Size], config.Columns);
        Assert.Single(notes);
    }

    [Fact]
    public void S1320_clone_does_not_share_the_column_list()
    {
        var config = new FolderConfig { Style = FolderStyle.Details };
        var copy = (FolderConfig)config.Clone();

        copy.Columns.Add(FolderColumn.Git);

        Assert.DoesNotContain(FolderColumn.Git, config.Columns);
    }

    // ---- columns --------------------------------------------------------------------------------

    [Fact]
    public void S1322_columns_fit_in_the_users_order_and_the_first_miss_closes_the_row()
    {
        IReadOnlyList<FolderColumn> chosen = [FolderColumn.Type, FolderColumn.Size, FolderColumn.Cloud];

        Assert.Empty(FolderColumns.Fit(chosen, 200));
        Assert.Equal([FolderColumn.Type], FolderColumns.Fit(chosen, FolderColumns.NameMinWidth + 104));

        // Cloud (24 + 8) would fit after Type alone, but Size came first and did not.
        Assert.Equal([FolderColumn.Type], FolderColumns.Fit(chosen, FolderColumns.NameMinWidth + 104 + 40));
        Assert.Equal(chosen, FolderColumns.Fit(chosen, 1000));
    }

    [Fact]
    public void S1322_toggling_a_column_back_on_restores_its_place()
    {
        List<FolderColumn> chosen = [FolderColumn.Size, FolderColumn.Type];

        Assert.Equal([FolderColumn.Size, FolderColumn.Modified, FolderColumn.Type], FolderColumns.Toggle(chosen, FolderColumn.Modified));
        Assert.Equal([FolderColumn.Type], FolderColumns.Toggle(chosen, FolderColumn.Size));
        Assert.Equal([FolderColumn.Size, FolderColumn.Type, FolderColumn.ItemCount], FolderColumns.Toggle(chosen, FolderColumn.ItemCount));
    }

    // ---- filter (fileFilter.test.ts) ------------------------------------------------------------

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("", new string[0])]
    [InlineData(" , ,", new string[0])]
    [InlineData("Report, .PDF ,", new[] { "report", ".pdf" })]
    public void S1321_keywords_are_trimmed_lowercased_and_empties_dropped(string? raw, string[] expected)
        => Assert.Equal(expected, FolderFilter.Keywords(raw));

    [Fact]
    public void S1321_a_name_matches_any_keyword_case_insensitively()
    {
        Assert.True(FolderFilter.Matches("Q3 Report.PDF", ["report"]));
        Assert.True(FolderFilter.Matches("photo.jpg", ["pdf", "jpg"]));
        Assert.False(FolderFilter.Matches("notes.txt", ["pdf"]));
        Assert.True(FolderFilter.Matches("anything", []));
    }

    [Fact]
    public void S1321_the_search_keeps_names_matching_any_keyword()
    {
        FolderEntry[] entries = [File("a report.pdf"), File("b report.docx"), File("c.txt")];

        Assert.Equal(["a report.pdf", "b report.docx"], FolderFilter.Apply(entries, "report").Select(e => e.Name));
        Assert.Equal(["a report.pdf", "c.txt"], FolderFilter.Apply(entries, "pdf, txt").Select(e => e.Name));
        Assert.Same(entries, FolderFilter.Apply(entries, "  "));
    }

    // ---- names ----------------------------------------------------------------------------------

    [Theory]
    [InlineData("report.docx", false, false, "report")]
    [InlineData("report.docx", false, true, "report.docx")]
    [InlineData(".gitignore", false, false, ".gitignore")]
    [InlineData("archive.tar.gz", false, false, "archive.tar")]
    [InlineData("v1.2", true, false, "v1.2")]
    public void S1321_extensions_are_hidden_from_files_only(string name, bool dir, bool show, string expected)
        => Assert.Equal(expected, FileNames.Display(name, dir, show));

    [Fact]
    public void S1331_a_rename_with_hidden_extensions_keeps_the_extension()
    {
        Assert.Equal("final.docx", FileNames.FromEdited("draft.docx", " final ", false, false));
        Assert.Equal("final.txt", FileNames.FromEdited("draft.docx", "final.txt", false, true));
        Assert.Equal("Archive", FileNames.FromEdited("Old.d", "Archive", true, false));
    }

    [Theory]
    [InlineData("ok name.txt", FileNameProblem.None)]
    [InlineData("  ", FileNameProblem.Empty)]
    [InlineData("..", FileNameProblem.DotSegment)]
    [InlineData("a:b", FileNameProblem.InvalidCharacter)]
    [InlineData("tab\there", FileNameProblem.InvalidCharacter)]
    [InlineData("CON", FileNameProblem.Reserved)]
    [InlineData("nul.txt", FileNameProblem.Reserved)]
    [InlineData("name.", FileNameProblem.TrailingDotOrSpace)]
    public void S1331_windows_name_rules(string name, FileNameProblem expected)
        => Assert.Equal(expected, FileNames.Validate(name));

    [Fact]
    public void S1331_the_next_free_name_numbers_like_explorer()
    {
        Assert.Equal("New folder", FileNames.Unique("New folder", ["Other"]));
        Assert.Equal("New folder (3)", FileNames.Unique("New folder", ["new folder", "New folder (2)"]));
        Assert.Equal("New Text Document (2).txt", FileNames.Unique("New Text Document.txt", ["New Text Document.txt"]));
    }

    // ---- selection ------------------------------------------------------------------------------

    private static FolderSelection Selection(int count)
    {
        var selection = new FolderSelection();
        selection.SetOrder([.. Enumerable.Range(0, count).Select(i => $@"C:\Root\{i}")]);
        return selection;
    }

    [Fact]
    public void S1327_click_ctrl_and_shift_select_as_explorer()
    {
        var s = Selection(6);

        s.Click(1, ctrl: false, shift: false);
        s.Click(3, ctrl: false, shift: true);
        Assert.Equal([@"C:\Root\1", @"C:\Root\2", @"C:\Root\3"], s.Selected);

        s.Click(5, ctrl: true, shift: false);
        Assert.Equal(4, s.Count);

        s.Click(2, ctrl: true, shift: false);
        Assert.False(s.IsSelected(@"C:\Root\2"));

        s.Click(0, ctrl: false, shift: false);
        Assert.Equal([@"C:\Root\0"], s.Selected);
    }

    [Fact]
    public void S1327_ctrl_shift_adds_the_range_to_the_selection()
    {
        var s = Selection(6);

        s.Click(0, false, false);
        s.Click(3, ctrl: true, shift: false);
        s.Click(5, ctrl: true, shift: true);

        Assert.Equal([@"C:\Root\0", @"C:\Root\3", @"C:\Root\4", @"C:\Root\5"], s.Selected);
    }

    [Fact]
    public void S1327_grid_keys_jump_by_columns_and_clamp_at_the_edges()
    {
        var s = Selection(10);

        Assert.True(s.Move(FolderMove.Down, columns: 4, pageRows: 2, extend: false));
        Assert.Equal(0, s.FocusIndex); // the first key lands on the first item

        s.Move(FolderMove.Down, 4, 2, false);
        Assert.Equal(4, s.FocusIndex);

        s.Move(FolderMove.Right, 4, 2, false);
        Assert.Equal(5, s.FocusIndex);

        // Down from the second row to a third row that only reaches index 9: Explorer goes to the last.
        s.Move(FolderMove.Down, 4, 2, false);
        Assert.Equal(9, s.FocusIndex);

        s.Move(FolderMove.Down, 4, 2, false);
        Assert.Equal(9, s.FocusIndex);

        s.Move(FolderMove.PageUp, 4, 2, false);
        Assert.Equal(1, s.FocusIndex);

        s.Move(FolderMove.Up, 4, 2, false);
        Assert.Equal(1, s.FocusIndex);
    }

    [Fact]
    public void S1327_left_and_right_do_nothing_in_a_list()
    {
        var s = Selection(3);
        s.Click(1, false, false);

        s.Move(FolderMove.Right, columns: 1, pageRows: 5, extend: false);

        Assert.Equal(1, s.FocusIndex);
    }

    [Fact]
    public void S1327_shift_keys_extend_from_the_anchor()
    {
        var s = Selection(6);
        s.Click(2, false, false);

        s.Move(FolderMove.Down, 1, 5, extend: true);
        s.Move(FolderMove.Down, 1, 5, extend: true);

        Assert.Equal([@"C:\Root\2", @"C:\Root\3", @"C:\Root\4"], s.Selected);

        s.Move(FolderMove.Home, 1, 5, extend: true);
        Assert.Equal([@"C:\Root\0", @"C:\Root\1", @"C:\Root\2"], s.Selected);
    }

    [Fact]
    public void S1327_a_reread_keeps_the_selection_of_items_still_there()
    {
        var s = Selection(4);
        s.Click(1, false, false);
        s.Click(3, ctrl: true, shift: false);

        s.SetOrder([@"C:\Root\0", @"C:\ROOT\1", @"C:\Root\2"]);

        Assert.Equal([@"C:\ROOT\1"], s.Selected);
        Assert.Null(s.Focus);
    }

    [Fact]
    public void S1327_right_click_keeps_a_selection_it_lands_in()
    {
        var s = Selection(4);
        s.Click(0, false, false);
        s.Click(2, false, true);

        s.Context(1);
        Assert.Equal(3, s.Count);

        s.Context(3);
        Assert.Equal([@"C:\Root\3"], s.Selected);
    }

    [Fact]
    public void S1328_an_additive_band_keeps_what_was_selected()
    {
        var s = Selection(5);
        s.Click(0, false, false);

        s.Band([2, 3], before: [@"C:\Root\0"], BandMode.Add);
        Assert.Equal([@"C:\Root\0", @"C:\Root\2", @"C:\Root\3"], s.Selected);

        s.Band([3], before: null, BandMode.Replace);
        Assert.Equal([@"C:\Root\3"], s.Selected);
    }

    // ---- gesture (gesture.test.ts, #58) ---------------------------------------------------------

    [Fact]
    public void S1328_movement_inside_the_dead_zone_stays_a_click()
    {
        var g = new FolderGesture();
        g.BeginPress(10, 10, FolderPress.Item, [@"C:\a"]);

        Assert.Equal(GestureStep.Idle, g.Move(13, 13));
        Assert.Equal((false, false), g.Release());
    }

    [Fact]
    public void S1328_a_press_on_an_item_becomes_a_drag_once_and_suppresses_the_click()
    {
        var g = new FolderGesture();
        g.BeginPress(10, 10, FolderPress.Item, [@"C:\a"]);

        Assert.Equal(GestureStep.StartDrag, g.Move(20, 10));
        Assert.Equal([@"C:\a"], g.DragPaths);
        Assert.Equal(GestureStep.Idle, g.Move(40, 10));
        Assert.Equal((true, false), g.Release());
    }

    [Fact]
    public void S1328_a_band_once_started_stays_a_band()
    {
        var g = new FolderGesture();
        g.BeginPress(0, 0, FolderPress.Background);

        Assert.Equal(GestureStep.Band, g.Move(10, 10));
        Assert.Equal(GestureStep.Band, g.Move(1, 1));
        Assert.Equal(new BandRect(0, 0, 1, 1), g.BandRect);
        Assert.Equal((false, true), g.Release());
    }

    [Fact]
    public void S1328_a_stale_drag_origin_never_leaks_into_the_next_press()
    {
        // #58: an aborted gesture left its origin; the next press must measure from its own.
        var g = new FolderGesture();
        g.BeginPress(0, 0, FolderPress.Item, [@"C:\a"]);
        var stale = g.Generation;

        g.BeginPress(500, 500, FolderPress.Item, [@"C:\b"]);

        Assert.NotEqual(stale, g.Generation);
        Assert.Equal(GestureStep.Idle, g.Move(502, 502));
        Assert.False(g.DragWasActive);
    }

    [Fact]
    public void S1328_release_resets_and_moves_after_it_do_nothing()
    {
        var g = new FolderGesture();
        g.BeginPress(0, 0, FolderPress.Background);
        var generation = g.Generation;

        g.Release();

        Assert.False(g.Pressed);
        Assert.NotEqual(generation, g.Generation);
        Assert.Equal(GestureStep.Idle, g.Move(100, 100));
    }

    [Fact]
    public void S1328_band_hits_overlapping_boxes_only()
    {
        var band = new BandRect(50, 50, 10, 10);

        Assert.True(band.Hits(0, 0, 20, 20));
        Assert.True(band.Hits(45, 45, 100, 100));
        Assert.False(band.Hits(60, 0, 20, 20));
    }

    // ---- drops (dropTarget.test.ts) -------------------------------------------------------------

    [Fact]
    public void S1332_paths_compare_across_separators_case_and_trailing_slashes()
    {
        Assert.True(FileDrops.SamePath("D:/Docs/", @"d:\docs"));
        Assert.Equal(@"d:\docs", FileDrops.Parent(@"D:\Docs\a.txt"));
        Assert.Equal(@"c:\", FileDrops.Parent(@"C:\x"));
        Assert.True(FileDrops.IsWithin(@"C:\A\B", @"C:\A"));
        Assert.False(FileDrops.IsWithin(@"C:\AB", @"C:\A"));
    }

    [Fact]
    public void S1332_a_same_folder_move_and_a_folder_into_itself_are_dropped()
    {
        string[] sources = [@"C:\Dest\a.txt", @"C:\Other\b.txt", @"C:\Dest", @"C:\Up"];

        Assert.Equal([@"C:\Other\b.txt"], FileDrops.Pastable(sources, @"C:\Dest", DropEffect.Move).Where(s => s != @"C:\Up"));
        Assert.DoesNotContain(@"C:\Up", FileDrops.Pastable([@"C:\Up"], @"C:\Up\Down", DropEffect.Move));
    }

    [Fact]
    public void S1332_a_same_folder_copy_is_kept_so_the_shell_makes_a_copy()
        => Assert.Equal([@"C:\Dest\a.txt"], FileDrops.Pastable([@"C:\Dest\a.txt"], @"C:\Dest", DropEffect.Copy));

    [Theory]
    [InlineData(false, false, DropEffect.Copy | DropEffect.Move | DropEffect.Link, DropEffect.Move)]
    [InlineData(true, false, DropEffect.Copy | DropEffect.Move, DropEffect.Copy)]
    [InlineData(true, true, DropEffect.Copy | DropEffect.Move | DropEffect.Link, DropEffect.Link)]
    [InlineData(false, false, DropEffect.Copy, DropEffect.Copy)]
    [InlineData(true, false, DropEffect.Move, DropEffect.Move)]
    [InlineData(false, false, DropEffect.None, DropEffect.None)]
    public void S1332_plain_drop_moves_ctrl_copies(bool ctrl, bool shift, DropEffect allowed, DropEffect expected)
        => Assert.Equal(expected, FileDrops.Choose(ctrl, shift, allowed));

    // ---- clipboard payloads ---------------------------------------------------------------------

    [Fact]
    public void S1329_cf_hdrop_round_trips()
    {
        string[] paths = [@"C:\a b\ü.txt", @"\\server\share\x"];

        var bytes = DropFiles.Encode(paths);

        Assert.Equal(DropFiles.HeaderSize, BinaryPrimitives.ReadInt32LittleEndian(bytes));
        Assert.Equal(paths, DropFiles.Decode(bytes));
    }

    [Fact]
    public void S1329_a_malformed_hdrop_yields_only_whole_paths()
    {
        var bytes = DropFiles.Encode([@"C:\one", @"C:\two"]);

        Assert.Empty(DropFiles.Decode(bytes.AsSpan(0, 10)));
        Assert.Equal([@"C:\one"], DropFiles.Decode(bytes.AsSpan(0, bytes.Length - 6)));

        var bad = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(bad, 9999);
        Assert.Empty(DropFiles.Decode(bad));
    }

    [Fact]
    public void S1329_ansi_hdrop_is_read()
    {
        var list = Encoding.Latin1.GetBytes("C:\\a\0C:\\b\0\0");
        var bytes = new byte[DropFiles.HeaderSize + list.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, DropFiles.HeaderSize);
        list.CopyTo(bytes, DropFiles.HeaderSize);

        Assert.Equal([@"C:\a", @"C:\b"], DropFiles.Decode(bytes));
    }

    [Theory]
    [InlineData(DropEffect.Move, DropEffect.Move)]
    [InlineData(DropEffect.Copy | DropEffect.Move, DropEffect.Move)]
    [InlineData(DropEffect.Copy, DropEffect.Copy)]
    [InlineData(DropEffect.Copy | DropEffect.Link, DropEffect.Copy)]
    public void S1329_preferred_drop_effect_says_cut_or_copy(DropEffect written, DropEffect read)
        => Assert.Equal(read, DropFiles.DecodeEffect(DropFiles.EncodeEffect(written)));

    [Fact]
    public void S1329_a_short_effect_payload_means_copy()
        => Assert.Equal(DropEffect.Copy, DropFiles.DecodeEffect([2, 0]));

    // ---- virtual files --------------------------------------------------------------------------

    private static byte[] Descriptors(uint claimed, params (string Name, uint Flags, uint Attributes, long Size)[] files)
    {
        var bytes = new byte[4 + (files.Length * FileDescriptors.DescriptorSize)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, claimed);

        for (var i = 0; i < files.Length; i++)
        {
            var d = bytes.AsSpan(4 + (i * FileDescriptors.DescriptorSize));
            BinaryPrimitives.WriteUInt32LittleEndian(d, files[i].Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(d[36..], files[i].Attributes);
            BinaryPrimitives.WriteUInt32LittleEndian(d[64..], (uint)(files[i].Size >> 32));
            BinaryPrimitives.WriteUInt32LittleEndian(d[68..], (uint)files[i].Size);
            Encoding.Unicode.GetBytes(files[i].Name).CopyTo(d[72..]);
        }

        return bytes;
    }

    [Fact]
    public void S1332_virtual_file_descriptors_use_the_sdk_layout()
    {
        var bytes = Descriptors(2, ("Invoice.pdf", 0x40, 0, 5_000_000_000), (@"Attachments\Logo.png", 0, 0, 0));

        var files = FileDescriptors.Parse(bytes);

        Assert.Equal(2, files.Count);
        Assert.Equal(new VirtualFile("Invoice.pdf", false, 5_000_000_000), files[0].File);
        Assert.Equal((1, new VirtualFile(@"Attachments\Logo.png", false, null)), files[1]);
    }

    [Fact]
    public void S1332_a_hostile_count_is_clamped_to_the_buffer()
        => Assert.Single(FileDescriptors.Parse(Descriptors(uint.MaxValue, ("a.txt", 0, 0, 0))));

    [Fact]
    public void S1332_a_folder_descriptor_is_a_folder()
        => Assert.True(FileDescriptors.Parse(Descriptors(1, ("Sub", 0x4, 0x10, 0)))[0].File.IsDirectory);

    [Theory]
    [InlineData(@"..\evil.exe")]
    [InlineData(@"C:\Windows\x.dll")]
    [InlineData(@"\rooted.txt")]
    [InlineData(@"a\..\..\b")]
    [InlineData("CON")]
    public void S1332_names_that_escape_the_destination_are_refused(string name)
        => Assert.Null(FileDescriptors.SafeRelative(name));

    [Fact]
    public void S1332_unsafe_descriptors_are_skipped_but_keep_the_others_indices()
    {
        var files = FileDescriptors.Parse(Descriptors(2, (@"..\x", 0, 0, 0), ("ok.txt", 0, 0, 0)));

        Assert.Equal([(1, new VirtualFile("ok.txt", false, null))], files);
    }

    // ---- badges ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, CloudState.None)]
    [InlineData(0x20, CloudState.None)]
    [InlineData(0x400000 | 0x400, CloudState.CloudOnly)]
    [InlineData(0x1000, CloudState.CloudOnly)]
    [InlineData(0x80000 | 0x400, CloudState.Pinned)]
    [InlineData(0x400, CloudState.Synced)]
    [InlineData(0x100000, CloudState.CloudOnly)]
    public void S1324_cloud_state_from_attributes(int attributes, CloudState expected)
        => Assert.Equal(expected, CloudBadges.State(attributes));

    [Fact]
    public void S1324_pinning_sets_one_pin_bit_and_clears_the_other()
    {
        Assert.Equal(0x20 | 0x80000, CloudBadges.WithPin(0x20 | 0x100000, CloudPin.Pin));
        Assert.Equal(0x100000, CloudBadges.WithPin(0x80000, CloudPin.Unpin));
    }

    [Fact]
    public void S1324_icons_are_shared_per_type_except_where_each_file_has_its_own()
    {
        Assert.Equal(".txt", FolderFormatting.IconKey(File("a.TXT")));
        Assert.Equal(FolderFormatting.IconKey(File("b.txt")), FolderFormatting.IconKey(File("a.TXT")));
        Assert.Equal(@"C:\Root\app.exe", FolderFormatting.IconKey(File("app.exe")));
        Assert.Equal("<file>", FolderFormatting.IconKey(File("LICENSE")));
        Assert.Equal("<folder>", FolderFormatting.IconKey(File("src", dir: true)));
        Assert.Equal(@"C:\Root\Documents", FolderFormatting.IconKey(File("Documents", dir: true, attributes: 0x1)));
    }

    [Fact]
    public void S1325_git_status_is_parsed_and_counted_as_the_original()
    {
        const string output = "## main...origin/main [ahead 1]\0M  staged.cs\0 M worktree.cs\0MM both.cs\0?? new.txt\0?? dir/\0"
            + "UU conflict.cs\0 D gone.cs\0R  renamed.cs\0old.cs\0";

        var s = GitStatusParser.Parse(output);

        Assert.Equal("main", s.Branch);
        Assert.Equal(3, s.Staged); // M, MM, R
        Assert.Equal(2, s.Modified);
        Assert.Equal(1, s.Deleted);
        Assert.Equal(2, s.Untracked);
        Assert.Equal(1, s.Conflicted);
        Assert.Equal("+3 ~2 -1 ?2 !1", s.Counts());
        Assert.False(s.Files.ContainsKey("old.cs"));
        Assert.Equal("M", s.Code("Staged.cs", false));
        Assert.Equal("??", s.Code("new.txt", false));
        Assert.Equal("•", s.Code("dir", true));
        Assert.Equal(string.Empty, s.Code("clean.cs", false));
    }

    [Fact]
    public void S1325_a_fresh_repository_names_its_branch()
    {
        var s = GitStatusParser.Parse("## No commits yet on main\0?? a\0");

        Assert.Equal("main", s.Branch);
        Assert.False(s.Clean);
        Assert.True(GitStatusParser.Parse("## dev\0").Clean);
    }

    [Fact]
    public void S1325_a_folder_shows_a_dot_when_anything_beneath_it_changed()
    {
        var s = GitStatusParser.Parse("## main\0 M src/app/x.cs\0");

        Assert.Equal("•", s.Code("src", true));
        Assert.Equal("•", s.Code(@"src\app", true));
        Assert.Equal(string.Empty, s.Code("docs", true));
    }

    // ---- formatting -----------------------------------------------------------------------------

    [Fact]
    public void S1322_item_counts_and_type_fallbacks()
    {
        Assert.Equal(string.Empty, FolderFormatting.ItemCount(null, 1000));
        Assert.Equal("12", FolderFormatting.ItemCount(12, 1000));
        Assert.Equal("999+", FolderFormatting.ItemCount(1000, 1000));
        Assert.Equal("File folder", FolderFormatting.TypeFallback("x", true));
        Assert.Equal("MD File", FolderFormatting.TypeFallback("notes.md", false));
        Assert.Equal("File", FolderFormatting.TypeFallback("LICENSE", false));
    }

    // ---- history --------------------------------------------------------------------------------

    [Fact]
    public void S1326_back_and_forward_restore_the_scroll_and_a_new_place_clears_forward()
    {
        var h = new FolderHistory(@"C:\Home");

        Assert.True(h.Go(@"C:\Home\A", scroll: 120));
        Assert.True(h.Go(@"C:\Home\A\B", scroll: 40));

        Assert.Equal(new FolderPlace(@"C:\Home\A", 40), h.Back(scroll: 7));
        Assert.True(h.CanGoForward);
        Assert.Equal(new FolderPlace(@"C:\Home\A\B", 7), h.Forward(scroll: 40));

        h.Back(0);
        h.Go(@"C:\Elsewhere", 0);
        Assert.False(h.CanGoForward);
        Assert.False(h.Go(@"c:\elsewhere", 0));
    }

    [Theory]
    [InlineData(@"C:\Users\nate", @"C:\Users")]
    [InlineData(@"C:\Users", @"C:\")]
    [InlineData(@"C:\", null)]
    [InlineData(@"\\server\share", null)]
    [InlineData(@"\\server\share\dir", @"\\server\share")]
    public void S1326_up_goes_to_the_parent_and_stops_at_a_root(string path, string? parent)
        => Assert.Equal(parent, FolderHistory.Parent(path));

    [Fact]
    public void S1323_the_breadcrumb_runs_from_the_root()
    {
        var crumbs = FolderHistory.Crumbs(@"C:\Users\nate");

        Assert.Equal([(@"C:", @"C:\"), ("Users", @"C:\Users"), ("nate", @"C:\Users\nate")], crumbs);
    }

    [Fact]
    public void S1326_history_is_capped()
    {
        var h = new FolderHistory(@"C:\0");

        for (var i = 1; i <= FolderHistory.Depth + 10; i++)
        {
            h.Go($@"C:\{i}", 0);
        }

        var steps = 0;

        while (h.Back(0) is not null)
        {
            steps++;
        }

        Assert.Equal(FolderHistory.Depth, steps);
    }

    // ---- the ladder -----------------------------------------------------------------------------

    [Fact]
    public void S1323_tiles_stretch_to_fill_the_row()
    {
        var plan = FolderLayout.Grid(width: 330, count: 7, FolderTileSize.Small, labelled: true);

        Assert.Equal(4, plan.Columns);
        Assert.Equal(82.5, plan.CellWidth);
        Assert.Equal(2, plan.Rows);
        Assert.Equal(90, plan.CellHeight);
    }

    [Fact]
    public void S4121_tiles_keep_their_preset_height_there_is_no_stretch()
    {
        // A grid that would fit twice over still draws its preset rows and leaves the rest empty.
        var plan = FolderLayout.Grid(320, 8, FolderTileSize.Small, true);
        Assert.Equal(90, plan.CellHeight);
        Assert.Equal(2, plan.Rows);
    }

    [Fact]
    public void S1323_icons_view_packs_tiles_to_the_icon()
    {
        var plan = FolderLayout.Grid(560, 20, FolderTileSize.Small, labelled: false);

        Assert.Equal(56, plan.CellHeight);
        Assert.Equal(10, plan.Columns);
    }

    [Fact]
    public void S1323_a_narrow_box_still_gets_one_column()
        => Assert.Equal(1, FolderLayout.Grid(30, 3, FolderTileSize.Large, true).Columns);

    [Fact]
    public void S1323_list_columns_grow_with_the_width()
    {
        Assert.Empty(FolderLayout.ListColumns(new WidgetArea(150, 100), 100, anyFile: true));
        Assert.Equal([FolderColumn.Modified], FolderLayout.ListColumns(new WidgetArea(300, 200), 250, anyFile: true));
        Assert.Equal([FolderColumn.Size, FolderColumn.Modified], FolderLayout.ListColumns(new WidgetArea(480, 116), 424, anyFile: true));
        Assert.Equal([FolderColumn.Type, FolderColumn.Size, FolderColumn.Modified], FolderLayout.ListColumns(new WidgetArea(800, 500), 744, anyFile: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void S6411_the_list_s_rightmost_column_is_one_every_row_fills_at_every_width(bool anyFile)
    {
        // The bug: from 400 DIPs the list put Size last, blank for every folder, so the dates ended
        // a hidden column's width short of the right edge.
        for (var width = 120; width <= 1200; width += 10)
        {
            var area = new WidgetArea(width, 300);
            var columns = FolderLayout.ListColumns(area, width - 56, anyFile);

            if (columns.Count > 0)
            {
                Assert.Equal(FolderColumn.Modified, columns[^1]);
            }

            if (!anyFile)
            {
                Assert.DoesNotContain(FolderColumn.Size, columns);
            }
        }
    }

    [Fact]
    public void S6411_the_last_details_column_ends_at_the_right_edge_whatever_it_is()
    {
        // Type, Git and Cloud are drawn from the left of their slot; as the last column that left
        // the slot's slack as an empty strip at the row's end.
        foreach (var last in FolderColumns.All)
        {
            List<FolderColumn> chosen = [.. FolderColumns.All.Where(c => c != last), last];

            for (var width = 200; width <= 1400; width += 20)
            {
                var shown = FolderColumns.Fit(chosen, width);

                for (var i = 0; i < shown.Count; i++)
                {
                    var end = FolderColumns.AlignsRight(shown[i], last: i == shown.Count - 1);
                    Assert.True(i < shown.Count - 1 || end, $"{shown[i]} last at {width}");
                }
            }
        }

        Assert.False(FolderColumns.AlignsRight(FolderColumn.Type, last: false));
        Assert.True(FolderColumns.AlignsRight(FolderColumn.Size, last: false));
    }

    [Fact]
    public void S6411_a_listing_of_folders_only_reserves_no_size_column()
    {
        var plan = FolderLayout.Plan(new WidgetArea(670, 300), new FolderConfig(), 4, anyFile: false, searchOpen: false, navigate: false, hasGit: false);

        Assert.Equal([FolderColumn.Modified], plan.Columns);
    }

    [Fact]
    public void S1323_chrome_appears_on_big_tiles_only()
    {
        var config = new FolderConfig { Style = FolderStyle.Details };

        var small = FolderLayout.Plan(new WidgetArea(200, 106), config, 20, true, searchOpen: false, navigate: true, hasGit: false);
        Assert.False(small.Breadcrumb);

        var big = FolderLayout.Plan(new WidgetArea(800, 516), config, 20, true, false, true, false);
        Assert.True(big.Breadcrumb);
        Assert.True(big.ColumnHeader);
        Assert.Equal(config.Columns, big.Columns);
    }

    [Fact]
    public void S1323_a_big_tile_has_a_breadcrumb_and_no_column_header_outside_details()
    {
        var plan = FolderLayout.Plan(new WidgetArea(800, 516), new FolderConfig { Style = FolderStyle.Grid }, 6, true, false, true, false);

        Assert.True(plan.Breadcrumb);
        Assert.False(plan.ColumnHeader);
        Assert.Empty(plan.Columns);
        Assert.NotNull(plan.Grid);
    }

    [Fact]
    public void S1323_an_open_search_box_is_shown_before_the_column_header()
    {
        var config = new FolderConfig { Style = FolderStyle.Details };

        var plan = FolderLayout.Plan(new WidgetArea(300, 80), config, 20, true, searchOpen: true, navigate: false, hasGit: false);

        Assert.True(plan.SearchBox);
        Assert.False(plan.ColumnHeader);
    }

    [Fact]
    public void S1323_xl_grid_tiles_are_one_size_larger()
    {
        var config = new FolderConfig { Style = FolderStyle.Grid, TileSize = FolderTileSize.Small };

        var plan = FolderLayout.Plan(new WidgetArea(800, 516), config, 20, true, false, false, false);

        Assert.Equal(48, plan.Grid?.Icon);
    }

    [Fact]
    public void S1326_header_buttons_shed_in_priority_order()
    {
        Assert.Equal(FolderHeaderPlan.None, FolderLayout.Header(150, FolderStyle.List, navigate: true, hasGit: true));

        var narrow = FolderLayout.Header(200, FolderStyle.List, true, true);
        Assert.True(narrow.Up);
        Assert.False(narrow.Back);

        var wide = FolderLayout.Header(800, FolderStyle.List, true, true);
        Assert.Equal(new FolderHeaderPlan(true, true, true, true, true, true, true), wide);

        var details = FolderLayout.Header(800, FolderStyle.Details, navigate: false, hasGit: false);
        Assert.False(details.Sort);
        Assert.False(details.Back);
        Assert.True(details.Search);

        Assert.False(FolderLayout.Header(800, FolderStyle.Grid, false, hasGit: true).Git);
    }

    // ---- keys -----------------------------------------------------------------------------------

    [Theory]
    [InlineData(0x43, WidgetKey.Copy)]
    [InlineData(0x58, WidgetKey.Cut)]
    [InlineData(0x56, WidgetKey.Paste)]
    [InlineData(0x41, WidgetKey.SelectAll)]
    public void S1327_the_clipboard_chords_reach_a_widget(int vk, WidgetKey expected)
        => Assert.Equal(expected, WidgetKeys.FromVirtualKey(vk, shift: false, ctrl: true, alt: false));

    [Fact]
    public void S1327_other_chords_still_do_not()
    {
        Assert.Null(WidgetKeys.FromVirtualKey(0x43, shift: true, ctrl: true, alt: false));
        Assert.Null(WidgetKeys.FromVirtualKey(0x43, shift: false, ctrl: true, alt: true));
        Assert.Null(WidgetKeys.FromVirtualKey(0x44, shift: false, ctrl: true, alt: false));
        Assert.Null(WidgetKeys.FromVirtualKey(0x43, shift: false, ctrl: false, alt: false));
        Assert.Equal(WidgetKey.Back, WidgetKeys.FromVirtualKey(0x08, false, false, false));
    }
}
