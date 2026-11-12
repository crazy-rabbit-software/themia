using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.UpNext.Tests;

/// <summary>[S-11408] After this: the meetings after the next one, their rows and its size.</summary>
public sealed class UpNextTests
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

    [Fact]
    public void S11408_the_meetings_after_this_one_in_order_without_declined_all_day_or_running_ones_today_then_later()
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

        Assert.Equal(["1:1", "Sync", "Planning"], NextMeetingRules.After(events, shown, Now, TimeZoneInfo.Utc).Select(e => e.Subject));
    }

    [Fact]
    public void S11408_under_way_the_following_are_those_still_to_start()
    {
        var running = At("Workshop", 10, 120);
        var after = NextMeetingRules.After([At("Earlier", 9.5, 120), running, At("Later", 11)], running, Now, TimeZoneInfo.Utc);
        Assert.Equal(["Later"], after.Select(e => e.Subject));
    }

    [Fact]
    public void S11408_the_feed_agrees_with_next_meeting_on_the_one_shown()
    {
        var feed = new MeetingFeed(() => new UpNextConfig()) { Events = [At("Design review", 10.5, 60), At("1:1", 14)] };
        Assert.Equal("Design review", feed.Shown(Now)!.Subject);
        Assert.Equal(["1:1"], feed.After(Now, TimeZoneInfo.Utc).Select(e => e.Subject));
        Assert.Empty(new MeetingFeed(() => new UpNextConfig()).After(Now, TimeZoneInfo.Utc));
    }

    [Fact]
    public void S11408_a_row_says_the_start_and_the_day_when_it_is_not_today()
    {
        var locale = Locale.En;
        var today = NextMeetingRules.Start(locale, At("1:1", 14), Now, TimeZoneInfo.Utc);
        var tomorrow = NextMeetingRules.Start(locale, At("Planning", 33.5), Now, TimeZoneInfo.Utc);

        Assert.Equal(LocaleFormat.Time(locale, Today.AddHours(14).DateTime), today);
        Assert.StartsWith("Tomorrow, ", tomorrow, StringComparison.Ordinal);
        Assert.EndsWith(LocaleFormat.Time(locale, Today.AddHours(33.5).DateTime), tomorrow, StringComparison.Ordinal);
    }

    [Fact]
    public void S11408_three_rows_of_one_line_as_wide_as_the_next_meeting_block()
    {
        var sizing = new UpNextModule().Descriptor.Sizing(new UpNextConfig());
        Assert.Equal(new SizeDip(320, 36 + (3 * 28) + 12), sizing.Natural);
        Assert.Equal((260, 520), (sizing.Min.Width, sizing.Max.Width));

        var next = (UpNextConfig)new UpNextModule().Descriptor.SizeSets!(new UpNextConfig(), new SizeDip(320, 36 + (5 * 28) + 12));
        Assert.Equal(5, next.Rows);
    }

    [Fact]
    public void S11408_it_never_asks_for_birthdays_and_a_copy_keeps_its_rows_and_calendars()
    {
        var config = new UpNextConfig { Rows = 99, ShowBirthdays = true, AccountIds = ["a"] };
        Assert.NotEmpty(config.Repair());
        var copy = Assert.IsType<UpNextConfig>(config.Clone());
        Assert.Equal((20, false, "a"), (copy.Rows, copy.ShowBirthdays, copy.AccountIds.Single()));
    }

    [Fact]
    public void S11408_the_menu_is_rows_then_the_calendar_accounts()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        new UpNextModule().MenuLines(menu, new UpNextConfig());
        Assert.Equal(Str.Kit_Rows.Text(), menu.Lines[0].Text);
        Assert.DoesNotContain(menu.Lines, l => l.Text == Str.Kit_Lines.Text());
    }
}
