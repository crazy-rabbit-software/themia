using Themia.Core.Model;
using Xunit;

namespace Themia.Widget.VideoStream.Tests;

/// <summary>[S-467] The camera's address gate on its config. Moved with the widget from Core's VideoStreamRulesTests, unchanged.</summary>
public sealed class VideoStreamConfigTests
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
}
