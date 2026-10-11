using System.Text.Json.Nodes;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-563] The calendar and contacts configs through the real state boundary: a garbled value never
/// refuses the file, and the current shape round-trips.
/// </summary>
public sealed class OfficeConfigTests
{
    private static JsonNode StateWith(WidgetKind kind, string configJson)
    {
        var state = AppState.CreateDefault();
        state.Views[0].Widgets.Add(WidgetInstance.Create(kind, new FracRect(0.1, 0.1, 0.2, 0.2)));
        state.Views[0].Widgets.Add(WidgetInstance.Create(WidgetKind.Clock, new FracRect(0.5, 0.1, 0.2, 0.2)));

        var document = JsonNode.Parse(SettingsTransfer.Serialize(state))!;
        var widget = document["views"]![0]!["widgets"]!.AsArray().First(w => w!["kind"]!.GetValue<string>() == kind.ToString())!;
        widget["config"] = JsonNode.Parse(configJson);
        return document;
    }

    private static T ConfigOf<T>(ParseResult parsed, WidgetKind kind)
        where T : WidgetConfig
        => Assert.IsType<T>(parsed.State!.Views[0].Widgets.First(w => w.Kind == kind).Config);

    [Fact]
    public void S563_GarbledValuesFallBackInsteadOfRefusingTheFile()
    {
        var calendar = AppStateStore.Parse(StateWith(WidgetKind.Calendar,
            """{"kind":"calendar","accountIds":"nope","calendars":{"a":"x","b":["c1",7]},"calendarColors":{"k":5,"j":"#123456"},"layout":"sideways","weekStart":3,"agendaCount":900}""").ToJsonString());

        Assert.Equal(ParseRefusal.None, calendar.Refusal);
        var c = ConfigOf<CalendarConfig>(calendar, WidgetKind.Calendar);
        Assert.Empty(c.AccountIds);
        Assert.Equal(["c1"], c.Calendars["b"]);
        Assert.Empty(c.Calendars["a"]);
        Assert.Equal("#123456", Assert.Single(c.CalendarColors).Value);
        Assert.Equal(CalendarLayout.Month, c.Layout);
        Assert.Equal(WeekStartDay.Monday, c.WeekStart);
        Assert.Equal(100, c.AgendaCount);

        var contacts = AppStateStore.Parse(StateWith(WidgetKind.Contacts,
            """{"kind":"contacts","layout":"tiles","columns":["email","bogus",3],"count":-4}""").ToJsonString());

        Assert.Equal(ParseRefusal.None, contacts.Refusal);
        var k = ConfigOf<ContactsConfig>(contacts, WidgetKind.Contacts);
        Assert.Equal(ContactsLayout.List, k.Layout);
        Assert.Equal([ContactColumn.Email], k.Columns);
        Assert.Equal(5, k.Count);
    }

    [Fact]
    public void S561_CurrentShapeRoundTrips()
    {
        var state = AppState.CreateDefault();
        var widget = WidgetInstance.Create(WidgetKind.Calendar, new FracRect(0.1, 0.1, 0.2, 0.2));
        widget.Config = new CalendarConfig
        {
            AccountIds = ["google:1"],
            Calendars = new() { ["google:1"] = ["work@group.calendar.google.com"] },
            CalendarColors = new() { ["work@group.calendar.google.com"] = "#ff9660" },
            Layout = CalendarLayout.Year,
            WeekStart = WeekStartDay.Sunday,
            ShowBirthdays = false,
            AgendaCount = 7,
        };
        state.Views[0].Widgets.Add(widget);

        var json = SettingsTransfer.Serialize(state);
        Assert.Contains("\"layout\": \"year\"", json, StringComparison.Ordinal);
        Assert.Contains("\"weekStart\": \"sunday\"", json, StringComparison.Ordinal);

        var parsed = AppStateStore.Parse(json);
        var back = ConfigOf<CalendarConfig>(parsed, WidgetKind.Calendar);
        Assert.Equal(["google:1"], back.AccountIds);
        Assert.Equal(["work@group.calendar.google.com"], back.Calendars["google:1"]);
        Assert.Equal("#ff9660", back.CalendarColors["work@group.calendar.google.com"]);
        Assert.Equal(CalendarLayout.Year, back.Layout);
        Assert.Equal(WeekStartDay.Sunday, back.WeekStart);
        Assert.False(back.ShowBirthdays);
        Assert.Equal(7, back.AgendaCount);
        Assert.DoesNotContain(parsed.Repairs, r => r.Contains("calendar", StringComparison.Ordinal));
    }
}
