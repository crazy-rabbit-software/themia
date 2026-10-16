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
        var calendar = AppStateStore.Parse(StateWith(WidgetKind.MonthCalendar,
            """{"kind":"monthCalendar","accountIds":"nope","calendars":{"a":"x","b":["c1",7]},"calendarColors":{"k":5,"j":"#123456"},"weekStart":3}""").ToJsonString());

        Assert.Equal(ParseRefusal.None, calendar.Refusal);
        var c = ConfigOf<MonthCalendarConfig>(calendar, WidgetKind.MonthCalendar);
        Assert.Empty(c.AccountIds);
        Assert.Equal(["c1"], c.Calendars["b"]);
        Assert.Empty(c.Calendars["a"]);
        Assert.Equal("#123456", Assert.Single(c.CalendarColors).Value);

        var agenda = AppStateStore.Parse(StateWith(WidgetKind.Agenda, """{"kind":"agenda","agendaCount":900}""").ToJsonString());
        Assert.Equal(ParseRefusal.None, agenda.Refusal);
        Assert.Equal(100, ConfigOf<AgendaConfig>(agenda, WidgetKind.Agenda).AgendaCount);

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
        var widget = WidgetInstance.Create(WidgetKind.YearCalendar, new FracRect(0.1, 0.1, 0.2, 0.2));
        widget.Config = new YearCalendarConfig
        {
            AccountIds = ["google:1"],
            Calendars = new() { ["google:1"] = ["work@group.calendar.google.com"] },
            CalendarColors = new() { ["work@group.calendar.google.com"] = "#ff9660" },
            ShowBirthdays = false,
        };
        state.Views[0].Widgets.Add(widget);
        var agenda = WidgetInstance.Create(WidgetKind.Agenda, new FracRect(0.4, 0.1, 0.2, 0.2));
        agenda.Config = new AgendaConfig { AgendaCount = 7 };
        state.Views[0].Widgets.Add(agenda);

        var json = SettingsTransfer.Serialize(state);

        var parsed = AppStateStore.Parse(json);
        var back = ConfigOf<YearCalendarConfig>(parsed, WidgetKind.YearCalendar);
        Assert.Equal(["google:1"], back.AccountIds);
        Assert.Equal(["work@group.calendar.google.com"], back.Calendars["google:1"]);
        Assert.Equal("#ff9660", back.CalendarColors["work@group.calendar.google.com"]);
        Assert.Equal(CalendarLayout.Year, back.Layout);
        Assert.False(back.ShowBirthdays);
        Assert.Equal(7, ConfigOf<AgendaConfig>(parsed, WidgetKind.Agenda).AgendaCount);
        Assert.DoesNotContain(parsed.Repairs, r => r.Contains("calendar", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(WidgetKind.MonthCalendar, CalendarLayout.Month, "monthCalendar")]
    [InlineData(WidgetKind.WeekCalendar, CalendarLayout.Week, "weekCalendar")]
    [InlineData(WidgetKind.DayCalendar, CalendarLayout.Day, "dayCalendar")]
    [InlineData(WidgetKind.YearCalendar, CalendarLayout.Year, "yearCalendar")]
    [InlineData(WidgetKind.Agenda, CalendarLayout.Agenda, "agenda")]
    public void S3600_each_calendar_kind_draws_one_layout_that_is_never_stored(WidgetKind kind, CalendarLayout layout, string discriminator)
    {
        var config = Assert.IsAssignableFrom<CalendarConfig>(WidgetConfig.CreateDefault(kind));
        Assert.Equal(layout, config.Layout);
        Assert.Equal(kind, CalendarConfig.For(layout).Kind);
        Assert.Equal(kind, config.Clone().Kind);

        var state = AppState.CreateDefault();
        state.Views[0].Widgets.Add(WidgetInstance.Create(kind, new FracRect(0.1, 0.1, 0.2, 0.2)));
        var json = SettingsTransfer.Serialize(state);
        Assert.Contains($"\"kind\": \"{discriminator}\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"layout\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void S3601_each_kind_stores_only_the_settings_it_uses()
    {
        static string Own(WidgetKind kind)
            => System.Text.Json.JsonSerializer.Serialize(WidgetInstance.Create(kind, new FracRect(0.1, 0.1, 0.2, 0.2)).Config, AppStateJsonContext.Default.WidgetConfig);

        var day = Own(WidgetKind.DayCalendar);
        Assert.DoesNotContain("agendaCount", day, StringComparison.Ordinal);
        Assert.Contains("showBirthdays", day, StringComparison.Ordinal);

        Assert.Contains("agendaCount", Own(WidgetKind.Agenda), StringComparison.Ordinal);

        // [S-3770] No calendar stores a week start: it is the shared regional setting.
        foreach (var kind in new[] { WidgetKind.MonthCalendar, WidgetKind.WeekCalendar, WidgetKind.YearCalendar, WidgetKind.DayCalendar, WidgetKind.Agenda })
        {
            Assert.DoesNotContain("weekStart", Own(kind), StringComparison.Ordinal);
        }

        Assert.DoesNotContain("agendaCount", Own(WidgetKind.WeekCalendar), StringComparison.Ordinal);
    }

    [Fact]
    public void S3600_a_widget_of_the_retired_calendar_kind_is_dropped_and_the_rest_load()
    {
        var document = StateWith(WidgetKind.MonthCalendar, """{"kind":"monthCalendar"}""");
        var widgets = document["views"]![0]!["widgets"]!.AsArray();
        widgets.Add(JsonNode.Parse("""{"id":"old","kind":"Calendar","rect":{"x":0.6,"y":0.6,"w":0.2,"h":0.2},"config":{"kind":"calendar","layout":"week"}}"""));

        var parsed = AppStateStore.Parse(document.ToJsonString());

        Assert.Equal(ParseRefusal.None, parsed.Refusal);
        Assert.DoesNotContain(parsed.State!.Views[0].Widgets, w => w.Id == "old");
        Assert.Contains(parsed.State.Views[0].Widgets, w => w.Kind == WidgetKind.MonthCalendar);
        Assert.Contains(parsed.Repairs, r => r.Contains("unknown kind 'Calendar'", StringComparison.Ordinal));
    }
}
