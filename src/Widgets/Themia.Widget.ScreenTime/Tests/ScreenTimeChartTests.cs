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
    public void S11505_S15772_the_calendar_is_its_letters_row_over_its_weeks_of_cells_from_the_top_edge()
    {
        Assert.Equal(1, ScreenTimeChartLayout.Weeks(ScreenTimePeriod.Week));
        Assert.Equal(5, ScreenTimeChartLayout.Weeks(ScreenTimePeriod.Month));
        Assert.Equal(1, ScreenTimeChartLayout.Weeks(ScreenTimePeriod.Day));

        // A cell is its ring and 8 in whole grid cells, the ring the box less its padding; the letters a Caption line in whole cells.
        var grid = ItemGrid.Default();
        Assert.Equal(30, ScreenTimeChartLayout.CellBox(grid));
        Assert.Equal(22, ScreenTimeChartLayout.Ring(grid));
        Assert.Equal(20, ScreenTimeChartLayout.HeadRow(grid));
        Assert.True(ScreenTimeChartLayout.Ring(grid) >= ScreenTimeChartLayout.Cell);

        // From the card's top edge to its foot: the letters' row over the weeks, touching, nothing round them.
        double Height(ScreenTimePeriod period) => Kind.Sizing(new ScreenTimeChartConfig { Style = ScreenTimeChartStyle.Calendar, Period = period }).Natural.Height;
        Assert.Equal(20 + 30, Height(ScreenTimePeriod.Week));
        Assert.Equal(20 + (5 * 30), Height(ScreenTimePeriod.Month));
    }

    [Fact]
    public void S15772_the_bars_stand_from_the_top_edge_with_no_day_line_over_a_window()
    {
        Assert.Equal(0, ScreenTimeChartLayout.Top(ScreenTimePeriod.Week));
        Assert.Equal(0, ScreenTimeChartLayout.Top(ScreenTimePeriod.Month));
    }

    [Fact]
    public void S15773_the_part_pointed_at_is_one_app_on_one_day_never_that_app_on_every_day()
    {
        var pointed = new ChartPart("code", 6);
        Assert.Equal(1, ScreenTimeChartLayout.PartStrength(0.8, pointed, pointed, null));

        // The same app on another day dims as every other part does (WR-13: never the items that share an app).
        Assert.Equal(0.8 * FamilyShape.LinkPointed, ScreenTimeChartLayout.PartStrength(0.8, new ChartPart("code", 5), pointed, null), 6);
        Assert.Equal(0.8 * FamilyShape.LinkPointed, ScreenTimeChartLayout.PartStrength(0.8, new ChartPart("edge", 6), pointed, null), 6);

        // Picked: the others at the picked strength; nothing pointed or picked: every part at its tone.
        Assert.Equal(0.8 * FamilyShape.LinkPicked, ScreenTimeChartLayout.PartStrength(0.8, new ChartPart("code", 5), null, pointed), 6);
        Assert.Equal(0.8, ScreenTimeChartLayout.PartStrength(0.8, new ChartPart("code", 5), null, null), 6);
    }
}
