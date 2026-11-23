using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>[S-16591] The forecast's days scrolled to by whole days: the steps, the wheel's notches and the limits.</summary>
public sealed class ForecastScrollTests
{
    // Fourteen rows of 30 in a card 140 tall: four whole, the fifth cut by the foot.
    private const int Days = 14;
    private const double Row = 30;
    private const double Viewport = 140;

    [Fact]
    public void S16591_the_last_step_stands_the_last_day_at_the_far_edge()
    {
        // 420 of days in 140: 280 over, ten whole days of it rounded up (the last turn ends on the last day).
        Assert.Equal(10, ForecastScroll.LastFirst(Days, Row, Viewport));
        Assert.Equal(300 - 20, ForecastScroll.Offset(10, Days, Row, Viewport));

        // Every day showing, or nothing measured: nothing to scroll.
        Assert.Equal(0, ForecastScroll.LastFirst(4, Row, Viewport));
        Assert.Equal(0, ForecastScroll.LastFirst(Days, Row, double.NaN));
        Assert.Equal(0, ForecastScroll.LastFirst(Days, 0, Viewport));
    }

    [Fact]
    public void S16591_offsets_are_whole_days_kept_in_the_limits()
    {
        Assert.Equal(0, ForecastScroll.Offset(0, Days, Row, Viewport));
        Assert.Equal(90, ForecastScroll.Offset(3, Days, Row, Viewport));
        Assert.Equal(280, ForecastScroll.Offset(13, Days, Row, Viewport));
        Assert.Equal(0, ForecastScroll.Offset(-2, Days, Row, Viewport));

        Assert.Equal(1, ForecastScroll.Step(0, 1, Days, Row, Viewport));
        Assert.Equal(0, ForecastScroll.Step(0, -1, Days, Row, Viewport));
        Assert.Equal(10, ForecastScroll.Step(9, 5, Days, Row, Viewport));
        Assert.Equal(4, ForecastScroll.Page(Row, Viewport));
    }

    [Fact]
    public void S16591_a_wheel_notch_is_a_day_and_a_touchpad_adds_up_to_one()
    {
        // A notch down (negative, as Windows reports it) is the next day; up the day before.
        Assert.Equal((1, 0), ForecastScroll.Wheel(0, 0, -120, Days, Row, Viewport));
        Assert.Equal((2, 0), ForecastScroll.Wheel(3, 0, 120, Days, Row, Viewport));
        Assert.Equal((3, 0), ForecastScroll.Wheel(0, 0, -360, Days, Row, Viewport));

        // A touchpad's small turns carry until they make a notch.
        Assert.Equal((0, -40), ForecastScroll.Wheel(0, 0, -40, Days, Row, Viewport));
        Assert.Equal((1, -20), ForecastScroll.Wheel(0, -40, -100, Days, Row, Viewport));

        // At a limit the rest is dropped, so a turn back moves at once.
        Assert.Equal((10, 0), ForecastScroll.Wheel(10, -80, -100, Days, Row, Viewport));
        Assert.Equal((0, 0), ForecastScroll.Wheel(0, 0, 60, Days, Row, Viewport));
    }
}
