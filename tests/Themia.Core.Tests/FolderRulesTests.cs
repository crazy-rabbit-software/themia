using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The order a folder listing is drawn in. Tested against a fixed list rather than against whatever
/// happens to be on the disk of whoever runs the suite, which is the whole reason the comparison
/// lives in Core and the enumeration does not.
/// </summary>
public class FolderRulesTests
{
    private static FolderEntry File(string name, long size = 0, int day = 1, bool hidden = false)
        => new(name, @"C:\x\" + name, false, size, At(day), hidden);

    private static FolderEntry Dir(string name, int day = 1, bool hidden = false)
        => new(name, @"C:\x\" + name, true, 0, At(day), hidden);

    private static DateTimeOffset At(int day) => new(2026, 9, day, 12, 0, 0, TimeSpan.Zero);

    private static string[] Names(IReadOnlyList<FolderEntry> entries) => [.. entries.Select(e => e.Name)];

    private static IReadOnlyList<FolderEntry> Arrange(
        IReadOnlyList<FolderEntry> entries,
        FolderSort sort = FolderSort.Name,
        bool descending = false,
        bool showHidden = true,
        int maxCount = 100)
        => FolderRules.Arrange(entries, sort, descending, showHidden, maxCount);

    // ---- one key at a time, both directions ------------------------------------------------

    [Fact]
    public void Name_sorts_alphabetically_ignoring_case()
    {
        var entries = new[] { File("zebra"), File("Apple"), File("mango") };

        Assert.Equal(["Apple", "mango", "zebra"], Names(Arrange(entries)));
        Assert.Equal(["zebra", "mango", "Apple"], Names(Arrange(entries, descending: true)));
    }

    [Fact]
    public void Modified_sorts_oldest_first_and_newest_first_when_reversed()
    {
        var entries = new[] { File("b", day: 5), File("a", day: 9), File("c", day: 1) };

        Assert.Equal(["c", "b", "a"], Names(Arrange(entries, FolderSort.Modified)));
        Assert.Equal(["a", "b", "c"], Names(Arrange(entries, FolderSort.Modified, descending: true)));
    }

    [Fact]
    public void Size_sorts_smallest_first_and_largest_first_when_reversed()
    {
        var entries = new[] { File("b", size: 500), File("a", size: 9_000), File("c", size: 10) };

        Assert.Equal(["c", "b", "a"], Names(Arrange(entries, FolderSort.Size)));
        Assert.Equal(["a", "b", "c"], Names(Arrange(entries, FolderSort.Size, descending: true)));
    }

    // ---- folders first, against each key ---------------------------------------------------

    [Theory]
    [InlineData(FolderSort.Name)]
    [InlineData(FolderSort.Modified)]
    [InlineData(FolderSort.Size)]
    public void Folders_stay_on_top_whichever_way_the_column_runs(FolderSort sort)
    {
        var entries = new[] { File("zz", size: 1, day: 1), Dir("aa", day: 9), File("aa-file", size: 9, day: 5), Dir("zz-dir", day: 2) };

        var ascending = Arrange(entries, sort, descending: false);
        var descending = Arrange(entries, sort, descending: true);

        // The grouping is not part of the column being sorted, so reversing the column must not
        // flip it. Explorer behaves the same way.
        Assert.Equal(2, ascending.Take(2).Count(e => e.IsDirectory));
        Assert.Equal(2, descending.Take(2).Count(e => e.IsDirectory));
    }

    [Fact]
    public void Folders_first_orders_within_each_group_by_the_chosen_key()
    {
        var entries = new[] { File("b-file"), Dir("z-dir"), File("a-file"), Dir("a-dir") };

        Assert.Equal(["a-dir", "z-dir", "a-file", "b-file"],
            Names(Arrange(entries)));

        Assert.Equal(["z-dir", "a-dir", "b-file", "a-file"],
            Names(Arrange(entries, descending: true)));
    }

    [Fact]
    public void S4121_folders_always_come_first_there_is_no_switch_for_it()
    {
        var entries = new[] { File("b"), Dir("c"), File("a") };

        Assert.Equal(["c", "a", "b"], Names(Arrange(entries)));
    }

    [Fact]
    public void A_directory_sorts_by_size_as_zero_rather_than_being_excluded()
    {
        // Directories carry no size, so under a size sort they land with the empty files. They are
        // still listed: dropping a row because its column is blank is not a sort.
        var entries = new[] { File("big", size: 5_000), Dir("folder"), File("empty", size: 0) };

        var ordered = Arrange(entries, FolderSort.Size);

        Assert.Equal(3, ordered.Count);
        Assert.Equal("big", ordered[^1].Name);
    }

    // ---- ties ------------------------------------------------------------------------------

    [Fact]
    public void Equal_sizes_are_broken_by_name_ascending_even_when_the_column_runs_down()
    {
        var entries = new[] { File("b", size: 100), File("a", size: 100), File("c", size: 100) };

        Assert.Equal(["a", "b", "c"], Names(Arrange(entries, FolderSort.Size)));
        Assert.Equal(["a", "b", "c"], Names(Arrange(entries, FolderSort.Size, descending: true)));
    }

    [Fact]
    public void Equal_timestamps_are_broken_by_name()
    {
        var entries = new[] { File("charlie", day: 3), File("alpha", day: 3), File("bravo", day: 3) };

        Assert.Equal(["alpha", "bravo", "charlie"], Names(Arrange(entries, FolderSort.Modified)));
    }

    [Fact]
    public void Entries_the_comparison_cannot_separate_keep_the_order_they_arrived_in()
    {
        // A stable sort, so a tick that changed nothing cannot reshuffle rows under the pointer.
        var first = new FolderEntry("same", @"C:\one\same", false, 10, At(1), false);
        var second = new FolderEntry("same", @"C:\two\same", false, 10, At(1), false);

        var ordered = Arrange([first, second], FolderSort.Name);
        Assert.Equal([@"C:\one\same", @"C:\two\same"], ordered.Select(e => e.FullPath));

        var reversed = Arrange([second, first], FolderSort.Name);
        Assert.Equal([@"C:\two\same", @"C:\one\same"], reversed.Select(e => e.FullPath));
    }

    [Fact]
    public void Names_differing_only_in_case_have_one_fixed_order()
    {
        var ordered = Names(Arrange([File("README"), File("readme")]));
        var again = Names(Arrange([File("readme"), File("README")]));

        Assert.Equal(ordered, again);
    }

    // ---- hidden ----------------------------------------------------------------------------

    [Fact]
    public void Hidden_entries_are_left_out_unless_they_are_asked_for()
    {
        var entries = new[] { File("visible"), File("secret", hidden: true), Dir("hidden-dir", hidden: true) };

        Assert.Equal(["visible"], Names(Arrange(entries, showHidden: false)));
        Assert.Equal(3, Arrange(entries, showHidden: true).Count);
    }

    [Fact]
    public void The_cap_applies_after_hidden_entries_are_dropped()
    {
        // Otherwise a folder of hidden files would show a short list with no explanation.
        var entries = new[]
        {
            File("h1", hidden: true), File("h2", hidden: true), File("a"), File("b"),
        };

        Assert.Equal(["a", "b"], Names(Arrange(entries, showHidden: false, maxCount: 2)));
    }

    // ---- the cap ---------------------------------------------------------------------------

    [Fact]
    public void The_cap_keeps_the_top_of_the_sorted_list_not_the_top_of_the_folder()
    {
        var entries = new[] { File("d"), File("a"), File("c"), File("b") };

        Assert.Equal(["a", "b"], Names(Arrange(entries, maxCount: 2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_cap_of_nothing_still_shows_a_row_rather_than_a_blank_tile(int maxCount)
    {
        Assert.Single(Arrange([File("a"), File("b")], maxCount: maxCount));
    }

    [Fact]
    public void An_empty_folder_comes_back_empty_rather_than_throwing()
    {
        Assert.Empty(Arrange([]));
    }

    // ---- redrawing only when something changed ---------------------------------------------

    [Fact]
    public void A_re_read_that_found_the_same_folder_compares_equal()
    {
        var entries = new[] { File("a", size: 1, day: 2), Dir("b", day: 3) };

        Assert.True(FolderRules.SameContent(Arrange(entries), Arrange(entries)));
        Assert.True(FolderRules.SameContent([], []));
    }

    [Fact]
    public void A_file_that_grew_or_was_touched_compares_different()
    {
        // Both are invisible to a name-only comparison, and both are what the detail column shows.
        Assert.False(FolderRules.SameContent([File("a", size: 1)], [File("a", size: 2)]));
        Assert.False(FolderRules.SameContent([File("a", day: 1)], [File("a", day: 2)]));
    }

    [Fact]
    public void A_renamed_added_or_removed_entry_compares_different()
    {
        Assert.False(FolderRules.SameContent([File("a")], [File("b")]));
        Assert.False(FolderRules.SameContent([File("a")], [File("a"), File("b")]));
        Assert.False(FolderRules.SameContent([File("a"), File("b")], [File("a")]));
    }

    [Fact]
    public void A_reordered_listing_compares_different_because_the_rows_moved()
    {
        Assert.False(FolderRules.SameContent([File("a"), File("b")], [File("b"), File("a")]));
    }

    [Fact]
    public void A_name_that_became_a_folder_compares_different()
    {
        Assert.False(FolderRules.SameContent([File("thing")], [Dir("thing")]));
    }
}
