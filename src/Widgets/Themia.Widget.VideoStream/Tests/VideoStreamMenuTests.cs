using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.VideoStream.Tests;

/// <summary>[S-473] [S-467] The camera's options on its properties menu: the address asked for and checked as typed.</summary>
public sealed class VideoStreamMenuTests
{
    private int _saves;

    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(VideoStreamConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        VideoStreamMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S473_appearance_then_the_address_and_its_refresh_the_picture_and_the_overlay()
    {
        var lines = Build(new VideoStreamConfig());

        Assert.Equal(
            [
                VideoStreamStr.WidgetsC_VideoStream_Settings_Address_Label.Text(),
                Str.Common_Refresh.Text(),
                string.Empty,
                VideoStreamStr.WidgetsC_VideoStream_Settings_Fit_Label.Text(),
                VideoStreamStr.VideoStream_RotationLabel.Text(),
                string.Empty,
                VideoStreamStr.WidgetsC_VideoStream_Settings_Hover_Label.Text(),
                VideoStreamStr.WidgetsC_VideoStream_Settings_Chip_Label.Text(),
                string.Empty,
                VideoStreamStr.WidgetsC_VideoStream_Settings_Note.Text(),
            ],
            lines.Lines.Select(l => l.IsSeparator ? string.Empty : l.Text));
    }

    [Fact]
    public void S473_the_refresh_reads_live_at_zero()
        => Assert.Equal(VideoStreamStr.VideoStream_Menu_Live.Text(), Build(new VideoStreamConfig { RefreshSeconds = 0 }).Lines[1].Value);

    [Theory]
    [InlineData("ftp://cam.local/stream")]
    [InlineData("http://user:secret@cam.local/stream")]
    [InlineData(@"\server\share\cam")]
    [InlineData("not an address")]
    public void S467_an_address_that_cannot_be_used_is_refused_with_a_reason(string typed)
        => Assert.False(string.IsNullOrEmpty(VideoStreamMenu.AddressRefusal(typed)));

    [Fact]
    public void S467_a_scheme_other_than_http_says_only_http()
        => Assert.Equal(VideoStreamStr.WidgetsC_VideoStream_Settings_OnlyHttp.Text(), VideoStreamMenu.AddressRefusal("rtsp://cam.local/live"));

    [Fact]
    public void S473_an_address_is_stored_trimmed_once_and_empty_clears_it()
    {
        var config = new VideoStreamConfig();
        var line = Build(config).Lines[0];
        Assert.Null(line.Value);

        line.Action!();
        Assert.Null(_asked!.Refuse(string.Empty));
        _asked.Done("  http://octopi.local/webcam/?action=stream ");
        Assert.Equal("http://octopi.local/webcam/?action=stream", config.Url);
        Assert.Equal(1, _saves);
        Assert.Equal("octopi.local", Build(config).Lines[0].Value);

        Build(config).Lines[0].Action!();
        _asked!.Done(string.Empty);
        Assert.Equal(string.Empty, config.Url);
        Assert.Equal(2, _saves);
    }
}
