using Themia.App.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.Music;

/// <summary>
/// [S-11850] [S-2056] A text of the track's sample: the paused sample track (title, artist, album) on screen without
/// anything playing, the cover's and the progress's moment. The real session is ignored while it shows.
/// </summary>
internal sealed partial class MusicTextView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _feed.Show(NowPlayingFeed.SampleTrack(SampleDay.Now));
        return Task.CompletedTask;
    }
}
