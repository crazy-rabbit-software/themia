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
            """{"kind":"calendar","accountIds":"nope","calendars":{"a":"x","b":["c1",7]},"calendarColors":{"k":5,"j":"#123456"},"weekStart":3}""").ToJsonString());

        Assert.Equal(ParseRefusal.None, calendar.Refusal);
        var c = ConfigOf<CalendarWidgetConfig>(calendar, WidgetKind.Calendar);
        Assert.Empty(c.AccountIds);
        Assert.Equal(["c1"], c.Calendars["b"]);
        Assert.Empty(c.Calendars["a"]);
        Assert.Equal("#123456", Assert.Single(c.CalendarColors).Value);

        var agenda = AppStateStore.Parse(StateWith(WidgetKind.Calendar, """{"kind":"calendar","style":"agenda","agendaCount":900}""").ToJsonString());
        Assert.Equal(ParseRefusal.None, agenda.Refusal);
        var a = ConfigOf<CalendarWidgetConfig>(agenda, WidgetKind.Calendar);
        Assert.Equal(100, a.AgendaCount);
        Assert.Equal(CalendarStyle.Agenda, a.Style);

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
        widget.Id = "year";
        widget.Config = new CalendarWidgetConfig
        {
            Style = CalendarStyle.Year,
            AccountIds = ["google:1"],
            Calendars = new() { ["google:1"] = ["work@group.calendar.google.com"] },
            CalendarColors = new() { ["work@group.calendar.google.com"] = "#ff9660" },
            ShowBirthdays = false,
        };
        state.Views[0].Widgets.Add(widget);
        var agenda = WidgetInstance.Create(WidgetKind.Calendar, new FracRect(0.4, 0.1, 0.2, 0.2));
        agenda.Id = "agenda";
        agenda.Config = new CalendarWidgetConfig { Style = CalendarStyle.Agenda, AgendaCount = 7 };
        state.Views[0].Widgets.Add(agenda);

        var json = SettingsTransfer.Serialize(state);

        var parsed = AppStateStore.Parse(json);
        CalendarWidgetConfig Of(string id) => Assert.IsType<CalendarWidgetConfig>(parsed.State!.Views[0].Widgets.First(w => w.Id == id).Config);
        var back = Of("year");
        Assert.Equal(["google:1"], back.AccountIds);
        Assert.Equal(["work@group.calendar.google.com"], back.Calendars["google:1"]);
        Assert.Equal("#ff9660", back.CalendarColors["work@group.calendar.google.com"]);
        Assert.Equal(CalendarStyle.Year, back.Layout);
        Assert.False(back.ShowBirthdays);
        Assert.Equal(7, Of("agenda").AgendaCount);
        Assert.Equal(CalendarStyle.Agenda, Of("agenda").Style);
        Assert.DoesNotContain(parsed.Repairs, r => r.Contains("calendar", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(CalendarStyle.Month, "month")]
    [InlineData(CalendarStyle.Week, "week")]
    [InlineData(CalendarStyle.Day, "day")]
    [InlineData(CalendarStyle.Year, "year")]
    [InlineData(CalendarStyle.Agenda, "agenda")]
    public void S3640_one_calendar_kind_stores_its_layout_as_its_style(CalendarStyle style, string wire)
    {
        var config = Assert.IsType<CalendarWidgetConfig>(WidgetConfig.CreateDefault(WidgetKind.Calendar));
        Assert.Equal(CalendarStyle.Month, config.Style);
        config.Style = style;
        Assert.Equal(style, config.Layout);

        var copy = Assert.IsType<CalendarWidgetConfig>(config.Clone());
        Assert.Equal(style, copy.Style);

        var json = System.Text.Json.JsonSerializer.Serialize<WidgetConfig>(config, AppStateJsonContext.Default.WidgetConfig);
        using (var document = System.Text.Json.JsonDocument.Parse(json))
        {
            Assert.Equal("calendar", document.RootElement.GetProperty("kind").GetString());
            Assert.Equal(wire, document.RootElement.GetProperty("style").GetString());
            Assert.False(document.RootElement.TryGetProperty("layout", out _));
        }

        var back = Assert.IsType<CalendarWidgetConfig>(System.Text.Json.JsonSerializer.Deserialize(json, AppStateJsonContext.Default.WidgetConfig));
        Assert.Equal(style, back.Style);
        Assert.Empty(back.Repair());
    }

    [Fact]
    public void S3640_the_styles_are_listed_month_week_day_year_agenda()
        => Assert.Equal(["month", "week", "day", "year", "agenda"], Themia.Core.Widgets.WidgetStyles.For(WidgetKind.Calendar).Select(s => s.Name));

    [Fact]
    public void S3640_an_unknown_calendar_style_is_repaired_to_the_month()
    {
        var parsed = AppStateStore.Parse(StateWith(WidgetKind.Calendar, """{"kind":"calendar","style":"fortnight"}""").ToJsonString());

        Assert.Equal(ParseRefusal.None, parsed.Refusal);
        Assert.Equal(CalendarStyle.Month, ConfigOf<CalendarWidgetConfig>(parsed, WidgetKind.Calendar).Style);
        Assert.Contains(parsed.Repairs, r => r.Contains("Calendar style unknown", StringComparison.Ordinal));
    }

    [Fact]
    public void S3641_the_calendar_stores_the_union_of_what_its_styles_use()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(WidgetInstance.Create(WidgetKind.Calendar, new FracRect(0.1, 0.1, 0.2, 0.2)).Config, AppStateJsonContext.Default.WidgetConfig);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        string[] names = [.. document.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)];

        // [S-3770] No calendar stores a week start: it is the shared regional setting.
        Assert.Equal(
            [.. new[] { "kind", "style", "accountIds", "calendars", "calendarColors", "showBirthdays", "weekDays", "weekFrom", "agendaCount", "agendaDays" }.Order(StringComparer.Ordinal)],
            names);
    }

    public static TheoryData<CalendarStyle, CalendarSetting[]> StyleSettings() => new()
    {
        { CalendarStyle.Month, [] },
        { CalendarStyle.Day, [] },
        { CalendarStyle.Year, [] },
        { CalendarStyle.Week, [CalendarSetting.WeekFrom, CalendarSetting.WeekDays] },
        { CalendarStyle.Agenda, [CalendarSetting.AgendaRange, CalendarSetting.AgendaCount] },
    };

    [Theory]
    [MemberData(nameof(StyleSettings))]
    public void S3641_each_style_shows_only_its_own_settings(CalendarStyle style, CalendarSetting[] rows)
        => Assert.Equal(rows, new CalendarWidgetConfig { Style = style }.ShownSettings);

    [Fact]
    public void S3641_a_style_reads_only_its_own_lengths()
    {
        var config = new CalendarWidgetConfig { WeekDays = 4, WeekFrom = WeekFirstDay.Today, AgendaDays = 2 };

        config.Style = CalendarStyle.Month;
        Assert.Equal(new Themia.Core.Office.CalendarPeriod(config.EffectiveWeekStart), config.Period);

        config.Style = CalendarStyle.Week;
        Assert.Equal(4, config.Period.Days);
        Assert.Equal(WeekFirstDay.Today, config.Period.WeekFrom);
        Assert.Equal(CalendarWidgetConfig.AgendaDefaultDays, config.Period.AgendaLength);

        config.Style = CalendarStyle.Agenda;
        Assert.Equal(2, config.Period.AgendaLength);
        Assert.Equal(CalendarWidgetConfig.WeekDefaultDays, config.Period.Days);
    }
}
