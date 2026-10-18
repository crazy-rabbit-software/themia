using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.RainNextHour.Tests;

/// <summary>[S-5126] The rain widget's ladder. Moved with the widget from Core's OnlineWidgetsTests, unchanged in what it asserts.</summary>
public sealed class RainNextHourTests
{
    [Fact]
    public void S5126_the_rain_ladder_adds_the_caption_then_the_bars()
    {
        var floor = FluentType.Subtitle.LineHeight;

        var tiny = RainLayout.Rain(new WidgetArea(160, 60), floor, true);
        var small = RainLayout.Rain(new WidgetArea(200, 80), floor, true);
        var medium = RainLayout.Rain(new WidgetArea(320, 200), floor, true);

        Assert.Equal(0, tiny.ChartHeight);
        Assert.True(small.Caption);
        Assert.Equal(0, small.ChartHeight);
        Assert.True(medium.ChartHeight >= RainLayout.ChartMin + FluentType.Caption.LineHeight);
    }
}
