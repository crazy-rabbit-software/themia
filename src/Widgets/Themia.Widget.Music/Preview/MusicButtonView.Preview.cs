using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.Music;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> the sample track, paused; <c>playing</c> it playing (play or pause the
/// accent, offering Pause); <c>idle</c> a session with no track, every button unavailable ([S-1940]).
/// </summary>
internal sealed partial class MusicButtonView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "playing", "idle"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();
            case "playing":
                _feed.Show(NowPlayingFeed.SampleTrack(SampleDay.Now) with { State = MusicState.Playing });
                break;
            case "idle":
                _feed.Show(NowPlaying.Of(MusicState.Idle));
                break;
        }

        return Task.CompletedTask;
    }
}
