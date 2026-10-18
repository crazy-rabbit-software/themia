using System.Buffers.Binary;
using System.Text;
using Themia.Core.Files;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The folder widget's file rules that are the SDK's (names, drops, the clipboard's formats, cloud states, git, counts, the clipboard chords; [S-1321]..[S-1332]); the widget's own are its module's FolderBrowserTests and the module host's FolderHostTests. No test touches a real file.</summary>
public sealed class FolderBrowserTests
{
    private static FolderEntry File(string name, bool dir = false, int attributes = 0, long size = 0)
        => new(name, $@"C:\Root\{name}", dir, size, new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero), false, attributes);

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
        // Ctrl+Shift+C is Explorer's Copy as path ([S-6417]); Ctrl+Shift with any other letter is not a widget's.
        Assert.Null(WidgetKeys.FromVirtualKey(0x58, shift: true, ctrl: true, alt: false));
        Assert.Null(WidgetKeys.FromVirtualKey(0x43, shift: false, ctrl: true, alt: true));
        Assert.Null(WidgetKeys.FromVirtualKey(0x44, shift: false, ctrl: true, alt: false));
        Assert.Null(WidgetKeys.FromVirtualKey(0x43, shift: false, ctrl: false, alt: false));
        Assert.Equal(WidgetKey.Back, WidgetKeys.FromVirtualKey(0x08, false, false, false));
    }
}
