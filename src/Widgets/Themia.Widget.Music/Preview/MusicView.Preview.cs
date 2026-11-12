#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: <c>sample</c> puts the renders' paused track on screen (title, artist, album, a
/// third of the way in, [S-2056]) without anything playing. The real session is ignored while it shows.
/// </summary>
internal sealed partial class MusicView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            _feed.Show(NowPlayingFeed.SampleTrack(SampleDay.Now));
        }

        return Task.CompletedTask;
    }
}
#endif
