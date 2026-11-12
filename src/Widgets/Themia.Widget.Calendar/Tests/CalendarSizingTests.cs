using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Calendar.Tests;

/// <summary>[S-11540]..[S-11546] Each calendar style's size follows its own settings, never the box.</summary>
public sealed class CalendarSizingTests
{
    private static WidgetDescriptor Kind => new CalendarModule().Descriptor;

    private static WidgetSizing Of(CalendarWidgetConfig config) => Kind.Sizing(config);

    [Fact]
    public void S11540_the_agenda_is_six_rows_of_its_lines_and_a_drag_sets_rows()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Agenda };
        Assert.Equal(new SizeDip(300, FamilySpace.ContentTop + (6 * FamilyShape.Row1) + FamilySpace.Inset), Of(config).Natural);
        Assert.Equal(260, Of(config).Min.Width);
        Assert.Equal(FamilySpace.ContentTop + (4 * FamilyShape.Row2) + FamilySpace.Inset, Of(new CalendarWidgetConfig { Style = CalendarStyle.Agenda, Rows = 4, Lines = 2 }).Natural.Height);

        var set = Assert.IsType<CalendarWidgetConfig>(Kind.SizeSets!(config, new SizeDip(300, FamilySpace.ContentTop + (9 * FamilyShape.Row1) + FamilySpace.Inset)));
        Assert.Equal(9, set.Rows);
        Assert.Equal(6, config.Rows);
    }

    [Fact]
    public void S11540_the_agenda_plans_one_column_of_its_lines_at_every_width()
    {
        var choices = new KitChoices(KitShow.None) { Rows = CalendarSizing.AgendaShown(new CalendarWidgetConfig { Lines = 2 }) };
        var plan = KitLadder.Plan(new WidgetArea(900, 300), AgendaSpec.Spec, choices);
        Assert.Equal((1, 2), (plan.Columns, plan.Lines));
    }

    [Theory]
    [InlineData(ReadoutSize.Small, false)]
    [InlineData(ReadoutSize.Medium, true)]
    public void S11542_a_year_is_twelve_months_one_size_by_its_size(ReadoutSize size, bool numbers)
    {
        var sizing = Of(new CalendarWidgetConfig { Style = CalendarStyle.Year, YearSize = size });
        Assert.True(sizing.FixedWidth && sizing.FixedHeight);

        var fit = CalendarSizing.Year(size);
        Assert.Equal((12, 4, 3, numbers, numbers), (fit.Months, fit.Columns, fit.Rows, fit.Numbers, fit.Weekdays));
        Assert.Equal((2 * FamilySpace.Inset) + (4 * 7 * fit.DayCell) + (3 * CalendarSizing.YearGap), sizing.Natural.Width);
        Assert.True(Of(new CalendarWidgetConfig { Style = CalendarStyle.Year, YearSize = ReadoutSize.Medium }).Natural.Width > Of(new CalendarWidgetConfig { Style = CalendarStyle.Year }).Natural.Width);
    }

    [Fact]
    public void S11543_a_day_is_its_band_line_over_its_hours_and_a_drag_sets_hours()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Day };
        Assert.Equal(FamilySpace.ContentTop + CalendarFit.DayBar + FamilySpace.S1 + (8 * CalendarSizing.Hour) + FamilySpace.Inset, Of(config).Natural.Height);
        Assert.Equal(16, Assert.IsType<CalendarWidgetConfig>(Kind.SizeSets!(config, new SizeDip(300, 2000))).DayHours);
        Assert.Equal(new DayBandFit(0, 0, false), CalendarSizing.DayBand(0));
        Assert.Equal(new DayBandFit(1, CalendarFit.DayBar, true), CalendarSizing.DayBand(3));
    }

    [Fact]
    public void S11543_the_hours_are_the_counts_around_the_focus_or_the_wanted_day_grown_evenly()
    {
        var focused = CalendarFit.HoursOf(4, 112, earliestHour: 9, latestHour: 17, focusHour: 14);
        Assert.Equal((13, 4, 28.0), (focused.First, focused.Count, focused.HourHeight));

        var whole = CalendarFit.HoursOf(16, 448, 9, 17, 14);
        Assert.Equal((6, 16), (whole.First, whole.Count));
        Assert.True(whole.End <= 24);
    }

    [Theory]
    [InlineData(MonthEvents.Dots)]
    [InlineData(MonthEvents.Chips)]
    public void S11544_a_month_is_six_weeks_of_its_cells_one_height_260_to_420_wide(MonthEvents events)
    {
        var sizing = Of(new CalendarWidgetConfig { MonthEvents = events });
        Assert.True(sizing.FixedHeight);
        Assert.Equal((260, 300, 420), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
        Assert.Equal(FamilySpace.ContentTop + FamilyType.Caption.LineHeight + (6 * CalendarSizing.MonthCell(events)) + FamilySpace.Inset, sizing.Natural.Height);

        var fit = CalendarSizing.Month(events, 236);
        Assert.Equal((6, events == MonthEvents.Chips ? MonthDensity.Chips : MonthDensity.Dots), (fit.Rows, fit.Density));
        Assert.Equal(MonthEvents.Dots, new CalendarWidgetConfig().MonthEvents);
    }

    [Fact]
    public void S11545_the_day_strip_is_its_strip_over_the_chosen_days_rows()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Week };
        Assert.Equal(FamilySpace.ContentTop + CalendarSizing.Strip + FamilySpace.S2 + (3 * FamilyShape.Row1) + FamilySpace.Inset, Of(config).Natural.Height);
        Assert.Equal((240, 520), (Of(config).Min.Width, Of(config).Max.Width));
        Assert.Equal(5, Assert.IsType<CalendarWidgetConfig>(Kind.SizeSets!(config, new SizeDip(300, FamilySpace.ContentTop + CalendarSizing.Strip + FamilySpace.S2 + (5 * FamilyShape.Row1) + FamilySpace.Inset))).WeekRows);
    }

    [Fact]
    public void S11546_titles_in_the_week_need_96_a_column_and_the_height_follows_the_hours()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Week, WeekTitles = true, WeekDays = 5, WeekHours = 10 };
        var sizing = Of(config);
        Assert.Equal((2 * FamilySpace.Inset) + CalendarSizing.HourLabels + (5 * 96), sizing.Min.Width);
        Assert.Equal(FamilySpace.ContentTop + CalendarSizing.WeekHead + CalendarFit.ChipLine + (10 * CalendarSizing.Hour) + FamilySpace.Inset, sizing.Natural.Height);
        Assert.Equal(6, Assert.IsType<CalendarWidgetConfig>(Kind.SizeSets!(config, new SizeDip(600, FamilySpace.ContentTop + CalendarSizing.WeekHead + CalendarFit.ChipLine + (6 * CalendarSizing.Hour) + FamilySpace.Inset))).WeekHours);
    }

    [Fact]
    public void S11540_settings_out_of_range_are_repaired_and_a_copy_keeps_them()
    {
        var config = new CalendarWidgetConfig { Rows = 40, Lines = 3, DayHours = 30, WeekHours = 1, WeekRows = 99, YearSize = ReadoutSize.Large };
        Assert.NotEmpty(config.Repair());
        Assert.Equal((20, 2, 16, 4, 12, ReadoutSize.Small), (config.Rows, config.Lines, config.DayHours, config.WeekHours, config.WeekRows, config.YearSize));

        var copy = (CalendarWidgetConfig)new CalendarWidgetConfig { WeekTitles = true, MonthEvents = MonthEvents.Chips, DayHours = 12 }.Clone();
        Assert.Equal((true, MonthEvents.Chips, 12), (copy.WeekTitles, copy.MonthEvents, copy.DayHours));
    }
}
