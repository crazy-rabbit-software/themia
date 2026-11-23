using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: <c>sample</c> is Now playing's sample (<see cref="ShowSampleAsync"/>); <c>playing</c> it playing;
/// <c>noArt</c> a track with no cover; <c>long</c> a long title and artist on an hour-long track (the times' wider template);
/// <c>idle</c> a session with no track in it and <c>noSession</c> no player at all, the face kept ([S-16508]).
/// </summary>
internal sealed partial class MusicView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "playing", "noArt", "long", "idle", "noSession"];

    public Task ShowPreviewStateAsync(string state)
    {
        var sample = NowPlayingFeed.SampleTrack(SampleDay.Now);
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();
            case "playing":
                return ShowTrackAsync(sample with { State = MusicState.Playing, AnchoredAt = DateTimeOffset.UtcNow }, art: true);
            case "noArt":
                return ShowTrackAsync(sample, art: false);
            case "long":
                return ShowTrackAsync(sample with
                {
                    Title = "Variations on a Theme That Keeps Going Long After the Card Ends",
                    Subtitle = "The Northern Lights Chamber Orchestra · Collected Recordings, Volume Two",
                    Artist = "The Northern Lights Chamber Orchestra",
                    Album = "Collected Recordings, Volume Two",
                    Timeline = new TrackTimeline(TimeSpan.FromSeconds(2591), TimeSpan.FromSeconds(3750)),
                }, art: true);
            case "idle":
                _feed.Show(NowPlaying.Of(MusicState.Idle));
                return Task.CompletedTask;
            case "noSession":
                _feed.Show(NowPlaying.Of(MusicState.NoSession));
                return Task.CompletedTask;
            default:
                return Task.CompletedTask;
        }
    }
}
