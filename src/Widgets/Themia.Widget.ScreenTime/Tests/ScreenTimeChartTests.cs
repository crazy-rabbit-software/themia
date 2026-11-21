using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.ScreenTime.Tests;

/// <summary>[S-11505] [S-3331] The chart's sizes: headerless, 200..520 wide, one height per style and period.</summary>
public sealed class ScreenTimeChartTests
{
    private static WidgetDescriptor Kind => new ScreenTimeChartModule().Descriptor;

    [Fact]
    public void S11505_the_bars_are_one_height_in_the_inset_their_end_labels_under_them()
    {
        var sizing = Kind.Sizing(new ScreenTimeChartConfig());
        var height = FamilySpace.Inset + ScreenTimeChartLayout.DayLine(ScreenTimePeriod.Week) + ScreenTimeChartLayout.BarHeight + FamilySpace.S1 + FamilyType.Caption.LineHeight + FamilySpace.Inset;
        Assert.Equal(new SizeDip(300, height), sizing.Natural);
        Assert.Equal((200d, Themia.Sdk.SizeStretch.Ceiling), (sizing.Min.Width, sizing.Max.Width)); // relaxed, [S-12411]: no practical Max
        Assert.Equal(sizing.Min.Height, sizing.Natural.Height);
        Assert.Equal(SizeStretch.Ceiling, sizing.Max.Height);

        // A month's bars are as tall as a week's.
        Assert.Equal(sizing, Kind.Sizing(new ScreenTimeChartConfig { Period = ScreenTimePeriod.Month }));
    }

    [Fact]
    public void S11505_the_calendar_is_its_letters_over_its_weeks_of_rings()
    {
        Assert.Equal(1, ScreenTimeChartLayout.Weeks(ScreenTimePeriod.Week));
        Assert.Equal(5, ScreenTimeChartLayout.Weeks(ScreenTimePeriod.Month));
        Assert.Equal(1, ScreenTimeChartLayout.Weeks(ScreenTimePeriod.Day));
        Assert.True(ScreenTimeChartLayout.Cell >= FamilyShape.Icon);

        double Height(ScreenTimePeriod period) => Kind.Sizing(new ScreenTimeChartConfig { Style = ScreenTimeChartStyle.Calendar, Period = period }).Natural.Height;
        var top = FamilySpace.Inset + ScreenTimeChartLayout.DayLine(ScreenTimePeriod.Week) + FamilyType.Caption.LineHeight + FamilySpace.S1;
        Assert.Equal(top + ScreenTimeChartLayout.Cell + FamilySpace.Inset, Height(ScreenTimePeriod.Week));
        Assert.Equal(top + (5 * ScreenTimeChartLayout.Cell) + (4 * FamilySpace.S1) + FamilySpace.Inset, Height(ScreenTimePeriod.Month));

        // Seven rings 4 apart fit the least width inside the inset.
        Assert.True((7 * ScreenTimeChartLayout.Cell) + (6 * FamilySpace.S1) <= ScreenTimeChartLayout.Narrowest - (2 * FamilySpace.Inset));
    }

    [Fact]
    public void S3331_bars_stand_8_apart_for_a_week_and_4_for_a_month()
    {
        Assert.Equal(FamilySpace.S2, ScreenTimeChartLayout.BarGap(7));
        Assert.Equal(FamilySpace.S1, ScreenTimeChartLayout.BarGap(30));
    }

    [Fact]
    public void S11505_S12808_the_calendars_rings_grow_as_one_block_until_the_height_or_the_width_stops_them()
    {
        // At its natural box the rings are their 20; a taller and wider box grows them until the height stops them, a
        // wide and short one until the height does, a narrow and tall one until a ring is a column wide (whole DIPs).
        var natural = ScreenTimeChartLayout.CalendarHeight(ScreenTimePeriod.Week, ScreenTimeChartLayout.Cell);
        Assert.Equal(ScreenTimeChartLayout.Cell, ScreenTimeChartLayout.CalendarCell(ScreenTimePeriod.Week, 276, natural));
        Assert.Equal(80, ScreenTimeChartLayout.CalendarCell(ScreenTimePeriod.Week, 876, 80 + ScreenTimeChartLayout.CalendarHeader));
        Assert.Equal(36, ScreenTimeChartLayout.CalendarCell(ScreenTimePeriod.Week, 276, 500));
        Assert.Equal(ScreenTimeChartLayout.Cell, ScreenTimeChartLayout.CalendarCell(ScreenTimePeriod.Week, 50, 10));
        Assert.Equal(ScreenTimeChartLayout.Cell, ScreenTimeChartLayout.CalendarCell(ScreenTimePeriod.Week, double.NaN, 100));

        // A month's five weeks share the height, 4 apart.
        var month = ScreenTimeChartLayout.CalendarCell(ScreenTimePeriod.Month, 876, 300);
        Assert.Equal(Math.Floor((300 - ScreenTimeChartLayout.CalendarHeader - (4 * FamilySpace.S1)) / 5), month);
        Assert.True(ScreenTimeChartLayout.CalendarHeight(ScreenTimePeriod.Month, month) <= 300);
    }
}
