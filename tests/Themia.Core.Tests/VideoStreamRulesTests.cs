using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The video stream's shared rules (the SDK's): the title, the reconnect backoff, rotation, and what a
/// reply's content type makes of it. The config's own are its module's (VideoStreamConfigTests) and the
/// module host's (VideoStreamHostTests).
/// </summary>
public class VideoStreamRulesTests
{
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

    [Fact]
    public void S468_rotation_cycles_a_quarter_turn_at_a_time()
    {
        Assert.Equal(StreamRotation.Quarter, VideoStreamRules.NextRotation(StreamRotation.None));
        Assert.Equal(StreamRotation.Half, VideoStreamRules.NextRotation(StreamRotation.Quarter));
        Assert.Equal(StreamRotation.ThreeQuarter, VideoStreamRules.NextRotation(StreamRotation.Half));
        Assert.Equal(StreamRotation.None, VideoStreamRules.NextRotation(StreamRotation.ThreeQuarter));
    }

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

}
