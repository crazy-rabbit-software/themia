using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.NextMeeting.Tests;

/// <summary>[S-2160] [S-2161] The meetings after the one shown, and the rows a size holds.</summary>
public sealed class NextMeetingFollowingTests
{
    private static readonly DateTimeOffset Today = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Today.AddHours(10).AddMinutes(18);

    private static CalendarEvent At(string subject, double hour, double minutes = 30) => new()
    {
        Id = subject,
        Subject = subject,
        Start = Today.AddHours(hour),
        End = Today.AddHours(hour).AddMinutes(minutes),
        AccountId = "a",
    };

    /// <summary>"Join" on the Body step with the button's padding and border, and "in 59 min." in BodyStrong: about 58 and 76.</summary>
    private const double Join = 58;
    private const double Countdown = 76;

    // A tier's content box, headerless as the widget starts: the card less the inset on every side ([S-5700]).
    private static int RowsAt(double w, double h, int count = 20)
    {
        var box = new WidgetArea(w - (2 * FamilySpace.Inset), h - (2 * FamilySpace.Inset));
        var end = NextMeetingJoin.EndWidth(Countdown, Join, hasJoin: true);
        var place = NextMeetingJoin.Place(box.Width, Countdown, Join, hasJoin: true);
        var foot = NextMeetingJoin.FootCost(place, hasJoin: true);
        var plan = NextMeetingLayout.Plan(new WidgetArea(NextMeetingJoin.TextWidth(place, box.Width, end), box.Height), foot, hasLocation: true, hasOrganizer: true);
        return NextMeetingFollowing.Rows(box.Height, plan, foot, place == EndPlace.Beside ? NextMeetingJoin.EndHeight(hasJoin: true) : 0, count);
    }

    [Fact]
    public void S2160_the_meetings_after_this_one_in_order_without_declined_all_day_or_running_ones()
    {
        var shown = At("Design review", 10.5, 60);
        var events = new[]
        {
            At("Standup", 9, 15),
            At("Long block", 8, 300),
            shown,
            At("Lunch", 12, 60) with { Declined = true },
            At("Sync", 15),
            At("1:1", 14),
            CalendarEvent.AllDayEvent("holiday", "Holiday", DateOnly.FromDateTime(Today.DateTime), DateOnly.FromDateTime(Today.DateTime).AddDays(1)),
            At("Planning", 33.5),
        };

        var after = NextMeetingFollowing.After(events, shown, Now, TimeZoneInfo.Utc, laterDays: true);

        Assert.Equal(["1:1", "Sync", "Planning"], after.Select(e => e.Subject));
    }

    [Fact]
    public void S2160_under_way_the_following_are_those_still_to_start()
    {
        var running = At("Workshop", 10, 120);
        var after = NextMeetingFollowing.After([At("Earlier", 9.5, 120), running, At("Later", 11)], running, Now, TimeZoneInfo.Utc, laterDays: true);
        Assert.Equal(["Later"], after.Select(e => e.Subject));
    }

    [Fact]
    public void S2160_the_next_days_come_only_on_an_expanded_width()
    {
        var shown = At("Design review", 10.5, 60);
        var events = new[] { shown, At("1:1", 14), At("Planning", 33.5) };

        Assert.Equal(["1:1"], NextMeetingFollowing.After(events, shown, Now, TimeZoneInfo.Utc, laterDays: false).Select(e => e.Subject));

        // The view's area (the card less its header): Tall and M are regular in width, L and XL expanded.
        Assert.False(NextMeetingFollowing.LaterDays(new WidgetArea(200, 364)));
        Assert.False(NextMeetingFollowing.LaterDays(new WidgetArea(320, 204)));
        Assert.True(NextMeetingFollowing.LaterDays(new WidgetArea(560, 384)));
        Assert.True(NextMeetingFollowing.LaterDays(new WidgetArea(800, 524)));
    }

    [Fact]
    public void S2161_whole_rows_fit_between_the_block_and_the_foot_only_once_the_details_are_whole()
    {
        // XS and S have the end column under the lines and their details cut short; Wide has 12 under its block.
        Assert.Equal(0, RowsAt(120, 120));
        Assert.Equal(0, RowsAt(200, 150));
        Assert.Equal(0, RowsAt(480, 160));

        // M: 216 less the block's 100, 8 and the label's 16 is 92: three rows of 28.
        Assert.Equal(3, RowsAt(320, 240));

        // Tall (the end column's row under the lines: 36): 376 less 36, 100 and 24 is 216: seven rows.
        Assert.Equal(7, RowsAt(200, 400));
        Assert.Equal(9, RowsAt(560, 420));
        Assert.Equal(14, RowsAt(800, 560));

        // Never more than there are.
        Assert.Equal(2, RowsAt(800, 560, count: 2));
        Assert.Equal(0, RowsAt(800, 560, count: 0));
    }

    [Fact]
    public void S5710_an_end_column_taller_than_the_lines_holds_the_rows_under_it()
    {
        var titleAndRange = new NextMeetingPlan(true, true, false, false);
        Assert.Equal(52, NextMeetingFollowing.DetailsHeight(titleAndRange));
        Assert.Equal(1, NextMeetingFollowing.Rows(130, titleAndRange, foot: 0, endHeight: 52, count: 5));
        Assert.Equal(0, NextMeetingFollowing.Rows(100, titleAndRange, foot: 0, endHeight: 70, count: 5));
    }

    [Fact]
    public void S2161_the_details_height_is_their_lines_4_apart()
    {
        var all = new NextMeetingPlan(true, true, true, true);
        Assert.Equal(28 + 20 + 20 + 20 + (3 * 4), NextMeetingFollowing.DetailsHeight(all));
        Assert.Equal(0, NextMeetingFollowing.DetailsHeight(default));
    }

    [Fact]
    public void S2160_a_row_says_the_start_and_the_day_when_it_is_not_today()
    {
        var locale = Locale.En;
        var today = NextMeetingFollowing.Start(locale, At("1:1", 14), Now, TimeZoneInfo.Utc);
        var tomorrow = NextMeetingFollowing.Start(locale, At("Planning", 33.5), Now, TimeZoneInfo.Utc);

        Assert.Equal(LocaleFormat.Time(locale, Today.AddHours(14).DateTime), today);
        Assert.StartsWith("Tomorrow, ", tomorrow, StringComparison.Ordinal);
        Assert.EndsWith(LocaleFormat.Time(locale, Today.AddHours(33.5).DateTime), tomorrow, StringComparison.Ordinal);
    }
}
