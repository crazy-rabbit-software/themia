using Themia.App.Widgets;
using Themia.Sdk.Samples;
using Themia.Sdk.WinUI;

namespace Themia.Widget.MusicCover;

/// <summary>[S-11850] [S-2056] The cover's sample: the paused sample track with a cover drawn in code.</summary>
internal sealed partial class MusicCoverView : IWidgetSample
{
    public async Task ShowSampleAsync()
    {
        var track = NowPlayingFeed.SampleTrack(SampleDay.Now);
        _feed.Show(track);
        var bytes = await SamplePictureFiles.CoverPngAsync(600).ConfigureAwait(true);
        Show(await NowPlayingFeed.DecodeAsync(bytes, CancellationToken.None).ConfigureAwait(true), track.TrackKey);
    }
}
