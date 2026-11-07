using System.Runtime.CompilerServices;
using Themia.Core.Accounts;
using Themia.Core.Office;
using Themia.Core.Tasks;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-618]..[S-621] Tasks over CalDAV: the VTODO lists of a home set, the REPORT, and VTODOs read into
/// the task model, over scrubbed answers in the shapes jtx Board (through DAVx⁵), Nextcloud Tasks and
/// iCloud Reminders write (Fixtures/caldav).
/// </summary>
public sealed class CalDavTasksTests
{
    private static readonly DateOnly Today = new(2026, 11, 7);

    private static readonly Uri Home = new("https://dav.example.com/dav/calendars/alex/");

    private static readonly Uri Account = new("https://dav.example.com/dav/calendars/alex/personal/");

    private static TimeZoneInfo? NoSystemZones(string _) => null;

    private static string Fixture(string name, [CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "caldav", name));

    /// <summary>Through the path the remote takes: each resource's calendar-data, then the parser.</summary>
    private static List<TaskItem> Report(string fixture, DateOnly? today = null)
        => [.. DavOffice.CalendarData(Fixture(fixture)).SelectMany(data => IcsTasks.Read(data, "list", today ?? Today, TimeZoneInfo.Utc, NoSystemZones))];

    private static List<TaskItem> Ics(string vtodos, DateOnly? today = null)
        => [.. IcsTasks.Read($"BEGIN:VCALENDAR\r\nVERSION:2.0\r\n{vtodos}END:VCALENDAR\r\n", "list", today ?? Today, TimeZoneInfo.Utc, NoSystemZones)];

    // ---- the account and its lists ----------------------------------------------------------------

    [Fact]
    public void S618_a_caldav_account_feeds_calendars_and_tasks()
    {
        var features = new CalDavProvider().Features;
        Assert.True(features.Has(HostFeatures.Calendar));
        Assert.True(features.Has(HostFeatures.Tasks));
        Assert.True(TaskWire.Supports(HostProviders.CalDav));
    }

    [Fact]
    public void S618_the_task_lists_are_the_collections_that_take_vtodo_on_the_account_origin()
    {
        var lists = DavOffice.TaskLists(Fixture("home-set-listing.xml"), Home, Account);

        Assert.Equal(
            ["https://dav.example.com/dav/calendars/alex/tasks/", "https://dav.example.com/dav/calendars/alex/personal/", "https://dav.example.com/dav/calendars/alex/old%20list/"],
            lists.Select(l => l.Id));
        Assert.Equal(["Household", "Personal", "old list"], lists.Select(l => l.Name));

        // The collection the account was set up with is the default when it takes tasks.
        Assert.Equal("Personal", Assert.Single(lists, l => l.IsDefault).Name);
    }

    [Fact]
    public void S618_with_the_account_collection_not_a_task_list_the_first_list_is_the_default()
    {
        var lists = DavOffice.TaskLists(Fixture("home-set-listing.xml"), Home, new Uri("https://dav.example.com/dav/calendars/alex/work/"));
        Assert.Equal("Household", Assert.Single(lists, l => l.IsDefault).Name);
    }

    [Fact]
    public void S618_the_calendars_still_leave_out_a_tasks_only_list()
    {
        var calendars = DavOffice.Calendars(Fixture("home-set-listing.xml"), Home, Account);
        Assert.Equal(["Work", "Personal", "old list"], calendars.Select(c => c.Name));
    }

    [Fact]
    public void S618_no_listing_is_no_list_and_not_a_throw()
    {
        Assert.Empty(DavOffice.TaskLists("<html>login</html>", Home, Account));
        Assert.Empty(DavOffice.TaskLists(null, Home, Account));
    }

    [Fact]
    public void S619_the_report_asks_for_every_vtodo_of_the_list()
    {
        var body = DavOffice.TodoQuery;
        Assert.Contains("<C:comp-filter name=\"VTODO\"/>", body, StringComparison.Ordinal);
        Assert.Contains("<C:calendar-data/>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("time-range", body, StringComparison.Ordinal);
        Assert.DoesNotContain("expand", body, StringComparison.Ordinal);
    }

    // ---- jtx Board through DAVx⁵ -------------------------------------------------------------------

    [Fact]
    public void S619_jtx_board_tasks_read_with_their_due_status_and_priority()
    {
        var tasks = Report("jtx-board-report.xml");

        Assert.Equal(
            ["6f1c2a6e-1b7e-4f0e-9c1d-3b1f0a2e7c11", "9a8d7c6b-5e4f-4a3b-8c2d-1e0f9a8b7c6d", "0c1d2e3f-4a5b-4c6d-9e7f-8a9b0c1d2e3f", "5b6c7d8e-9f0a-4b1c-8d2e-3f4a5b6c7d8e"],
            tasks.Select(t => t.Id));

        var report = tasks[0];
        Assert.Equal("Prepare the quarterly report", report.Title);
        Assert.False(report.Completed); // IN-PROCESS is open work
        Assert.True(report.Important); // PRIORITY:2
        Assert.Equal("2026-11-10T16:00:00.0000000Z", report.DueRaw); // 17:00 in Berlin (CET) through its VTIMEZONE
        Assert.Equal("list", report.ListId);

        // A subtask (RELATED-TO) is a row of its own: the model is flat.
        Assert.Equal("Collect the sales figures", tasks[1].Title);
        Assert.Equal("2026-11-09", tasks[1].DueRaw);

        var done = tasks[2];
        Assert.True(done.Completed);
        Assert.False(done.Important); // PRIORITY:5 is medium
        Assert.Equal(TaskStamp.At(new DateTimeOffset(2026, 11, 6, 11, 59, 0, TimeSpan.Zero)), done.CompletedOn);
    }

    [Fact]
    public void S619_a_cancelled_task_is_no_row()
        => Assert.DoesNotContain(Report("jtx-board-report.xml"), t => t.Title == "Order the old printer toner");

    [Fact]
    public void S620_a_recurring_task_shows_its_current_instance_past_a_finished_one_and_an_exdate()
    {
        // Weekly on Fridays: the 6th was ticked off (an override), the 13th is excluded, so the 20th is current.
        var plants = Assert.Single(Report("jtx-board-report.xml"), t => t.Title == "Water the office plants");
        Assert.Equal("5b6c7d8e-9f0a-4b1c-8d2e-3f4a5b6c7d8e", plants.Id);
        Assert.False(plants.Completed);
        Assert.Equal("2026-11-20", plants.DueRaw);
    }

    // ---- Nextcloud Tasks ---------------------------------------------------------------------------

    [Fact]
    public void S619_nextcloud_tasks_read_a_zoned_midnight_as_the_day()
    {
        var tasks = Report("nextcloud-tasks-report.xml");
        Assert.Equal(3, tasks.Count);

        var plumber = tasks[0];
        Assert.Equal("Call the plumber about the boiler", plumber.Title);
        Assert.True(plumber.Important); // the star is PRIORITY:1
        Assert.Equal("2026-11-07", plumber.DueRaw); // DUE;TZID=Europe/Zurich:…T000000 is a day, not 23:00 UTC the day before

        Assert.True(tasks[1].Completed);
        Assert.False(tasks[1].Important); // PRIORITY:9 is low

        Assert.Equal("Ideas for the garden", tasks[2].Title);
        Assert.Null(tasks[2].DueRaw);
        Assert.False(tasks[2].Completed);
    }

    // ---- iCloud Reminders ----------------------------------------------------------------------------

    [Fact]
    public void S619_icloud_reminders_read_folded_titles_timed_dues_and_a_completion_without_status()
    {
        var tasks = Report("icloud-reminders-report.xml");

        // The VTODO without a UID has no id to keep across refreshes: skipped, the rest read.
        Assert.Equal(3, tasks.Count);

        var cleaning = tasks[0];
        Assert.Equal("Pick up the dry cleaning", cleaning.Title);
        Assert.True(cleaning.Important);
        Assert.Equal("2026-11-08T16:30:00.0000000Z", cleaning.DueRaw);

        // Reminders on a Mac may leave STATUS out and write only COMPLETED.
        var milk = tasks[1];
        Assert.Equal("Buy milk", milk.Title);
        Assert.True(milk.Completed);
        Assert.NotNull(milk.CompletedOn);
    }

    [Fact]
    public void S620_a_monthly_reminder_shows_the_next_month_once_this_months_has_passed()
    {
        var rent = Assert.Single(Report("icloud-reminders-report.xml"), t => t.Title == "Pay the rent");
        Assert.Equal("2026-12-01", rent.DueRaw);
        Assert.False(rent.Completed);
    }

    // ---- the rules, one by one ---------------------------------------------------------------------

    [Fact]
    public void S620_a_series_that_has_run_out_shows_its_last_occurrence()
    {
        var task = Assert.Single(Ics("BEGIN:VTODO\r\nUID:s\r\nSUMMARY:Pay\r\nDUE;VALUE=DATE:20260101\r\nRRULE:FREQ=MONTHLY;COUNT=2\r\nEND:VTODO\r\n"));
        Assert.Equal("2026-02-01", task.DueRaw);
        Assert.False(task.Completed);
    }

    [Fact]
    public void S620_a_finished_series_is_the_finished_master()
    {
        var task = Assert.Single(Ics("BEGIN:VTODO\r\nUID:s\r\nSUMMARY:Pay\r\nSTATUS:COMPLETED\r\nDUE;VALUE=DATE:20260101\r\nRRULE:FREQ=MONTHLY\r\nEND:VTODO\r\n"));
        Assert.True(task.Completed);
        Assert.Equal("2026-01-01", task.DueRaw);
    }

    [Fact]
    public void S620_a_due_today_instance_is_current_and_a_cancelled_one_is_skipped()
    {
        const string series = "BEGIN:VTODO\r\nUID:d\r\nSUMMARY:Stand-up notes\r\nDUE:20261107T090000Z\r\nRRULE:FREQ=DAILY\r\nEND:VTODO\r\n";
        Assert.Equal("2026-11-07T09:00:00.0000000Z", Assert.Single(Ics(series)).DueRaw);

        var cancelled = series + "BEGIN:VTODO\r\nUID:d\r\nRECURRENCE-ID:20261107T090000Z\r\nSTATUS:CANCELLED\r\nSUMMARY:Stand-up notes\r\nDUE:20261107T090000Z\r\nEND:VTODO\r\n";
        Assert.Equal("2026-11-08T09:00:00.0000000Z", Assert.Single(Ics(cancelled)).DueRaw);
    }

    [Fact]
    public void S620_a_moved_instance_shows_with_its_own_fields()
    {
        var task = Assert.Single(Ics(
            "BEGIN:VTODO\r\nUID:m\r\nSUMMARY:Bins\r\nDUE;VALUE=DATE:20261102\r\nRRULE:FREQ=WEEKLY\r\nEND:VTODO\r\n"
            + "BEGIN:VTODO\r\nUID:m\r\nRECURRENCE-ID;VALUE=DATE:20261109\r\nSUMMARY:Bins (holiday week)\r\nDUE;VALUE=DATE:20261110\r\nEND:VTODO\r\n"));
        Assert.Equal("Bins (holiday week)", task.Title);
        Assert.Equal("2026-11-10", task.DueRaw);
        Assert.Equal("m", task.Id);
    }

    [Fact]
    public void S620_a_task_without_due_recurs_on_its_start()
    {
        var task = Assert.Single(Ics("BEGIN:VTODO\r\nUID:r\r\nSUMMARY:Review\r\nDTSTART;VALUE=DATE:20261001\r\nRRULE:FREQ=WEEKLY\r\nEND:VTODO\r\n"));
        Assert.Null(task.DueRaw); // nothing is due; the instance is still the current one
        Assert.False(task.Completed);
    }

    [Fact]
    public void S619_completion_follows_status_first()
    {
        // NEEDS-ACTION with a stale COMPLETED (a reopened task) is open; 100 % without STATUS is done.
        Assert.False(Assert.Single(Ics("BEGIN:VTODO\r\nUID:a\r\nSTATUS:NEEDS-ACTION\r\nCOMPLETED:20261101T100000Z\r\nEND:VTODO\r\n")).Completed);
        Assert.True(Assert.Single(Ics("BEGIN:VTODO\r\nUID:b\r\nPERCENT-COMPLETE:100\r\nEND:VTODO\r\n")).Completed);
        Assert.Null(Assert.Single(Ics("BEGIN:VTODO\r\nUID:c\r\nSTATUS:NEEDS-ACTION\r\nCOMPLETED:20261101T100000Z\r\nEND:VTODO\r\n")).CompletedOn);
    }

    [Fact]
    public void S619_priority_one_to_four_is_important()
    {
        foreach (var (priority, important) in new[] { ("0", false), ("1", true), ("4", true), ("5", false), ("9", false), ("high", false) })
        {
            var task = Assert.Single(Ics($"BEGIN:VTODO\r\nUID:p\r\nPRIORITY:{priority}\r\nEND:VTODO\r\n"));
            Assert.Equal(important, task.Important);
        }
    }

    [Fact]
    public void S619_a_floating_due_is_read_on_the_local_clock()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("plus2", TimeSpan.FromHours(2), "plus2", "plus2");
        var task = Assert.Single(IcsTasks.Read("BEGIN:VTODO\r\nUID:f\r\nDUE:20261110T093000\r\nEND:VTODO\r\n", "list", Today, zone, NoSystemZones));
        Assert.Equal("2026-11-10T07:30:00.0000000Z", task.DueRaw);
    }

    [Fact]
    public void S619_a_uid_seen_twice_is_one_task_and_no_summary_is_untitled()
    {
        var tasks = Ics("BEGIN:VTODO\r\nUID:x\r\nEND:VTODO\r\n"
            + "END:VCALENDAR\r\nBEGIN:VCALENDAR\r\nBEGIN:VTODO\r\nUID:x\r\nSUMMARY:Again\r\nEND:VTODO\r\n");
        var task = Assert.Single(tasks);
        Assert.Equal(string.Empty, task.Title);
    }

    [Fact]
    public void S619_events_and_journals_are_not_tasks_and_junk_is_nothing()
    {
        Assert.Empty(Ics("BEGIN:VEVENT\r\nUID:e\r\nDTSTART:20261107T090000Z\r\nEND:VEVENT\r\nBEGIN:VJOURNAL\r\nUID:j\r\nEND:VJOURNAL\r\n"));
        Assert.Empty(IcsTasks.Read("not a calendar", "list", Today, TimeZoneInfo.Utc));
        Assert.Empty(IcsTasks.Read(null, "list", Today, TimeZoneInfo.Utc));
    }

    // ---- read-only -----------------------------------------------------------------------------------

    [Fact]
    public void S621_caldav_tasks_are_read_only_and_the_others_are_not()
    {
        Assert.True(TaskWire.ReadOnly(HostProviders.CalDav));
        Assert.False(TaskWire.ReadOnly(HostProviders.Microsoft));
        Assert.False(TaskWire.ReadOnly(HostProviders.Google));
    }
}
