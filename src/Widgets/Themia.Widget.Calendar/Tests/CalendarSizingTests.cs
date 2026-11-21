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
    public void S11540_S14500_the_agenda_is_six_rows_of_its_lines_new_and_every_cell_from_one_row_up()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Agenda };
        Assert.Equal(new SizeDip(300, 6 * 30), Of(config).Natural);
        Assert.Equal((260d, 30d), (Of(config).Min.Width, Of(config).Min.Height));
        Assert.Equal(4 * 40, Of(new CalendarWidgetConfig { Style = CalendarStyle.Agenda, Rows = 4, Lines = 2 }).Natural.Height);
        Assert.Equal(277, Of(config).FitHeight(277));
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
    public void S11543_S14502_a_day_is_its_band_line_over_its_hours_and_its_hours_what_the_card_holds()
    {
        // A new one's eight hours at their optimal (30); [S-14810] the hours shown after are every one its timeline reaches,
        // the last cut by the card's foot, 4..16.
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Day };
        Assert.Equal(CalendarSizing.ContentTop + CalendarFit.DayBar + FamilySpace.S1 + (8 * 30) + FamilySpace.Inset, Of(config).Natural.Height);
        var grid = Themia.Core.Widgets.ItemGrid.Default();
        Assert.Equal((4, 8, 9, 16), (CalendarSizing.HoursIn(10, grid), CalendarSizing.HoursIn(240, grid), CalendarSizing.HoursIn(269, grid), CalendarSizing.HoursIn(2000, grid)));
        Assert.Equal(new DayBandFit(0, 0, false), CalendarSizing.DayBand(0));
        Assert.Equal(new DayBandFit(1, CalendarFit.DayBar, true), CalendarSizing.DayBand(3));
    }

    [Fact]
    public void S11543_the_hours_are_the_counts_around_the_focus_or_the_wanted_day_grown_evenly()
    {
        // [S-14810] Every hour at its one fixed size, whatever the timeline's height.
        var focused = CalendarFit.HoursOf(4, 30, earliestHour: 9, latestHour: 17, focusHour: 14);
        Assert.Equal((13, 4, 30.0), (focused.First, focused.Count, focused.HourHeight));

        var whole = CalendarFit.HoursOf(16, 30, 9, 17, 14);
        Assert.Equal((6, 16), (whole.First, whole.Count));
        Assert.True(whole.End <= 24);
    }

    [Fact]
    public void S11544_S14500_a_month_of_dots_is_six_weeks_of_28_at_least_and_every_cell_taller_and_wider()
    {
        // [S-14903] The inset and the weekday line (30, no period's line), six weeks of three cells; every cell from there.
        var sizing = Of(new CalendarWidgetConfig());
        Assert.Equal(sizing.Min.Height, sizing.Natural.Height);
        Assert.Equal(30 + (6 * 30), sizing.Natural.Height);
        Assert.Equal((260d, 300d), (sizing.Min.Width, sizing.Natural.Width));
        Assert.Equal(253, sizing.FitHeight(253));
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
        // [S-14500] Seven columns from 56, a new one 100 each, edge to edge, every cell between.
        Assert.Equal((7 * 56d, 7 * 100d), (sizing.Min.Width, sizing.Natural.Width));
        Assert.True(sizing.Max.Width >= 7 * 180);

        // The inset with the weekday line, six weeks of a number line over three chip lines, whole cells each.
        var cell = FamilySpace.S1 + FamilyType.Caption.LineHeight + (3 * CalendarFit.ChipLine);
        Assert.Equal(30 + (6 * (20 + (3 * 20))), sizing.Natural.Height);
        Assert.Equal(cell, CalendarSizing.MonthCell(MonthEvents.Chips, MonthPlan.DefaultLines));
        Assert.True(sizing.Max.Height > sizing.Natural.Height && sizing.Natural.Height > sizing.Min.Height);

        // A new one's column leads its chips with their start time.
        var fit = CalendarSizing.Month(MonthEvents.Chips, 7 * 100, MonthPlan.DefaultLines);
        Assert.Equal((MonthDensity.Chips, 3, true), (fit.Density, fit.ChipLines, fit.Times));
        Assert.False(CalendarSizing.Month(MonthEvents.Chips, 7 * 56, 3).Times);
    }

    [Fact]
    public void S11547_S14502_a_month_with_titles_lists_the_chip_lines_its_weeks_hold()
    {
        // [S-14502] No stored count of lines: a week as tall as a number line and five chip lines lists five.
        var grid = Themia.Core.Widgets.ItemGrid.Default();
        var top = FamilySpace.Inset;
        var week = CalendarSizing.MonthNumberLine + (5 * CalendarFit.ChipLine);
        var month = MonthLayouts.Plan(724, top + 20 + (6 * week), top, grid, anchorRow: 2, mostItems: 9, MonthChipRoom.Titles);
        Assert.Equal((MonthFace.Chips, 5), (month.Face, month.ChipLanes));
    }

    [Fact]
    public void S11545_S14410_the_day_strip_is_a_40_column_a_day_over_the_chosen_days_rows()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Week };
        Assert.Equal(CalendarSizing.ContentTop + CalendarSizing.Strip + FamilySpace.S2 + (3 * 30) + FamilySpace.Inset, Of(config).Natural.Height);

        // [S-14410] [S-14502] The days are the card's width: a 40 column a day, 1..14, a new one's seven, never stored.
        Assert.Equal((2 * FamilySpace.Inset) + (7 * CalendarSizing.StripColumn), Of(config).Natural.Width);
        Assert.Equal((2 * FamilySpace.Inset) + CalendarSizing.StripColumn, Of(config).Min.Width);
        Assert.Equal(5, CalendarSizing.WeekDaysIn((2 * FamilySpace.Inset) + (5 * CalendarSizing.StripColumn) + 30, titles: false));
        Assert.Equal(14, CalendarSizing.WeekDaysIn(5000, titles: false));
        Assert.Equal(1, CalendarSizing.WeekDaysIn(10, titles: false));
    }

    [Fact]
    public void S11546_S14410_titles_in_the_week_are_96_a_column_and_the_days_and_hours_follow_the_card()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Week, WeekTitles = true };
        var sizing = Of(config);
        Assert.Equal((2 * FamilySpace.Inset) + CalendarSizing.HourLabels + (7 * 96), sizing.Natural.Width);
        Assert.Equal(CalendarSizing.ContentTop + CalendarSizing.WeekHead + CalendarFit.ChipLine + (8 * 30) + FamilySpace.Inset, sizing.Natural.Height);
        Assert.Equal(3, CalendarSizing.WeekDaysIn((2 * FamilySpace.Inset) + CalendarSizing.HourLabels + (3 * 96) + 50, titles: true));
    }

    [Theory]
    [InlineData(CalendarStyle.Month)]
    [InlineData(CalendarStyle.Week)]
    [InlineData(CalendarStyle.Day)]
    [InlineData(CalendarStyle.Year)]
    [InlineData(CalendarStyle.Agenda)]
    public void S14903_no_style_counts_a_periods_line_over_its_content(CalendarStyle style)
    {
        // Every style starts at the card's inset: the period's line is the calendar header's own widget.
        Assert.Equal(FamilySpace.Inset, CalendarSizing.ContentTop);
        var sizing = Of(new CalendarWidgetConfig { Style = style });
        var expected = style switch
        {
            CalendarStyle.Month => CalendarSizing.MonthOver + (6 * 30),
            CalendarStyle.Week => FamilySpace.Inset + CalendarSizing.Strip + FamilySpace.S2 + (3 * 30) + FamilySpace.Inset,
            CalendarStyle.Day => FamilySpace.Inset + CalendarFit.DayBar + FamilySpace.S1 + (8 * 30) + FamilySpace.Inset,
            CalendarStyle.Year => FamilySpace.Inset + CalendarSizing.YearBlock(CalendarSizing.Year(false)).Height + FamilySpace.Inset,
            _ => 6 * 30d,
        };
        Assert.Equal(expected, sizing.Natural.Height);
    }

    [Fact]
    public void S14901_the_header_is_one_line_of_whole_cells_at_every_width()
    {
        var header = new CalendarHeaderModule().Descriptor;
        Assert.Equal(new WidgetId("calendarHeader"), header.Id);
        var sizing = header.Sizing(new CalendarHeaderConfig());

        // The card's inset over the tool line and its gap, whole cells: 40 at the least and a new one; the title beside its
        // two arrows at 200 at the least, a new one as wide as a new month. Relaxed, [S-12411]: larger is the user's.
        Assert.Equal((40d, 40d), (sizing.Min.Height, sizing.Natural.Height));
        Assert.Equal((200d, 300d), (sizing.Min.Width, sizing.Natural.Width));
        Assert.Equal(new SizeDip(SizeStretch.Ceiling, SizeStretch.Ceiling), sizing.Max);
        Assert.Equal(LineRules.BandOver, FamilySpace.Inset + FamilySpace.HeaderLine + FamilySpace.HeaderGap);
    }

    [Fact]
    public void S14902_the_header_pages_by_its_style_and_a_copy_keeps_it()
    {
        Assert.Equal(CalendarStyle.Month, new CalendarHeaderConfig().Unit);
        Assert.Equal(
            [CalendarStyle.Month, CalendarStyle.Week, CalendarStyle.Day, CalendarStyle.Year],
            Enum.GetValues<CalendarHeaderStyle>().Select(style => new CalendarHeaderConfig { Style = style }.Unit));
        Assert.Equal(CalendarHeaderStyle.Year, ((CalendarHeaderConfig)new CalendarHeaderConfig { Style = CalendarHeaderStyle.Year }.Clone()).Style);
        Assert.Equal(["month", "week", "day", "year"], WidgetStyles.For(new WidgetId("calendarHeader")).Select(s => s.Name));
    }

    [Fact]
    public void S11540_settings_out_of_range_are_repaired_and_a_copy_keeps_them()
    {
        var config = new CalendarWidgetConfig { Rows = 40, Lines = 3, WeekRows = 99 };
        Assert.NotEmpty(config.Repair());
        Assert.Equal((20, 2, 12), (config.Rows, config.Lines, config.WeekRows));

        var copy = (CalendarWidgetConfig)new CalendarWidgetConfig { WeekTitles = true, MonthEvents = MonthEvents.Chips, WeekRows = 5 }.Clone();
        Assert.Equal((true, MonthEvents.Chips, 5), (copy.WeekTitles, copy.MonthEvents, copy.WeekRows));
    }
}
