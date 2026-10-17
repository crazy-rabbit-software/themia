using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>Year progress, Next meeting, Recent files and Recycle Bin (S-4800..S-4863).</summary>
public class DeskInfoWidgetsTests
{
    /// <summary>A Central European zone: +1, +2 from the last Sunday of March 02:00 to the last Sunday of October 03:00.</summary>
    private static readonly TimeZoneInfo Cet = TimeZoneInfo.CreateCustomTimeZone(
        "Test/CET",
        TimeSpan.FromHours(1),
        "CET",
        "CET",
        "CEST",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date,
                DateTime.MaxValue.Date,
                TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday)),
        ]);

    private static DateTimeOffset Local(int year, int month, int day, int hour = 0, int minute = 0)
    {
        var clock = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(clock, Cet.GetUtcOffset(clock));
    }

    // -----------------------------------------------------------------------------------------
    // [S-4800] Four kinds, four configs
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(WidgetKind.YearProgress, typeof(YearProgressConfig))]
    [InlineData(WidgetKind.NextMeeting, typeof(NextMeetingConfig))]
    [InlineData(WidgetKind.RecentFiles, typeof(RecentFilesConfig))]
    [InlineData(WidgetKind.RecycleBin, typeof(RecycleBinConfig))]
    public void S4800_each_kind_has_its_config_and_round_trips(WidgetKind kind, Type type)
    {
        var config = WidgetConfig.CreateDefault(kind);
        Assert.IsType(type, config);
        Assert.Equal(kind, config.Kind);

        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
        var back = JsonSerializer.Deserialize(json, StateJson.WidgetConfig);
        Assert.IsType(type, back);
        Assert.Empty(back!.Repair());
    }

    [Fact]
    public void S4800_only_next_meeting_needs_setting_up_and_only_without_a_calendar()
    {
        Assert.False(WidgetSetup.Needed(new YearProgressConfig(), AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new RecentFilesConfig(), AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new RecycleBinConfig(), AccountFeatures.None));
        Assert.True(WidgetSetup.Needed(new NextMeetingConfig(), AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new NextMeetingConfig(), AccountFeatures.Calendar));
    }

    [Fact]
    public void S4820_next_meeting_never_asks_for_birthdays_and_keeps_its_calendars_on_clone()
    {
        var config = new NextMeetingConfig { AccountIds = ["a"], Calendars = new() { ["a"] = ["c1"] } };
        Assert.False(config.ShowBirthdays);

        var clone = Assert.IsType<NextMeetingConfig>(config.Clone());
        Assert.Equal(["a"], clone.AccountIds);
        Assert.Equal(["c1"], clone.Calendars["a"]);

        var edited = new NextMeetingConfig { ShowBirthdays = true };
        Assert.NotEmpty(edited.Repair());
        Assert.False(edited.ShowBirthdays);
    }

    // -----------------------------------------------------------------------------------------
    // [S-4801] Fractions
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S4801_a_spring_forward_day_is_23_hours_and_half_gone_at_half_past_twelve()
    {
        var day = TimeProgress.Of(ProgressSpan.Day, Local(2026, 3, 29, 12, 30), Cet, WeekStartDay.Monday);

        Assert.Equal(TimeSpan.FromHours(23), day.Length);
        Assert.Equal(0.5, day.Fraction, 6);
    }

    [Fact]
    public void S4801_a_fall_back_day_is_25_hours()
    {
        var day = TimeProgress.Of(ProgressSpan.Day, Local(2026, 10, 25, 12, 0), Cet, WeekStartDay.Monday);

        Assert.Equal(TimeSpan.FromHours(25), day.Length);
        Assert.Equal(13.0 / 25, day.Fraction, 6);
    }

    [Fact]
    public void S4801_a_leap_year_has_366_days_and_is_half_gone_at_noon_on_2_july()
    {
        var year = TimeProgress.Of(ProgressSpan.Year, new DateTimeOffset(2028, 7, 2, 0, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, WeekStartDay.Monday);
        Assert.Equal(366, year.Days);
        Assert.Equal(183.0 / 366, year.Fraction, 6);
        Assert.Equal(184, year.DayOf(new DateOnly(2028, 7, 2)));

        var common = TimeProgress.Of(ProgressSpan.Year, new DateTimeOffset(2027, 7, 2, 0, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, WeekStartDay.Monday);
        Assert.Equal(365, common.Days);
    }

    [Fact]
    public void S4801_february_of_a_leap_year_has_29_days()
    {
        var month = TimeProgress.Of(ProgressSpan.Month, new DateTimeOffset(2028, 2, 15, 0, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, WeekStartDay.Monday);
        Assert.Equal(29, month.Days);
        Assert.Equal(new DateOnly(2028, 2, 1), month.First);
        Assert.Equal(14.0 / 29, month.Fraction, 6);
    }

    [Fact]
    public void S4801_the_spans_are_read_in_the_zone_not_in_utc()
    {
        // 23:30 UTC on 31 December is already New Year in Central Europe.
        var year = TimeProgress.Of(ProgressSpan.Year, new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero), Cet, WeekStartDay.Monday);
        Assert.Equal(new DateOnly(2027, 1, 1), year.First);
        Assert.True(year.Fraction < 0.001);
    }

    [Fact]
    public void S4801_percent_floors_so_a_hundred_is_only_the_end()
    {
        Assert.Equal(99, TimeProgress.Percent(0.99999));
        Assert.Equal(100, TimeProgress.Percent(1));
        Assert.Equal(78.4, TimeProgress.Percent(0.78449, 1));
        Assert.Equal(0, TimeProgress.Percent(-0.2));
    }

    // -----------------------------------------------------------------------------------------
    // [S-4802] Week start and week numbers
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S4802_the_week_starts_on_the_regional_first_day()
    {
        var wednesday = new DateOnly(2026, 10, 14);
        Assert.Equal(new DateOnly(2026, 10, 12), TimeProgress.WeekStart(wednesday, WeekStartDay.Monday));
        Assert.Equal(new DateOnly(2026, 10, 11), TimeProgress.WeekStart(wednesday, WeekStartDay.Sunday));
        Assert.Equal(new DateOnly(2026, 10, 10), TimeProgress.WeekStart(wednesday, WeekStartDay.Saturday));

        // Sunday: the last day of a Monday week, the first of a Sunday week.
        var sunday = new DateOnly(2026, 10, 18);
        Assert.Equal(new DateOnly(2026, 10, 12), TimeProgress.WeekStart(sunday, WeekStartDay.Monday));
        Assert.Equal(sunday, TimeProgress.WeekStart(sunday, WeekStartDay.Sunday));

        var monday = TimeProgress.Of(ProgressSpan.Week, new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, WeekStartDay.Monday);
        var sundayWeek = TimeProgress.Of(ProgressSpan.Week, new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, WeekStartDay.Sunday);
        Assert.Equal(6.5 / 7, monday.Fraction, 6);
        Assert.Equal(0.5 / 7, sundayWeek.Fraction, 6);
    }

    [Theory]
    [InlineData(2026, 12, 31, WeekStartDay.Monday, 53)]
    [InlineData(2027, 1, 1, WeekStartDay.Monday, 53)]
    [InlineData(2024, 12, 30, WeekStartDay.Monday, 1)]
    [InlineData(2026, 10, 12, WeekStartDay.Monday, 42)]
    [InlineData(2026, 1, 1, WeekStartDay.Sunday, 1)]
    [InlineData(2026, 1, 3, WeekStartDay.Sunday, 1)]
    [InlineData(2026, 1, 4, WeekStartDay.Sunday, 2)]
    [InlineData(2026, 12, 31, WeekStartDay.Sunday, 53)]
    [InlineData(2026, 1, 2, WeekStartDay.Saturday, 1)]
    [InlineData(2026, 1, 3, WeekStartDay.Saturday, 2)]
    public void S4802_week_numbers_are_iso_for_monday_and_us_for_sunday(int year, int month, int day, WeekStartDay start, int expected)
        => Assert.Equal(expected, TimeProgress.WeekNumber(new DateOnly(year, month, day), start));

    // -----------------------------------------------------------------------------------------
    // [S-4803] The ladder
    // -----------------------------------------------------------------------------------------

    private static WidgetArea Inset(double width, double height) => new WidgetArea(width, height).Deflate(16, 16);

    [Fact]
    public void S4803_the_smallest_tile_shows_the_year_alone()
    {
        var plan = YearProgressLayout.Plan(Inset(120, 76));
        Assert.True(plan.Hero);
        Assert.Equal([ProgressSpan.Year], plan.Rows);
    }

    [Fact]
    public void S4803_a_short_tile_keeps_the_year_then_the_day_in_list_order()
    {
        var plan = YearProgressLayout.Plan(Inset(200, 106));
        Assert.False(plan.Hero);
        Assert.Equal([ProgressSpan.Day, ProgressSpan.Year], plan.Rows);
    }

    [Fact]
    public void S4803_a_medium_tile_shows_all_four_and_a_large_one_adds_details()
    {
        var medium = YearProgressLayout.Plan(Inset(320, 196));
        Assert.Equal(TimeProgress.All, medium.Rows);
        Assert.False(medium.Details);

        var large = YearProgressLayout.Plan(Inset(560, 376));
        Assert.Equal(TimeProgress.All, large.Rows);
        Assert.True(large.Details);
    }

    [Fact]
    public void S4803_a_tall_tile_puts_the_year_large_over_the_rows()
    {
        Assert.True(YearProgressLayout.Plan(Inset(200, 356)).Headline);
        Assert.False(YearProgressLayout.Plan(Inset(320, 196)).Headline);
    }

    [Fact]
    public void S4803_a_strip_stands_the_four_side_by_side()
    {
        var plan = YearProgressLayout.Plan(Inset(480, 116));
        Assert.True(plan.Columns);
        Assert.Equal(TimeProgress.All, plan.Rows);
    }

    [Fact]
    public void S4803_growing_a_tile_never_drops_a_row()
    {
        var previous = 0;
        for (var height = 40; height <= 600; height += 4)
        {
            var count = YearProgressLayout.Plan(new WidgetArea(260, height)).Rows.Count;
            Assert.True(count >= previous, $"height {height}: {count} rows after {previous}");
            previous = count;
        }
    }

    // -----------------------------------------------------------------------------------------
    // [S-4821] Which event
    // -----------------------------------------------------------------------------------------

    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 18, 0, TimeSpan.Zero);

    private static CalendarEvent Timed(string subject, int startMinutes, int minutes) => new()
    {
        Id = subject,
        Subject = subject,
        Start = Now.AddMinutes(startMinutes),
        End = Now.AddMinutes(startMinutes + minutes),
    };

    [Fact]
    public void S4821_skips_all_day_declined_and_ended_events()
    {
        var events = new[]
        {
            CalendarEvent.AllDayEvent("h", "Holiday", new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 13)),
            Timed("Ended", -60, 30),
            Timed("Declined", 5, 30) with { Declined = true },
            Timed("Review", 12, 60),
        };

        Assert.Equal("Review", NextMeetingRules.Pick(events, Now)?.Subject);
        Assert.Null(NextMeetingRules.Pick([events[0], events[1], events[2]], Now));
    }

    [Fact]
    public void S4821_a_running_event_shows_until_the_next_is_within_the_lead_in()
    {
        var running = Timed("Workshop", -30, 120);
        var next = Timed("Call", 20, 30);

        Assert.Equal("Workshop", NextMeetingRules.Pick([running, next], Now)?.Subject);
        Assert.Equal(MeetingPhase.Running, NextMeetingRules.Phase(running, Now));
        Assert.Equal("Call", NextMeetingRules.Pick([running, next], Now.AddMinutes(16))?.Subject);
    }

    [Fact]
    public void S4821_of_two_running_the_later_start_and_of_two_together_the_one_with_a_link()
    {
        var block = Timed("Office hours", -120, 240);
        var inner = Timed("Sync", -5, 30);
        Assert.Equal("Sync", NextMeetingRules.Pick([block, inner], Now)?.Subject);

        var plain = Timed("A plain one", 30, 30);
        var online = Timed("B online", 30, 30) with { JoinUrl = "https://zoom.us/j/1" };
        Assert.Equal("B online", NextMeetingRules.Pick([plain, online], Now)?.Subject);
    }

    [Fact]
    public void S4823_says_when_in_words()
    {
        var running = Timed("Sync", -10, 30);
        Assert.StartsWith("Now · ends ", NextMeetingRules.When(Locale.En, running, Now, TimeZoneInfo.Utc));

        Assert.Equal("in 12 min.", NextMeetingRules.When(Locale.En, Timed("Soon", 12, 30), Now, TimeZoneInfo.Utc));
        Assert.StartsWith("Tomorrow, ", NextMeetingRules.When(Locale.En, Timed("Later", 24 * 60, 30), Now, TimeZoneInfo.Utc));
        Assert.StartsWith("Today, ", NextMeetingRules.When(Locale.En, Timed("Afternoon", 180, 30), Now, TimeZoneInfo.Utc));
    }

    [Fact]
    public void S4824_join_is_always_there_and_the_title_without_it()
    {
        var xs = NextMeetingLayout.Plan(new WidgetArea(88, 60), hasJoin: true, hasLocation: true, hasOrganizer: true);
        Assert.True(xs.When);
        Assert.Equal(0, xs.Title);

        var xsPlain = NextMeetingLayout.Plan(new WidgetArea(88, 60), hasJoin: false, hasLocation: true, hasOrganizer: true);
        Assert.True(xsPlain.Title >= 1);

        var medium = NextMeetingLayout.Plan(new WidgetArea(288, 164), hasJoin: true, hasLocation: true, hasOrganizer: true);
        Assert.True(medium is { When: true, Range: true, Location: true });
        Assert.True(medium.Title >= 1);

        var large = NextMeetingLayout.Plan(new WidgetArea(528, 344), hasJoin: true, hasLocation: true, hasOrganizer: true);
        Assert.True(large is { Large: true, Organizer: true });

        Assert.True(NextMeetingLayout.Plan(new WidgetArea(448, 84), hasJoin: true, hasLocation: false, hasOrganizer: false).JoinBeside);
    }

    // -----------------------------------------------------------------------------------------
    // [S-4822] Join links
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S4822_the_providers_own_link_wins()
    {
        var link = MeetingLinks.Find(["https://teams.microsoft.com/l/meetup-join/19%3a1"], "https://zoom.us/j/2", null);
        Assert.Equal("https://teams.microsoft.com/l/meetup-join/19%3a1", link);
    }

    [Theory]
    [InlineData("Join: https://us02web.zoom.us/j/123456?pwd=abc.", "https://us02web.zoom.us/j/123456?pwd=abc")]
    [InlineData("Meet at https://meet.google.com/abc-defg-hij", "https://meet.google.com/abc-defg-hij")]
    [InlineData("<https://acme.webex.com/acme/j.php?MTID=m1>", "https://acme.webex.com/acme/j.php?MTID=m1")]
    [InlineData("Click here to join (https://teams.microsoft.com/l/meetup-join/19%3ameeting_x%40thread.v2/0?context=%7b%7d)", "https://teams.microsoft.com/l/meetup-join/19%3ameeting_x%40thread.v2/0?context=%7b%7d")]
    [InlineData("x https://zoom.us/j/9?a=1&amp;b=2 y", "https://zoom.us/j/9?a=1&b=2")]
    [InlineData("msteams:/l/meetup-join/19%3ameeting_y", "msteams:/l/meetup-join/19%3ameeting_y")]
    public void S4822_finds_a_meeting_services_link_in_text(string text, string expected)
        => Assert.Equal(expected, MeetingLinks.FromText(text));

    [Theory]
    [InlineData("Agenda: https://wiki.example.com/meeting-notes")]
    [InlineData("javascript:alert(1) https://zoom")]
    [InlineData("https://user:pw@zoom.us/j/1")]
    [InlineData("")]
    [InlineData(null)]
    public void S4822_finds_nothing_in_text_without_a_meeting_link(string? text)
        => Assert.Null(MeetingLinks.FromText(text));

    [Fact]
    public void S4822_outlook_safe_links_are_unwrapped()
    {
        var wrapped = "https://eur01.safelinks.protection.outlook.com/?url=https%3A%2F%2Fzoom.us%2Fj%2F42%3Fpwd%3Dx&data=05";
        Assert.Equal("https://zoom.us/j/42?pwd=x", MeetingLinks.FromText($"Join {wrapped}"));
    }

    [Theory]
    [InlineData("https://teams.microsoft.com/l/meetup-join/1", true)]
    [InlineData("http://zoom.us/j/1", true)]
    [InlineData("msteams:/l/meetup-join/1", true)]
    [InlineData("zoommtg://zoom.us/join?confno=1", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/evil.exe", false)]
    [InlineData("https://a b.com/x", false)]
    [InlineData("https://user@zoom.us/j/1", false)]
    [InlineData("https://zoom.us/j/1\n", false)]
    public void S4822_only_web_teams_and_zoom_links_are_launchable(string link, bool launchable)
        => Assert.Equal(launchable, MeetingLinks.IsLaunchable(link));

    [Fact]
    public void S4822_an_explicit_link_that_is_not_launchable_falls_through_to_the_text()
        => Assert.Equal("https://zoom.us/j/7", MeetingLinks.Find(["javascript:alert(1)", null], "https://zoom.us/j/7"));

    [Fact]
    public void S4822_graph_events_carry_their_teams_link_and_their_answer()
    {
        var item = new GraphEvent
        {
            Id = "e1",
            Subject = "Review",
            IsAllDay = false,
            Start = new GraphDateTime { DateTime = "2026-10-12T10:30:00" },
            End = new GraphDateTime { DateTime = "2026-10-12T11:30:00" },
            OnlineMeeting = new GraphOnlineMeeting { JoinUrl = "https://teams.microsoft.com/l/meetup-join/19%3a1" },
            ResponseStatus = new GraphResponseStatus { Response = "declined" },
        };

        var e = OfficeWire.FromGraph(item)!;
        Assert.Equal("https://teams.microsoft.com/l/meetup-join/19%3a1", e.JoinUrl);
        Assert.True(e.Declined);

        var fromBody = OfficeWire.FromGraph(new GraphEvent
        {
            Id = "e2",
            IsAllDay = false,
            Start = new GraphDateTime { DateTime = "2026-10-12T10:30:00" },
            BodyPreview = "Zoom: https://zoom.us/j/55 see you",
            ResponseStatus = new GraphResponseStatus { Response = "accepted" },
        })!;
        Assert.Equal("https://zoom.us/j/55", fromBody.JoinUrl);
        Assert.False(fromBody.Declined);
    }

    [Fact]
    public void S4822_google_events_carry_their_meet_link_and_the_users_answer()
    {
        var item = new GoogleEvent
        {
            Id = "g1",
            Start = new GoogleEventTime { DateTime = "2026-10-12T10:30:00+02:00" },
            End = new GoogleEventTime { DateTime = "2026-10-12T11:00:00+02:00" },
            ConferenceData = new GoogleConferenceData
            {
                EntryPoints = [new GoogleEntryPoint { EntryPointType = "phone", Uri = "tel:+41-44" }, new GoogleEntryPoint { EntryPointType = "video", Uri = "https://meet.google.com/abc-defg-hij" }],
            },
            Attendees = [new GoogleAttendee { Self = false, ResponseStatus = "declined" }, new GoogleAttendee { Self = true, ResponseStatus = "accepted" }],
        };

        var e = OfficeWire.FromGoogle(item)!;
        Assert.Equal("https://meet.google.com/abc-defg-hij", e.JoinUrl);
        Assert.False(e.Declined);

        var declined = OfficeWire.FromGoogle(new GoogleEvent
        {
            Id = "g2",
            Start = new GoogleEventTime { DateTime = "2026-10-12T10:30:00+02:00" },
            HangoutLink = "https://meet.google.com/xyz-abcd-efg",
            Attendees = [new GoogleAttendee { Self = true, ResponseStatus = "declined" }],
        })!;
        Assert.Equal("https://meet.google.com/xyz-abcd-efg", declined.JoinUrl);
        Assert.True(declined.Declined);
    }

    [Fact]
    public void S4822_caldav_events_read_the_teams_property()
    {
        var data = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\nUID:1\r\nSUMMARY:Sync\r\n"
            + "DTSTART:20261012T080000Z\r\nDTEND:20261012T083000Z\r\n"
            + "X-MICROSOFT-SKYPETEAMSMEETINGURL:https://teams.microsoft.com/l/meetup-join/19%3a9\r\n"
            + "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var e = Assert.Single(IcsEvents.Read(data, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, _ => null));
        Assert.Equal("https://teams.microsoft.com/l/meetup-join/19%3a9", e.JoinUrl);
    }

    [Fact]
    public void S4822_the_store_keeps_the_link_and_the_answer_and_drops_a_bad_link()
    {
        var e = Timed("Sync", 0, 30) with { JoinUrl = "https://zoom.us/j/1", Declined = true };
        var back = OfficeShelves.Restore(OfficeShelves.Store(e));
        Assert.Equal("https://zoom.us/j/1", back.JoinUrl);
        Assert.True(back.Declined);

        Assert.Null(OfficeShelves.Store(e with { JoinUrl = "javascript:alert(1)" }).JoinUrl);
    }

    // -----------------------------------------------------------------------------------------
    // [S-4841] Recent files
    // -----------------------------------------------------------------------------------------

    private static readonly DateTimeOffset T = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void S4841_lists_files_that_are_there_newest_first_each_once()
    {
        var links = new[]
        {
            new RecentLink(@"R:\Recent\a.lnk", T.AddHours(-3), @"C:\Docs\a.docx"),
            new RecentLink(@"R:\Recent\b.lnk", T.AddHours(-1), @"C:\Docs\b.pdf"),
            new RecentLink(@"R:\Recent\gone.lnk", T, @"C:\Docs\gone.txt"),
            new RecentLink(@"R:\Recent\folder.lnk", T, @"C:\Docs"),
            new RecentLink(@"R:\Recent\a again.lnk", T.AddHours(-2), @"c:\docs\A.DOCX"),
            new RecentLink(@"R:\Recent\broken.lnk", T, null),
            new RecentLink(@"R:\Recent\relative.lnk", T, @"Docs\x.txt"),
        };
        var there = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\Docs\a.docx", @"C:\Docs\b.pdf" };

        var files = RecentFileRules.Select(links, there.Contains);

        Assert.Equal([@"C:\Docs\b.pdf", @"c:\docs\A.DOCX"], files.Select(f => f.Path));
        Assert.Equal("Docs", files[0].Folder);
        Assert.Equal("b.pdf", files[0].Name);
        Assert.Equal(@"R:\Recent\a again.lnk", files[1].LinkPath);
    }

    [Fact]
    public void S4841_stops_at_the_cap_and_names_a_drive_root()
    {
        var links = Enumerable.Range(0, 100).Select(i => new RecentLink($@"R:\{i}.lnk", T.AddMinutes(-i), $@"D:\f{i}.txt"));
        var files = RecentFileRules.Select(links, _ => true, max: 5);

        Assert.Equal(5, files.Count);
        Assert.Equal(@"D:\f0.txt", files[0].Path);
        Assert.Equal(@"D:\", files[0].Folder);
    }

    [Fact]
    public void S4841_only_the_newest_shortcuts_are_resolved()
    {
        var links = Enumerable.Range(0, 300).Select(i => ($@"R:\{i}.lnk", T.AddMinutes(-i))).Append((@"R:\desktop.ini", T.AddDays(1)));
        var newest = RecentFileRules.Newest(links);

        Assert.Equal(RecentFileRules.MaxLinksRead, newest.Count);
        Assert.Equal(@"R:\0.lnk", newest[0].LinkPath);
        Assert.DoesNotContain(newest, l => l.LinkPath.EndsWith(".ini", StringComparison.Ordinal));
    }

    [Fact]
    public void S4842_rows_grow_a_folder_line_then_a_time_column()
    {
        Assert.True(RecentFilesLayout.Plan(new WidgetArea(120, 76)).Compact);
        Assert.False(RecentFilesLayout.Plan(new WidgetArea(120, 76)).TwoLine);
        Assert.True(RecentFilesLayout.Plan(new WidgetArea(320, 196)).TwoLine);
        Assert.False(RecentFilesLayout.Plan(new WidgetArea(320, 196)).TimeColumn);
        Assert.True(RecentFilesLayout.Plan(new WidgetArea(560, 376)).TimeColumn);
    }

    // -----------------------------------------------------------------------------------------
    // [S-4861] Recycle Bin
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S4861_says_how_many_items_or_that_it_is_empty()
    {
        Assert.Null(RecycleBinLayout.CountText(Locale.En, null));
        Assert.Equal("No items", RecycleBinLayout.CountText(Locale.En, new RecycleBinState(0, 0)));
        Assert.Equal("1 item", RecycleBinLayout.CountText(Locale.En, new RecycleBinState(1, 10)));
        Assert.Equal("37 items", RecycleBinLayout.CountText(Locale.En, new RecycleBinState(37, 10)));
        Assert.Equal("–", RecycleBinLayout.SizeText(Locale.En, null));
    }

    [Fact]
    public void S4862_the_size_always_shows_and_the_button_comes_with_room()
    {
        var xs = RecycleBinLayout.Plan(Inset(120, 76));
        Assert.False(xs.Button);

        var medium = RecycleBinLayout.Plan(Inset(200, 200));
        Assert.True(medium.Count);
        Assert.True(medium.Button);

        var strip = RecycleBinLayout.Plan(Inset(480, 116));
        Assert.True(strip.Side);
        Assert.True(strip.Button);
    }
}
