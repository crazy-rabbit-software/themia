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

    // A tier's content box: the card less its 44 header, the inset either side and at the foot.
    private static int RowsAt(double w, double h, int count = 20)
    {
        var box = new WidgetArea(w - (2 * FamilySpace.Inset), h - FamilySpace.ContentTop - FamilySpace.Inset);
        var plan = NextMeetingLayout.Plan(box, hasJoin: true, hasLocation: true, hasOrganizer: true);
        return NextMeetingFollowing.Rows(box.Height, plan, hasJoin: true, count);
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
        Assert.False(NextMeetingFollowing.LaterDays(new WidgetArea(200, 356)));
        Assert.False(NextMeetingFollowing.LaterDays(new WidgetArea(320, 196)));
        Assert.True(NextMeetingFollowing.LaterDays(new WidgetArea(560, 376)));
        Assert.True(NextMeetingFollowing.LaterDays(new WidgetArea(800, 516)));
    }

    [Fact]
    public void S2161_whole_rows_fit_between_the_details_and_join_only_once_the_details_are_whole()
    {
        // XS, S and Wide show the when line and the title at most; M has 8 left under its five lines.
        Assert.Equal(0, RowsAt(120, 120));
        Assert.Equal(0, RowsAt(200, 150));
        Assert.Equal(0, RowsAt(480, 160));
        Assert.Equal(0, RowsAt(320, 240));

        // Tall: 340 less Join 40, the details' 124, 8 and the label's 16 is 152: four rows of 32.
        Assert.Equal(4, RowsAt(200, 400));
        Assert.Equal(5, RowsAt(560, 420));
        Assert.Equal(9, RowsAt(800, 560));

        // Never more than there are.
        Assert.Equal(2, RowsAt(800, 560, count: 2));
        Assert.Equal(0, RowsAt(800, 560, count: 0));
    }

    [Fact]
    public void S2161_the_details_height_is_their_lines_4_apart()
    {
        var all = new NextMeetingPlan(true, true, true, true, true);
        Assert.Equal(20 + 28 + 20 + 20 + 20 + (4 * 4), NextMeetingFollowing.DetailsHeight(all));
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
