using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6602] The renditions of an HLS master playlist, and the cap a tile's size puts on them.</summary>
public class HlsVariantsTests
{
    /// <summary>The Mux test stream's master playlist, as served.</summary>
    private const string Master = """
        #EXTM3U
        #EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=2149280,CODECS="mp4a.40.2,avc1.64001f",RESOLUTION=1280x720,NAME="720"
        url_0/193039199_mp4_h264_aac_hd_7.m3u8
        #EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=246440,CODECS="mp4a.40.5,avc1.42000d",RESOLUTION=320x184,NAME="240"
        url_2/193039199_mp4_h264_aac_ld_7.m3u8
        #EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=460560,CODECS="mp4a.40.5,avc1.420016",RESOLUTION=512x288,NAME="380"
        url_4/193039199_mp4_h264_aac_7.m3u8
        #EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=836280,CODECS="mp4a.40.2,avc1.64001f",RESOLUTION=848x480,NAME="480"
        url_6/193039199_mp4_h264_aac_hq_7.m3u8
        #EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=6221600,CODECS="mp4a.40.2,avc1.640028",RESOLUTION=1920x1080,NAME="1080"
        url_8/193039199_mp4_h264_aac_fhd_7.m3u8
        """;

    [Fact]
    public void S6602_a_master_playlist_lists_each_rendition_with_its_size_quoted_commas_and_all()
    {
        var variants = HlsVariants.Parse(Master);

        Assert.Equal(5, variants.Count);
        Assert.Equal(new HlsVariant(2149280, 1280, 720), variants[0]);
        Assert.Equal(new HlsVariant(6221600, 1920, 1080), variants[4]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html><body>login</body></html>")]
    [InlineData("#EXTM3U\n#EXT-X-TARGETDURATION:10\n#EXTINF:10,\nseg0.ts\n")]
    public void S6602_what_is_not_a_master_playlist_has_no_renditions(string text)
        => Assert.Empty(HlsVariants.Parse(text));

    [Fact]
    public void S6602_a_rendition_without_a_bandwidth_is_skipped_and_a_bad_resolution_is_unknown()
    {
        var variants = HlsVariants.Parse("#EXTM3U\r\n#EXT-X-STREAM-INF:RESOLUTION=640x360\r\na.m3u8\r\n#EXT-X-STREAM-INF:BANDWIDTH=900000,RESOLUTION=wide\r\nb.m3u8\r\n");

        Assert.Equal([new HlsVariant(900000, 0, 0)], variants);
    }

    [Fact]
    public void S6602_a_playlist_past_the_limit_is_not_read()
        => Assert.Empty(HlsVariants.Parse("#EXTM3U\n" + new string('#', HlsVariants.MaxPlaylistChars)));

    [Theory]
    [InlineData(1024, 576, false, 2149280L)] // a 1000 px tile: 720p, not 1080p
    [InlineData(400, 225, false, 460560L)]
    [InlineData(300, 160, false, 246440L)]
    [InlineData(1920, 1080, false, 6221600L)]
    [InlineData(500, 1000, false, 460560L)] // a tall letterboxed tile needs the width only
    [InlineData(500, 1000, true, 6221600L)] // cropped to fill it, only 1080p is tall enough
    public void S6602_the_cap_is_the_smallest_rendition_that_fills_the_tile(int width, int height, bool cover, long? expected)
        => Assert.Equal(expected, HlsVariants.CapFor(HlsVariants.Parse(Master), width, height, cover));

    [Fact]
    public void S6602_a_tile_larger_than_every_rendition_is_not_capped()
        => Assert.Null(HlsVariants.CapFor(HlsVariants.Parse(Master), 3840, 2160, cover: false));

    [Fact]
    public void S6602_renditions_of_unknown_size_are_not_capped()
        => Assert.Null(HlsVariants.CapFor([new HlsVariant(500000, 640, 360), new HlsVariant(4000000, 0, 0)], 320, 180, cover: false));

    [Fact]
    public void S6602_nothing_known_is_no_cap()
    {
        Assert.Null(HlsVariants.CapFor([], 640, 360, cover: false));
        Assert.Null(HlsVariants.CapFor(HlsVariants.Parse(Master), 0, 0, cover: false));
    }
}
