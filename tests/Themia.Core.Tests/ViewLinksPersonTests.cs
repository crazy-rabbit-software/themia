using System.Text.Json;
using Themia.Core.Links;
using Themia.Core.Office;
using Themia.Core.Sync;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-11943] An event's person is its organiser: by the address the provider gave, else the name it shows.</summary>
public sealed class ViewLinksPersonTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private static CalendarEvent Event(string? organizer, string? address = null)
        => new() { Id = "e", Start = At, End = At.AddHours(1), Organizer = organizer, OrganizerAddress = address };

    [Fact]
    public void S11943_an_organiser_with_an_address_is_the_same_person_as_a_sender_or_a_contact()
    {
        var organiser = CalendarShare.Person(Event("Ana Silva", "Ana.Silva@Lumen.example"));

        Assert.Equal(new PersonLink("ana.silva@lumen.example"), organiser);
        Assert.Equal(LinkKeys.PersonLink("ana.silva@lumen.example", "Ana"), organiser);
    }

    [Fact]
    public void S11943_an_organiser_named_only_is_the_name_folded_and_none_is_no_one()
    {
        Assert.Equal(new PersonLink("ana silva"), CalendarShare.Person(Event("  Ana   Silva ")));
        Assert.Null(CalendarShare.Person(Event(null)));
        Assert.Null(CalendarShare.Person(Event(" ")));
    }

    [Fact]
    public void S11943_the_providers_give_the_organisers_address_and_the_shelf_keeps_it()
    {
        var graph = OfficeWire.FromGraph(JsonSerializer.Deserialize("""
            { "id": "g", "subject": "Review", "isAllDay": false, "start": { "dateTime": "2026-11-12T09:00:00" }, "end": { "dateTime": "2026-11-12T10:00:00" },
              "organizer": { "emailAddress": { "name": "Ana Silva", "address": "ana.silva@lumen.example" } } }
            """, OfficeJsonContext.Default.GraphEvent)!)!;
        Assert.Equal("Ana Silva", graph.Organizer);
        Assert.Equal("ana.silva@lumen.example", graph.OrganizerAddress);

        var google = OfficeWire.FromGoogle(JsonSerializer.Deserialize("""
            { "id": "o", "summary": "Review", "start": { "dateTime": "2026-11-12T09:00:00Z" }, "end": { "dateTime": "2026-11-12T10:00:00Z" },
              "organizer": { "displayName": "Ana Silva", "email": "ana.silva@lumen.example" } }
            """, OfficeJsonContext.Default.GoogleEvent)!)!;
        Assert.Equal("ana.silva@lumen.example", google.OrganizerAddress);

        var ics = Assert.Single(IcsEvents.Read(
            "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:i\r\nSUMMARY:S\r\nDTSTART:20261112T090000Z\r\nORGANIZER;CN=\"Ana Silva\":mailto:ana.silva@lumen.example\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n",
            At.AddDays(-1), At.AddDays(1), TimeZoneInfo.Utc, _ => null));
        Assert.Equal("Ana Silva", ics.Organizer);
        Assert.Equal("ana.silva@lumen.example", ics.OrganizerAddress);

        var named = Assert.Single(IcsEvents.Read(
            "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:n\r\nSUMMARY:S\r\nDTSTART:20261112T090000Z\r\nORGANIZER;CN=Ana:urn:x-name:ana\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n",
            At.AddDays(-1), At.AddDays(1), TimeZoneInfo.Utc, _ => null));
        Assert.Null(named.OrganizerAddress);

        Assert.Equal("ana.silva@lumen.example", OfficeShelves.Restore(OfficeShelves.Store(graph)).OrganizerAddress);
    }
}
