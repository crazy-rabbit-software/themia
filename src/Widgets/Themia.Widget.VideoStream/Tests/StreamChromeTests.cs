using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Widget.VideoStream;
using Xunit;

namespace Themia.Widget.VideoStream.Tests;

/// <summary>[S-1312]/[S-1313] The camera's optional chrome: which parts show at which size, and their words. Moved with the widget from Core's StreamChromeTests, unchanged.</summary>
public sealed class StreamChromeTests
{
    [Fact]
    public void S11208_the_chrome_is_the_settings_at_every_size()
    {
        Assert.Equal(new StreamChrome(true, true, false), StreamChromeRules.Chrome(hoverControls: true, showStatus: false));
        Assert.Equal(new StreamChrome(false, false, true), StreamChromeRules.Chrome(hoverControls: false, showStatus: true));
        Assert.Equal(default, StreamChromeRules.Chrome(false, false));
    }

    [Fact]
    public void S11208_a_shaped_stream_keeps_its_shape_its_height_following_its_width()
    {
        var wide = VideoStreamSizing.Sizing(new VideoStreamConfig());
        Assert.Equal(new Themia.Sdk.SizeDip(320, 180), wide.Natural);
        Assert.True(wide.FixedHeight);
        Assert.Equal((160.0, 800.0), (wide.Min.Width, wide.Max.Width));
        Assert.Empty(wide.Problems());

        // A quarter turn stands the shape up; never taller than 600.
        var standing = VideoStreamSizing.Sizing(new VideoStreamConfig { Rotation = StreamRotation.Quarter, Span = 99 });
        Assert.True(standing.Natural.Height <= 600);
        Assert.Equal(standing.Natural.Width * 16 / 9, standing.Natural.Height, 3);

        var free = VideoStreamSizing.Sizing(new VideoStreamConfig { Aspect = StreamAspect.Free });
        Assert.Equal((new Themia.Sdk.SizeDip(160, 120), new Themia.Sdk.SizeDip(320, 240), new Themia.Sdk.SizeDip(800, 600)), (free.Min, free.Natural, free.Max));
    }

    [Fact]
    public void S11208_dragging_a_shaped_stream_sets_its_span_on_a_copy()
    {
        var config = new VideoStreamConfig();
        var next = Assert.IsType<VideoStreamConfig>(VideoStreamSizing.SizeSets(config, new Themia.Sdk.SizeDip(480, 10)));

        Assert.Equal(10, next.Span);
        Assert.Equal(VideoStreamSizing.DefaultSpan, config.Span);
        Assert.Equal(270, VideoStreamSizing.Sizing(next).Natural.Height, 3);

        var free = new VideoStreamConfig { Aspect = StreamAspect.Free };
        Assert.Same(free, VideoStreamSizing.SizeSets(free, new Themia.Sdk.SizeDip(480, 300)));
    }
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

/// <summary>[S-1314] A stream with no picture says so in the Status row at the top; the state dot's role.</summary>
public sealed class StreamStatusTests
{
    [Fact]
    public void S1314_no_address_is_the_set_up_row_and_a_live_stream_says_nothing()
    {
        Assert.Null(StreamChromeRules.Look(hasAddress: false, addressValid: false, paused: false, Themia.App.Services.Web.StreamPhase.Connecting));
        Assert.Null(StreamChromeRules.Look(hasAddress: true, addressValid: true, paused: false, Themia.App.Services.Web.StreamPhase.Live));
    }

    [Theory]
    [InlineData(false, false, Themia.App.Services.Web.StreamPhase.Live, Themia.Sdk.Kit.StatusKind.Problem, StreamAction.None)]
    [InlineData(true, true, Themia.App.Services.Web.StreamPhase.Live, Themia.Sdk.Kit.StatusKind.Empty, StreamAction.None)]
    [InlineData(true, false, Themia.App.Services.Web.StreamPhase.Connecting, Themia.Sdk.Kit.StatusKind.Empty, StreamAction.None)]
    [InlineData(true, false, Themia.App.Services.Web.StreamPhase.Retrying, Themia.Sdk.Kit.StatusKind.Problem, StreamAction.Retry)]
    [InlineData(true, false, Themia.App.Services.Web.StreamPhase.Unsupported, Themia.Sdk.Kit.StatusKind.Problem, StreamAction.None)]
    public void S1314_each_state_is_plain_or_a_problem_with_its_one_action(bool valid, bool paused, Themia.App.Services.Web.StreamPhase phase, Themia.Sdk.Kit.StatusKind kind, StreamAction action)
        => Assert.Equal(new StreamLook(kind, action), StreamChromeRules.Look(hasAddress: true, valid, paused, phase));

    [Fact]
    public void S1314_the_dot_is_the_accent_while_live_caution_while_waiting_tertiary_while_paused_never_critical()
    {
        Assert.Equal(Themia.Sdk.Kit.ColourRole.Accent, StreamChromeRules.DotRole(live: true, paused: false));
        Assert.Equal(Themia.Sdk.Kit.ColourRole.Caution, StreamChromeRules.DotRole(live: false, paused: false));
        Assert.Equal(Themia.Sdk.Kit.ColourRole.TextTertiary, StreamChromeRules.DotRole(live: false, paused: true));
    }

    [Fact]
    public void S1314_set_up_asks_for_a_stream()
        => Assert.Equal(VideoStreamStr.VideoStream_Setup.Text(), ((ISetupRule)new VideoStreamConfig()).SetupSentence);
}
