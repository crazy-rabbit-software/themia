using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3725] A search's answers keep one height while it is typed into.</summary>
public sealed class SearchRoomTests
{
    private static int Height(SearchFill fill) => fill.Shown + (fill.ShowAll ? 1 : 0) + fill.Free;

    [Theory]
    [InlineData(0)] // nothing asked yet
    [InlineData(1)] // a word in place of answers: searching, no match, offline
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(12)]
    public void S3725_the_room_is_six_lines_whatever_was_answered(int answers)
        => Assert.Equal(SearchRoom.Lines, Height(SearchRoom.Fill(answers, showAll: false)));

    [Fact]
    public void S3725_five_answers_keep_the_show_all_slot_free()
        => Assert.Equal(new SearchFill(5, false, 1), SearchRoom.Fill(5, showAll: false));

    [Fact]
    public void S3725_more_than_five_show_five_and_show_all_in_the_slot()
        => Assert.Equal(new SearchFill(5, true, 0), SearchRoom.Fill(12, showAll: false));

    [Fact]
    public void S3725_show_all_shows_every_answer_and_only_then_grows()
    {
        Assert.Equal(new SearchFill(12, false, 0), SearchRoom.Fill(12, showAll: true));
        Assert.Equal(new SearchFill(3, false, 3), SearchRoom.Fill(3, showAll: true));
    }

    [Fact]
    public void S3725_a_negative_count_is_refused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SearchRoom.Fill(-1, showAll: false));
}
