using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2056] The music widget's sample: a paused track on screen (title, artist, album, a third
/// of the way in) without anything playing. The real session is ignored while it shows.
/// </summary>
internal sealed partial class MusicView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _feed.Show(NowPlayingFeed.SampleTrack(SampleDay.Now));
        return Task.CompletedTask;
    }
}
