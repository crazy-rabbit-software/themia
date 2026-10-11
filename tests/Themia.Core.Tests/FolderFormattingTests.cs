using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The strings a folder row and a folder's empty state are drawn from. The rules that matter are
/// "a folder has no size" and "every state says which state it is" — both of which otherwise turn
/// into a wrong number and a blank tile.
/// </summary>
public class FolderFormattingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 14, 0, 0, TimeSpan.Zero);

    private static FolderEntry File(long size) => new("f", @"C:\x\f", false, size, Now, false);

    private static FolderEntry Dir() => new("d", @"C:\x\d", true, 0, Now, false);

    [Fact]
    public void A_file_shows_its_size_in_the_units_the_rest_of_windows_uses()
    {
        Assert.Equal("512 B", FolderFormatting.Size(File(512)));
        Assert.Equal("1.5 KB", FolderFormatting.Size(File(1536)));
    }

    [Fact]
    public void A_folder_shows_no_size_rather_than_a_wrong_one()
    {
        // Explorer leaves the column blank. "0 B" would be an answer, and the wrong one: a folder's
        // size is a recursive walk nobody asked this widget to do.
        Assert.Equal(string.Empty, FolderFormatting.Size(Dir()));
    }

    [Fact]
    public void Something_written_today_shows_the_time()
    {
        var when = new DateTimeOffset(2026, 9, 24, 9, 5, 0, TimeSpan.Zero);

        Assert.Equal("9:05 AM", FolderFormatting.Modified(Locale.En, when, Now).Replace('\u202F', ' '));
        Assert.Equal("09:05", FolderFormatting.Modified(Locale.De, when, Now));
    }

    [Fact]
    public void Something_written_earlier_this_year_shows_the_day_and_month()
    {
        var when = new DateTimeOffset(2026, 9, 22, 23, 59, 0, TimeSpan.Zero);

        Assert.Equal("Sep 22", FolderFormatting.Modified(Locale.En, when, Now));
        Assert.Equal("22. Sept.", FolderFormatting.Modified(Locale.De, when, Now));
    }

    [Fact]
    public void Something_written_in_another_year_shows_the_year_too()
    {
        var when = new DateTimeOffset(2025, 12, 31, 8, 0, 0, TimeSpan.Zero);

        Assert.Equal("Dec 31, 2025", FolderFormatting.Modified(Locale.En, when, Now));
        Assert.Equal("31.12.2025", FolderFormatting.Modified(Locale.De, when, Now));
    }

    [Fact]
    public void A_midnight_boundary_is_a_different_day_not_a_different_hour()
    {
        var justBefore = new DateTimeOffset(2026, 9, 23, 23, 59, 59, TimeSpan.Zero);
        var justAfter = new DateTimeOffset(2026, 9, 24, 0, 0, 1, TimeSpan.Zero);

        Assert.Equal("Sep 23", FolderFormatting.Modified(Locale.En, justBefore, Now));
        Assert.Equal("00:00", FolderFormatting.Modified(Locale.De, justAfter, Now));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1601)]
    public void A_file_with_no_usable_timestamp_shows_nothing_rather_than_the_filetime_epoch(int year)
    {
        var when = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(string.Empty, FolderFormatting.Modified(when, Now));
    }

    [Fact]
    public void The_default_value_of_the_struct_shows_nothing()
    {
        Assert.Equal(string.Empty, FolderFormatting.Modified(default, Now));
    }

    [Theory]
    [InlineData(FolderListingStatus.Loading)]
    [InlineData(FolderListingStatus.Empty)]
    [InlineData(FolderListingStatus.NotFound)]
    [InlineData(FolderListingStatus.NotAFolder)]
    [InlineData(FolderListingStatus.AccessDenied)]
    [InlineData(FolderListingStatus.Failed)]
    [InlineData(FolderListingStatus.InvalidPath)]
    public void Every_state_without_rows_has_a_sentence(FolderListingStatus status)
    {
        // A widget that shows nothing and explains nothing is indistinguishable from a broken one.
        Assert.False(string.IsNullOrWhiteSpace(FolderFormatting.StatusMessage(status)));
    }

    [Fact]
    public void The_state_with_rows_has_no_message_to_draw_over_them()
    {
        Assert.Equal(string.Empty, FolderFormatting.StatusMessage(FolderListingStatus.Ok));
    }

    [Fact]
    public void Each_reason_a_path_was_refused_gets_its_own_sentence()
    {
        // They need different corrections from the reader, so they do not share wording.
        var messages = new[]
        {
            FolderPathProblem.Empty,
            FolderPathProblem.InvalidCharacters,
            FolderPathProblem.NotRooted,
            FolderPathProblem.TooLong,
        }.Select(FolderFormatting.PathMessage).ToArray();

        Assert.All(messages, message => Assert.False(string.IsNullOrWhiteSpace(message)));
        Assert.Equal(messages.Length, messages.Distinct().Count());
    }

    [Fact]
    public void A_path_that_is_fine_has_nothing_to_complain_about()
    {
        Assert.Equal(string.Empty, FolderFormatting.PathMessage(FolderPathProblem.None));
    }

    [Fact]
    public void An_invalid_path_reports_the_reason_rather_than_a_generic_failure()
    {
        var rooted = FolderFormatting.StatusMessage(FolderListingStatus.InvalidPath, FolderPathProblem.NotRooted);
        var characters = FolderFormatting.StatusMessage(FolderListingStatus.InvalidPath, FolderPathProblem.InvalidCharacters);

        Assert.NotEqual(rooted, characters);
    }

    /// <summary>The empty-state sentences and the type fallback exist in every language, never blank.</summary>
    [Fact]
    public void Messages_and_types_are_translated()
    {
        Assert.Equal("Dieser Ordner ist leer.", FolderFormatting.StatusMessage(Locale.De, FolderListingStatus.Empty));
        Assert.Equal("MD-Datei", FolderFormatting.TypeFallback(Locale.De, "notes.md", false));
        Assert.Equal("Dateiordner", FolderFormatting.TypeFallback(Locale.De, "x", true));
    }
}
