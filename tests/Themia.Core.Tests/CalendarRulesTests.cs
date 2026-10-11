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
    }

    [Fact]
    public void S564_StartOfWeekAndColumnOrder()
    {
        Assert.Equal(new DateOnly(2026, 9, 28), CalendarRules.StartOfWeek(Today, WeekStartDay.Monday));
        Assert.Equal(Today, CalendarRules.StartOfWeek(Today, WeekStartDay.Sunday));
        Assert.Equal(DayOfWeek.Monday, CalendarRules.WeekdayOrder(WeekStartDay.Monday)[0]);
        Assert.Equal(DayOfWeek.Sunday, CalendarRules.WeekdayOrder(WeekStartDay.Monday)[6]);
        Assert.Equal(DayOfWeek.Sunday, CalendarRules.WeekdayOrder(WeekStartDay.Sunday)[0]);
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
        var (first, last) = CalendarRules.ViewRange(CalendarLayout.Month, Today, Today, WeekStartDay.Monday);
        Assert.Equal(new DateOnly(2026, 9, 28), first);
        Assert.Equal(new DateOnly(2026, 11, 1), last);

        var (sFirst, sLast) = CalendarRules.ViewRange(CalendarLayout.Month, Today, Today, WeekStartDay.Sunday);
        Assert.Equal(new DateOnly(2026, 9, 27), sFirst);
        Assert.Equal(new DateOnly(2026, 10, 31), sLast);

        Assert.Equal(42, CalendarRules.MonthCells(Today, WeekStartDay.Monday).Count);
    }

    [Fact]
    public void S568_WeekDayAndAgendaRanges()
    {
        Assert.Equal((new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 4)), CalendarRules.ViewRange(CalendarLayout.Week, Today, Today, WeekStartDay.Monday));
        Assert.Equal((Today, Today), CalendarRules.ViewRange(CalendarLayout.Day, Today, Today, WeekStartDay.Monday));
        Assert.Equal((Today, Today.AddDays(30)), CalendarRules.ViewRange(CalendarLayout.Agenda, Today.AddDays(-90), Today, WeekStartDay.Monday));
    }

    [Fact]
    public void S2707_AYearFetchesAndTitlesItsWholeYearAndPagesByYear()
    {
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), CalendarRules.ViewRange(CalendarLayout.Year, Today, Today, WeekStartDay.Monday));
        Assert.Equal("2026", CalendarRules.Title(CalendarLayout.Year, Today, WeekStartDay.Monday, Locale.En));
        Assert.Equal(new DateOnly(2027, 2, 28), CalendarRules.Step(CalendarLayout.Year, new DateOnly(2028, 2, 29), -1));
        Assert.Equal(new DateOnly(2025, 10, 4), CalendarRules.Step(CalendarLayout.Year, Today, -1));
        Assert.True(CalendarRules.InView(CalendarLayout.Year, Today.AddMonths(-3), Today, WeekStartDay.Monday, Today));
        Assert.False(CalendarRules.InView(CalendarLayout.Year, Today.AddYears(1), Today, WeekStartDay.Monday, Today));
    }

    [Fact]
    public void S569_AMonthIsInViewOnlyForItsOwnDaysAndTheAgendaAlwaysHoldsToday()
    {
        Assert.False(CalendarRules.InView(CalendarLayout.Month, Today, Today, WeekStartDay.Monday, new DateOnly(2026, 9, 30)));
        Assert.True(CalendarRules.InView(CalendarLayout.Week, Today, Today, WeekStartDay.Monday, new DateOnly(2026, 9, 30)));
        Assert.True(CalendarRules.InView(CalendarLayout.Agenda, Today.AddDays(-90), Today, WeekStartDay.Monday, Today));
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
        Assert.Equal("October 2026", CalendarRules.Title(CalendarLayout.Month, Today, WeekStartDay.Monday, Locale.En));
        Assert.Equal("Sep 28 – Oct 4", CalendarRules.Title(CalendarLayout.Week, Today, WeekStartDay.Monday, Locale.En));
        Assert.Equal("Sun, Oct 4", CalendarRules.Title(CalendarLayout.Day, Today, WeekStartDay.Monday, Locale.En));
        Assert.Equal(string.Empty, CalendarRules.Title(CalendarLayout.Agenda, Today, WeekStartDay.Monday, Locale.En));
    }

    // ---- [S-569] navigation ----------------------------------------------------------------------

    [Fact]
    public void S569_StepsByMonthWeekDayAndTheAgendaStaysPut()
    {
        Assert.Equal(new DateOnly(2026, 2, 28), CalendarRules.Step(CalendarLayout.Month, new DateOnly(2026, 1, 31), 1));
        Assert.Equal(Today.AddDays(-7), CalendarRules.Step(CalendarLayout.Week, Today, -1));
        Assert.Equal(Today.AddDays(1), CalendarRules.Step(CalendarLayout.Day, Today, 1));
        Assert.Equal(Today, CalendarRules.Step(CalendarLayout.Agenda, Today, 1));
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
