#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: real pictures without a configured folder. The pictures are Windows' own
/// wallpapers, handed in as a finished scan so no folder is walked and nothing is configured.
/// </summary>
internal sealed partial class GalleryView : IWidgetPreviewState
{
    private const string PreviewFolder = @"C:\Windows\Web\Wallpaper";

    public IReadOnlyList<string> PreviewStates { get; } = ["slideshow", "grid", "scatter", "mosaic", "file"];

    public async Task ShowPreviewStateAsync(string state)
    {
        var (source, layout) = state switch
        {
            "slideshow" => (GallerySource.Folder, GalleryLayout.Slideshow),
            "grid" => (GallerySource.Folder, GalleryLayout.Grid),
            "scatter" => (GallerySource.Folder, GalleryLayout.Scatter),
            "mosaic" => (GallerySource.Folder, GalleryLayout.Mosaic),
            "file" => (GallerySource.File, GalleryLayout.Slideshow),
            _ => ((GallerySource?)null, GalleryLayout.Slideshow),
        };

        if (source is not { } chosen)
        {
            return;
        }

        List<string> paths;

        try
        {
            paths = [.. System.IO.Directory.EnumerateFiles(PreviewFolder, "*.jpg", System.IO.SearchOption.AllDirectories).Order(StringComparer.Ordinal).Take(24)];
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"widget: Gallery preview could not read {PreviewFolder}: {ex.Message}");
            return;
        }

        if (chosen == GallerySource.File)
        {
            paths = [.. paths.Take(1)];
        }

        // Unshuffled, so a render is the same picture every time. No source is held: the scan is
        // handed in, and only the decoding goes through the shared cache.
        _watch?.Dispose();
        _watch = null;
        _config = new GalleryConfig { Source = chosen, Layout = layout, Path = PreviewFolder, FilePath = paths.FirstOrDefault() ?? string.Empty, Shuffle = false };
        PublishTitle();
        _scan = new GalleryScan(paths.Count == 0 ? GalleryStatus.NoImages : GalleryStatus.Ok, paths);
        Select(keepPath: null, index: 0);
        _slideTimer?.Stop();
        Lay();

        // The decodes are asynchronous; give them a moment before the render is taken.
        await Task.Delay(1500).ConfigureAwait(true);
    }
}
#endif
