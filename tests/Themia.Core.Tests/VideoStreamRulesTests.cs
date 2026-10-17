using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The video stream widget's rules: the address gate, the title, the reconnect backoff, the refresh
/// clamp, rotation, and what a reply's content type makes of it.
/// </summary>
public class VideoStreamRulesTests
{
    [Theory]
    [InlineData("http://192.168.1.50:8080/?action=stream")]
    [InlineData("https://cam.example.com/stream.mjpg")]
    [InlineData("  http://octopi.local/webcam/?action=stream  ")]
    public void S467_the_http_endpoints_real_cameras_expose_are_accepted(string url)
        => Assert.True(new VideoStreamConfig { Url = url }.ResolvedUrl.IsValid);

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("file:///C:/secret.jpg")]
    [InlineData("rtsp://192.168.1.50/live")]
    [InlineData("192.168.1.50:8080/stream")]
    [InlineData("")]
    [InlineData("http://user:pass@cam/stream")]
    public void S467_every_other_scheme_and_unparseable_text_is_refused(string url)
        => Assert.False(new VideoStreamConfig { Url = url }.ResolvedUrl.IsValid);

    [Fact]
    public void S467_an_accepted_address_keeps_its_query_string()
    {
        // Cameras route on the query; dropping or reordering it breaks them.
        var resolved = new VideoStreamConfig { Url = " http://cam/?action=stream " }.ResolvedUrl;
        Assert.Equal("http://cam/?action=stream", resolved.Url);
    }

    [Fact]
    public void S460_the_title_is_the_cameras_host_and_port()
        => Assert.Equal("192.168.1.50:8080", VideoStreamRules.Title("http://192.168.1.50:8080/?action=stream"));

    [Fact]
    public void S460_a_default_port_is_left_out_of_the_title()
        => Assert.Equal("cam.example.com", VideoStreamRules.Title("https://cam.example.com/s"));

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("rtsp://cam/live")]
    public void S460_no_usable_address_means_no_title_so_the_card_uses_the_widget_name(string url)
        => Assert.Equal(string.Empty, VideoStreamRules.Title(url));

    [Fact]
    public void S464_the_backoff_doubles_from_two_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), VideoStreamRules.RetryDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(4), VideoStreamRules.RetryDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(8), VideoStreamRules.RetryDelay(3));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(50)]
    [InlineData(int.MaxValue)]
    public void S464_the_backoff_is_capped_so_an_offline_camera_is_still_retried(int attempt)
        => Assert.Equal(TimeSpan.FromSeconds(30), VideoStreamRules.RetryDelay(attempt));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(int.MinValue)]
    public void S464_a_nonsense_attempt_count_gets_the_base_delay(int attempt)
        => Assert.Equal(TimeSpan.FromSeconds(2), VideoStreamRules.RetryDelay(attempt));

    [Theory]
    [InlineData(5, 5)]
    [InlineData(0, 0)]
    [InlineData(-10, 0)]
    [InlineData(99999, 300)]
    public void S470_the_refresh_is_clamped_into_range(int raw, int expected)
        => Assert.Equal(expected, new VideoStreamConfig { RefreshSeconds = raw }.EffectiveRefreshSeconds);

    [Fact]
    public void S468_rotation_cycles_a_quarter_turn_at_a_time()
    {
        Assert.Equal(StreamRotation.Quarter, VideoStreamRules.NextRotation(StreamRotation.None));
        Assert.Equal(StreamRotation.Half, VideoStreamRules.NextRotation(StreamRotation.Quarter));
        Assert.Equal(StreamRotation.ThreeQuarter, VideoStreamRules.NextRotation(StreamRotation.Half));
        Assert.Equal(StreamRotation.None, VideoStreamRules.NextRotation(StreamRotation.ThreeQuarter));
    }

    [Fact]
    public void S468_a_rotation_the_enum_does_not_name_is_read_as_none()
        => Assert.Equal(StreamRotation.None, new VideoStreamConfig { Rotation = (StreamRotation)45 }.EffectiveRotation);

    [Fact]
    public void S468_only_quarter_turns_put_the_picture_on_its_side()
    {
        Assert.True(VideoStreamRules.IsSideways(StreamRotation.Quarter));
        Assert.True(VideoStreamRules.IsSideways(StreamRotation.ThreeQuarter));
        Assert.False(VideoStreamRules.IsSideways(StreamRotation.None));
        Assert.False(VideoStreamRules.IsSideways(StreamRotation.Half));
    }

    [Theory]
    [InlineData("multipart/x-mixed-replace", StreamTransport.Mjpeg)]
    [InlineData("Multipart/X-Mixed-Replace", StreamTransport.Mjpeg)]
    [InlineData("multipart/mixed", StreamTransport.Mjpeg)]
    [InlineData("image/jpeg", StreamTransport.Image)]
    [InlineData("image/png", StreamTransport.Image)]
    [InlineData("image/svg+xml", StreamTransport.Unsupported)]
    [InlineData("video/mp4", StreamTransport.Video)]
    [InlineData("application/vnd.apple.mpegurl", StreamTransport.Hls)]
    [InlineData("application/x-mpegURL", StreamTransport.Hls)]
    [InlineData("text/html", StreamTransport.Unsupported)]
    [InlineData("", StreamTransport.Unsupported)]
    [InlineData(null, StreamTransport.Unsupported)]
    public void S471_the_transport_follows_the_content_type(string? mediaType, StreamTransport expected)
        => Assert.Equal(expected, VideoStreamRules.Classify(mediaType));

    [Theory]
    [InlineData("multipart/x-mixed-replace; boundary=frame", "frame")]
    [InlineData("multipart/x-mixed-replace;boundary=\"frame\"", "frame")]
    [InlineData("multipart/x-mixed-replace; boundary=--boundarydonotcross", "boundarydonotcross")]
    [InlineData("multipart/x-mixed-replace; charset=x; BOUNDARY=myboundary", "myboundary")]
    public void S472_the_boundary_is_read_however_the_camera_spells_it(string header, string expected)
        => Assert.Equal(expected, VideoStreamRules.Boundary(header));

    [Theory]
    [InlineData("multipart/x-mixed-replace")]
    [InlineData("multipart/x-mixed-replace; boundary=")]
    [InlineData("multipart/x-mixed-replace; boundary=\"\"")]
    [InlineData(null)]
    public void S472_a_missing_boundary_is_null_not_empty(string? header)
        => Assert.Null(VideoStreamRules.Boundary(header));

    [Fact]
    public async Task S469_paused_survives_a_round_trip_through_the_state_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "themia-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "state.json");

        try
        {
            var state = AppState.CreateDefault();
            var widget = WidgetInstance.Create(VideoStreamConfig.Descriptor.Id, new Geometry.FracRect(0.1, 0.1, 0.2, 0.2));
            widget.Config = new VideoStreamConfig
            {
                Url = "http://cam/?action=stream",
                Paused = true,
                Rotation = StreamRotation.ThreeQuarter,
                Fit = StreamFit.Cover,
                RefreshSeconds = 10,
            };
            state.Views[0].Widgets.Add(widget);

            var store = new AppStateStore(path, TimeSpan.Zero);
            store.SaveSoon(state);
            await store.FlushAsync();
            await store.DisposeAsync();

            var loaded = new AppStateStore(path).Load().State;
            var back = Assert.IsType<VideoStreamConfig>(loaded.Views[0].Widgets.Single(w => w.Id == widget.Id).Config);
            AssertRoundTrip(back);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static void AssertRoundTrip(VideoStreamConfig back)
    {
        Assert.True(back.Paused);
        Assert.Equal(StreamRotation.ThreeQuarter, back.Rotation);
        Assert.Equal(StreamFit.Cover, back.Fit);
        Assert.Equal(10, back.RefreshSeconds);
        Assert.Equal("http://cam/?action=stream", back.Url);
    }
}
