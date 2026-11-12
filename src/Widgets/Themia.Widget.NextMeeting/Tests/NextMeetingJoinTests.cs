using Themia.Core.Localization;
using Themia.Core.Office;
using Xunit;

namespace Themia.Widget.NextMeeting.Tests;

/// <summary>
/// [S-2591] [S-2592] [S-5710] [S-11407] The countdown over Join at the end side at every width of the
/// block's range; without a link the countdown alone on the title's line.
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

    [Fact]
    public void S5710_the_end_column_is_the_wider_of_the_countdown_and_join_and_the_lines_have_the_rest()
    {
        Assert.Equal(Join + 40, NextMeetingJoin.EndWidth(Countdown, Join + 40, hasJoin: true));
        Assert.Equal(Countdown, NextMeetingJoin.EndWidth(Countdown, Join, hasJoin: true));

        // At the narrowest block (260 less its insets) the title keeps the rest beside the column.
        Assert.Equal(236 - Countdown - 12, NextMeetingJoin.TextWidth(236, Countdown));
        Assert.Equal(0, NextMeetingJoin.TextWidth(double.NaN, Countdown));
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
    public void S2592_no_join_link_is_no_button_the_countdown_alone_on_the_titles_line()
    {
        Assert.Equal(Countdown, NextMeetingJoin.EndWidth(Countdown, Join + 100, hasJoin: false));

        // On the title's 28 line, its middle on the title's: 4 down.
        Assert.Equal(4, NextMeetingJoin.TitleLineOffset);
        Assert.Equal(4 + 20, NextMeetingJoin.EndHeight(hasJoin: false));
        Assert.Equal(20 + 4 + 28, NextMeetingJoin.EndHeight(hasJoin: true));
    }
}
