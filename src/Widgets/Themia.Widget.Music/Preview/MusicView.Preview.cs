using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: <c>sample</c> is Now playing's sample (<see cref="ShowSampleAsync"/>); <c>playing</c> it playing;
/// <c>noArt</c> a track with no cover; <c>long</c> a long title and artist on an hour-long track (the times' widest template);
/// <c>idle</c> a session with no track in it and <c>noSession</c> no player at all, the face kept ([S-16508]). [S-16515] And
/// the element sets the folds and the page make, each the sample in that set, the card sized by it (<see cref="DrawnConfig"/>):
/// <c>coverOnly</c> (a placed cover), <c>progressOnly</c> (a placed progress), <c>textsTransport</c> (the track as it was),
/// <c>transportOnly</c>, <c>playOnly</c> and <c>titleOnly</c>.
/// </summary>
internal sealed partial class MusicView : IWidgetPreviewState
{
    /// <summary>[S-16515] The element sets drawn as states, by name.</summary>
    private static readonly Dictionary<string, MusicElement[]> Sets = new(StringComparer.Ordinal)
    {
        ["coverOnly"] = [MusicElement.Cover],
        ["progressOnly"] = [MusicElement.Elapsed, MusicElement.Bar, MusicElement.Length],
        ["textsTransport"] = [MusicElement.Title, MusicElement.Artist, MusicElement.Album, MusicElement.Previous, MusicElement.PlayPause, MusicElement.Next],
        ["transportOnly"] = [MusicElement.Previous, MusicElement.PlayPause, MusicElement.Next],
        ["playOnly"] = [MusicElement.PlayPause],
        ["titleOnly"] = [MusicElement.Title],
    };

    /// <summary>[S-16515] The config an element-set state draws; null for the card's own.</summary>
    private MusicConfig? _drawn;

    public IReadOnlyList<string> PreviewStates { get; } =
        ["sample", "playing", "noArt", "long", "idle", "noSession", .. Sets.Keys];

    /// <summary>[S-11603] An element-set state is drawn at the size its set makes.</summary>
    public WidgetConfig? DrawnConfig => _drawn;

    public Task ShowPreviewStateAsync(string state)
    {
        var sample = NowPlayingFeed.SampleTrack(SampleDay.Now);
        if (Sets.TryGetValue(state, out var set))
        {
            var drawn = (MusicConfig)_config.Clone();
            drawn.Shows = [.. set];
            _drawn = drawn;
            _config = drawn;
            _face.Shows(drawn);
            return ShowSampleAsync();
        }

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
