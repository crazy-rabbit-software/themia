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
        Assert.Equal(new SizeDip(300, 6 * 30), Of(config).Natural);
        Assert.Equal(260, Of(config).Min.Width);
        Assert.Equal(4 * 40, Of(new CalendarWidgetConfig { Style = CalendarStyle.Agenda, Rows = 4, Lines = 2 }).Natural.Height);

        var set = Assert.IsType<CalendarWidgetConfig>(Kind.SizeSets!(config, new SizeDip(300, 9 * 30)));
        Assert.Equal(9, set.Rows);
        Assert.Equal(6, config.Rows);
    }

    [Fact]
    public void S11540_the_agenda_plans_one_column_of_its_lines_at_every_width()
    {
        var choices = new KitChoices(KitShow.None) { Rows = CalendarSizing.AgendaShown(new CalendarWidgetConfig { Lines = 2 }) };
        var plan = KitPlanner.Plan(new WidgetArea(900, 300), AgendaSpec.Spec, choices);
        Assert.Equal((1, 2), (plan.Columns, plan.Lines));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S11542_a_year_is_twelve_months_at_their_own_size_and_a_larger_card_draws_them_larger(bool numbers)
    {
        var sizing = Of(new CalendarWidgetConfig { Style = CalendarStyle.Year, YearNumbers = numbers });
        Assert.Equal(sizing.Min, sizing.Natural);

        // Relaxed, [S-12411]: the card's size is the user's ([S-2449] retired).
        Assert.Equal(new SizeDip(SizeStretch.Ceiling, SizeStretch.Ceiling), sizing.Max);

        var fit = CalendarSizing.Year(numbers);
        Assert.Equal((12, 4, 3, numbers, numbers), (fit.Months, fit.Columns, fit.Rows, fit.Numbers, fit.Weekdays));
        Assert.Equal((2 * FamilySpace.Inset) + (4 * 7 * fit.DayCell) + (3 * CalendarSizing.YearGap), sizing.Natural.Width);
        Assert.True(Of(new CalendarWidgetConfig { Style = CalendarStyle.Year, YearNumbers = true }).Natural.Width > Of(new CalendarWidgetConfig { Style = CalendarStyle.Year }).Natural.Width);

        // The months at their own size in a room their size; twice as large in a room twice as wide and tall; never smaller.
        var block = CalendarSizing.YearBlock(fit);
        Assert.Equal(1, CalendarSizing.YearScale(fit, block.Width, block.Height));
        Assert.Equal(2, CalendarSizing.YearScale(fit, 2 * block.Width, 3 * block.Height));
        Assert.Equal(1, CalendarSizing.YearScale(fit, block.Width / 2, block.Height));
    }

    [Fact]
    public void S11543_a_day_is_its_band_line_over_its_hours_and_a_drag_sets_hours()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Day };
        Assert.Equal(CalendarSizing.ContentTop + CalendarFit.DayBar + FamilySpace.S1 + (8 * CalendarSizing.Hour) + FamilySpace.Inset, Of(config).Natural.Height);
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

    [Fact]
    public void S11544_S14101_a_month_of_dots_is_six_weeks_of_28_at_least_growing_a_cell_a_week_and_a_column()
    {
        // The period's line with the inset (40), the weekday line (20), six weeks of three cells; a step a cell each.
        var sizing = Of(new CalendarWidgetConfig());
        Assert.Equal(sizing.Min.Height, sizing.Natural.Height);
        Assert.Equal(40 + 20 + (6 * 30), sizing.Natural.Height);
        Assert.Equal((60d, 70d), (sizing.HeightSteps!.Step, sizing.WidthSteps!.Step));
        Assert.Equal((280d, 280d), (sizing.Min.Width, sizing.Natural.Width));
        Assert.True(sizing.Max.Height > 1000 && sizing.Max.Width > 1000);
        Assert.Empty(sizing.Problems());

        var fit = CalendarSizing.Month(MonthEvents.Dots, 236);
        Assert.Equal((6, MonthDensity.Dots, 0), (fit.Rows, fit.Density, fit.ChipLines));
        Assert.Equal(MonthEvents.Dots, new CalendarWidgetConfig().MonthEvents);
    }

    [Fact]
    public void S11547_a_month_with_titles_is_seven_columns_of_56_to_180_its_height_its_lines()
    {
        var config = new CalendarWidgetConfig { MonthEvents = MonthEvents.Chips };
        var sizing = Of(config);
        // [S-14101] Seven columns of whole cells from 56 (six cells), a cell a column a step, edge to edge.
        Assert.Equal((7 * 60d, 7 * 100d), (sizing.Min.Width, sizing.Natural.Width));
        Assert.True(sizing.Max.Width >= 7 * 180);

        // The period's line with the inset, the weekday line, six weeks of a number line over three chip lines, whole cells each.
        var cell = FamilySpace.S1 + FamilyType.Caption.LineHeight + (3 * CalendarFit.ChipLine);
        Assert.Equal(40 + 20 + (6 * (20 + (3 * 20))), sizing.Natural.Height);
        Assert.Equal(cell, CalendarSizing.MonthCell(MonthEvents.Chips, config.MonthLines));
        Assert.True(sizing.Max.Height > sizing.Natural.Height && sizing.Natural.Height > sizing.Min.Height);

        // A new one's column leads its chips with their start time.
        var fit = CalendarSizing.Month(MonthEvents.Chips, 7 * 100, config.MonthLines);
        Assert.Equal((MonthDensity.Chips, 3, true), (fit.Density, fit.ChipLines, fit.Times));
        Assert.False(CalendarSizing.Month(MonthEvents.Chips, 7 * 56, 3).Times);
    }

    [Fact]
    public void S11547_dragging_a_month_with_titles_sets_its_lines()
    {
        var config = new CalendarWidgetConfig { MonthEvents = MonthEvents.Chips };
        var steps = CalendarSizing.MonthLineSteps(config.MonthLines);
        Assert.Equal(6 * CalendarFit.ChipLine, steps.Step);

        var taller = Assert.IsType<CalendarWidgetConfig>(Kind.SizeSets!(config, new SizeDip(724, steps.At(5))));
        Assert.Equal(5, taller.MonthLines);
        Assert.Equal(3, config.MonthLines);
        Assert.Equal(6, Assert.IsType<CalendarWidgetConfig>(Kind.SizeSets!(config, new SizeDip(724, 5000))).MonthLines);
        Assert.Equal(1, Assert.IsType<CalendarWidgetConfig>(Kind.SizeSets!(config, new SizeDip(724, 10))).MonthLines);

        // Dots have one height: a drag changes nothing.
        var dots = new CalendarWidgetConfig();
        Assert.Same(dots, Kind.SizeSets!(dots, new SizeDip(300, 5000)));
    }

    [Fact]
    public void S11545_S14410_the_day_strip_is_a_40_column_a_day_over_the_chosen_days_rows()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Week };
        Assert.Equal(CalendarSizing.ContentTop + CalendarSizing.Strip + FamilySpace.S2 + (3 * FamilyShape.Row1) + FamilySpace.Inset, Of(config).Natural.Height);

        // [S-14410] The days are the card's width: a 40 column a day, 1..14, a new one's seven.
        Assert.Equal((2 * FamilySpace.Inset) + (7 * CalendarSizing.StripColumn), Of(config).Natural.Width);
        Assert.Equal(((2 * FamilySpace.Inset) + CalendarSizing.StripColumn, (2 * FamilySpace.Inset) + (14 * CalendarSizing.StripColumn)), (Of(config).Min.Width, Of(config).Max.Width));
        var set = Assert.IsType<CalendarWidgetConfig>(Kind.SizeSets!(config, new SizeDip((2 * FamilySpace.Inset) + (5 * CalendarSizing.StripColumn), CalendarSizing.ContentTop + CalendarSizing.Strip + FamilySpace.S2 + (5 * FamilyShape.Row1) + FamilySpace.Inset)));
        Assert.Equal((5, 5), (set.WeekRows, set.WeekDays));
        Assert.Equal(7, config.WeekDays);
    }

    [Fact]
    public void S11546_S14410_titles_in_the_week_are_96_a_column_and_the_days_and_hours_follow_the_card()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Week, WeekTitles = true, WeekDays = 5, WeekHours = 10 };
        var sizing = Of(config);
        Assert.Equal((2 * FamilySpace.Inset) + CalendarSizing.HourLabels + (5 * 96), sizing.Natural.Width);
        Assert.Equal(5, sizing.WidthSteps!.Count);
        Assert.Equal(CalendarSizing.ContentTop + CalendarSizing.WeekHead + CalendarFit.ChipLine + (10 * CalendarSizing.Hour) + FamilySpace.Inset, sizing.Natural.Height);
        var set = Assert.IsType<CalendarWidgetConfig>(Kind.SizeSets!(config, new SizeDip((2 * FamilySpace.Inset) + CalendarSizing.HourLabels + (3 * 96), CalendarSizing.ContentTop + CalendarSizing.WeekHead + CalendarFit.ChipLine + (6 * CalendarSizing.Hour) + FamilySpace.Inset)));
        Assert.Equal((6, 3), (set.WeekHours, set.WeekDays));
    }

    [Fact]
    public void S11540_settings_out_of_range_are_repaired_and_a_copy_keeps_them()
    {
        var config = new CalendarWidgetConfig { Rows = 40, Lines = 3, DayHours = 30, WeekHours = 1, WeekRows = 99, MonthLines = 0 };
        Assert.NotEmpty(config.Repair());
        Assert.Equal((20, 2, 16, 4, 12, 1), (config.Rows, config.Lines, config.DayHours, config.WeekHours, config.WeekRows, config.MonthLines));

        var copy = (CalendarWidgetConfig)new CalendarWidgetConfig { WeekTitles = true, MonthEvents = MonthEvents.Chips, DayHours = 12, MonthLines = 5 }.Clone();
        Assert.Equal((true, MonthEvents.Chips, 12, 5), (copy.WeekTitles, copy.MonthEvents, copy.DayHours, copy.MonthLines));
    }
}
