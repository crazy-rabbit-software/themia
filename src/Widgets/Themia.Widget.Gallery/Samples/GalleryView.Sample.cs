using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2056] The gallery's sample: the sample day's pictures, drawn in code
/// (<see cref="SamplePictures"/>) and written under the temp folder, in the card's own style; never a
/// picture of this PC. The scan is handed in, so no folder is walked; the decoding goes through the
/// gallery service the card's host hands it.
/// </summary>
internal sealed partial class GalleryView : IWidgetSample
{
    public Task ShowSampleAsync() => ShowSamplePicturesAsync(GallerySource.Folder, _config.Style);

    /// <summary>The sample pictures from <paramref name="source"/> (a folder of them, or the first as a file) in <paramref name="layout"/>.</summary>
    internal async Task ShowSamplePicturesAsync(GallerySource source, GalleryStyle layout)
    {
        var folder = await SamplePictureFiles.WriteAsync("Pictures", SamplePictures.Names).ConfigureAwait(true);
        if (folder is null)
        {
            return;
        }

        List<string> paths = [.. SamplePictures.Names.Select(name => System.IO.Path.Combine(folder, name))];

        if (source == GallerySource.File)
        {
            paths = [.. paths.Take(1)];
        }

        // Unshuffled, so a picture is the same every time. No source is held: the scan is handed in,
        // and only the decoding goes through the gallery service.
        _watch?.Dispose();
        _watch = null;
        _config = new GalleryConfig { Source = source, Style = layout, Path = folder, FilePath = paths.FirstOrDefault() ?? string.Empty, Shuffle = false };
        PublishTitle();
        _scan = new GalleryScan(paths.Count == 0 ? GalleryStatus.NoImages : GalleryStatus.Ok, paths);
        Select(keepPath: null, index: 0);
        _slideTimer?.Stop();
        Lay();

        // The decodes are asynchronous; give them a moment before the picture is taken.
        await Task.Delay(1500).ConfigureAwait(true);
    }
}
