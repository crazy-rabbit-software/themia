using System.Text.Json;
using Themia.Core.Office;
using Themia.Core.Sync;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3610]..[S-3612] Provider events as the user's real calendars send them, on synthetic wire
/// fixtures: a change round that only names an event never blanks it or makes an all-day event a
/// timed one across two days; an all-day event covers exactly its days in every zone; an untitled
/// event stays untitled for the widget to word.
/// </summary>
public sealed class CalendarProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Zurich in summer, the user's zone when the bugs were reported.</summary>
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.CreateCustomTimeZone("test+2", TimeSpan.FromHours(2), "test+2", "test+2");

    private static readonly TimeZoneInfo Kiribati = TimeZoneInfo.CreateCustomTimeZone("test+14", TimeSpan.FromHours(14), "test+14", "test+14");

    private static readonly TimeZoneInfo Hawaii = TimeZoneInfo.CreateCustomTimeZone("test-10", TimeSpan.FromHours(-10), "test-10", "test-10");

    private static readonly TimeZoneInfo[] Zones = [TimeZoneInfo.Utc, Zurich, Kiribati, Hawaii];

    private static readonly DateOnly Sunday = new(2026, 10, 11);

    private static GraphEvent Graph(string json) => JsonSerializer.Deserialize(json, OfficeJsonContext.Default.GraphEvent)!;

    private static GoogleEvent Google(string json) => JsonSerializer.Deserialize(json, OfficeJsonContext.Default.GoogleEvent)!;

    private static CalendarSpan Span(params CalendarEvent[] events) => new()
    {
        From = Now.AddDays(-365),
        To = Now.AddDays(365),
        NearAt = Now.AddHours(-1),
        FullAt = Now.AddHours(-1),
        Events = [.. events.Select(OfficeShelves.Store)],
    };

    /// <summary>Every local day an event is drawn on, as the month and week draw it.</summary>
    private static List<DateOnly> DaysShown(CalendarEvent item, TimeZoneInfo zone)
        => [.. Enumerable.Range(-3, 7).Select(Sunday.AddDays).Where(d => CalendarRules.OccursOn(item, d, zone))];

    // ---- [S-3610] a change round that only names an event ----------------------------------------

    /// <summary>A delta round's bare notice: the id and the times, as UTC midnights; no subject, no isAllDay.</summary>
    private const string Notice = """
        { "@odata.type": "#microsoft.graph.event", "id": "holiday",
          "start": { "dateTime": "2026-10-11T00:00:00.0000000", "timeZone": "UTC" },
          "end": { "dateTime": "2026-10-12T00:00:00.0000000", "timeZone": "UTC" } }
        """;

    [Fact]
    public void S3610_a_change_notice_without_isAllDay_is_no_event()
    {
        // Read as an event it was a timed one at 02:00–02:00 in Zurich: two days, and "No title".
        Assert.Null(OfficeWire.FromGraph(Graph(Notice)));
    }

    [Fact]
    public void S3610_a_delta_round_never_folds_a_notice_over_the_event_it_names()
    {
        var held = Span(CalendarEvent.AllDayEvent("holiday", "Public holiday", Sunday, Sunday.AddDays(1)));
        var body = $$"""
            {
              "@odata.deltaLink": "https://graph.microsoft.com/v1.0/me/calendarView/delta?$deltatoken=d2",
              "value": [
                {{Notice}},
                { "id": "standup", "subject": "Standup", "isAllDay": false, "isCancelled": false,
                  "start": { "dateTime": "2026-10-12T07:00:00.0000000" }, "end": { "dateTime": "2026-10-12T07:15:00.0000000" } },
                { "id": "dropped", "subject": "Lunch", "isAllDay": false, "isCancelled": true,
                  "start": { "dateTime": "2026-10-12T10:00:00.0000000" }, "end": { "dateTime": "2026-10-12T11:00:00.0000000" } },
                { "id": "gone", "@removed": { "reason": "deleted" } }
              ]
            }
            """;

        var page = DeltaPages.Read(body, DeltaDialect.Graph, OfficeJsonContext.Default.GraphEvent);
        var changes = OfficeWire.SortGraphChanges(page.Items, page.Removed);

        Assert.Equal(["holiday"], changes.Reread);
        Assert.Equal(["standup"], changes.Changed.Select(e => e.Id));
        Assert.Equal(["gone", "dropped"], changes.Removed);
        Assert.False(changes.ReadWindowInstead);

        var span = DeltaMerge.Events(held, changes.Changed, changes.Removed, Now);
        var holiday = OfficeShelves.Restore(span.Events.Single(e => e.Id == "holiday"));
        Assert.Equal("Public holiday", holiday.Subject);
        Assert.True(holiday.AllDay);
        Assert.Equal([Sunday], DaysShown(holiday, Zurich));
    }

    [Fact]
    public void S3610_the_event_read_again_by_id_replaces_the_held_one()
    {
        var held = Span(CalendarEvent.AllDayEvent("holiday", "Public holiday", Sunday, Sunday.AddDays(1)));
        var fresh = OfficeWire.FromGraph(Graph("""
            { "id": "holiday", "subject": "Bank holiday", "isAllDay": true, "isCancelled": false,
              "start": { "dateTime": "2026-10-11T00:00:00.0000000", "timeZone": "UTC" },
              "end": { "dateTime": "2026-10-13T00:00:00.0000000", "timeZone": "UTC" } }
            """))!;

        var span = DeltaMerge.Events(held, [fresh], [], Now);

        var back = OfficeShelves.Restore(Assert.Single(span.Events));
        Assert.Equal("Bank holiday", back.Subject);
        Assert.Equal([Sunday, Sunday.AddDays(1)], DaysShown(back, Zurich));
    }

    [Fact]
    public void S3610_many_notices_read_the_window_whole_instead()
    {
        var notices = Enumerable.Range(0, GraphChanges.MaxReread + 1)
            .Select(i => new GraphEvent { Id = $"e{i}", Start = new GraphDateTime { DateTime = "2026-10-11T09:00:00" } })
            .ToList();

        var changes = OfficeWire.SortGraphChanges(notices, []);

        Assert.True(changes.ReadWindowInstead);
        Assert.False(OfficeWire.SortGraphChanges(notices.Take(GraphChanges.MaxReread).ToList(), []).ReadWindowInstead);
    }

    [Fact]
    public void S3610_an_untitled_event_is_kept_untitled_for_the_widget_to_word()
    {
        var item = OfficeWire.FromGraph(Graph("""
            { "id": "x", "subject": "", "isAllDay": false, "start": { "dateTime": "2026-10-11T09:00:00" }, "end": { "dateTime": "2026-10-11T10:00:00" } }
            """))!;

        Assert.Equal(string.Empty, item.Subject);
    }

    // ---- [S-3611] all-day events are dates ---------------------------------------------------------

    [Theory]
    [InlineData("2026-10-11T00:00:00.0000000", "2026-10-12T00:00:00.0000000")]
    [InlineData("2026-10-11T00:00:00Z", "2026-10-12T00:00:00Z")]
    [InlineData("2026-10-10T22:00:00.0000000", "2026-10-11T22:00:00.0000000")]
    [InlineData("2026-10-11T05:00:00.0000000", "2026-10-12T05:00:00.0000000")]
    public void S3611_a_graph_all_day_event_covers_exactly_its_day_in_every_zone(string start, string end)
    {
        var item = OfficeWire.FromGraph(Graph($$"""
            { "id": "h", "subject": "Holiday", "isAllDay": true,
              "start": { "dateTime": "{{start}}", "timeZone": "UTC" }, "end": { "dateTime": "{{end}}", "timeZone": "UTC" } }
            """))!;

        Assert.True(item.AllDay);
        Assert.Equal(Sunday, item.FirstDay);
        Assert.Equal(Sunday.AddDays(1), item.EndDay);

        foreach (var zone in Zones)
        {
            Assert.Equal([Sunday], DaysShown(item, zone));
            Assert.Equal(Sunday, CalendarFit.LastDay(item, zone));
            Assert.False(CalendarFit.IsMultiDay(item, zone));
        }
    }

    [Fact]
    public void S3611_a_google_all_day_event_ends_before_its_exclusive_end_date()
    {
        var one = OfficeWire.FromGoogle(Google("""{"id":"g","summary":"Holiday","start":{"date":"2026-10-11"},"end":{"date":"2026-10-12"}}"""))!;
        var three = OfficeWire.FromGoogle(Google("""{"id":"t","summary":"Trip","start":{"date":"2026-10-11"},"end":{"date":"2026-10-14"}}"""))!;

        foreach (var zone in Zones)
        {
            Assert.Equal([Sunday], DaysShown(one, zone));
            Assert.Equal([Sunday, Sunday.AddDays(1), Sunday.AddDays(2)], DaysShown(three, zone));
        }
    }

    [Fact]
    public void S3611_a_stored_all_day_event_still_covers_its_day()
    {
        var item = CalendarEvent.AllDayEvent("h", "Holiday", Sunday, Sunday.AddDays(1));

        var back = OfficeShelves.Restore(OfficeShelves.Store(item));

        foreach (var zone in Zones)
        {
            Assert.Equal([Sunday], DaysShown(back, zone));
        }
    }

    // ---- [S-3612] iCalendar ------------------------------------------------------------------------

    private static IReadOnlyList<CalendarEvent> Ics(string vevent, TimeZoneInfo zone)
        => IcsEvents.Read($"BEGIN:VCALENDAR\r\nVERSION:2.0\r\n{vevent}END:VCALENDAR\r\n",
            Now.AddDays(-30), Now.AddDays(30), zone, _ => null);

    [Fact]
    public void S3612_a_date_valued_event_covers_its_day_with_an_exclusive_dtend()
    {
        foreach (var zone in Zones)
        {
            var item = Assert.Single(Ics("BEGIN:VEVENT\r\nUID:a\r\nSUMMARY:Holiday\r\nDTSTART;VALUE=DATE:20261011\r\nDTEND;VALUE=DATE:20261012\r\nEND:VEVENT\r\n", zone));
            Assert.True(item.AllDay);
            Assert.Equal([Sunday], DaysShown(item, zone));
        }
    }

    [Fact]
    public void S3612_an_outlook_flagged_all_day_event_is_a_date_not_two_zoned_midnights()
    {
        const string vevent = "BEGIN:VEVENT\r\nUID:o\r\nSUMMARY:Holiday\r\nX-MICROSOFT-CDO-ALLDAYEVENT:TRUE\r\n"
            + "DTSTART;TZID=W. Europe Standard Time:20261011T000000\r\nDTEND;TZID=W. Europe Standard Time:20261012T000000\r\nEND:VEVENT\r\n";

        foreach (var zone in Zones)
        {
            var item = Assert.Single(Ics(vevent, zone));
            Assert.True(item.AllDay);
            Assert.Equal([Sunday], DaysShown(item, zone));
        }
    }

    [Fact]
    public void S3612_an_event_without_a_summary_is_untitled_not_an_english_placeholder()
    {
        var item = Assert.Single(Ics("BEGIN:VEVENT\r\nUID:n\r\nDTSTART:20261011T090000Z\r\nDTEND:20261011T100000Z\r\nEND:VEVENT\r\n", Zurich));

        Assert.Equal(string.Empty, item.Subject);
    }

    [Fact]
    public void S3612_a_folded_escaped_summary_reads_whole()
    {
        var item = Assert.Single(Ics("BEGIN:VEVENT\r\nUID:f\r\nSUMMARY:Quarterly planning\\, Zürich\r\n  office\r\nDTSTART:20261011T090000Z\r\nDTEND:20261011T100000Z\r\nEND:VEVENT\r\n", Zurich));

        Assert.Equal("Quarterly planning, Zürich office", item.Subject);
    }
}
