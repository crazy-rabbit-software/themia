using Themia.App.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.Music;

/// <summary>
/// [S-11850] [S-2056] A transport button's sample: the sample track, paused, so play or pause offers Play. The real session
/// is ignored while it shows.
/// </summary>
internal sealed partial class MusicButtonView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _feed.Show(NowPlayingFeed.SampleTrack(SampleDay.Now));
        return Task.CompletedTask;
    }
}
