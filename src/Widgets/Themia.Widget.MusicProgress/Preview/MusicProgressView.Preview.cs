#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.App.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.MusicProgress;

/// <summary>Debug renders only: <c>sample</c> is the renders' paused track, a third of the way in ([S-2056]).</summary>
internal sealed partial class MusicProgressView : IWidgetPreviewState
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
