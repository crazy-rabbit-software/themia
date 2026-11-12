using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-6590] [S-6592] The card's background image: under the fills, the header and the view, across
/// the whole card and clipped to its corners. The fills then paint only what <see cref="BackgroundImage.Over"/>
/// says: a colour of the widget's own as a tint, else nothing, so the picture is the card.
/// </summary>
internal sealed partial class WidgetCard
{
    /// <summary>The smallest and largest width an image is decoded to, in pixels.</summary>
    private const int ImageDecodeMin = 640;
    private const int ImageDecodeMax = 2560;

    /// <summary>The picture, as the bottom layer of the card's content.</summary>
    private readonly Border _image = new() { IsHitTestVisible = false };

    /// <summary>The path the layer was last asked to show, so a design pass that did not change it loads nothing.</summary>
    private string? _imagePath;

    /// <summary>Counts loads, so one that finishes after the path changed again is dropped.</summary>
    private int _imageLoad;

    /// <summary>Whether a picture is drawn now (a path whose file loaded).</summary>
    private bool HasImage => _image.Background is not null;

    /// <summary>The fill the body and the header paint: the card's own, or over a picture only its tint ([S-6592]).</summary>
    private CardFill PaintedFillNow() => BackgroundImage.Over(CardFillNow(), HasImage);

    /// <summary>
    /// [S-6590] Show the widget's image, loading it when its path changed. Nothing to show clears the
    /// layer at once; a load runs off the design pass and repaints the card when it lands.
    /// </summary>
    private void ApplyImage(double radius)
    {
        _image.CornerRadius = new CornerRadius(radius);

        var path = Model.BackgroundImage;
        if (string.Equals(path, _imagePath, StringComparison.Ordinal))
        {
            return;
        }

        _imagePath = path;
        var load = ++_imageLoad;
        _image.Background = null;

        if (!string.IsNullOrEmpty(path))
        {
            _ = LoadImageAsync(path, load);
        }
    }

    /// <summary>
    /// Decode the file near the card's own pixel width (twice it, within bounds), so a photo of many
    /// megapixels does not sit in memory at full size behind a small card. A file that is not there
    /// is drawn as no image and logged: the path stays, so the picture is back when the file is.
    /// </summary>
    private async Task LoadImageAsync(string path, int load)
    {
        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenReadAsync();

            var scale = XamlRoot?.RasterizationScale ?? 1.0;
            var width = (int)Math.Clamp(ActualWidth * scale * 2, ImageDecodeMin, ImageDecodeMax);
            var bitmap = new BitmapImage { DecodePixelWidth = width, DecodePixelType = DecodePixelType.Physical };
            await bitmap.SetSourceAsync(stream);

            if (load != _imageLoad)
            {
                return; // ignore: the path changed while this one loaded.
            }

            _image.Background = new ImageBrush
            {
                ImageSource = bitmap,
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
            };
            ApplyDesign();
        }
        catch (FileNotFoundException)
        {
            Log.Warn(Scope, $"widget {Model.Id}: background image '{Path.GetFileName(path)}' is not there; drawn without it");
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warn(Scope, $"widget {Model.Id}: background image '{Path.GetFileName(path)}' cannot be read: {ex.Message}");
        }
        catch (Exception ex)
        {
            // A file that is not a picture Windows can decode, or a drive that went away mid-read:
            // the card draws its fill alone, and nothing above this may see it.
            Log.Error(Scope, $"widget {Model.Id}: background image '{Path.GetFileName(path)}' could not be shown", ex);
        }
    }
}
