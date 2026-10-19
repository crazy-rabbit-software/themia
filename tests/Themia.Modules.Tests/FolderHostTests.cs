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
}
