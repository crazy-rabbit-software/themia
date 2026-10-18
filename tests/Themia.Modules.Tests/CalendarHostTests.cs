using System.Text.Json.Nodes;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-3636] [S-3637] The calendar's config, now its module's: the agenda's range and the week's columns, alone and through the state file (moved from Core's CalendarDaysTests, unchanged).</summary>
public sealed class CalendarHostTests
{
    [Fact]
    public void S3636_the_agenda_config_carries_its_range_and_offers_the_named_ones()
    {
        var agenda = new CalendarWidgetConfig { Style = CalendarStyle.Agenda };
        Assert.Equal(30, agenda.AgendaDays);
        Assert.Equal(30, agenda.Period.AgendaLength);
        Assert.Equal([1, 2, 3, 7, 30], CalendarPeriod.AgendaDayPresets);

        agenda.AgendaDays = 12;
        Assert.Equal(12, agenda.Period.AgendaLength);
        Assert.Equal(12, ((CalendarWidgetConfig)agenda.Clone()).AgendaDays);
    }

    // ---- [S-3636] / [S-3637] through the state file ------------------------------------------------

    private static JsonNode StateWith(WidgetId kind, string configJson)
    {
        var state = AppState.CreateDefault();
        state.Views[0].Widgets.Add(WidgetInstance.Create(kind, new FracRect(0.1, 0.1, 0.2, 0.2)));
        var document = JsonNode.Parse(SettingsTransfer.Serialize(state))!;
        var widget = document["views"]![0]!["widgets"]!.AsArray().First(w => w!["config"]!["kind"]!.GetValue<string>() == kind.Value)!;
        widget["config"] = JsonNode.Parse(configJson);
        return document;
    }

    private static T ConfigOf<T>(ParseResult parsed, WidgetId kind)
        where T : WidgetConfig
        => Assert.IsType<T>(parsed.State!.Views[0].Widgets.First(w => w.Kind == kind).Config);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(900, 365)]
    public void S3636_an_agenda_range_out_of_bounds_is_repaired_and_said(int stored, int repaired)
    {
        var parsed = AppStateStore.Parse(StateWith(new WidgetId("calendar"), $$"""{"kind":"calendar","style":"agenda","agendaDays":{{stored}}}""").ToJsonString());

        Assert.Equal(ParseRefusal.None, parsed.Refusal);
        Assert.Equal(repaired, ConfigOf<CalendarWidgetConfig>(parsed, new WidgetId("calendar")).AgendaDays);
        Assert.Contains(parsed.Repairs, r => r.Contains("agenda days", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{"kind":"calendar","style":"week","weekDays":0,"weekFrom":"today"}""", 1, WeekFirstDay.Today)]
    [InlineData("""{"kind":"calendar","style":"week","weekDays":99,"weekFrom":"tomorrow"}""", 14, WeekFirstDay.Week)]
    [InlineData("""{"kind":"calendar","style":"week","weekFrom":7}""", 7, WeekFirstDay.Week)]
    public void S3637_a_week_length_or_first_day_out_of_bounds_falls_back(string json, int days, WeekFirstDay from)
    {
        var parsed = AppStateStore.Parse(StateWith(new WidgetId("calendar"), json).ToJsonString());

        Assert.Equal(ParseRefusal.None, parsed.Refusal);
        var week = ConfigOf<CalendarWidgetConfig>(parsed, new WidgetId("calendar"));
        Assert.Equal(days, week.WeekDays);
        Assert.Equal(from, week.WeekFrom);
    }

    [Fact]
    public void S3637_the_week_and_agenda_settings_round_trip_with_their_style()
    {
        var state = AppState.CreateDefault();
        var week = WidgetInstance.Create(new WidgetId("calendar"), new FracRect(0.1, 0.1, 0.2, 0.2));
        week.Id = "week";
        week.Config = new CalendarWidgetConfig { Style = CalendarStyle.Week, WeekDays = 5, WeekFrom = WeekFirstDay.Today };
        var agenda = WidgetInstance.Create(new WidgetId("calendar"), new FracRect(0.4, 0.1, 0.2, 0.2));
        agenda.Id = "agenda";
        agenda.Config = new CalendarWidgetConfig { Style = CalendarStyle.Agenda, AgendaDays = 2 };
        state.Views[0].Widgets.Add(week);
        state.Views[0].Widgets.Add(agenda);

        var parsed = AppStateStore.Parse(SettingsTransfer.Serialize(state));
        CalendarWidgetConfig Of(string id) => Assert.IsType<CalendarWidgetConfig>(parsed.State!.Views[0].Widgets.First(w => w.Id == id).Config);

        var w = Of("week");
        Assert.Equal(CalendarStyle.Week, w.Style);
        Assert.Equal(5, w.WeekDays);
        Assert.Equal(WeekFirstDay.Today, w.WeekFrom);
        Assert.Equal(new CalendarPeriod(w.EffectiveWeekStart, 5, WeekFirstDay.Today), w.Period);
        Assert.Equal(2, Of("agenda").AgendaDays);
        Assert.Equal(new CalendarPeriod(w.EffectiveWeekStart, AgendaDays: 2), Of("agenda").Period);
        Assert.DoesNotContain(parsed.Repairs, r => r.Contains("calendar", StringComparison.Ordinal) || r.Contains("agenda", StringComparison.Ordinal));
    }
}
