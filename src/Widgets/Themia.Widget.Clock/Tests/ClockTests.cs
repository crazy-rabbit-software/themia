using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Time;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Clock.Tests;

public class ClockFormatterTests
{
    private static DateTimeOffset At(int hour, int minute, int second = 0)
        => new(2026, 3, 9, hour, minute, second, TimeSpan.Zero);

    private static ClockConfig Utc(Action<ClockConfig>? tweak = null)
    {
        var config = new ClockConfig { TimeZoneId = "Etc/UTC", ShowDate = false };
        tweak?.Invoke(config);
        return config;
    }

    [Fact]
    public void Twenty_four_hour_time_is_zero_padded()
    {
        var reading = ClockFormatter.Read(At(9, 5), Utc(), Locale.En, use24Hour: true);

        Assert.Equal("09:05", reading.Time);
        Assert.Null(reading.Meridiem);
    }

    [Fact]
    public void Twelve_hour_time_is_not_padded_and_carries_a_meridiem()
    {
        var reading = ClockFormatter.Read(At(9, 5), Utc(), Locale.En, use24Hour: false);

        Assert.Equal("9:05", reading.Time);
        Assert.Equal("AM", reading.Meridiem);
    }

    [Theory]
    [InlineData(0, 12, "AM")]
    [InlineData(12, 12, "PM")]
    [InlineData(13, 1, "PM")]
    [InlineData(23, 11, "PM")]
    [InlineData(11, 11, "AM")]
    public void Midnight_and_noon_read_as_twelve_not_zero(int hour24, int expectedHour, string expectedMeridiem)
    {
        var reading = ClockFormatter.Read(At(hour24, 30), Utc(), Locale.En, use24Hour: false);

        Assert.Equal($"{expectedHour}:30", reading.Time);
        Assert.Equal(expectedMeridiem, reading.Meridiem);
        Assert.Equal(expectedHour, ClockFormatter.ToTwelveHour(hour24));
    }

    [Fact]
    public void Seconds_appear_only_when_asked_for()
    {
        Assert.Equal("14:07", ClockFormatter.Read(At(14, 7, 42), Utc(c => c.ShowSeconds = false), Locale.En, use24Hour: true).Time);
        Assert.Equal("14:07:42", ClockFormatter.Read(At(14, 7, 42), Utc(c => c.ShowSeconds = true), Locale.En, use24Hour: true).Time);
    }

    [Fact]
    public void The_date_is_omitted_unless_enabled()
    {
        Assert.Null(ClockFormatter.Read(At(14, 7), Utc(c => c.ShowDate = false), Locale.En, use24Hour: true).Date);
        Assert.Equal("Monday, March 9", ClockFormatter.Read(At(14, 7), Utc(c => c.ShowDate = true), Locale.En, use24Hour: true).Date);
    }

    [Fact]
    public void Hands_sweep_between_ticks_rather_than_stepping()
    {
        var onTheHour = ClockFormatter.Read(At(3, 0), Utc(), Locale.En, use24Hour: true);
        var halfPast = ClockFormatter.Read(At(3, 30), Utc(), Locale.En, use24Hour: true);

        Assert.Equal(90, onTheHour.HourAngle, 3);
        Assert.Equal(105, halfPast.HourAngle, 3); // the hour hand has moved half a division
        Assert.Equal(180, halfPast.MinuteAngle, 3);
    }

    [Fact]
    public void All_hand_angles_stay_within_one_revolution()
    {
        for (var hour = 0; hour < 24; hour++)
        {
            for (var minute = 0; minute < 60; minute += 7)
            {
                var reading = ClockFormatter.Read(At(hour, minute, 33), Utc(), Locale.En, use24Hour: true);

                Assert.InRange(reading.HourAngle, 0, 360);
                Assert.InRange(reading.MinuteAngle, 0, 360);
                Assert.InRange(reading.SecondAngle, 0, 360);
            }
        }
    }

    [Fact]
    public void An_unknown_time_zone_degrades_to_local_instead_of_throwing()
    {
        var config = new ClockConfig { TimeZoneId = "Mars/Olympus_Mons" };

        var zone = config.ResolveTimeZone();

        Assert.Equal(TimeZoneInfo.Local.Id, zone.Id);
        _ = ClockFormatter.Read(DateTimeOffset.UtcNow, config, Locale.En, use24Hour: true);
    }

    [Fact]
    public void A_clock_without_seconds_only_asks_to_be_woken_once_a_minute()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), new ClockConfig { ShowSeconds = false, Style = ClockStyle.Digital }.TickInterval);
        Assert.Equal(TimeSpan.FromSeconds(1), new ClockConfig { ShowSeconds = true }.TickInterval);

        Assert.Equal(TimeSpan.FromSeconds(1), new ClockConfig { ShowSeconds = true, Style = ClockStyle.Analog }.TickInterval);
    }

    [Theory]
    [InlineData(ClockStyle.Digital)]
    [InlineData(ClockStyle.Analog)]
    [InlineData(ClockStyle.Stacked)]
    [InlineData(ClockStyle.Flip)]
    [InlineData(ClockStyle.Word)]
    [InlineData(ClockStyle.Minimal)]
    [InlineData(ClockStyle.Date)]
    public void S52_every_style_without_seconds_wakes_once_a_minute(ClockStyle style)
    {
        // Was every second for the analog face whatever the seconds switch said: no second hand
        // drawn, and the whole face redrawn sixty times a minute for a minute hand's tenth of a degree.
        Assert.Equal(TimeSpan.FromMinutes(1), new ClockConfig { ShowSeconds = false, Style = style }.TickInterval);
    }

    [Fact]
    public void S51_a_face_without_its_second_hand_stands_its_minute_hand_on_the_whole_minute()
    {
        var withSeconds = ClockFormatter.Read(At(3, 30, 30), Utc(c => { c.Style = ClockStyle.Analog; c.ShowSeconds = true; }), Locale.En, use24Hour: true);
        var without = ClockFormatter.Read(At(3, 30, 30), Utc(c => { c.Style = ClockStyle.Analog; c.ShowSeconds = false; }), Locale.En, use24Hour: true);
        var onTheMinute = ClockFormatter.Read(At(3, 30, 0), Utc(c => { c.Style = ClockStyle.Analog; c.ShowSeconds = false; }), Locale.En, use24Hour: true);

        Assert.Equal(183, withSeconds.MinuteAngle, 3);
        Assert.Equal(180, without.MinuteAngle, 3);
        Assert.Equal(onTheMinute.MinuteAngle, without.MinuteAngle, 3);
        Assert.Equal(105, without.HourAngle, 3); // the hour hand still advances with the minutes
    }

    [Theory]
    [InlineData(ClockStyle.Digital, true)]
    [InlineData(ClockStyle.Stacked, true)]
    [InlineData(ClockStyle.Flip, true)]
    [InlineData(ClockStyle.Word, false)]
    [InlineData(ClockStyle.Minimal, false)]
    public void S6015_words_and_the_minimal_clock_never_draw_seconds_nor_wake_for_them(ClockStyle style, bool seconds)
    {
        var config = new ClockConfig { ShowSeconds = true, Style = style };

        Assert.Equal(seconds, config.DrawsSeconds);
        Assert.Equal(seconds ? TimeSpan.FromSeconds(1) : TimeSpan.FromMinutes(1), config.TickInterval);
        Assert.Equal(seconds ? 8 : 5, ClockFormatter.Read(DateTimeOffset.UtcNow, config, Themia.Core.Localization.Locale.En, use24Hour: true).Time.Length);
    }

    [Fact]
    public void S1483_a_long_meridiem_takes_its_width_from_the_ladder()
    {
        // Regression: the ladder counted every marker as "PM", so Tamil's "பிற்பகல்" clipped a tall clock.
        Assert.Equal("AM", ClockFormatter.WidestMeridiem(Locale.En));
        Assert.True(HeroFit.Ems(ClockFormatter.WidestMeridiem(Locale.Ta)) > 2 * HeroFit.Ems("PM"));

        var english = HeroFit.Row(288, 400, false, ClockFormatter.WidestMeridiem(Locale.En), 4);
        var tamil = HeroFit.Row(288, 400, false, ClockFormatter.WidestMeridiem(Locale.Ta), 4);
        Assert.True(tamil.Time.FontSize < english.Time.FontSize);
    }
}
