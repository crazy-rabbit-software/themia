#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.App.Widgets;
using Themia.Sdk.Samples;
using Themia.Sdk.WinUI;

namespace Themia.Widget.MusicCover;

/// <summary>Debug renders only: <c>sample</c> is the renders' paused track with a cover drawn in code ([S-2056]).</summary>
internal sealed partial class MusicCoverView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public async Task ShowPreviewStateAsync(string state)
    {
        if (state != "sample")
        {
            return;
        }

        var track = NowPlayingFeed.SampleTrack(SampleDay.Now);
        _feed.Show(track);
        var bytes = await SamplePictureFiles.CoverPngAsync(600).ConfigureAwait(true);
        Show(await NowPlayingFeed.DecodeAsync(bytes, CancellationToken.None).ConfigureAwait(true), track.TrackKey);
    }
}
#endif
