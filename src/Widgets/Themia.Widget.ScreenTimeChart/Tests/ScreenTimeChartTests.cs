using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.ScreenTimeChart.Tests;

/// <summary>[S-11505] [S-3331] The chart's sizes: headerless, 200..520 wide, one height per style and period.</summary>
public sealed class ScreenTimeChartTests
{
    private static WidgetDescriptor Kind => new ScreenTimeChartModule().Descriptor;

    [Fact]
    public void S11505_the_bars_are_one_height_in_the_inset_their_end_labels_under_them()
    {
        var sizing = Kind.Sizing(new ScreenTimeChartConfig());
        var height = FamilySpace.Inset + ScreenTimeChartLayout.BarHeight + FamilySpace.S1 + FamilyType.Caption.LineHeight + FamilySpace.Inset;
        Assert.Equal(new SizeDip(300, height), sizing.Natural);
        Assert.Equal((200, 520), (sizing.Min.Width, sizing.Max.Width));
        Assert.True(sizing.FixedHeight);
        Assert.Null(Kind.SizeSets);
        Assert.True(Kind.Traits.HasFlag(WidgetTraits.StartsHeaderless));

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
        var top = FamilySpace.Inset + FamilyType.Caption.LineHeight + FamilySpace.S1;
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
}
