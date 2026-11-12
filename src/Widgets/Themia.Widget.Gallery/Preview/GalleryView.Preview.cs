#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-2056]): pictures without a configured folder: <c>sample</c> is the gallery's
/// sample (<see cref="ShowSampleAsync"/>), the other picture states the same sample pictures in a fixed style.
/// </summary>
internal sealed partial class GalleryView : IWidgetPreviewState
{
    /// <summary>Where the empty, missing and failed states say they looked: a name only, never a folder of this PC.</summary>
    private const string PreviewFolder = @"C:\Users\Robin\Pictures";

    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "slideshow", "grid", "scatter", "mosaic", "file", "sticker", "controls", "empty", "missing", "failed"];

    public async Task ShowPreviewStateAsync(string state)
    {
        // [S-6714] A scan with nothing to draw, as the Status row says it: plain, a problem, a problem with Retry.
        if (state is "empty" or "missing" or "failed")
        {
            _watch?.Dispose();
            _watch = null;
            _config = new GalleryConfig { Source = GallerySource.Folder, Path = PreviewFolder };
            PublishTitle();
            _scan = GalleryScan.Of(state switch { "empty" => GalleryStatus.NoImages, "missing" => GalleryStatus.NotFound, _ => GalleryStatus.Failed });
            _pictures = [];
            Lay();
            return;
        }

        // [S-769] The hover bar, as the pointer over a slideshow shows it.
        _hovering = state == "controls";

        if (state == "sticker")
        {
            // [S-774] [S-775] A cut-out PNG (the app's own logo), turned and outlined.
            var logo = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "themia-logo.png");
            _watch?.Dispose();
            _watch = null;
            _config = new GalleryConfig { Source = GallerySource.File, FilePath = logo, Rotation = -15, Outline = true, OutlineColor = StickerRules.DefaultOutlineColor, OutlineWidth = 6 };
            PublishTitle();
            _scan = new GalleryScan(GalleryStatus.Ok, [logo]);
            Select(keepPath: null, index: 0);
            _slideTimer?.Stop();
            Lay();
            await Task.Delay(2500).ConfigureAwait(true);
            return;
        }

        // [S-6006] "sample": the gallery's sample, in whatever style the render chose (--style).
        if (state == "sample")
        {
            await ShowSampleAsync().ConfigureAwait(true);
            return;
        }

        var (source, layout) = state switch
        {
            "slideshow" or "controls" => (GallerySource.Folder, GalleryStyle.Slideshow),
            "grid" => (GallerySource.Folder, GalleryStyle.Grid),
            "scatter" => (GallerySource.Folder, GalleryStyle.Scatter),
            "mosaic" => (GallerySource.Folder, GalleryStyle.Mosaic),
            "file" => (GallerySource.File, GalleryStyle.Slideshow),
            _ => ((GallerySource?)null, GalleryStyle.Slideshow),
        };

        if (source is not { } chosen)
        {
            return;
        }

        // [S-2056] The sample day's pictures, never a picture of this PC.
        await ShowSamplePicturesAsync(chosen, layout).ConfigureAwait(true);
    }
}
#endif
