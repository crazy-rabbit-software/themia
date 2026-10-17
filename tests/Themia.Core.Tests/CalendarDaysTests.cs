using System.Text.Json.Nodes;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The days a week calendar and an agenda cover ([S-2719], [S-3636], [S-3637]), the agenda's day
/// headings ([S-2716]), wrapped titles ([S-2717]) and the day's span bars ([S-2718]), against a fixed
/// Sunday.
/// </summary>
public sealed class CalendarDaysTests
{
    private static readonly DateOnly Today = new(2026, 10, 4); // a Sunday

    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static CalendarPeriod Next(int days) => new(WeekStartDay.Monday, days, WeekFirstDay.Today);

    private static CalendarPeriod FromWeekStart(int days) => new(WeekStartDay.Monday, days, WeekFirstDay.Week);

    // ---- [S-2719] the week calendar's days ---------------------------------------------------------

    [Fact]
    public void S2719_a_whole_week_from_the_week_start_is_the_week_holding_the_anchor_wherever_it_is()
    {
        for (var offset = -60; offset <= 60; offset++)
        {
            var anchor = Today.AddDays(offset);
            foreach (var start in new[] { WeekStartDay.Monday, WeekStartDay.Sunday, WeekStartDay.Saturday })
            {
                var first = CalendarRules.StartOfWeek(anchor, start);
                Assert.Equal((first, first.AddDays(6)), CalendarRules.ViewRange(CalendarStyle.Week, anchor, Today, start));
            }
        }
    }

    [Fact]
    public void S2719_the_next_days_start_today_and_page_by_their_own_length()
    {
        Assert.Equal((Today, Today.AddDays(3)), CalendarRules.ViewRange(CalendarStyle.Week, Today, Today, Next(4)));

        var forward = CalendarRules.Step(CalendarStyle.Week, Today, 1, Next(4));
        Assert.Equal((Today.AddDays(4), Today.AddDays(7)), CalendarRules.ViewRange(CalendarStyle.Week, forward, Today, Next(4)));

        var back = CalendarRules.Step(CalendarStyle.Week, Today, -1, Next(4));
        Assert.Equal((Today.AddDays(-4), Today.AddDays(-1)), CalendarRules.ViewRange(CalendarStyle.Week, back, Today, Next(4)));

        Assert.True(CalendarRules.InView(CalendarStyle.Week, Today, Today, Next(4), Today));
        Assert.False(CalendarRules.InView(CalendarStyle.Week, forward, Today, Next(4), Today));
    }

    [Fact]
    public void S2719_a_day_selected_inside_the_period_on_screen_keeps_it_there()
    {
        // [S-3621] The anchor becomes the selected day; the period holding it is the same one.
        var range = CalendarRules.ViewRange(CalendarStyle.Week, Today, Today, Next(5));
        var selected = CalendarShare.AnchorFor(CalendarStyle.Week, Today, Today.AddDays(3));
        Assert.Equal(range, CalendarRules.ViewRange(CalendarStyle.Week, selected, Today, Next(5)));

        // One past it is the next period, which starts right after this one.
        Assert.Equal((Today.AddDays(5), Today.AddDays(9)), CalendarRules.ViewRange(CalendarStyle.Week, Today.AddDays(5), Today, Next(5)));
    }

    [Fact]
    public void S2719_a_working_week_from_the_week_start_steps_monday_to_monday()
    {
        var monday = new DateOnly(2026, 9, 28);
        Assert.Equal((monday, monday.AddDays(4)), CalendarRules.ViewRange(CalendarStyle.Week, Today, Today, FromWeekStart(5)));
        Assert.Equal(7, CalendarRules.WeekStep(FromWeekStart(5)));
        Assert.Equal("Sep 28 – Oct 2", CalendarRules.Title(CalendarStyle.Week, Today, Today, FromWeekStart(5), Locale.En));

        // The weekend is in no period: a Saturday anchor shows its own working week.
        Assert.Equal((monday, monday.AddDays(4)), CalendarRules.ViewRange(CalendarStyle.Week, new DateOnly(2026, 10, 3), Today, FromWeekStart(5)));
    }

    [Fact]
    public void S2719_two_weeks_from_the_week_start_page_by_two_weeks_from_this_one()
    {
        var monday = new DateOnly(2026, 9, 28);
        Assert.Equal(14, CalendarRules.WeekStep(FromWeekStart(14)));
        Assert.Equal((monday, monday.AddDays(13)), CalendarRules.ViewRange(CalendarStyle.Week, Today, Today, FromWeekStart(14)));
        Assert.Equal((monday.AddDays(14), monday.AddDays(27)), CalendarRules.ViewRange(CalendarStyle.Week, monday.AddDays(14), Today, FromWeekStart(14)));
        Assert.Equal((monday.AddDays(-14), monday.AddDays(-1)), CalendarRules.ViewRange(CalendarStyle.Week, monday.AddDays(-1), Today, FromWeekStart(14)));
    }

    [Fact]
    public void S2719_one_day_is_titled_like_a_day_and_steps_a_day()
    {
        Assert.Equal("Sun, Oct 4", CalendarRules.Title(CalendarStyle.Week, Today, Today, Next(1), Locale.En));
        Assert.Equal(Today.AddDays(1), CalendarRules.Step(CalendarStyle.Week, Today, 1, Next(1)));
        Assert.Equal("Oct 4 – Oct 10", CalendarRules.Title(CalendarStyle.Week, Today, Today, Next(7), Locale.En));
    }

    [Fact]
    public void S2719_fewer_days_give_each_column_more_width()
    {
        Assert.False(CalendarFit.Week(480, 200, 7).Detail);
        Assert.True(CalendarFit.Week(480, 200, 4).Detail);
        Assert.True(CalendarFit.Week(150, 200, 14).Dots);
        Assert.False(CalendarFit.Week(150, 200, 3).Dots);
    }

    [Fact]
    public void S2719_a_period_holds_its_lengths_inside_their_ranges()
    {
        Assert.Equal(14, new CalendarPeriod(WeekStartDay.Monday, 40).Days);
        Assert.Equal(1, new CalendarPeriod(WeekStartDay.Monday, 0).Days);
        Assert.Equal(7, ((CalendarPeriod)WeekStartDay.Sunday).Days);
        Assert.Equal(CalendarWidgetConfig.AgendaDefaultDays, ((CalendarPeriod)WeekStartDay.Sunday).AgendaLength);
        Assert.Equal(365, new CalendarPeriod(WeekStartDay.Monday, AgendaDays: 9999).AgendaLength);
    }

    // ---- [S-3636] the agenda's date range ----------------------------------------------------------

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(7, 6)]
    [InlineData(30, 29)]
    public void S3636_the_agenda_covers_its_days_from_today_wherever_it_is_anchored(int days, int lastOffset)
    {
        var period = new CalendarPeriod(WeekStartDay.Monday, AgendaDays: days);
        Assert.Equal((Today, Today.AddDays(lastOffset)), CalendarRules.ViewRange(CalendarStyle.Agenda, Today.AddDays(-40), Today, period));
    }

    [Fact]
    public void S3636_today_only_lists_nothing_of_tomorrow()
    {
        var at = new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero);
        CalendarEvent Meeting(string id, DateTimeOffset start) => new() { Id = id, Subject = id, Start = start, End = start.AddHours(1), AccountId = "a" };
        var events = new[] { Meeting("today", at.AddHours(2)), Meeting("tomorrow", at.AddDays(1)) };

        var (first, last) = CalendarRules.ViewRange(CalendarStyle.Agenda, Today, Today, new CalendarPeriod(WeekStartDay.Monday, AgendaDays: 1));
        var groups = CalendarRules.Agenda(events, [], first, last, Utc, 100, at);

        var day = Assert.Single(groups);
        Assert.Equal(Today, day.Day);
        Assert.Equal("today", Assert.Single(day.Items).Event!.Id);
    }

    [Fact]
    public void S3636_the_agenda_config_carries_its_range_and_offers_the_named_ones()
    {
        var agenda = new CalendarWidgetConfig { Style = CalendarStyle.Agenda };
        Assert.Equal(30, agenda.AgendaDays);
        Assert.Equal(30, agenda.Period.AgendaLength);
        Assert.Equal([1, 2, 3, 7, 30], CalendarWidgetConfig.AgendaDayPresets);

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
        var parsed = AppStateStore.Parse(StateWith(CalendarWidgetConfig.Descriptor.Id, $$"""{"kind":"calendar","style":"agenda","agendaDays":{{stored}}}""").ToJsonString());

        Assert.Equal(ParseRefusal.None, parsed.Refusal);
        Assert.Equal(repaired, ConfigOf<CalendarWidgetConfig>(parsed, CalendarWidgetConfig.Descriptor.Id).AgendaDays);
        Assert.Contains(parsed.Repairs, r => r.Contains("agenda days", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{"kind":"calendar","style":"week","weekDays":0,"weekFrom":"today"}""", 1, WeekFirstDay.Today)]
    [InlineData("""{"kind":"calendar","style":"week","weekDays":99,"weekFrom":"tomorrow"}""", 14, WeekFirstDay.Week)]
    [InlineData("""{"kind":"calendar","style":"week","weekFrom":7}""", 7, WeekFirstDay.Week)]
    public void S3637_a_week_length_or_first_day_out_of_bounds_falls_back(string json, int days, WeekFirstDay from)
    {
        var parsed = AppStateStore.Parse(StateWith(CalendarWidgetConfig.Descriptor.Id, json).ToJsonString());

        Assert.Equal(ParseRefusal.None, parsed.Refusal);
        var week = ConfigOf<CalendarWidgetConfig>(parsed, CalendarWidgetConfig.Descriptor.Id);
        Assert.Equal(days, week.WeekDays);
        Assert.Equal(from, week.WeekFrom);
    }

    [Fact]
    public void S3637_the_week_and_agenda_settings_round_trip_with_their_style()
    {
        var state = AppState.CreateDefault();
        var week = WidgetInstance.Create(CalendarWidgetConfig.Descriptor.Id, new FracRect(0.1, 0.1, 0.2, 0.2));
        week.Id = "week";
        week.Config = new CalendarWidgetConfig { Style = CalendarStyle.Week, WeekDays = 5, WeekFrom = WeekFirstDay.Today };
        var agenda = WidgetInstance.Create(CalendarWidgetConfig.Descriptor.Id, new FracRect(0.4, 0.1, 0.2, 0.2));
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

    // ---- [S-2716] agenda headings ------------------------------------------------------------------

    [Fact]
    public void S2716_today_and_tomorrow_are_named_and_carry_their_date_where_it_fits()
    {
        Assert.Equal("Today", CalendarFit.AgendaHeading(Locale.En, Today, Today, dated: false));
        Assert.Equal("Today · Sun, Oct 4", CalendarFit.AgendaHeading(Locale.En, Today, Today, dated: true));
        Assert.Equal("Tomorrow · Mon, Oct 5", CalendarFit.AgendaHeading(Locale.En, Today.AddDays(1), Today, dated: true));
        Assert.Equal("Tomorrow", CalendarFit.AgendaHeading(Locale.En, Today.AddDays(1), Today, dated: false));

        // Any other day is its date, with room or without.
        Assert.Equal("Tue, Oct 6", CalendarFit.AgendaHeading(Locale.En, Today.AddDays(2), Today, dated: true));
        Assert.Equal("Tue, Oct 6", CalendarFit.AgendaHeading(Locale.En, Today.AddDays(2), Today, dated: false));
    }

    [Fact]
    public void S2716_the_date_joins_only_a_heading_line_wide_enough_for_the_longest_one()
    {
        Assert.False(CalendarFit.AgendaHeadingDated(Locale.En, 80));
        Assert.True(CalendarFit.AgendaHeadingDated(Locale.En, 400));

        // Monotonic: once it fits, wider still fits.
        var from = Enumerable.Range(40, 400).First(w => CalendarFit.AgendaHeadingDated(Locale.En, w));
        Assert.All(Enumerable.Range(from, 200), w => Assert.True(CalendarFit.AgendaHeadingDated(Locale.En, w)));
    }

    // ---- [S-2717] wrapped titles -------------------------------------------------------------------

    [Fact]
    public void S2717_second_lines_go_in_order_to_the_rows_that_wrap_while_the_room_lasts()
    {
        Assert.Equal([true, false, true, false], CalendarFit.SecondLines([true, false, true, true], 40, 16));
        Assert.Equal([false, false], CalendarFit.SecondLines([true, true], 10, 16));
        Assert.Equal([false, true], CalendarFit.SecondLines([false, true], 16, 16));
        Assert.Empty(CalendarFit.SecondLines([], 100, 16));
    }

    [Theory]
    [InlineData(0, true, true, 1, false, false)]
    [InlineData(1, true, true, 1, false, false)]
    [InlineData(2, true, true, 1, true, false)]
    [InlineData(3, true, true, 2, true, false)]
    [InlineData(3, false, true, 1, true, true)]
    [InlineData(4, true, true, 2, true, true)]
    [InlineData(4, true, false, 2, true, false)]
    public void S2717_a_timed_block_shows_title_time_second_title_line_then_where(int lines, bool wraps, bool meta, int titleLines, bool time, bool where)
        => Assert.Equal((titleLines, time, where), CalendarFit.BlockParts(lines, wraps, meta));

    [Fact]
    public void S2717_the_agenda_columns_height_counts_rows_and_headings_at_one_line()
    {
        DateOnly[] days = [Today, Today, Today.AddDays(1), Today.AddDays(1), Today.AddDays(3)];
        Assert.Equal(32 + 40 + 40, CalendarFit.AgendaColumnHeight(days, 0, 2, 32, 40, firstHeading: true));
        Assert.Equal(40 + 40 + 32 + 40, CalendarFit.AgendaColumnHeight(days, 0, 3, 32, 40, firstHeading: false));
        Assert.Equal(32 + 40 + 32 + 40, CalendarFit.AgendaColumnHeight(days, 3, 9, 32, 40, firstHeading: true));
    }

    // ---- [S-2718] the day's span bars --------------------------------------------------------------

    [Fact]
    public void S2718_a_roomy_day_gives_each_all_day_item_its_own_bar()
    {
        var keep = CalendarFit.DayBandKeepsHours * CalendarFit.MinHour;
        Assert.Equal(new DayBandFit(0, 0, false), CalendarFit.DayBand(400, 0));
        Assert.Equal(new DayBandFit(2, CalendarFit.DayBar, false), CalendarFit.DayBand(400, 2));
        Assert.Equal(new DayBandFit(1, CalendarFit.DayBar, false), CalendarFit.DayBand(keep + CalendarFit.DayBar, 1));
    }

    [Fact]
    public void S2718_items_past_the_bars_share_the_last_line()
    {
        Assert.Equal(new DayBandFit(3, CalendarFit.DayBar, true), CalendarFit.DayBand(600, 5));

        // Room for two bars over the hours kept: one own bar, then the rest share the second line.
        var keep = CalendarFit.DayBandKeepsHours * CalendarFit.MinHour;
        Assert.Equal(new DayBandFit(2, CalendarFit.DayBar, true), CalendarFit.DayBand(keep + (2 * CalendarFit.DayBar), 4));
    }

    [Fact]
    public void S2718_a_short_day_falls_back_to_one_shared_chip_line_then_to_none()
    {
        var keep = CalendarFit.DayBandKeepsHours * CalendarFit.MinHour;
        Assert.Equal(new DayBandFit(1, CalendarFit.ChipLine, true), CalendarFit.DayBand(keep + CalendarFit.DayBar - 2, 2));
        Assert.Equal(new DayBandFit(0, 0, false), CalendarFit.DayBand(CalendarFit.MinHour, 2));
    }
}
