using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.NextMeeting.Tests;

/// <summary>
/// [S-2591] [S-2592] [S-5710] The countdown over Join at the end side where the title keeps its width,
/// one row under the lines where not; without a link the countdown alone.
/// </summary>
public sealed class NextMeetingJoinTests
{
    /// <summary>"Join" on the Body step with the button's padding and border: about 58.</summary>
    private const double Join = 58;

    /// <summary>"in 59 min." in BodyStrong: about 76.</summary>
    private const double Countdown = 76;

    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 42, 0, TimeSpan.Zero);

    private static CalendarEvent In(double minutes, double length = 30) => new()
    {
        Id = "m",
        Subject = "Design review",
        Start = Now.AddMinutes(minutes),
        End = Now.AddMinutes(minutes + length),
        AccountId = "a",
    };

    [Theory]
    [InlineData(496)] // 520 × 230 less the side insets
    [InlineData(296)] // 320 × 200
    [InlineData(248)]
    public void S5710_the_end_column_stands_beside_the_lines_where_the_title_keeps_its_width(double content)
    {
        var end = NextMeetingJoin.EndWidth(Countdown, Join, hasJoin: true);
        Assert.Equal(Countdown, end);
        Assert.Equal(EndPlace.Beside, NextMeetingJoin.Place(content, end));
        Assert.Equal(content - end - FamilySpace.ColumnGap, NextMeetingJoin.TextWidth(EndPlace.Beside, content, end));
        Assert.Equal(0, NextMeetingJoin.FootCost(EndPlace.Beside, hasJoin: true));

        // The countdown over Join, 4 apart: one unit.
        Assert.Equal(20 + 4 + 28, NextMeetingJoin.EndHeight(hasJoin: true));
    }

    [Theory]
    [InlineData(96)]  // XS: 120 less the insets
    [InlineData(176)] // S and Tall
    [InlineData(247)]
    public void S5710_the_end_column_goes_under_the_lines_as_one_row_where_the_column_beside_it_would_be_too_narrow(double content)
    {
        var end = NextMeetingJoin.EndWidth(Countdown, Join, hasJoin: true);
        Assert.Equal(EndPlace.Under, NextMeetingJoin.Place(content, end));
        Assert.Equal(content, NextMeetingJoin.TextWidth(EndPlace.Under, content, end));

        // Its row: Join's 28 and the 8 over it.
        Assert.Equal(36, NextMeetingJoin.FootCost(EndPlace.Under, hasJoin: true));
    }

    [Fact]
    public void S5710_too_narrow_for_join_and_the_countdown_side_by_side_they_stack_at_the_foot()
    {
        // XS: 96 holds neither the lines beside the column nor Join, 12 and the countdown in a row (146).
        Assert.Equal(EndPlace.Stacked, NextMeetingJoin.Place(96, Countdown, Join, hasJoin: true));
        Assert.Equal(20 + 4 + 28 + 8, NextMeetingJoin.FootCost(EndPlace.Stacked, hasJoin: true));
        Assert.Equal(96, NextMeetingJoin.TextWidth(EndPlace.Stacked, 96, Countdown));

        // S and Tall (176) hold the row; wider, the column stands beside the lines.
        Assert.Equal(EndPlace.Under, NextMeetingJoin.Place(176, Countdown, Join, hasJoin: true));
        Assert.Equal(EndPlace.Beside, NextMeetingJoin.Place(296, Countdown, Join, hasJoin: true));

        // Without Join the countdown alone is the row, at its end.
        Assert.Equal(EndPlace.Under, NextMeetingJoin.Place(96, Countdown, Join, hasJoin: false));
    }

    [Fact]
    public void S5710_the_end_column_is_the_wider_of_the_countdown_and_join()
    {
        // A longer word for Join (German's "Teilnehmen") is the column's width, and moves it under sooner.
        Assert.Equal(Join + 40, NextMeetingJoin.EndWidth(Countdown, Join + 40, hasJoin: true));
        var edge = NextMeetingJoin.MinTextColumn + Countdown + NextMeetingJoin.Gap;
        Assert.Equal(EndPlace.Beside, NextMeetingJoin.Place(edge, Countdown));
        Assert.Equal(EndPlace.Under, NextMeetingJoin.Place(edge - 1, Countdown));
        Assert.Equal(EndPlace.Under, NextMeetingJoin.Place(edge, NextMeetingJoin.EndWidth(Countdown, Join + 40, hasJoin: true)));
    }

    [Fact]
    public void S5710_a_long_countdown_wording_moves_the_column_under_sooner()
    {
        // "Tomorrow, 10:30 AM" in BodyStrong is about 140: at 320 × 200 the lines would keep 144.
        var end = NextMeetingJoin.EndWidth(140, Join, hasJoin: true);
        Assert.Equal(EndPlace.Under, NextMeetingJoin.Place(296, end));
        Assert.Equal(EndPlace.Beside, NextMeetingJoin.Place(496, end));
    }

    [Fact]
    public void S5710_the_countdown_is_measured_on_every_minute_of_the_hour_so_the_column_does_not_move()
    {
        var soon = NextMeetingJoin.Wordings(Locale.En, In(18), Now, TimeZoneInfo.Utc);
        Assert.Contains("in 18 min.", soon);
        Assert.Contains("in 59 min.", soon);
        Assert.Contains("in 1 min.", soon);

        // A day and a time, or a meeting under way, does not tick: it is its one wording.
        Assert.Single(NextMeetingJoin.Wordings(Locale.En, In(180), Now, TimeZoneInfo.Utc));
        Assert.Single(NextMeetingJoin.Wordings(Locale.En, In(-5), Now, TimeZoneInfo.Utc));
    }

    [Fact]
    public void S2591_an_unmeasured_card_keeps_the_end_column_under()
        => Assert.Equal(EndPlace.Under, NextMeetingJoin.Place(double.NaN, Countdown));

    [Theory]
    [InlineData(96)]
    [InlineData(496)]
    public void S2592_no_join_link_is_no_button_the_countdown_alone_on_the_titles_line(double content)
    {
        var end = NextMeetingJoin.EndWidth(Countdown, Join + 100, hasJoin: false);
        Assert.Equal(Countdown, end);

        // On the title's 28 line, its middle on the title's: 4 down.
        Assert.Equal(4, NextMeetingJoin.TitleLineOffset);
        Assert.Equal(4 + 20, NextMeetingJoin.EndHeight(hasJoin: false));

        // Under the lines its row is the countdown's line.
        var place = NextMeetingJoin.Place(content, end);
        Assert.Equal(place == EndPlace.Under ? 28 : 0, NextMeetingJoin.FootCost(place, hasJoin: false));
    }
}
