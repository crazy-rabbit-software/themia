using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.Music;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is the sample track (<see cref="ShowSampleAsync"/>); <c>idle</c> a session
/// with no track in it, the face kept ([S-12813]); <c>bare</c> a track with nothing but its title (a stream, a video).
/// </summary>
internal sealed partial class MusicTextView
{
    protected override IReadOnlyList<string> OwnPreviewStates { get; } = ["sample", "idle", "bare"];

    protected override Task ShowOwnPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();
            case "idle":
                _feed.Show(NowPlaying.Of(MusicState.Idle));
                break;
            case "bare":
                _feed.Show(NowPlayingFeed.SampleTrack(SampleDay.Now) with { Artist = string.Empty, Album = string.Empty, Subtitle = string.Empty });
                break;
        }

        return Task.CompletedTask;
    }
}
