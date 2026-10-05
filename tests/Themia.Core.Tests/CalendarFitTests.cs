using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The calendar's disclosure ladder, Outlook hand-off and key map ([S-1260]..[S-1269]), at the
/// size tiers' slots (card less a ~44 px header).
/// </summary>
public sealed class CalendarFitTests
{
    private static readonly TimeZoneInfo Plus2 = TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    private static readonly DateOnly Today = new(2026, 10, 4); // a Sunday

    // ---- [S-1260] the ladder -----------------------------------------------------------------------

    [Theory]
    [InlineData(CalendarViewMode.Month, 120, 76, CalendarLayout.Next)]            // XS
    [InlineData(CalendarViewMode.Month, 200, 106, CalendarLayout.Agenda)]         // S
    [InlineData(CalendarViewMode.Month, 320, 196, CalendarLayout.Month)]          // M
    [InlineData(CalendarViewMode.Month, 480, 116, CalendarLayout.Strip)]          // Wide
    [InlineData(CalendarViewMode.Month, 200, 356, CalendarLayout.MonthAgendaBelow)] // Tall
    [InlineData(CalendarViewMode.Month, 560, 376, CalendarLayout.Month)]          // L
    [InlineData(CalendarViewMode.Month, 800, 516, CalendarLayout.MonthAgendaBeside)] // XL
    [InlineData(CalendarViewMode.Week, 480, 116, CalendarLayout.Strip)]
    [InlineData(CalendarViewMode.Week, 200, 356, CalendarLayout.Agenda)]
    [InlineData(CalendarViewMode.Week, 320, 196, CalendarLayout.Week)]
    [InlineData(CalendarViewMode.Day, 200, 356, CalendarLayout.Day)]
    [InlineData(CalendarViewMode.Day, 200, 106, CalendarLayout.Agenda)]
    [InlineData(CalendarViewMode.Agenda, 480, 116, CalendarLayout.Strip)]
    [InlineData(CalendarViewMode.Agenda, 800, 516, CalendarLayout.Agenda)]
    [InlineData(CalendarViewMode.Agenda, 120, 76, CalendarLayout.Next)]
    [InlineData(CalendarViewMode.Week, 0, 0, CalendarLayout.Week)]               // unmeasured: as configured
    [InlineData(CalendarViewMode.Month, 400, 50, CalendarLayout.Next)]           // too short even for a strip
    public void S1260_EveryTierGetsALayoutThatFits(CalendarViewMode wanted, double width, double height, CalendarLayout expected)
        => Assert.Equal(expected, CalendarFit.Layout(wanted, width, height));

    [Fact]
    public void S1260_OnlyCalendarsThatPageHaveNavigation()
    {
        Assert.False(CalendarFit.Pages(CalendarLayout.Next));
        Assert.False(CalendarFit.Pages(CalendarLayout.Agenda));
        Assert.True(CalendarFit.Pages(CalendarLayout.Strip));
        Assert.True(CalendarFit.Pages(CalendarLayout.MonthAgendaBeside));
    }

    // ---- [S-1261] the next thing -------------------------------------------------------------------

    [Fact]
    public void S1261_NextPrefersTheMeetingUnderWayOverTheAllDayEventAndCountsTheRest()
    {
        var now = new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero); // 10:00 local
        var allDay = CalendarEvent.AllDayEvent("holiday", "Holiday", Today, Today.AddDays(1));
        var over = Timed("over", new DateTimeOffset(2026, 10, 4, 6, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));
        var running = Timed("running", new DateTimeOffset(2026, 10, 4, 7, 30, 0, TimeSpan.Zero), TimeSpan.FromHours(1));
        var later = Timed("later", new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));

        var next = CalendarFit.Next([later, over, allDay, running], [], Today, Plus2, now);

        Assert.NotNull(next);
        Assert.Equal("running", next.Item.Event!.Id);
        Assert.Equal(Today, next.Day);
        Assert.Equal(2, next.MoreThatDay); // the holiday and the later meeting; the one over is gone
    }

    [Fact]
    public void S1261_NextFallsToALaterDayAndToABirthdayAndIsNullWhenNothingIsOn()
    {
        var now = new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero);
        var tuesday = Timed("tue", new DateTimeOffset(2026, 10, 6, 7, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));

        var later = CalendarFit.Next([tuesday], [], Today, Plus2, now);
        Assert.Equal(Today.AddDays(2), later!.Day);
        Assert.Equal(0, later.MoreThatDay);

        var birthday = CalendarFit.Next([], [new Birthday("Mia Chen", 10, 4, 1994)], Today, Plus2, now);
        Assert.True(birthday!.Item.IsBirthday);

        Assert.Null(CalendarFit.Next([], [], Today, Plus2, now));
    }

    // ---- [S-1262] the strip ------------------------------------------------------------------------

    [Theory]
    [InlineData(CalendarViewMode.Month, 480, 4)]
    [InlineData(CalendarViewMode.Month, 360, 3)]
    [InlineData(CalendarViewMode.Day, 1200, 5)]
    [InlineData(CalendarViewMode.Agenda, 100, 3)]
    [InlineData(CalendarViewMode.Week, 480, 7)]
    public void S1262_StripDaysByWidthAndAWeekIsSeven(CalendarViewMode wanted, double width, int expected)
        => Assert.Equal(expected, CalendarFit.StripDays(wanted, width));

    [Fact]
    public void S1262_AStripRangesStepsAndTitlesByItsOwnDays()
    {
        Assert.Equal((Today, Today.AddDays(3)), CalendarFit.Range(CalendarLayout.Strip, CalendarViewMode.Month, Today, Today, WeekStartDay.Monday, 4));
        Assert.Equal(Today.AddDays(4), CalendarFit.Step(CalendarLayout.Strip, Today, 1, 4));
        Assert.Equal("Oct 4 – Oct 7", CalendarFit.Title(CalendarLayout.Strip, CalendarViewMode.Month, Today, WeekStartDay.Monday, 4, Locale.En));

        // A week strip is the week holding the anchor, Monday first.
        Assert.Equal((new DateOnly(2026, 9, 28), Today), CalendarFit.Range(CalendarLayout.Strip, CalendarViewMode.Week, Today, Today, WeekStartDay.Monday, 7));
        Assert.True(CalendarFit.InView(CalendarLayout.Strip, CalendarViewMode.Month, Today.AddDays(-4), Today, WeekStartDay.Monday, 4, Today.AddDays(-1)));
        Assert.False(CalendarFit.InView(CalendarLayout.Strip, CalendarViewMode.Month, Today.AddDays(-4), Today, WeekStartDay.Monday, 4, Today));
    }

    [Fact]
    public void S1263_TheSplitMonthsFetchAndPageAsTheMonth()
    {
        var month = CalendarRules.ViewRange(CalendarViewMode.Month, Today, Today, WeekStartDay.Monday);
        Assert.Equal(month, CalendarFit.Range(CalendarLayout.MonthAgendaBelow, CalendarViewMode.Month, Today, Today, WeekStartDay.Monday, 0));
        Assert.Equal(new DateOnly(2026, 11, 4), CalendarFit.Step(CalendarLayout.MonthAgendaBeside, Today, 1, 0));
        Assert.Equal("October 2026", CalendarFit.Title(CalendarLayout.MonthAgendaBelow, CalendarViewMode.Month, Today, WeekStartDay.Monday, 0, Locale.En));
        Assert.Equal(24, CalendarFit.SquareCell(120));
        Assert.Equal(28, CalendarFit.SquareCell(196));
        Assert.Equal(32, CalendarFit.SquareCell(800));
    }

    // ---- [S-1264] month rows ----------------------------------------------------------------------

    [Theory]
    [InlineData(2026, 10, WeekStartDay.Monday, 5)]  // Thu 1st .. Sat 31st
    [InlineData(2026, 3, WeekStartDay.Monday, 6)]   // Sun 1st, Monday weeks: six rows
    [InlineData(2026, 2, WeekStartDay.Sunday, 4)]   // Sun 1st .. Sat 28th: exactly four
    [InlineData(2026, 8, WeekStartDay.Sunday, 6)]   // Sat 1st .. Mon 31st
    public void S1264_AMonthNeedsOnlyTheWeeksHoldingItsDays(int year, int month, WeekStartDay start, int rows)
        => Assert.Equal(rows, CalendarFit.MonthRows(new DateOnly(year, month, 15), start));

    // ---- [S-1266] / [S-1267] ----------------------------------------------------------------------

    [Theory]
    [InlineData(300, 40)]
    [InlineData(480, 32)]
    [InlineData(560, 32)]
    [InlineData(600, 40)]
    public void S1266_HoursCompactOnlyWhenThatShowsTheWholeDay(double available, double hour)
        => Assert.Equal(hour, CalendarFit.HourHeight(available));

    [Fact]
    public void S1267_OnlyAShortTilesFirstTodayLosesItsHeading()
    {
        Assert.False(CalendarFit.ShowsHeading(Today, Today, first: true, height: 106));
        Assert.True(CalendarFit.ShowsHeading(Today.AddDays(1), Today, first: true, height: 106));
        Assert.True(CalendarFit.ShowsHeading(Today, Today, first: true, height: 200));
        Assert.True(CalendarFit.ShowsHeading(Today, Today, first: false, height: 106));
    }

    // ---- [S-1268] Outlook hand-off ----------------------------------------------------------------

    [Fact]
    public void S1268_MicrosoftEventsOpenInOutlookWhenItIsInstalled()
    {
        var e = Timed("AAMk/a+b=", Today.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(1)) with { WebLink = "https://outlook.office.com/calendar/item/1" };

        Assert.Equal("ms-outlook://events/AAMk%2Fa%2Bb%3D", CalendarLinks.Open(e, AccountProvider.Microsoft, outlookInstalled: true));
        Assert.Equal("https://outlook.office.com/calendar/item/1", CalendarLinks.Open(e, AccountProvider.Microsoft, outlookInstalled: false));

        // A Google event is never handed to Outlook, whatever its link says.
        Assert.Equal("https://outlook.office.com/calendar/item/1", CalendarLinks.Open(e, AccountProvider.Google, outlookInstalled: true));
        Assert.Null(CalendarLinks.Open(e with { WebLink = null }, AccountProvider.CalDav, outlookInstalled: true));
        Assert.Null(CalendarLinks.Open(e with { WebLink = "http://example.com/x" }, AccountProvider.Google, outlookInstalled: false));
        Assert.Null(CalendarLinks.Open(e with { WebLink = "https://user:pw@example.com/x" }, AccountProvider.Google, outlookInstalled: false));
    }

    [Theory]
    [InlineData("ms-outlook://events/AAMk%2Fa", true)]
    [InlineData("https://calendar.google.com/event?eid=x", true)]
    [InlineData("ms-outlook://events/", false)]
    [InlineData("ms-outlook://events/a b", false)]
    [InlineData("ms-outlook://emails/abc", false)]
    [InlineData("file:///C:/Windows/notepad.exe", false)]
    [InlineData("https://example.com/\n", false)]
    [InlineData(null, false)]
    public void S1268_OnlyTheTwoShapesAreLaunchable(string? link, bool expected)
        => Assert.Equal(expected, CalendarLinks.IsLaunchable(link));

    // ---- [S-1269] keys -----------------------------------------------------------------------------

    [Fact]
    public void S1269_KeysFollowTheClassicMap()
    {
        Assert.Equal(CalendarKeyCommand.Back, CalendarFit.Key(WidgetKey.Left, CalendarLayout.Month));
        Assert.Equal(CalendarKeyCommand.Forward, CalendarFit.Key(WidgetKey.Right, CalendarLayout.Strip));
        Assert.Equal(CalendarKeyCommand.WeekBack, CalendarFit.Key(WidgetKey.Up, CalendarLayout.MonthAgendaBelow));
        Assert.Equal(CalendarKeyCommand.WeekForward, CalendarFit.Key(WidgetKey.Down, CalendarLayout.Week));
        Assert.Equal(CalendarKeyCommand.None, CalendarFit.Key(WidgetKey.Down, CalendarLayout.Day));
        Assert.Equal(CalendarKeyCommand.Today, CalendarFit.Key(WidgetKey.Home, CalendarLayout.Day));
        Assert.Equal(CalendarKeyCommand.OpenFirst, CalendarFit.Key(WidgetKey.Enter, CalendarLayout.Agenda));
        Assert.Equal(CalendarKeyCommand.None, CalendarFit.Key(WidgetKey.Left, CalendarLayout.Agenda));
        Assert.Equal(CalendarKeyCommand.None, CalendarFit.Key(WidgetKey.Escape, CalendarLayout.Month));
    }

    private static CalendarEvent Timed(string id, DateTimeOffset start, TimeSpan length)
        => new() { Id = id, Subject = id, Start = start, End = start + length };
}
