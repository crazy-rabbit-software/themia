using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The calendar's layouts fitted to their boxes ([S-2700]..[S-2710]), the Outlook hand-off
/// ([S-1268]), at the size tiers' slots (card less a 36 header).
/// </summary>
public sealed class CalendarFitTests
{
    /// <summary>[S-7035] The rows' groups as the kit pages them: each day's key.</summary>
    private static IReadOnlyList<string> K(IReadOnlyList<DateOnly> days) => [.. days.Select(d => d.ToString("O", System.Globalization.CultureInfo.InvariantCulture))];

    private static readonly TimeZoneInfo Plus2 = TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    private static readonly DateOnly Today = new(2026, 10, 4); // a Sunday

    private static readonly DateOnly Monday = new(2026, 9, 28);

    // ---- [S-2702] month ----------------------------------------------------------------------------

    // The tiers' month boxes: the card less its 16 inset either side, the header and the bottom inset.
    [Theory]
    [InlineData(88, 60, WeekdayLabel.None, MonthDensity.Number, 0, 3)]       // XS: the weeks that fit
    [InlineData(168, 90, WeekdayLabel.None, MonthDensity.Number, 0, 5)]      // S: every week, no header
    [InlineData(288, 136, WeekdayLabel.Initials, MonthDensity.Dots, 0, 5)]   // M
    [InlineData(448, 56, WeekdayLabel.None, MonthDensity.Number, 0, 3)]      // Wide
    [InlineData(168, 340, WeekdayLabel.Initials, MonthDensity.Dots, 0, 5)]   // Tall: cells too narrow for words
    [InlineData(528, 316, WeekdayLabel.Short, MonthDensity.Chips, 2, 5)]     // L
    [InlineData(768, 456, WeekdayLabel.Short, MonthDensity.Chips, 3, 5)]     // XL
    public void S2702_AMonthShedsWithinItself(double width, double height, WeekdayLabel weekdays, MonthDensity density, int chips, int rows)
    {
        var fit = CalendarFit.Month(width, height, rows: 5, focusRow: 4);

        Assert.Equal(rows, fit.Rows);
        Assert.Equal(weekdays, fit.Weekdays);
        Assert.Equal(density, fit.Density);
        Assert.Equal(chips, fit.ChipLines);
        Assert.Equal(height, fit.HeaderHeight + (fit.Rows * fit.CellHeight), 6);
        Assert.True(fit.CellHeight >= FamilyType.Caption.LineHeight, "a number's Caption line is never squeezed");
    }

    [Fact]
    public void S2702_a_box_too_short_for_every_week_keeps_the_focused_one_among_those_it_shows()
    {
        Assert.Equal(2, CalendarFit.Month(88, 60, rows: 5, focusRow: 4).FirstRow);   // rows 2..4
        Assert.Equal(0, CalendarFit.Month(88, 60, rows: 5, focusRow: 0).FirstRow);
        Assert.Equal(1, CalendarFit.Month(88, 60, rows: 5, focusRow: 2).FirstRow);   // centred: 1..3
        Assert.Equal(0, CalendarFit.Month(288, 136, rows: 5, focusRow: 4).FirstRow);
    }

    [Fact]
    public void S2702_a_column_too_narrow_for_two_digits_holds_no_day_grid()
    {
        Assert.False(CalendarFit.HoldsDays(88 / 7.0));    // XS: the day as a Readout
        Assert.True(CalendarFit.HoldsDays(168 / 7.0));    // S
        Assert.True(CalendarFit.HoldsDays(168 / 4.0));    // a four-day week on S
    }

    [Fact]
    public void S2702_ChipTimesFromWideCellsAndWeekdayLettersByColumn()
    {
        Assert.True(CalendarFit.Month(768, 456, 5).Times);
        Assert.False(CalendarFit.Month(528, 316, 5).Times);
        Assert.Equal(WeekdayLabel.Narrow, CalendarFit.Weekdays(15));
        Assert.Equal(WeekdayLabel.None, CalendarFit.Weekdays(8));
        Assert.True(CalendarFit.Month(448, 56, 5).InlineDots);   // Wide: dots beside the numbers
        Assert.False(CalendarFit.Month(168, 90, 5).InlineDots);  // S: too narrow
        Assert.False(CalendarFit.Month(288, 136, 5).InlineDots);
    }

    [Theory]
    [InlineData(2026, 10, WeekStartDay.Monday, 5)]  // Thu 1st .. Sat 31st
    [InlineData(2026, 3, WeekStartDay.Monday, 6)]   // Sun 1st, Monday weeks: six rows
    [InlineData(2026, 2, WeekStartDay.Sunday, 4)]   // Sun 1st .. Sat 28th: exactly four
    [InlineData(2026, 8, WeekStartDay.Sunday, 6)]   // Sat 1st .. Mon 31st
    public void S1264_AMonthNeedsOnlyTheWeeksHoldingItsDays(int year, int month, WeekStartDay start, int rows)
        => Assert.Equal(rows, CalendarFit.MonthRows(new DateOnly(year, month, 15), start));

    // ---- [S-2703] bars -----------------------------------------------------------------------------

    [Fact]
    public void S2703_AMultiDayEventIsOneBarAndSinglesFillTheFreeLines()
    {
        var monday = Timed("mon", At(Monday, 9), TimeSpan.FromHours(1));
        var trip = CalendarEvent.AllDayEvent("trip", "Trip", Monday.AddDays(1), Monday.AddDays(4)); // Tue..Thu
        var wednesday = Timed("wed", At(Monday.AddDays(2), 9), TimeSpan.FromHours(1));

        var bars = CalendarFit.Bars([wednesday, trip, monday], [], Monday, 7, Plus2, singleTimed: true);

        var t = bars.Single(b => b.Item.Event!.Id == "trip");
        Assert.Equal((1, 3, 0), (t.First, t.Last, t.Lane));
        Assert.False(t.ContinuesBefore || t.ContinuesAfter);
        Assert.Equal(0, bars.Single(b => b.Item.Event!.Id == "mon").Lane);
        Assert.Equal(1, bars.Single(b => b.Item.Event!.Id == "wed").Lane);
        Assert.Equal(2, CalendarFit.LaneCount(bars));
        Assert.Equal(1, CalendarFit.Hidden(bars, column: 2, lines: 1));
        Assert.Equal(0, CalendarFit.Hidden(bars, column: 1, lines: 1));
    }

    [Fact]
    public void S2703_BarsClipToTheRowAndTheWeekKeepsItsTimedSinglesOut()
    {
        var long_ = CalendarEvent.AllDayEvent("long", "Holiday", Monday.AddDays(-3), Monday.AddDays(2)); // Fri..Tue
        var overnight = Timed("night", At(Monday.AddDays(4), 22), TimeSpan.FromHours(4));                 // Fri 22:00 – Sat 02:00
        var toMidnight = Timed("late", At(Monday.AddDays(5), 20), TimeSpan.FromHours(4));                 // Sat 20:00 – 24:00
        var single = Timed("single", At(Monday, 9), TimeSpan.FromHours(1));
        var birthday = new Birthday("Mia Chen", 10, 1, 1994);

        var bars = CalendarFit.Bars([long_, overnight, toMidnight, single], [birthday], Monday, 7, Plus2, singleTimed: false);

        var l = bars.Single(b => b.Item.Event?.Id == "long");
        Assert.Equal((0, 1), (l.First, l.Last));
        Assert.True(l.ContinuesBefore);
        Assert.False(l.ContinuesAfter);

        var n = bars.Single(b => b.Item.Event?.Id == "night");
        Assert.Equal((4, 5), (n.First, n.Last));

        Assert.DoesNotContain(bars, b => b.Item.Event?.Id is "single" or "late");
        Assert.Equal(3, bars.Single(b => b.Item.IsBirthday).First);
        Assert.Equal(Monday.AddDays(5), CalendarFit.LastDay(toMidnight, Plus2));
    }

    // ---- [S-2704] week -----------------------------------------------------------------------------

    [Theory]
    [InlineData(800, 524, true, false, WeekdayLabel.Short)]
    [InlineData(560, 384, false, true, WeekdayLabel.Short)]    // 80 a column: marks, no hour grid
    [InlineData(320, 204, false, true, WeekdayLabel.Initials)]
    [InlineData(200, 364, false, true, WeekdayLabel.Initials)]
    [InlineData(120, 84, false, true, WeekdayLabel.Narrow)]
    [InlineData(40, 76, false, true, WeekdayLabel.Narrow)]  // never without its day
    public void S2704_AWeekIsAnHourGridOnALargeTileElseColumns(double width, double height, bool timeline, bool marks, WeekdayLabel weekdays)
    {
        var fit = CalendarFit.Week(width, height, 7);
        Assert.Equal(timeline, fit.Timeline);
        Assert.Equal(marks, fit.Marks);
        Assert.Equal(weekdays, fit.Weekdays);
    }

    [Theory]
    [InlineData(7 * 55, true)]
    [InlineData(7 * 95.9, true)]
    [InlineData(7 * 96, false)]
    [InlineData(7 * 130, false)]
    public void S2281_a_column_under_96_holds_marks_and_from_96_titles(double width, bool marks)
    {
        Assert.Equal(marks, CalendarFit.Week(width, 600, 7).Marks);
        Assert.Equal(marks, CalendarFit.Week(width * 2, 600, 14).Marks);   // the column decides, not the card
        Assert.False(CalendarFit.Week(width, 600, 7).Timeline && marks);
    }

    [Fact]
    public void S2282_a_day_shows_three_marks_at_most_and_the_rest_as_plus_n()
    {
        Assert.Equal((0, 0), CalendarFit.Marks(0));
        Assert.Equal((2, 0), CalendarFit.Marks(2));
        Assert.Equal((3, 0), CalendarFit.Marks(3));
        Assert.Equal((3, 5), CalendarFit.Marks(8));

        Assert.Equal(0, CalendarFit.MarksHeight([0, 0, 0]));
        Assert.Equal(FamilyShape.Track, CalendarFit.MarksHeight([1, 0]));
        Assert.Equal((3 * FamilyShape.Track) + (2 * FamilySpace.S1), CalendarFit.MarksHeight([3, 1]));
        Assert.Equal((3 * FamilyShape.Track) + (3 * FamilySpace.S1) + FamilyType.Caption.LineHeight, CalendarFit.MarksHeight([8, 1]));
    }

    [Fact]
    public void S2283_the_strip_comes_first_and_the_rows_take_whole_rows_of_what_is_left()
    {
        var marks = CalendarFit.MarksHeight([3]);
        var strip = FamilyType.Caption.LineHeight + FamilyType.BodyStrong.LineHeight + FamilySpace.S1 + marks;

        var tall = CalendarFit.Strip(400, marks, 1);
        Assert.True(tall.Weekdays && tall.Marks);
        Assert.Equal(strip, tall.Height, 6);
        Assert.Equal(RowRules.Fit(400 - FamilySpace.Inset - strip - FamilySpace.S2, 1), tall.Rows);
        Assert.Equal(RowRules.Fit(400 - FamilySpace.Inset - strip - FamilySpace.S2, 2), CalendarFit.Strip(400, marks, 2).Rows);

        // One row short: only the strip.
        var justStrip = CalendarFit.Strip(FamilySpace.Inset + strip + FamilySpace.S2 + FamilyShape.Row1 - 1, marks, 1);
        Assert.True(justStrip.Marks);
        Assert.Equal(0, justStrip.Rows);
        Assert.Equal(1, CalendarFit.Strip(FamilySpace.Inset + strip + FamilySpace.S2 + FamilyShape.Row1, marks, 1).Rows);

        // A week with no events: no marks, the rows start under the numbers.
        var empty = CalendarFit.Strip(400, 0, 1);
        Assert.False(empty.Marks);
        Assert.Equal(strip - marks, empty.Height, 6);
    }

    [Fact]
    public void S2284_the_chosen_day_is_the_selection_in_the_week_else_today_else_the_first_day()
    {
        var first = new DateOnly(2026, 11, 8);
        var today = new DateOnly(2026, 11, 12);
        Assert.Equal(today, CalendarFit.ChosenDay(null, first, 7, today));
        Assert.Equal(first.AddDays(1), CalendarFit.ChosenDay(first.AddDays(1), first, 7, today));
        Assert.Equal(today, CalendarFit.ChosenDay(first.AddDays(7), first, 7, today));   // selected outside the week
        Assert.Equal(first.AddDays(7), CalendarFit.ChosenDay(null, first.AddDays(7), 7, today));   // a week without today
        Assert.Equal(first.AddDays(9), CalendarFit.ChosenDay(first.AddDays(9), first.AddDays(7), 7, today));
    }

    // ---- [S-2705] hours ----------------------------------------------------------------------------

    [Fact]
    public void S2705_TheWholeDayFillsTheBoxAndATallOneShowsMoreHours()
    {
        Assert.Equal(new HourWindow(7, 15, 40), CalendarFit.Hours(600, 24, 0, 9));

        var tall = CalendarFit.Hours(900, 24, 0, 9);
        Assert.Equal((3, 19), (tall.First, tall.Count));
        Assert.True(tall.HourHeight <= CalendarFit.MaxHour);
        Assert.Equal(900, tall.HourHeight * tall.Count, 6);
    }

    [Fact]
    public void S2705_AShortBoxShowsFewerHoursAroundTheFocus()
    {
        Assert.Equal(new HourWindow(13, 5, 20), CalendarFit.Hours(100, 24, 0, 14));
        Assert.Equal(7, CalendarFit.Hours(100, 24, 0, 6).First);   // never before the day it wants
        Assert.Equal(17, CalendarFit.Hours(100, 24, 0, 23).First); // nor past it
        Assert.Equal(5, CalendarFit.Hours(600, 5, 7, 6).First);    // an early meeting widens the day
    }

    [Fact]
    public void S2705_EventHoursAndTheFocusFollowTheDaysEvents()
    {
        var early = Timed("early", At(Today, 5, 30), TimeSpan.FromHours(1));
        var night = Timed("night", At(Today, 21), TimeSpan.FromHours(4));
        Assert.Equal((5, 24), CalendarFit.EventHours([early, night], Today, Plus2));

        var over = Timed("over", At(Today, 8), TimeSpan.FromHours(1));
        var later = Timed("later", At(Today, 13), TimeSpan.FromHours(1));
        var now = At(Today, 10);
        Assert.Equal(13, CalendarFit.FocusHour([over, later], Today, Today, now, Plus2));
        Assert.Equal(8, CalendarFit.FocusHour([later, over], Today, Today.AddDays(-1), now, Plus2));
        Assert.Equal(10, CalendarFit.FocusHour([], Today, Today, now, Plus2));
        Assert.Equal(10, CalendarFit.FocusHour([over], Today, Today, now, Plus2)); // all behind: now
        Assert.Equal(0, CalendarFit.FocusHour([Timed("x", At(Today.AddDays(-1), 22), TimeSpan.FromHours(4))], Today, Today.AddDays(5), now, Plus2));
    }

    [Fact]
    public void S2705_PlaceClipsToTheWindowAndDropsWhatIsOutside()
    {
        var window = new HourWindow(7, 15, 40);
        var (top, height) = CalendarFit.Place(At(Today, 9), At(Today, 10), Today, Plus2, window)!.Value;
        Assert.Equal(2.0 / 15, top, 6);
        Assert.Equal(1.0 / 15, height, 6);

        Assert.Null(CalendarFit.Place(At(Today, 5), At(Today, 5, 10), Today, Plus2, window));

        var (clippedTop, clipped) = CalendarFit.Place(At(Today, 6, 30), At(Today, 8), Today, Plus2, window)!.Value;
        Assert.Equal(0, clippedTop);
        Assert.Equal(1.0 / 15, clipped, 6);
    }

    // ---- [S-2707] year -----------------------------------------------------------------------------

    [Theory]
    [InlineData(800, 524, 12, 4, 3, true, true)]   // XL
    [InlineData(560, 384, 12, 4, 3, true, false)]  // L
    [InlineData(320, 204, 12, 4, 3, false, false)] // M: squares
    [InlineData(480, 124, 12, 6, 2, false, false)] // Wide
    [InlineData(200, 364, 12, 3, 4, false, false)] // Tall
    [InlineData(200, 114, 6, 3, 2, false, false)]  // S: half a year
    [InlineData(120, 84, 3, 3, 1, false, false)]   // XS: three months
    public void S2707_AYearReflowsByShapeAndShowsFewerMonthsWhenTiny(double width, double height, int months, int columns, int rows, bool numbers, bool weekdays)
    {
        var fit = CalendarFit.Year(width, height, gap: 8);
        Assert.Equal((months, columns, rows, numbers, weekdays), (fit.Months, fit.Columns, fit.Rows, fit.Numbers, fit.Weekdays));
        Assert.True(fit.DayCell >= CalendarFit.MinYearCell);
    }

    [Theory]
    [InlineData(10, 12, 1)]
    [InlineData(10, 3, 9)]
    [InlineData(12, 3, 10)]
    [InlineData(1, 6, 1)]
    [InlineData(10, 6, 7)]
    [InlineData(10, 2, 10)]
    [InlineData(10, 1, 10)]
    public void S2707_FewerMonthsSitAroundTheCurrentOneInsideItsYear(int month, int months, int first)
        => Assert.Equal(first, CalendarFit.FirstMonth(new DateOnly(2026, month, 4), months));

    [Fact]
    public void S2707_AMiniMonthHasItsOwnDaysOnly()
    {
        var cells = CalendarFit.MiniMonth(2026, 10, WeekStartDay.Monday);
        Assert.Equal(42, cells.Count);
        Assert.Null(cells[2]);
        Assert.Equal(new DateOnly(2026, 10, 1), cells[3]);
        Assert.Equal(new DateOnly(2026, 10, 31), cells[33]);
        Assert.Null(cells[34]);
        Assert.Equal(new DateOnly(2026, 10, 1), CalendarFit.MiniMonth(2026, 10, WeekStartDay.Sunday)[4]);
    }

    [Fact]
    public void S2707_ByDayFilesEveryDayAnEventCoversInADaysOrder()
    {
        var trip = CalendarEvent.AllDayEvent("trip", "Trip", new DateOnly(2026, 12, 30), new DateOnly(2027, 1, 3));
        var meeting = Timed("meet", At(new DateOnly(2026, 12, 31), 9), TimeSpan.FromHours(1));
        var birthday = new Birthday("Mia Chen", 12, 31, 1994);

        var days = CalendarFit.ByDay([meeting, trip], [birthday], new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Plus2);

        Assert.Equal(2, days.Count); // the trip's January days are outside the year
        var eve = days[new DateOnly(2026, 12, 31)];
        Assert.True(eve[0].IsBirthday);
        Assert.Equal("trip", eve[1].Event!.Id);
        Assert.Equal("meet", eve[2].Event!.Id);
    }

    // ---- [S-2709] agenda ---------------------------------------------------------------------------

    private static readonly DateOnly[] AgendaDays = [Today, Today, Today.AddDays(1), Today.AddDays(1), Today.AddDays(3)];

    [Fact]
    public void S2709_AgendaFitsWholeRowsAndRepeatsTheHeadingOfADaySplitByAPage()
    {
        Assert.Equal(2, KitPages.Fit(K(AgendaDays), 0, 150, 32, 40, firstHeading: true));
        Assert.Equal(2, KitPages.Fit(K(AgendaDays), 2, 150, 32, 40, firstHeading: true));
        Assert.Equal(2, KitPages.Fit(K(AgendaDays), 0, 106, 32, 40, firstHeading: false));
        Assert.Equal(1, KitPages.Fit(K(AgendaDays), 0, 20, 32, 40, firstHeading: true)); // paging must move
        Assert.Equal(0, KitPages.Fit(K(AgendaDays), 5, 500, 32, 40, firstHeading: true));
    }

    [Fact]
    public void S2709_AgendaPagesFillEveryColumnInTurn()
    {
        Assert.Equal([0, 2, 4], KitPages.Pages(K(AgendaDays), 1, 150, 32, 40, firstHeading: true));
        Assert.Equal([0, 4], KitPages.Pages(K(AgendaDays), 2, 150, 32, 40, firstHeading: true));
        Assert.Equal([0], KitPages.Pages([], 1, 150, 32, 40, firstHeading: true));
    }

    [Fact]
    public void S1267_OnlyAShortTilesTodayLosesItsHeading()
    {
        Assert.False(KitPages.ShowsFirstHeading(Today, Today, height: 106));
        Assert.True(KitPages.ShowsFirstHeading(Today.AddDays(1), Today, height: 106));
        Assert.True(KitPages.ShowsFirstHeading(Today, Today, height: 200));
    }

    // ---- [S-1268] Outlook hand-off ----------------------------------------------------------------

    [Fact]
    public void S1268_MicrosoftEventsOpenInOutlookWhenItIsInstalled()
    {
        var e = Timed("AAMk/a+b=", Today.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(1)) with { WebLink = "https://outlook.office.com/calendar/item/1" };

        Assert.Equal("ms-outlook://events/AAMk%2Fa%2Bb%3D", CalendarLinks.Open(e, HostProviders.Microsoft, outlookInstalled: true));
        Assert.Equal("https://outlook.office.com/calendar/item/1", CalendarLinks.Open(e, HostProviders.Microsoft, outlookInstalled: false));

        // A Google event is never handed to Outlook, whatever its link says.
        Assert.Equal("https://outlook.office.com/calendar/item/1", CalendarLinks.Open(e, HostProviders.Google, outlookInstalled: true));
        Assert.Null(CalendarLinks.Open(e with { WebLink = null }, HostProviders.CalDav, outlookInstalled: true));
        Assert.Null(CalendarLinks.Open(e with { WebLink = "http://example.com/x" }, HostProviders.Google, outlookInstalled: false));
        Assert.Null(CalendarLinks.Open(e with { WebLink = "https://user:pw@example.com/x" }, HostProviders.Google, outlookInstalled: false));
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

    // ---- [S-2710] keys -----------------------------------------------------------------------------

    [Fact]
    public void S2710_EveryLayoutPagesByItsOwnUnit()
    {
        Assert.Equal(CalendarKeyCommand.Back, CalendarFit.Key(WidgetKey.Left, CalendarStyle.Month));
        Assert.Equal(CalendarKeyCommand.Forward, CalendarFit.Key(WidgetKey.Right, CalendarStyle.Year));
        Assert.Equal(CalendarKeyCommand.Back, CalendarFit.Key(WidgetKey.PageUp, CalendarStyle.Agenda));
        Assert.Equal(CalendarKeyCommand.WeekBack, CalendarFit.Key(WidgetKey.Up, CalendarStyle.Month));
        Assert.Equal(CalendarKeyCommand.WeekForward, CalendarFit.Key(WidgetKey.Down, CalendarStyle.Week));
        Assert.Equal(CalendarKeyCommand.None, CalendarFit.Key(WidgetKey.Down, CalendarStyle.Day));
        Assert.Equal(CalendarKeyCommand.None, CalendarFit.Key(WidgetKey.Up, CalendarStyle.Year));
        Assert.Equal(CalendarKeyCommand.Today, CalendarFit.Key(WidgetKey.Home, CalendarStyle.Agenda));
        Assert.Equal(CalendarKeyCommand.OpenFirst, CalendarFit.Key(WidgetKey.Enter, CalendarStyle.Month));
        Assert.Equal(CalendarKeyCommand.None, CalendarFit.Key(WidgetKey.Enter, CalendarStyle.Year));
        Assert.Equal(CalendarKeyCommand.None, CalendarFit.Key(WidgetKey.Escape, CalendarStyle.Month));
    }

    /// <summary>
    /// [S-3652] [S-7065] The agenda is the kit's list: its cursor keys (Up, Down, End, Enter, the Menu
    /// key) are the list's, so Enter opens the row the cursor is on, never one the user has not
    /// pointed at; paging and today stay the calendar's.
    /// </summary>
    [Fact]
    public void S3652_the_agendas_cursor_keys_are_the_kits_lists()
    {
        foreach (var key in new[] { WidgetKey.Up, WidgetKey.Down, WidgetKey.End, WidgetKey.Enter, WidgetKey.Menu })
        {
            Assert.Equal(CalendarKeyCommand.None, CalendarFit.Key(key, CalendarStyle.Agenda));
        }

        Assert.Equal(CalendarKeyCommand.Back, CalendarFit.Key(WidgetKey.Left, CalendarStyle.Agenda));
        Assert.Equal(CalendarKeyCommand.Forward, CalendarFit.Key(WidgetKey.PageDown, CalendarStyle.Agenda));
    }

    /// <summary>A wall-clock time on a day in the test zone.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0)
        => new(day.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(2));

    private static CalendarEvent Timed(string id, DateTimeOffset start, TimeSpan length)
        => new() { Id = id, Subject = id, Start = start, End = start + length };
}
