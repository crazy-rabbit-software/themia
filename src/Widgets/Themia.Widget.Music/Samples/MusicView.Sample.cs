using Themia.App.Services;
using Themia.Sdk.Samples;
using Themia.Sdk.WinUI;
using Themia.Widget.Music;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2056] Now playing's sample: a paused track on screen (its title, artist and album, a third of the way in,
/// its cover drawn in code) without anything playing, owned by Windows' Media Player so its face lights as a real one does.
/// The real session is ignored while it shows.
/// </summary>
internal sealed partial class MusicView : IWidgetSample
{
    /// <summary>The sample's player: Windows' own Media Player.</summary>
    private const string SamplePlayer = "Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic";

    public Task ShowSampleAsync() => ShowTrackAsync(NowPlayingFeed.SampleTrack(SampleDay.Now), art: true);

    /// <summary>Show <paramref name="track"/> as the sample does, its cover drawn in code or none.</summary>
    private async Task ShowTrackAsync(NowPlaying track, bool art)
    {
        var shown = track with { AppId = SamplePlayer };
        _feed.Show(shown);
        if (art)
        {
            var bytes = await SamplePictureFiles.CoverPngAsync(600).ConfigureAwait(true);
            ShowArt(await NowPlayingFeed.DecodeAsync(bytes, CancellationToken.None).ConfigureAwait(true), MusicWords.ArtKey(shown));
        }
    }
}
