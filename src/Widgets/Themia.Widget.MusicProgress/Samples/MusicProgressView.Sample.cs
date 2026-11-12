using Themia.App.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.MusicProgress;

/// <summary>[S-11850] [S-2056] The progress bar's sample: the paused sample track, a third of the way in.</summary>
internal sealed partial class MusicProgressView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _feed.Show(NowPlayingFeed.SampleTrack(SampleDay.Now));
        return Task.CompletedTask;
    }
}
