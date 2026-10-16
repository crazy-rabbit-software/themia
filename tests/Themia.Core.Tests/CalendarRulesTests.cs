using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The calendar widget's arithmetic, ported from the classic app's <c>dates</c>, <c>colors</c>,
/// <c>targets</c>, <c>dayLayout</c> and <c>responsive</c> tests, against a fixed day and zone.
/// </summary>
public sealed class CalendarRulesTests
{
    /// <summary>UTC+2 with no DST, so a local day and a UTC day visibly differ.</summary>
    private static readonly TimeZoneInfo Plus2 = TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    private static readonly DateOnly Today = new(2026, 10, 4); // a Sunday

    // ---- [S-565] provider datetimes (classic S-41 / issue #48) -----------------------------------

    [Theory]
    [InlineData("2026-06-25T10:00:00-04:00", "2026-06-25T14:00:00Z")]
    [InlineData("2026-06-25T10:00:00-02:30", "2026-06-25T12:30:00Z")]
    [InlineData("2026-06-25T10:00:00+02:00", "2026-06-25T08:00:00Z")]
    [InlineData("2026-06-25T10:00:00+0200", "2026-06-25T08:00:00Z")]
    [InlineData("2026-06-25T10:00:00", "2026-06-25T10:00:00Z")]
    [InlineData("2026-06-25T10:00:00.0000000", "2026-06-25T10:00:00Z")]
    [InlineData("2026-06-25T10:00:00Z", "2026-06-25T10:00:00Z")]
    public void S565_ProviderDatetimesKeepTheirZoneAndNaiveOnesAreUtc(string text, string utc)
    {
        var parsed = CalendarRules.ParseDateTime(text);
        Assert.NotNull(parsed);
        Assert.Equal(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture), parsed.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not a date")]
    public void S565_GarbageIsNoDatetime(string? text) => Assert.Null(CalendarRules.ParseDateTime(text));

    // ---- [S-564] week start ----------------------------------------------------------------------

    [Fact]
    public void S564_WeekStartOffsets()
    {
        Assert.Equal(0, CalendarRules.WeekStartOffset(DayOfWeek.Monday, WeekStartDay.Monday));
        Assert.Equal(5, CalendarRules.WeekStartOffset(DayOfWeek.Saturday, WeekStartDay.Monday));
        Assert.Equal(6, CalendarRules.WeekStartOffset(DayOfWeek.Sunday, WeekStartDay.Monday));
        Assert.Equal(0, CalendarRules.WeekStartOffset(DayOfWeek.Sunday, WeekStartDay.Sunday));
        Assert.Equal(6, CalendarRules.WeekStartOffset(DayOfWeek.Saturday, WeekStartDay.Sunday));
        Assert.Equal(0, CalendarRules.WeekStartOffset(DayOfWeek.Saturday, WeekStartDay.Saturday));
        Assert.Equal(6, CalendarRules.WeekStartOffset(DayOfWeek.Friday, WeekStartDay.Saturday));
        Assert.Equal(1, CalendarRules.WeekStartOffset(DayOfWeek.Sunday, WeekStartDay.Saturday));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, WeekStartDay.Monday)]
    [InlineData(DayOfWeek.Sunday, WeekStartDay.Sunday)]
    [InlineData(DayOfWeek.Saturday, WeekStartDay.Saturday)]
    [InlineData(DayOfWeek.Friday, WeekStartDay.Monday)]
    public void S564_a_first_weekday_names_its_week_start(DayOfWeek first, WeekStartDay expected)
    {
        Assert.Equal(expected, CalendarRules.WeekStartOf(first));
        if (expected != WeekStartDay.Monday || first == DayOfWeek.Monday)
        {
            Assert.Equal(first, CalendarRules.FirstDay(expected));
        }
    }

    [Fact]
    public void S564_StartOfWeekAndColumnOrder()
    {
        Assert.Equal(new DateOnly(2026, 9, 28), CalendarRules.StartOfWeek(Today, WeekStartDay.Monday));
        Assert.Equal(Today, CalendarRules.StartOfWeek(Today, WeekStartDay.Sunday));
        Assert.Equal(DayOfWeek.Monday, CalendarRules.WeekdayOrder(WeekStartDay.Monday)[0]);
        Assert.Equal(DayOfWeek.Sunday, CalendarRules.WeekdayOrder(WeekStartDay.Monday)[6]);
        Assert.Equal(DayOfWeek.Sunday, CalendarRules.WeekdayOrder(WeekStartDay.Sunday)[0]);
        Assert.Equal(new DateOnly(2026, 10, 3), CalendarRules.StartOfWeek(Today, WeekStartDay.Saturday));
        Assert.Equal(
            [DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            CalendarRules.WeekdayOrder(WeekStartDay.Saturday));
        Assert.Equal(new DateOnly(2026, 9, 26), CalendarRules.MonthCells(Today, WeekStartDay.Saturday)[0]);
    }

    // ---- [S-566] colours -------------------------------------------------------------------------

    [Fact]
    public void S566_PaletteWrapsAndToleratesNegative()
    {
        Assert.Equal(CalendarRules.Palette[0], CalendarRules.PaletteColor(0));
        Assert.Equal(CalendarRules.Palette[5], CalendarRules.PaletteColor(5));
        Assert.Equal(CalendarRules.Palette[1], CalendarRules.PaletteColor(7));
        Assert.Contains(CalendarRules.PaletteColor(-1), CalendarRules.Palette);
    }

    [Theory]
    [InlineData("#AB12Cd", "#ab12cd")]
    [InlineData("#FF2968FF", "#ff2968")]
    [InlineData("  #00ff00  ", "#00ff00")]
    [InlineData("auto", null)]
    [InlineData("", null)]
    [InlineData("#12345", null)]
    [InlineData("#gggggg", null)]
    [InlineData(null, null)]
    public void S566_ColoursAreNormalisedAtTheBoundary(string? raw, string? expected)
        => Assert.Equal(expected, CalendarRules.NormalizeColor(raw));

    [Fact]
    public void S566_AValidOverrideWinsAndACorruptOneFallsBack()
    {
        Assert.Equal("#123456", CalendarRules.ResolveColor("cal-1", 0, new Dictionary<string, string> { ["cal-1"] = "#123456" }));
        Assert.Equal(CalendarRules.PaletteColor(2), CalendarRules.ResolveColor("cal-1", 2, new Dictionary<string, string> { ["cal-1"] = "not-a-color" }));
        Assert.Equal(CalendarRules.PaletteColor(4), CalendarRules.ResolveColor("cal-1", 4, new Dictionary<string, string> { ["cal-2"] = "#000000" }));
        Assert.Equal(CalendarRules.PaletteColor(3), CalendarRules.ResolveColor("cal-1", 3, null));
        Assert.False(CalendarRules.IsHexColor("#abc"));
        Assert.False(CalendarRules.IsHexColor("60cdff"));
    }

    [Fact]
    public void S566_ColorKeyIsTheCalendarElseTheAccount()
    {
        Assert.Equal("cal-1", CalendarRules.ColorKey("acct-a", "cal-1"));
        Assert.Equal("acct-a", CalendarRules.ColorKey("acct-a", null));
        Assert.Equal("acct-a", CalendarRules.ColorKey("acct-a", "   "));
    }

    [Fact]
    public void S566_ColoursShowForSeveralCalendarsOrAChosenColour()
    {
        CalendarTarget[] one = [new("a", null)];
        Assert.False(CalendarRules.ShowColors(one, null));
        Assert.True(CalendarRules.ShowColors(one, new Dictionary<string, string> { ["a"] = "#112233" }));
        Assert.True(CalendarRules.ShowColors([new("a", null), new("b", null)], null));
    }

    // ---- [S-567] targets -------------------------------------------------------------------------

    [Fact]
    public void S567_NoSelectionIsTheDefaultCalendar()
    {
        Assert.Equal([new CalendarTarget("a", null), new CalendarTarget("b", null)], CalendarRules.Targets(["a", "b"], null));
        Assert.Equal([new CalendarTarget("a", null)], CalendarRules.Targets(["a"], new Dictionary<string, List<string>> { ["a"] = [] }));
    }

    [Fact]
    public void S567_ExplicitSelectionMixedWithDefaultsDropsBlanksAndDuplicates()
    {
        var targets = CalendarRules.Targets(["a", "b"], new Dictionary<string, List<string>> { ["a"] = ["cal-1", "", "cal-1", "cal-2"] });
        Assert.Equal([new CalendarTarget("a", "cal-1"), new CalendarTarget("a", "cal-2"), new CalendarTarget("b", null)], targets);
        Assert.Equal([new CalendarTarget("a", null)], CalendarRules.Targets(["a"], new Dictionary<string, List<string>> { ["a"] = ["", "  "] }));
    }

    [Fact]
    public void S567_TogglingStartsFromTheDefaultAndEmptyDropsTheKey()
    {
        CalendarInfo[] cals = [new("primary", "Calendar", true, null), new("work", "Work", false, null)];
        var empty = new Dictionary<string, List<string>>();

        Assert.True(CalendarRules.IsCalendarShown(empty, "a", cals[0]));
        Assert.False(CalendarRules.IsCalendarShown(empty, "a", cals[1]));

        var both = CalendarRules.ToggleCalendar(empty, "a", "work", cals);
        Assert.Equal(["primary", "work"], both["a"]);

        var onlyWork = CalendarRules.ToggleCalendar(both, "a", "primary", cals);
        Assert.Equal(["work"], onlyWork["a"]);

        var none = CalendarRules.ToggleCalendar(onlyWork, "a", "work", cals);
        Assert.False(none.ContainsKey("a"));
    }

    [Fact]
    public void S562_EmptyChoiceMeansEveryAccountAndGoneOnesAreDropped()
    {
        Assert.Equal(["a", "b"], CalendarRules.SelectAccounts([], ["a", "b"]));
        Assert.Equal(["b"], CalendarRules.SelectAccounts(["b", "gone"], ["a", "b"]));
        Assert.Empty(CalendarRules.SelectAccounts(["gone"], ["a", "b"]));
    }

    // ---- [S-568] ranges --------------------------------------------------------------------------

    [Fact]
    public void S568_MonthCoversWholeWeeksAroundTheMonth()
    {
        var (first, last) = CalendarRules.ViewRange(CalendarStyle.Month, Today, Today, WeekStartDay.Monday);
        Assert.Equal(new DateOnly(2026, 9, 28), first);
        Assert.Equal(new DateOnly(2026, 11, 1), last);

        var (sFirst, sLast) = CalendarRules.ViewRange(CalendarStyle.Month, Today, Today, WeekStartDay.Sunday);
        Assert.Equal(new DateOnly(2026, 9, 27), sFirst);
        Assert.Equal(new DateOnly(2026, 10, 31), sLast);

        Assert.Equal(42, CalendarRules.MonthCells(Today, WeekStartDay.Monday).Count);
    }

    [Fact]
    public void S568_WeekDayAndAgendaRanges()
    {
        Assert.Equal((new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 4)), CalendarRules.ViewRange(CalendarStyle.Week, Today, Today, WeekStartDay.Monday));
        Assert.Equal((Today, Today), CalendarRules.ViewRange(CalendarStyle.Day, Today, Today, WeekStartDay.Monday));
        Assert.Equal((Today, Today.AddDays(29)), CalendarRules.ViewRange(CalendarStyle.Agenda, Today.AddDays(-90), Today, WeekStartDay.Monday));
    }

    [Fact]
    public void S2707_AYearFetchesAndTitlesItsWholeYearAndPagesByYear()
    {
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), CalendarRules.ViewRange(CalendarStyle.Year, Today, Today, WeekStartDay.Monday));
        Assert.Equal("2026", CalendarRules.Title(CalendarStyle.Year, Today, Today, WeekStartDay.Monday, Locale.En));
        Assert.Equal(new DateOnly(2027, 2, 28), CalendarRules.Step(CalendarStyle.Year, new DateOnly(2028, 2, 29), -1, WeekStartDay.Monday));
        Assert.Equal(new DateOnly(2025, 10, 4), CalendarRules.Step(CalendarStyle.Year, Today, -1, WeekStartDay.Monday));
        Assert.True(CalendarRules.InView(CalendarStyle.Year, Today.AddMonths(-3), Today, WeekStartDay.Monday, Today));
        Assert.False(CalendarRules.InView(CalendarStyle.Year, Today.AddYears(1), Today, WeekStartDay.Monday, Today));
    }

    [Fact]
    public void S569_AMonthIsInViewOnlyForItsOwnDaysAndTheAgendaAlwaysHoldsToday()
    {
        Assert.False(CalendarRules.InView(CalendarStyle.Month, Today, Today, WeekStartDay.Monday, new DateOnly(2026, 9, 30)));
        Assert.True(CalendarRules.InView(CalendarStyle.Week, Today, Today, WeekStartDay.Monday, new DateOnly(2026, 9, 30)));
        Assert.True(CalendarRules.InView(CalendarStyle.Agenda, Today.AddDays(-90), Today, WeekStartDay.Monday, Today));
    }

    [Fact]
    public void S568_FetchWindowIsLocalMidnightNotUtcMidnight()
    {
        var (from, to) = CalendarRules.FetchWindow(Today, Today, Plus2);
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 22, 0, 0, TimeSpan.Zero), from.ToUniversalTime());
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 22, 0, 0, TimeSpan.Zero), to.ToUniversalTime());
    }

    [Fact]
    public void S568_Titles()
    {
        Assert.Equal("October 2026", CalendarRules.Title(CalendarStyle.Month, Today, Today, WeekStartDay.Monday, Locale.En));
        Assert.Equal("Sep 28 – Oct 4", CalendarRules.Title(CalendarStyle.Week, Today, Today, WeekStartDay.Monday, Locale.En));
        Assert.Equal("Sun, Oct 4", CalendarRules.Title(CalendarStyle.Day, Today, Today, WeekStartDay.Monday, Locale.En));
        Assert.Equal(string.Empty, CalendarRules.Title(CalendarStyle.Agenda, Today, Today, WeekStartDay.Monday, Locale.En));
    }

    // ---- [S-569] navigation ----------------------------------------------------------------------

    [Fact]
    public void S569_StepsByMonthWeekDayAndTheAgendaStaysPut()
    {
        Assert.Equal(new DateOnly(2026, 2, 28), CalendarRules.Step(CalendarStyle.Month, new DateOnly(2026, 1, 31), 1, WeekStartDay.Monday));
        Assert.Equal(Today.AddDays(-7), CalendarRules.Step(CalendarStyle.Week, Today, -1, WeekStartDay.Monday));
        Assert.Equal(Today.AddDays(1), CalendarRules.Step(CalendarStyle.Day, Today, 1, WeekStartDay.Monday));
        Assert.Equal(Today, CalendarRules.Step(CalendarStyle.Agenda, Today, 1, WeekStartDay.Monday));
    }

    // ---- [S-570] what falls on a day -------------------------------------------------------------

    [Fact]
    public void S570_AllDayEndIsExclusiveAndNeverShortened()
    {
        var one = CalendarEvent.AllDayEvent("x", "Conference", new DateOnly(2026, 6, 5), new DateOnly(2026, 6, 6));
        Assert.True(CalendarRules.OccursOn(one, new DateOnly(2026, 6, 5), Plus2));
        Assert.False(CalendarRules.OccursOn(one, new DateOnly(2026, 6, 6), Plus2));

        var trip = CalendarEvent.AllDayEvent("y", "Trip", new DateOnly(2026, 6, 5), new DateOnly(2026, 6, 8));
        Assert.True(CalendarRules.OccursOn(trip, new DateOnly(2026, 6, 7), Plus2));
        Assert.False(CalendarRules.OccursOn(trip, new DateOnly(2026, 6, 8), Plus2));

        var sameDay = CalendarEvent.AllDayEvent("z", "Bad end", new DateOnly(2026, 6, 5), new DateOnly(2026, 6, 5));
        Assert.True(CalendarRules.OccursOn(sameDay, new DateOnly(2026, 6, 5), Plus2));
    }

    [Fact]
    public void S570_TimedEventsAreFiledOnTheLocalDay()
    {
        // 23:30 UTC on the 3rd is 01:30 on the 4th at UTC+2.
        var late = Timed("late", new DateTimeOffset(2026, 10, 3, 23, 30, 0, TimeSpan.Zero), TimeSpan.FromMinutes(30));
        Assert.True(CalendarRules.OccursOn(late, Today, Plus2));
        Assert.False(CalendarRules.OccursOn(late, Today.AddDays(-1), Plus2));
        Assert.Equal(Today, CalendarRules.StartDay(late, Plus2));

        var instant = Timed("zero", new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero), TimeSpan.Zero);
        Assert.True(CalendarRules.OccursOn(instant, Today, Plus2));
    }

    [Fact]
    public void S570_DayItemsAreBirthdaysThenAllDayThenByStart()
    {
        var at10 = Timed("b", new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));
        var at9 = Timed("a", new DateTimeOffset(2026, 10, 4, 7, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));
        var allDay = CalendarEvent.AllDayEvent("c", "Holiday", Today, Today.AddDays(1));
        var birthday = new Birthday("Lena Fischer", 10, 4, 1990);

        var items = CalendarRules.DayItems([at10, allDay, at9], [birthday], Today, Plus2);

        Assert.Equal(4, items.Count);
        Assert.True(items[0].IsBirthday);
        Assert.Equal("c", items[1].Event!.Id);
        Assert.Equal("a", items[2].Event!.Id);
        Assert.Equal("b", items[3].Event!.Id);
    }

    // ---- [S-571] agenda --------------------------------------------------------------------------

    [Fact]
    public void S571_AgendaGroupsByDayFilesRunningEventsUnderTodayDropsEndedOnesAndCaps()
    {
        var running = Timed("running", new DateTimeOffset(2026, 10, 3, 20, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(6));
        var over = Timed("over", new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));
        var tomorrow = Timed("tomorrow", new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));
        var later = Timed("later", new DateTimeOffset(2026, 10, 9, 8, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));
        var birthday = new Birthday("Mia", 10, 5, null);

        var ended = Timed("ended", new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromMinutes(30));
        var now = new DateTimeOffset(2026, 10, 4, 1, 0, 0, TimeSpan.Zero);

        var agenda = CalendarRules.Agenda([tomorrow, ended, running, over, later], [birthday], Today, Today.AddDays(30), Plus2, limit: 3, now);

        Assert.Equal(2, agenda.Count);
        Assert.Equal(Today, agenda[0].Day);
        Assert.Equal("running", Assert.Single(agenda[0].Items).Event!.Id);
        Assert.Equal(Today.AddDays(1), agenda[1].Day);
        Assert.True(agenda[1].Items[0].IsBirthday);
        Assert.Equal("tomorrow", agenda[1].Items[1].Event!.Id);
    }

    [Fact]
    public void S571_issue_155_a_multi_day_event_is_listed_under_every_day_it_covers()
    {
        // Monday to Thursday, seen on the Sunday before: classic issue #155 listed it on Monday only.
        var trip = CalendarEvent.AllDayEvent("trip", "Trip", Today.AddDays(1), Today.AddDays(5));
        var workshop = Timed("workshop", new DateTimeOffset(2026, 10, 5, 16, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(40));
        var now = new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero);

        var agenda = CalendarRules.Agenda([trip, workshop], [], Today, Today.AddDays(30), Plus2, limit: 20, now);

        Assert.Equal([Today.AddDays(1), Today.AddDays(2), Today.AddDays(3), Today.AddDays(4)], agenda.Select(g => g.Day));
        Assert.All(agenda, g => Assert.Contains(g.Items, i => i.Event!.Id == "trip"));

        // 18:00 Monday to 10:00 Wednesday (UTC+2): Monday, Tuesday and Wednesday, not Thursday.
        Assert.Equal(
            [Today.AddDays(1), Today.AddDays(2), Today.AddDays(3)],
            agenda.Where(g => g.Items.Any(i => i.Event!.Id == "workshop")).Select(g => g.Day));
    }

    [Fact]
    public void S571_issue_155_a_running_multi_day_event_starts_today_and_ended_days_are_gone()
    {
        // Began Friday, ends Tuesday: from today on, never under the days already past.
        var trip = CalendarEvent.AllDayEvent("trip", "Trip", Today.AddDays(-2), Today.AddDays(3));
        var over = CalendarEvent.AllDayEvent("over", "Over", Today.AddDays(-3), Today);
        var now = new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero);

        var agenda = CalendarRules.Agenda([trip, over], [], Today, Today.AddDays(30), Plus2, limit: 20, now);

        Assert.Equal([Today, Today.AddDays(1), Today.AddDays(2)], agenda.Select(g => g.Day));
        Assert.All(agenda, g => Assert.Equal("trip", Assert.Single(g.Items).Event!.Id));
    }

    [Fact]
    public void S571_issue_155_a_continuing_day_shows_that_days_part_of_the_span()
    {
        var workshop = Timed("workshop", new DateTimeOffset(2026, 10, 5, 16, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(40));

        // Monday from 18:00 local, then the whole of Tuesday, then Wednesday until 10:00.
        var monday = CalendarRules.AgendaSpan(workshop, Today.AddDays(1), Plus2);
        Assert.False(monday.WholeDay);
        Assert.Equal(workshop.Start, monday.Start);
        Assert.Equal(CalendarRules.LocalMidnight(Today.AddDays(2), Plus2), monday.End);

        Assert.True(CalendarRules.AgendaSpan(workshop, Today.AddDays(2), Plus2).WholeDay);

        var wednesday = CalendarRules.AgendaSpan(workshop, Today.AddDays(3), Plus2);
        Assert.False(wednesday.WholeDay);
        Assert.Equal(CalendarRules.LocalMidnight(Today.AddDays(3), Plus2), wednesday.Start);
        Assert.Equal(workshop.End, wednesday.End);

        var allDay = CalendarEvent.AllDayEvent("trip", "Trip", Today, Today.AddDays(2));
        Assert.True(CalendarRules.AgendaSpan(allDay, Today.AddDays(1), Plus2).WholeDay);
    }

    [Fact]
    public void S571_issue_153_the_agenda_is_chronological_across_and_within_days()
    {
        // Handed over in no order at all, as several calendars merged arrive.
        var events = new[]
        {
            Timed("oct13", new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1)),
            Timed("oct5-late", new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1)),
            CalendarEvent.AllDayEvent("oct9", "Holiday", new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 10)),
            Timed("oct5-early", new DateTimeOffset(2026, 10, 5, 7, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1)),
            Timed("oct5-mid", new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1)),
        };

        var agenda = CalendarRules.Agenda(events, [], Today, Today.AddDays(30), Plus2, 20, new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(["oct5-early", "oct5-mid", "oct5-late", "oct9", "oct13"], agenda.SelectMany(g => g.Items).Select(i => i.Event!.Id));
    }

    [Fact]
    public void S570_issue_139_the_day_view_shows_what_the_agenda_files_under_that_day()
    {
        // A day weeks ahead, in a zone west of UTC and one east of it.
        var day = new DateOnly(2026, 11, 8);
        foreach (var zone in new[] { Plus2, TimeZoneInfo.CreateCustomTimeZone("Test-5", TimeSpan.FromHours(-5), "Test-5", "Test-5") })
        {
            var events = new[]
            {
                CalendarEvent.AllDayEvent("allday", "Fair", day, day.AddDays(1)),
                Timed("morning", new DateTimeOffset(day.ToDateTime(new TimeOnly(9, 0)), zone.BaseUtcOffset), TimeSpan.FromHours(1)),
                Timed("late", new DateTimeOffset(day.ToDateTime(new TimeOnly(23, 30)), zone.BaseUtcOffset), TimeSpan.FromMinutes(20)),
            };

            var agenda = CalendarRules.Agenda(events, [], Today, Today.AddDays(40), zone, 50, new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero));
            var filed = agenda.Single(g => g.Day == day).Items.Select(i => i.Event!.Id);

            // The day view asks exactly its own day, and the shelf answers that window.
            var (from, to) = CalendarRules.FetchWindow(day, day, zone);
            var asked = events.Where(e => Themia.Core.Sync.CalendarSync.Overlaps(Themia.Core.Sync.OfficeShelves.Store(e), from, to));
            var shown = CalendarRules.DayItems(asked, [], day, zone).Select(i => i.Event!.Id);

            Assert.Equal(filed, shown);
            Assert.Equal(3, shown.Count());
        }
    }

    [Fact]
    public void S571_AgendaBirthdaysCrossNewYear()
    {
        var agenda = CalendarRules.Agenda([], [new Birthday("Jan", 1, 2, null)], new DateOnly(2026, 12, 20), new DateOnly(2027, 1, 19), Plus2, 20, new DateTimeOffset(2026, 12, 20, 0, 0, 0, TimeSpan.Zero));
        Assert.Single(agenda);
        Assert.Equal(new DateOnly(2027, 1, 2), agenda[0].Day);
    }

    // ---- [S-572] / [S-573] / [S-574] layout ------------------------------------------------------

    [Theory]
    [InlineData(0, 2)]
    [InlineData(-50, 2)]
    [InlineData(double.NaN, 2)]
    [InlineData(28, 1)]
    [InlineData(50, 2)]
    [InlineData(66, 3)]
    [InlineData(100, 6)]
    [InlineData(2000, 6)]
    public void S572_ChipCapacityMatchesTheOriginal(double cellHeight, int expected)
        => Assert.Equal(expected, CalendarRules.ChipCapacity(cellHeight, 16, 14));

    [Fact]
    public void S572_CellDensityShedsChipsThenDots()
    {
        Assert.Equal(MonthDensity.Chips, CalendarRules.MonthCellDensity(70, 50));
        Assert.Equal(MonthDensity.Dots, CalendarRules.MonthCellDensity(30, 28));
        Assert.Equal(MonthDensity.Number, CalendarRules.MonthCellDensity(20, 16));
    }

    [Fact]
    public void S573_LanesMatchTheOriginal()
    {
        Assert.Empty(CalendarRules.Lanes([]));
        Assert.Equal([(0, 1), (0, 1), (0, 1)], CalendarRules.Lanes([(0, 10), (10, 20), (30, 40)]));
        Assert.Equal([(1, 2), (0, 2)], CalendarRules.Lanes([(10, 30), (0, 20)]));
        Assert.Equal([(0, 2), (1, 2), (0, 1)], CalendarRules.Lanes([(0, 20), (10, 30), (40, 50)]));
        Assert.Equal([(0, 2), (1, 2), (0, 2)], CalendarRules.Lanes([(0, 30), (10, 50), (35, 45)]));
        var three = CalendarRules.Lanes([(0, 30), (5, 30), (10, 30)]);
        Assert.All(three, l => Assert.Equal(3, l.Columns));
        Assert.Equal([0, 1, 2], three.Select(l => l.Column).Order());
    }


    private static CalendarEvent Timed(string id, DateTimeOffset start, TimeSpan length)
        => new() { Id = id, Subject = id, Start = start, End = start + length };
}
