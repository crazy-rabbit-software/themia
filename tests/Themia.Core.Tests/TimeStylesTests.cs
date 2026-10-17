using Themia.Core.Astronomy;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6030]..[S-6061] The styles of the countdown, the pomodoro, the sun and the moon, and the
/// shared ring and dot-grid rules they draw with (year progress's are its module's).
/// </summary>
public class TimeStylesTests
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    // ---- the styles themselves ---------------------------------------------------------------

    [Theory]
    [InlineData("countdown", "text,ring,flip")]
    [InlineData("pomodoro", "ring,bar,digits")]
    [InlineData("sun", "arc,dayBar,times")]
    [InlineData("moon", "disc,phaseMonth")]
    [InlineData("clock", "digital,analog,stacked,flip,word,minimal,date")]
    public void S6030_each_kind_offers_its_styles_in_order_the_old_look_first(string kindId, string names)
    {
        var kind = new WidgetId(kindId);
        Assert.Equal(names, string.Join(",", WidgetStyles.For(kind).Select(s => s.Name)));
    }

    [Fact]
    public void S6035_the_pomodoro_ring_style_alone_draws_a_ring_and_digits_draws_neither()
    {
        Assert.True(new PomodoroConfig().DrawsRing);
        Assert.True(new PomodoroConfig().DrawsBar);
        Assert.False(new PomodoroConfig { Style = PomodoroStyle.Bar }.DrawsRing);
        Assert.True(new PomodoroConfig { Style = PomodoroStyle.Bar }.DrawsBar);
        Assert.False(new PomodoroConfig { Style = PomodoroStyle.Digits }.DrawsRing);
        Assert.False(new PomodoroConfig { Style = PomodoroStyle.Digits }.DrawsBar);
    }

    // ---- countdown ----------------------------------------------------------------------------

    [Fact]
    public void S6031_the_ring_is_the_share_still_to_go_and_unknown_without_a_creation_time()
    {
        var created = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var trip = new CountdownConfig { Label = "Trip" };
        trip.Retarget(created.AddDays(10), created);

        Assert.Equal(1, CountdownLayout.Left(trip, created));
        Assert.Equal(0.75, CountdownLayout.Left(trip, created.AddDays(2.5))!.Value, 6);
        Assert.Equal(0, CountdownLayout.Left(trip, created.AddDays(12)));
        Assert.Null(CountdownLayout.Left(new CountdownConfig { Target = created.AddDays(10) }, created));
        Assert.Null(CountdownLayout.Left(new CountdownConfig(), created));
    }

    [Fact]
    public void S6032_flip_cards_are_days_while_there_are_any_then_two_figure_hours_and_minutes()
    {
        var now = new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

        var far = CountdownLayout.Cards(now.AddDays(12).AddHours(3).AddMinutes(7), now, seconds: false);
        Assert.Equal(["12", "03", "07"], far.Select(c => c.Digits));
        Assert.Equal([CountdownUnit.Days, CountdownUnit.Hours, CountdownUnit.Minutes], far.Select(c => c.Unit));

        var near = CountdownLayout.Cards(now.AddHours(5).AddMinutes(4).AddSeconds(9), now, seconds: true);
        Assert.Equal(["05", "04", "09"], near.Select(c => c.Digits));
        Assert.Equal(CountdownUnit.Seconds, near[^1].Unit);

        // Gone by: it counts up, as the text style's "… ago" does.
        var past = CountdownLayout.Cards(now.AddMinutes(-90), now, seconds: false);
        Assert.Equal(["01", "30"], past.Select(c => c.Digits));

        Assert.Equal("365", CountdownLayout.Cards(now.AddDays(365), now, false)[0].Digits);
    }

    // ---- the shared ring and dot grid ----------------------------------------------------------

    [Fact]
    public void S6060_a_ring_runs_clockwise_from_twelve_and_is_empty_or_whole_at_its_ends()
    {
        var quarter = RingArc.Of(100, 10, 0.25);
        Assert.Equal((50, 45), (quarter.Centre, quarter.Radius));
        Assert.Equal((50d, 5d), (quarter.StartX, quarter.StartY));
        Assert.Equal(95, quarter.EndX, 6);
        Assert.Equal(50, quarter.EndY, 6);
        Assert.False(quarter.IsLarge);

        Assert.True(RingArc.Of(100, 10, 0.75).IsLarge);
        Assert.True(RingArc.Of(100, 10, 0).Empty);
        Assert.True(RingArc.Of(100, 10, double.NaN).Empty);
        Assert.True(RingArc.Of(100, 10, 1.4).Full);
        Assert.False(RingArc.Of(100, 10, 0.5).Full);
        Assert.Equal(1, RingArc.Of(4, 10, 0.5).Radius);
        Assert.Equal(3, RingArc.Stroke(20));
        Assert.Equal(12, RingArc.Stroke(1000));
    }

    [Fact]
    public void S6061_the_dot_grid_reflows_to_the_box_and_every_dot_fits()
    {
        var wide = DotGrid.Fit(365, 448, 128);
        var tall = DotGrid.Fit(365, 168, 368);
        Assert.True(wide.Columns > wide.Rows);
        Assert.True(tall.Rows > tall.Columns);

        foreach (var (plan, w, h) in new[] { (wide, 448d, 128d), (tall, 168d, 368d) })
        {
            Assert.True(plan.Columns * plan.Rows >= 365);
            Assert.True(plan.Width <= w + 1e-6 && plan.Height <= h + 1e-6);
            Assert.True(plan.Diameter < plan.Pitch);
        }

        var month = DotGrid.Fit(31, 1000, 100, maxColumns: 7);
        Assert.Equal(7, month.Columns);
        Assert.Equal((2, 4), month.Cell(31 - 1));
        Assert.Equal(0, DotGrid.Fit(0, 100, 100).Columns);
        Assert.Equal(0, DotGrid.Fit(10, 0, 100).Pitch);
    }

    // ---- sun ----------------------------------------------------------------------------------

    [Fact]
    public void S6046_the_day_bar_places_sunrise_sunset_and_now_by_their_share_of_the_local_day()
    {
        var now = new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.FromHours(2));
        var times = new SunTimes(new DateTimeOffset(2026, 6, 21, 6, 0, 0, TimeSpan.FromHours(2)), new DateTimeOffset(2026, 6, 21, 21, 0, 0, TimeSpan.FromHours(2)), PolarState.None);

        var bar = SunDayBar.Of(now, times, Zurich);
        Assert.Equal(0.25, bar.Rise!.Value, 6);
        Assert.Equal(21 / 24d, bar.Set!.Value, 6);
        Assert.Equal(0.5, bar.Now, 6);
        Assert.True(bar.NowLit);
        Assert.Equal((0.25, 21 / 24d), (Math.Round(bar.Daylight.From, 6), Math.Round(bar.Daylight.To, 6)));
    }

    [Fact]
    public void S6046_a_spring_forward_day_is_23_hours_across_and_the_polar_states_light_all_or_nothing()
    {
        // 29 March 2026: 02:00 is 03:00 in Zurich. 12:00 local is 11 real hours in, of 23.
        var noon = new DateTimeOffset(2026, 3, 29, 12, 0, 0, TimeSpan.FromHours(2));
        var none = new SunTimes(null, null, PolarState.None);
        Assert.Equal(11 / 23d, SunDayBar.Of(noon, none, Zurich).Now, 6);

        var day = SunDayBar.Of(noon, new SunTimes(null, null, PolarState.Day), Zurich);
        Assert.Equal((0d, 1d), day.Daylight);
        Assert.True(day.NowLit);

        var night = SunDayBar.Of(noon, new SunTimes(null, null, PolarState.Night), Zurich);
        Assert.Equal((0d, 0d), night.Daylight);
        Assert.False(night.NowLit);

        // A sunrise that is not on this day has no tick.
        Assert.Null(SunDayBar.Share(noon.AddDays(-2), noon.AddHours(-12), noon.AddHours(11)));
    }

    // ---- moon ---------------------------------------------------------------------------------

    [Fact]
    public void S6051_the_phase_month_is_every_day_of_this_month_today_marked_once()
    {
        var now = new DateTimeOffset(2026, 2, 10, 20, 0, 0, TimeSpan.FromHours(1));
        var days = MoonMonth.Days(now, Zurich);

        Assert.Equal(28, days.Count);
        Assert.Equal(new DateOnly(2026, 2, 1), days[0].Date);
        Assert.Equal(new DateOnly(2026, 2, 10), days.Single(d => d.Today).Date);

        // A day on, the moon is a 29.53rd of its cycle further, wrapping at the new moon.
        for (var i = 1; i < days.Count; i++)
        {
            var step = (days[i].Phase - days[i - 1].Phase + 1) % 1;
            Assert.Equal(1 / 29.53058867, step, 3);
        }

        Assert.Equal(MoonPhase.At(new DateTimeOffset(2026, 2, 10, 12, 0, 0, TimeSpan.FromHours(1))).Phase, days[9].Phase, 9);
    }
}
