using Themia.Core.Office;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// CalDAV calendar data to events: the classic app's <c>parse_ics_events</c> tests, plus the time
/// zone, recurrence and cancellation handling it did not have.
/// </summary>
public sealed class IcsEventsTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A zone lookup that knows nothing, as an invariant-globalization build mostly does.</summary>
    private static TimeZoneInfo? NoSystemZones(string _) => null;

    /// <summary>A REPORT answer as a server writes it: one resource whose calendar-data is the object.</summary>
    private static string Wrap(string vevents, string vtimezones = "")
        => "<D:multistatus xmlns:D=\"DAV:\" xmlns:C=\"urn:ietf:params:xml:ns:caldav\"><D:response><D:href>/cal/1.ics</D:href>"
            + "<D:propstat><D:prop><C:calendar-data>"
            + $"BEGIN:VCALENDAR\r\nVERSION:2.0\r\n{vtimezones}{vevents}END:VCALENDAR\r\n"
            + "</C:calendar-data></D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat></D:response></D:multistatus>";

    /// <summary>Through the same path the widget uses: the calendar data out of the XML, then the parser.</summary>
    private static IReadOnlyList<CalendarEvent> Read(string text, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        var data = text.TrimStart().StartsWith('<') ? string.Join("\r\n", DavOffice.CalendarData(text)) : text;
        return IcsEvents.Read(data, from ?? From, to ?? To, Utc, NoSystemZones);
    }

    // ---- the classic cases ------------------------------------------------------------------------

    [Fact]
    public void S576_ExtractsTheFields()
    {
        var events = Read(Wrap(
            "BEGIN:VEVENT\r\nUID:evt-1@example.com\r\nSUMMARY:Team Sync\r\nDTSTART:20260401T090000Z\r\nDTEND:20260401T093000Z\r\n"
            + "LOCATION:Room 4\r\nORGANIZER;CN=\"Alice Boss\":mailto:alice@example.com\r\nEND:VEVENT\r\n"));

        var e = Assert.Single(events);
        Assert.Equal("evt-1@example.com", e.Id);
        Assert.Equal("Team Sync", e.Subject);
        Assert.Equal(new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), e.Start);
        Assert.Equal(new DateTimeOffset(2026, 4, 1, 9, 30, 0, TimeSpan.Zero), e.End);
        Assert.False(e.AllDay);
        Assert.Equal("Room 4", e.Location);
        Assert.Equal("Alice Boss", e.Organizer);
    }

    [Fact]
    public void S576_AllDayKeepsItsExclusiveEnd()
    {
        var one = Assert.Single(Read(Wrap("BEGIN:VEVENT\r\nUID:a\r\nSUMMARY:Conference\r\nDTSTART;VALUE=DATE:20260605\r\nDTEND;VALUE=DATE:20260606\r\nEND:VEVENT\r\n")));
        Assert.True(one.AllDay);
        Assert.Equal(new DateOnly(2026, 6, 5), one.FirstDay);
        Assert.Equal(new DateOnly(2026, 6, 6), one.EndDay);

        var trip = Assert.Single(Read(Wrap("BEGIN:VEVENT\r\nUID:b\r\nSUMMARY:Trip\r\nDTSTART;VALUE=DATE:20260605\r\nDTEND;VALUE=DATE:20260608\r\nEND:VEVENT\r\n")));
        Assert.Equal(new DateOnly(2026, 6, 8), trip.EndDay);

        var bare = Assert.Single(Read(Wrap("BEGIN:VEVENT\r\nUID:c\r\nDTSTART;VALUE=DATE:20260605\r\nEND:VEVENT\r\n")));
        Assert.Equal(new DateOnly(2026, 6, 6), bare.EndDay);
        Assert.Equal(string.Empty, bare.Subject); // [S-3612] untitled: the widget words it
    }

    [Fact]
    public void S576_NonAsciiOrganizerAndBareVevent()
    {
        var e = Assert.Single(Read("BEGIN:VEVENT\r\nUID:x\r\nSUMMARY:S\r\nDTSTART:20260401T090000Z\r\n"
            + "ORGANIZER;CN=\"Renée Straße Müller\":mailto:r@x.com\r\nEND:VEVENT"));
        Assert.Equal("Renée Straße Müller", e.Organizer);
    }

    [Fact]
    public void S576_FoldedLinesEscapesAndQuotedParameters()
    {
        var e = Assert.Single(Read(Wrap(
            "BEGIN:VEVENT\r\nUID:f\r\nSUMMARY:Planning\\, Q3 \r\n review\r\nDTSTART:20260401T090000Z\r\nDURATION:PT1H30M\r\n"
            + "ORGANIZER;CN=\"Doe; Jane: PhD\":mailto:j@x.com\r\nLOCATION:Room 1\\nWest\r\nEND:VEVENT\r\n")));
        Assert.Equal("Planning, Q3 review", e.Subject);
        Assert.Equal("Doe; Jane: PhD", e.Organizer);
        Assert.Equal("Room 1\nWest", e.Location);
        Assert.Equal(TimeSpan.FromMinutes(90), e.End - e.Start);
    }

    [Fact]
    public void S576_XmlEntitiesInCalendarDataAreDecoded()
    {
        // What DavXml hands over is already decoded; the parser must not see "&amp;".
        var e = Assert.Single(Read(Wrap("BEGIN:VEVENT\r\nUID:e\r\nSUMMARY:R&amp;D review\r\nDTSTART:20260401T090000Z\r\nEND:VEVENT\r\n")));
        Assert.Equal("R&D review", e.Subject);
    }

    [Fact]
    public void S576_CancelledAndUndatedEventsAreNotListed()
    {
        var events = Read(Wrap(
            "BEGIN:VEVENT\r\nUID:c\r\nSTATUS:CANCELLED\r\nDTSTART:20260401T090000Z\r\nEND:VEVENT\r\n"
            + "BEGIN:VEVENT\r\nUID:n\r\nSUMMARY:No start\r\nEND:VEVENT\r\n"
            + "BEGIN:VEVENT\r\nUID:k\r\nDTSTART:20260401T100000Z\r\nEND:VEVENT\r\n"));
        Assert.Equal("k", Assert.Single(events).Id);
    }

    [Fact]
    public void S576_GarbageNeverThrows()
    {
        Assert.Empty(Read("not ics at all"));
        Assert.Empty(Read(""));
        Assert.Empty(Read("BEGIN:VEVENT\r\nDTSTART:20€60401\r\nEND:VEVENT"));
        Assert.Empty(Read("END:VEVENT\r\nEND:VCALENDAR"));
        Assert.Null(Ical.ParseDuration("P99999999999999W"));
        Assert.Null(Ical.ParseDuration("PT"));
        Assert.Equal(TimeSpan.FromDays(15), Ical.ParseDuration("P2W1D"));
        Assert.Equal(TimeSpan.FromMinutes(-15), Ical.ParseDuration("-PT15M"));
    }

    // ---- [S-578] time zones ------------------------------------------------------------------------

    private const string NewYork =
        "BEGIN:VTIMEZONE\r\nTZID:America/New_York\r\n"
        + "BEGIN:DAYLIGHT\r\nTZOFFSETFROM:-0500\r\nTZOFFSETTO:-0400\r\nDTSTART:20070311T020000\r\nRRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=2SU\r\nEND:DAYLIGHT\r\n"
        + "BEGIN:STANDARD\r\nTZOFFSETFROM:-0400\r\nTZOFFSETTO:-0500\r\nDTSTART:20071104T020000\r\nRRULE:FREQ=YEARLY;BYMONTH=11;BYDAY=1SU\r\nEND:STANDARD\r\n"
        + "END:VTIMEZONE\r\n";

    [Fact]
    public void S578_ZonedTimesResolveThroughTheCalendarsOwnVtimezone()
    {
        var events = Read(Wrap(
            "BEGIN:VEVENT\r\nUID:s\r\nDTSTART;TZID=America/New_York:20260401T120000\r\nDTEND;TZID=America/New_York:20260401T130000\r\nEND:VEVENT\r\n"
            + "BEGIN:VEVENT\r\nUID:w\r\nDTSTART;TZID=America/New_York:20260115T120000\r\nDTEND;TZID=America/New_York:20260115T130000\r\nEND:VEVENT\r\n",
            NewYork));

        var summer = events.Single(e => e.Id == "s");
        Assert.Equal(new DateTimeOffset(2026, 4, 1, 16, 0, 0, TimeSpan.Zero), summer.Start.ToUniversalTime());
        Assert.Equal(new DateTimeOffset(2026, 4, 1, 17, 0, 0, TimeSpan.Zero), summer.End.ToUniversalTime());

        var winter = events.Single(e => e.Id == "w");
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 17, 0, 0, TimeSpan.Zero), winter.Start.ToUniversalTime());
    }

    [Fact]
    public void S578_TransitionDaysFollowTheRule()
    {
        var zones = new IcalZones(Ical.Descendants(Ical.Parse("BEGIN:VCALENDAR\r\n" + NewYork + "END:VCALENDAR"), "VTIMEZONE"), Utc, NoSystemZones);
        Assert.Equal(TimeSpan.FromHours(-5), zones.OffsetOf("America/New_York", new DateTime(2026, 3, 8, 1, 0, 0)));
        Assert.Equal(TimeSpan.FromHours(-4), zones.OffsetOf("America/New_York", new DateTime(2026, 3, 8, 3, 0, 0)));
        Assert.Equal(TimeSpan.FromHours(-4), zones.OffsetOf("America/New_York", new DateTime(2026, 11, 1, 1, 0, 0)));
        Assert.Equal(TimeSpan.FromHours(-5), zones.OffsetOf("America/New_York", new DateTime(2026, 11, 1, 3, 0, 0)));
    }

    [Fact]
    public void S578_UnknownZoneFallsBackToTheLocalClockAndSystemZonesAreUsed()
    {
        var floating = Assert.Single(Read(Wrap("BEGIN:VEVENT\r\nUID:m\r\nDTSTART;TZID=Mars/Olympus_Mons:20260401T120000\r\nEND:VEVENT\r\n")));
        Assert.Equal(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero), floating.Start);

        var plus3 = TimeZoneInfo.CreateCustomTimeZone("Fixed+3", TimeSpan.FromHours(3), "Fixed+3", "Fixed+3");
        var viaSystem = Assert.Single(IcsEvents.Read(
            Assert.Single(DavOffice.CalendarData(Wrap("BEGIN:VEVENT\r\nUID:o\r\nDTSTART;TZID=Fixed+3:20260401T120000\r\nEND:VEVENT\r\n"))),
            From, To, Utc, id => id == "Fixed+3" ? plus3 : null));
        Assert.Equal(new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), viaSystem.Start.ToUniversalTime());
    }

    [Theory]
    [InlineData("+0100", 60)]
    [InlineData("-0500", -300)]
    [InlineData("+0530", 330)]
    [InlineData("+053000", 330)]
    public void S578_Offsets(string text, int minutes) => Assert.Equal(TimeSpan.FromMinutes(minutes), IcalZones.ParseOffset(text));

    [Theory]
    [InlineData("0100")]
    [InlineData("+1")]
    [InlineData("+2500")]
    [InlineData(null)]
    public void S578_BadOffsetsAreNull(string? text) => Assert.Null(IcalZones.ParseOffset(text));

    // ---- [S-577] recurrence ------------------------------------------------------------------------

    [Fact]
    public void S577_WeeklyMasterIsExpandedWithinTheWindow()
    {
        var events = Read(
            Wrap("BEGIN:VEVENT\r\nUID:standup\r\nSUMMARY:Standup\r\nDTSTART:20260105T090000Z\r\nDTEND:20260105T091500Z\r\nRRULE:FREQ=WEEKLY;BYDAY=MO,WE\r\nEND:VEVENT\r\n"),
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal([new DateTime(2026, 3, 2, 9, 0, 0), new DateTime(2026, 3, 4, 9, 0, 0)], events.Select(e => e.Start.UtcDateTime));
        Assert.All(events, e => Assert.Equal(TimeSpan.FromMinutes(15), e.End - e.Start));
        Assert.Equal(2, events.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void S577_CountUntilExdateAndOverrides()
    {
        var events = Read(Wrap(
            "BEGIN:VEVENT\r\nUID:r\r\nSUMMARY:Daily\r\nDTSTART:20260601T080000Z\r\nDTEND:20260601T083000Z\r\nRRULE:FREQ=DAILY;COUNT=5\r\n"
            + "EXDATE:20260602T080000Z\r\nEND:VEVENT\r\n"
            + "BEGIN:VEVENT\r\nUID:r\r\nRECURRENCE-ID:20260603T080000Z\r\nSUMMARY:Moved\r\nDTSTART:20260603T150000Z\r\nDTEND:20260603T153000Z\r\nEND:VEVENT\r\n"
            + "BEGIN:VEVENT\r\nUID:r\r\nRECURRENCE-ID:20260604T080000Z\r\nSTATUS:CANCELLED\r\nDTSTART:20260604T080000Z\r\nEND:VEVENT\r\n"
            + "BEGIN:VEVENT\r\nUID:u\r\nSUMMARY:Until\r\nDTSTART:20260701T080000Z\r\nRRULE:FREQ=DAILY;UNTIL=20260703T080000Z\r\nEND:VEVENT\r\n"));

        var daily = events.Where(e => e.Id.StartsWith("r#", StringComparison.Ordinal)).OrderBy(e => e.Start).ToList();
        Assert.Equal(["Daily", "Moved", "Daily"], daily.Select(e => e.Subject));
        Assert.Equal(new DateTime(2026, 6, 3, 15, 0, 0), daily[1].Start.UtcDateTime);
        Assert.Equal(new DateTime(2026, 6, 5, 8, 0, 0), daily[2].Start.UtcDateTime);

        Assert.Equal(3, events.Count(e => e.Subject == "Until"));
    }

    [Fact]
    public void S577_MonthlyNthWeekdayAndYearlyLeapDay()
    {
        var events = Read(Wrap(
            "BEGIN:VEVENT\r\nUID:m\r\nSUMMARY:Review\r\nDTSTART:20260109T100000Z\r\nRRULE:FREQ=MONTHLY;BYDAY=-1FR;COUNT=4\r\nEND:VEVENT\r\n"
            + "BEGIN:VEVENT\r\nUID:l\r\nSUMMARY:Leap\r\nDTSTART;VALUE=DATE:20240229\r\nRRULE:FREQ=YEARLY\r\nEND:VEVENT\r\n"),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2029, 1, 1, 0, 0, 0, TimeSpan.Zero));

        // DTSTART is always the first instance, then the last Friday of each month.
        Assert.Equal(
            [new DateTime(2026, 1, 9, 10, 0, 0), new DateTime(2026, 1, 30, 10, 0, 0), new DateTime(2026, 2, 27, 10, 0, 0), new DateTime(2026, 3, 27, 10, 0, 0)],
            events.Where(e => e.Subject == "Review").Select(e => e.Start.UtcDateTime));

        // RFC 5545: a yearly 29 February only recurs in leap years.
        Assert.Equal([new DateOnly(2028, 2, 29)], events.Where(e => e.Subject == "Leap").Select(e => e.FirstDay));
    }

    [Fact]
    public void S577_ServerExpandedInstancesAreEventsAndUnsupportedRulesShowOnce()
    {
        var events = Read(Wrap(
            "BEGIN:VEVENT\r\nUID:x\r\nRECURRENCE-ID:20260301T090000Z\r\nSUMMARY:Inst\r\nDTSTART:20260301T090000Z\r\nEND:VEVENT\r\n"
            + "BEGIN:VEVENT\r\nUID:x\r\nRECURRENCE-ID:20260302T090000Z\r\nSUMMARY:Inst\r\nDTSTART:20260302T090000Z\r\nEND:VEVENT\r\n"
            + "BEGIN:VEVENT\r\nUID:p\r\nSUMMARY:SetPos\r\nDTSTART:20260310T090000Z\r\nRRULE:FREQ=MONTHLY;BYDAY=MO,TU;BYSETPOS=-1\r\nEND:VEVENT\r\n"));

        Assert.Equal(2, events.Count(e => e.Subject == "Inst"));
        Assert.Equal("p", Assert.Single(events, e => e.Subject == "SetPos").Id);
    }

    [Fact]
    public void S577_RuleParsing()
    {
        var rule = RecurrenceRule.Parse("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,-1FR;WKST=SU");
        Assert.NotNull(rule);
        Assert.Equal(RecurrenceFrequency.Weekly, rule.Frequency);
        Assert.Equal(2, rule.Interval);
        Assert.Equal(DayOfWeek.Sunday, rule.WeekStart);
        Assert.Equal([(0, DayOfWeek.Monday), (-1, DayOfWeek.Friday)], rule.ByDay);
        Assert.True(rule.Supported);

        Assert.Null(RecurrenceRule.Parse("INTERVAL=2"));
        Assert.False(RecurrenceRule.Parse("FREQ=HOURLY")?.Supported ?? false);
        Assert.False(RecurrenceRule.Parse("FREQ=YEARLY;BYDAY=20MO")!.Supported);
    }

    [Fact]
    public void S577_ImpossibleRuleTerminates()
    {
        var rule = RecurrenceRule.Parse("FREQ=YEARLY;BYMONTH=2;BYMONTHDAY=30")!;
        Assert.Single(rule.Occurrences(new DateTime(2026, 1, 5, 9, 0, 0)).Take(10));
    }

    // ---- [S-578] DAV listing -----------------------------------------------------------------------

    [Fact]
    public void S578_CalendarListingResolvesFlagsTheDefaultAndDropsForeignHosts()
    {
        const string listing = """
            <d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav" xmlns:x="http://apple.com/ns/ical/">
              <d:response><d:href>/dav/calendars/alice/</d:href>
                <d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>
              <d:response><d:href>/dav/calendars/alice/personal/</d:href>
                <d:propstat><d:prop><d:displayname>Personal</d:displayname>
                  <d:resourcetype><d:collection/><c:calendar/></d:resourcetype>
                  <c:supported-calendar-component-set><c:comp name="VEVENT"/></c:supported-calendar-component-set>
                  <x:calendar-color>#FF2968FF</x:calendar-color></d:prop></d:propstat></d:response>
              <d:response><d:href>/dav/calendars/alice/work/</d:href>
                <d:propstat><d:prop><d:resourcetype><d:collection/><c:calendar/></d:resourcetype></d:prop></d:propstat></d:response>
              <d:response><d:href>/dav/calendars/alice/tasks/</d:href>
                <d:propstat><d:prop><d:displayname>Tasks</d:displayname><d:resourcetype><d:collection/><c:calendar/></d:resourcetype>
                  <c:supported-calendar-component-set><c:comp name="VTODO"/></c:supported-calendar-component-set></d:prop></d:propstat></d:response>
              <d:response><d:href>https://evil.example/steal/</d:href>
                <d:propstat><d:prop><d:resourcetype><d:collection/><c:calendar/></d:resourcetype></d:prop></d:propstat></d:response>
            </d:multistatus>
            """;

        var listedAt = new Uri("https://dav.example.com/dav/calendars/alice/");
        var calendars = DavOffice.Calendars(listing, listedAt, new Uri("https://dav.example.com/dav/calendars/alice/work"));

        Assert.Equal(2, calendars.Count);
        Assert.Equal(new CalendarInfo("https://dav.example.com/dav/calendars/alice/personal/", "Personal", false, "#ff2968"), calendars[0]);
        Assert.Equal(new CalendarInfo("https://dav.example.com/dav/calendars/alice/work/", "work", true, null), calendars[1]);
    }

    [Fact]
    public void S578_OnlyTheAccountsOriginGetsItsPassword()
    {
        var account = new Uri("https://dav.example.com/cal/");
        Assert.True(DavOffice.SameOrigin(new Uri("https://dav.example.com/other/"), account));
        Assert.False(DavOffice.SameOrigin(new Uri("http://dav.example.com/cal/"), account));
        Assert.False(DavOffice.SameOrigin(new Uri("https://dav.example.com:8443/cal/"), account));
        Assert.False(DavOffice.SameOrigin(new Uri("https://evil.example/cal/"), account));
    }

    [Fact]
    public void S578_QueryAsksForExpansionInUtc()
    {
        var body = DavOffice.CalendarQuery(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(2)), new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(1)));
        Assert.Contains("<C:expand start=\"20260930T220000Z\" end=\"20261031T230000Z\"/>", body, StringComparison.Ordinal);
        Assert.Contains("<C:time-range start=\"20260930T220000Z\" end=\"20261031T230000Z\"/>", body, StringComparison.Ordinal);
    }
}
