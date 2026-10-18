using Themia.Core.Time;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1825] The shared ticker's schedule (the SDK's <see cref="TickSchedule"/>). Split out of the clock's tests when the widget moved to its module.</summary>
public class TickScheduleTests
{
    [Fact]
    public void A_delay_always_lands_after_the_boundary_never_before()
    {
        // Sweep a whole second in 1 ms steps: every wake must land past the next boundary, or the
        // formatter renders the previous second.
        for (var ms = 0; ms < 1000; ms++)
        {
            var now = new DateTimeOffset(2026, 1, 1, 10, 30, 15, ms, TimeSpan.Zero);
            var delay = TickSchedule.DelayToNextBoundary(now, TimeSpan.FromSeconds(1));
            var wake = now + delay;

            Assert.True(wake.Millisecond >= 0 && wake.Second != now.Second, $"wake at {ms}ms landed in the same second");
            Assert.True(delay > TimeSpan.Zero);
        }
    }

    [Fact]
    public void A_delay_never_exceeds_one_period_plus_the_bias()
    {
        var interval = TimeSpan.FromSeconds(1);

        for (var ms = 0; ms < 1000; ms++)
        {
            var now = new DateTimeOffset(2026, 1, 1, 10, 30, 15, ms, TimeSpan.Zero);
            var delay = TickSchedule.DelayToNextBoundary(now, interval);

            Assert.True(delay <= interval + interval + TickSchedule.Bias, $"delay {delay} at {ms}ms is too long");
        }
    }

    [Fact]
    public void A_minute_subscriber_fires_once_per_minute_not_once_per_second()
    {
        var minute = TimeSpan.FromMinutes(1);
        var start = new DateTimeOffset(2026, 1, 1, 10, 30, 0, TimeSpan.Zero);

        var indices = Enumerable.Range(0, 60)
            .Select(s => TickSchedule.PeriodIndex(start.AddSeconds(s), minute))
            .Distinct()
            .Count();

        Assert.Equal(1, indices);
        Assert.NotEqual(
            TickSchedule.PeriodIndex(start, minute),
            TickSchedule.PeriodIndex(start.AddMinutes(1), minute));
    }

    [Fact]
    public void A_degenerate_interval_does_not_produce_a_zero_or_negative_delay()
    {
        var delay = TickSchedule.DelayToNextBoundary(DateTimeOffset.Now, TimeSpan.Zero);

        Assert.True(delay > TimeSpan.Zero);
    }

    [Theory]
    [InlineData(0.2, 1)]
    [InlineData(1, 1)]
    [InlineData(2.4, 2)]
    [InlineData(5, 5)]
    [InlineData(30, 30)]
    [InlineData(59, 59)]
    [InlineData(60, 60)]
    [InlineData(3600, 60)]
    public void S1825_Normalise_keeps_whole_seconds_under_a_minute_and_caps_at_a_minute(double requestedSeconds, int expectedSeconds)
    {
        // Regression: every request under a minute collapsed to one second, so a disk meter set to
        // read every 5 s read every second and its 3-minute chart spanned 36 s (S-1825).
        var normalised = TickSchedule.Normalise(TimeSpan.FromSeconds(requestedSeconds));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), normalised);
    }

    [Fact]
    public void S1825_a_five_second_subscriber_fires_once_per_five_second_period()
    {
        var five = TickSchedule.Normalise(TimeSpan.FromSeconds(5));
        var start = new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(TickSchedule.PeriodIndex(start, five), TickSchedule.PeriodIndex(start.AddSeconds(4.9), five));
        Assert.NotEqual(TickSchedule.PeriodIndex(start, five), TickSchedule.PeriodIndex(start.AddSeconds(5), five));
    }
}
