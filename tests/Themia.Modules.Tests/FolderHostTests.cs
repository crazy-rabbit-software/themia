using System.Buffers.Binary;
using System.Text;
using Themia.Core.Files;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-1321] [S-1322] [S-1327] The folder widget's search filter, columns and selection under the root's tags (the extensions, the item counts and the clipboard chords there are the SDK's): the rules are its module's, so they run here. Moved from Core's FolderBrowserTests, unchanged.</summary>
public sealed class FolderHostTests
{
    private static FolderEntry File(string name, bool dir = false, int attributes = 0, long size = 0)
        => new(name, $@"C:\Root\{name}", dir, size, new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero), false, attributes);

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
}
