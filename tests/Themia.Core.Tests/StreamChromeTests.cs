using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1312]/[S-1313] The camera's optional chrome: which parts show at which size, and their words.</summary>
public class StreamChromeTests
{
    // The size tiers, as the card's slot (card less a ~44 DIP header).
    private static readonly WidgetArea Xs = new(120, 76);
    private static readonly WidgetArea S = new(200, 106);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea Wide = new(480, 116);
    private static readonly WidgetArea Tall = new(200, 356);
    private static readonly WidgetArea L = new(560, 376);
    private static readonly WidgetArea Xl = new(800, 516);

    [Fact]
    public void S1312_nothing_but_the_picture_on_the_small_tiles_and_a_wide_strip()
    {
        foreach (var area in new[] { Xs, S, Wide })
        {
            Assert.Equal(default, StreamChromeRules.Chrome(area, hoverControls: true, showStatus: true));
        }
    }

    [Fact]
    public void S1312_the_hover_strip_from_M_and_controls_from_L()
    {
        Assert.Equal(new StreamChrome(true, false, false), StreamChromeRules.Chrome(M, true, false));
        Assert.Equal(new StreamChrome(true, false, false), StreamChromeRules.Chrome(Tall, true, false));
        Assert.Equal(new StreamChrome(true, true, false), StreamChromeRules.Chrome(L, true, false));
        Assert.Equal(new StreamChrome(true, true, false), StreamChromeRules.Chrome(Xl, true, false));
    }

    [Fact]
    public void S1312_the_status_chip_only_at_XL_and_only_when_turned_on()
    {
        Assert.False(StreamChromeRules.Chrome(L, true, showStatus: true).StatusChip);
        Assert.True(StreamChromeRules.Chrome(Xl, true, showStatus: true).StatusChip);
        Assert.False(StreamChromeRules.Chrome(Xl, true, showStatus: false).StatusChip);
    }

    [Fact]
    public void S1312_hover_controls_off_hides_every_hover_part_but_not_the_chip()
    {
        Assert.Equal(new StreamChrome(false, false, true), StreamChromeRules.Chrome(Xl, hoverControls: false, showStatus: true));
    }

    [Fact]
    public void S1312_an_unmeasured_area_shows_nothing()
        => Assert.Equal(default, StreamChromeRules.Chrome(new WidgetArea(0, 0), true, true));

    [Fact]
    public void S1312_the_defaults_are_hover_on_and_status_off()
    {
        var config = new VideoStreamConfig();
        Assert.True(config.HoverControls);
        Assert.False(config.ShowStatus);

        var clone = (VideoStreamConfig)new VideoStreamConfig { HoverControls = false, ShowStatus = true }.Clone();
        Assert.False(clone.HoverControls);
        Assert.True(clone.ShowStatus);
    }

    [Theory]
    [InlineData(true, 0, 0, "LIVE")]
    [InlineData(true, 0, 10, "every 10 s")]
    [InlineData(true, 0, 999, "every 300 s")]
    [InlineData(false, 0, 0, "connecting")]
    [InlineData(false, 3, 0, "reconnecting 3")]
    public void S1313_the_status_says_what_the_stream_is_doing(bool live, int reconnects, int refresh, string expected)
        => Assert.Equal(expected, StreamChromeRules.Status(Locale.En, live, reconnects, refresh));

    [Theory]
    [InlineData(0.4, "now")]
    [InlineData(4.9, "4 s ago")]
    [InlineData(59, "59 s ago")]
    [InlineData(150, "2 min ago")]
    [InlineData(7300, "2 h ago")]
    public void S1313_the_frame_age_reads_in_seconds_then_minutes_then_hours(double seconds, string expected)
        => Assert.Equal(expected, StreamChromeRules.FrameAge(Locale.En, TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(null, "")]
    [InlineData(0.0, "")]
    [InlineData(double.NaN, "")]
    [InlineData(2.46, "2.5 fps")]
    [InlineData(12.4, "12 fps")]
    [InlineData(29.97, "30 fps")]
    public void S1313_the_rate_is_whole_from_ten_and_one_decimal_under(double? fps, string expected)
        => Assert.Equal(expected, StreamChromeRules.Rate(Locale.En, fps));

    [Fact]
    public void S1313_status_age_and_rate_are_in_the_language()
    {
        Assert.Equal("alle 10 s", StreamChromeRules.Status(Locale.De, live: true, 0, 10));
        Assert.Equal("vor 2 Min.", StreamChromeRules.FrameAge(Locale.De, TimeSpan.FromMinutes(2.5)));
        Assert.Equal("2,5 fps", StreamChromeRules.Rate(Locale.De, 2.46));
        Assert.Equal("2 fps", StreamChromeRules.Rate(Locale.En, 2.0));
    }

    [Fact]
    public void S1313_resolution_and_transport_words()
    {
        Assert.Equal("1280×720", StreamChromeRules.Resolution(1280, 720));
        Assert.Equal(string.Empty, StreamChromeRules.Resolution(0, 720));
        Assert.Equal("MJPEG", StreamChromeRules.Transport(StreamTransport.Mjpeg));
        Assert.Equal("Snapshot", StreamChromeRules.Transport(StreamTransport.Image));
        Assert.Equal("HLS", StreamChromeRules.Transport(StreamTransport.Hls));
        Assert.Equal(string.Empty, StreamChromeRules.Transport(StreamTransport.Unsupported));
    }

    [Fact]
    public void S1313_the_rate_is_frames_over_the_span_they_arrived_in()
    {
        var meter = new FrameRateMeter();
        Assert.Null(meter.Rate);

        for (var i = 0; i <= 20; i++)
        {
            meter.Add(TimeSpan.FromMilliseconds(100 * i));
        }

        Assert.Equal(10, meter.Rate!.Value, 3);
        Assert.Equal(21, meter.Count);
    }

    [Fact]
    public void S1313_the_rate_follows_a_camera_that_slows_down()
    {
        var meter = new FrameRateMeter();

        for (var i = 0; i < 100; i++)
        {
            meter.Add(TimeSpan.FromMilliseconds(40 * i));
        }

        var start = TimeSpan.FromMilliseconds(40 * 99);
        for (var i = 1; i <= 10; i++)
        {
            meter.Add(start + TimeSpan.FromSeconds(i));
        }

        // Only the last five seconds count: one frame a second.
        Assert.InRange(meter.Rate!.Value, 0.9, 1.1);
    }

    [Fact]
    public void S1313_a_timestamp_running_backwards_is_ignored_and_reset_clears()
    {
        var meter = new FrameRateMeter();
        meter.Add(TimeSpan.FromSeconds(2));
        meter.Add(TimeSpan.FromSeconds(1));
        Assert.Equal(1, meter.Count);
        Assert.Null(meter.Rate);

        meter.Reset();
        Assert.Equal(0, meter.Count);
        Assert.Null(meter.Last);
    }
}
