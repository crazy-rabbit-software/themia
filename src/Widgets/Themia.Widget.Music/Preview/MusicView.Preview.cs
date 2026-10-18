#if DEBUG
using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: <c>sample</c> puts a paused track on screen (title, artist, album, a third
/// of the way in), with one of Windows' own wallpapers as its art, so every style ([S-6406]) can
/// be looked at without anything playing. The real session is ignored while it shows.
/// </summary>
internal sealed partial class MusicView : IWidgetPreviewState
{
    private const string PreviewFolder = @"C:\Windows\Web\Wallpaper";

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
            DateTimeOffset.UtcNow,
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

        byte[]? bytes = null;

        try
        {
            var path = System.IO.Directory.EnumerateFiles(PreviewFolder, "*.jpg", System.IO.SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .FirstOrDefault();
            bytes = path is null ? null : await System.IO.File.ReadAllBytesAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"widget: Music preview could not read {PreviewFolder}: {ex.Message}");
        }

        await ShowArtworkAsync(bytes, _now.TrackKey, generation).ConfigureAwait(true);
    }
}
#endif
