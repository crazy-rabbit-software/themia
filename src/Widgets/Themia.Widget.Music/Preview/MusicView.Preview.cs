#if DEBUG
using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: <c>sample</c> puts a paused track on screen (title, artist, album, a third
/// of the way in), with a picture drawn in code as its art ([S-2056]), so every style ([S-6406]) can
/// be looked at without anything playing. The real session is ignored while it shows.
/// </summary>
internal sealed partial class MusicView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public async Task ShowPreviewStateAsync(string state)
    {
        if (state != "sample")
        {
            return;
        }

        _frozen = true;
        _reader?.Cancel();
        var generation = ++_generation;

        _now = new NowPlaying(
            MusicState.Paused,
            "Northern Lights",
            "Aurora Quartet · Long Winter",
            "preview-track",
            new TrackTimeline(TimeSpan.FromSeconds(74), TimeSpan.FromSeconds(222)),
            SampleDay.Now,
            CanPlayPause: true,
            CanNext: true,
            CanPrevious: true,
            CanSeek: true,
            Artist: "Aurora Quartet",
            Album: "Long Winter");
        Present();

        if (!_config.ShowsArt)
        {
            return;
        }

        // [S-2056] Art drawn in code, never a picture of this PC.
        var bytes = await SamplePictureFiles.PngAsync(2, 600, 600).ConfigureAwait(true);

        await ShowArtworkAsync(bytes, _now.TrackKey, generation).ConfigureAwait(true);
    }
}
#endif
