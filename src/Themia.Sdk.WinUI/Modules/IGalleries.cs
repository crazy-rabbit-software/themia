using Microsoft.UI.Xaml.Media.Imaging;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-3410] [S-3411] Where every gallery widget gets its pictures (<see cref="IWidgetHost.Gallery"/>): the
/// host's one service, which reads each source once for every widget showing it (a folder's watcher, the
/// photo of the day's download), decodes each picture once at the size it is drawn, and remembers each
/// widget's place across view switches. The reads, the cache and the decoding stay host code.
/// </summary>
public interface IGalleries
{
    /// <summary>
    /// Watch a source. The current read, when there is one, is <see cref="IGalleryWatch.Scan"/> at once;
    /// every read after this is <paramref name="onUpdate"/>, on the UI thread, with why it was read.
    /// Never calls back from inside this call. Dispose to let go.
    /// </summary>
    /// <param name="refresh">The widget's refresh interval ([S-772]); the shortest of a source's widgets wins.</param>
    IGalleryWatch Watch(GallerySourceKey key, TimeSpan refresh, Action<GalleryRefresh> onUpdate);

    /// <summary>[S-3412] Each widget's place (its slide, its seed, paused), kept by widget id for the session.</summary>
    GalleryPositions Positions { get; }

    /// <summary>[S-3411] Decode one picture at <paramref name="pixelWidth"/> physical pixels, or null when it will not decode. Dispose to unpin it.</summary>
    Task<IGalleryPicture?> LoadPictureAsync(string path, double pixelWidth, CancellationToken token);
}

/// <summary>One widget's hold on a gallery source. Dispose to let go, once.</summary>
public interface IGalleryWatch : IDisposable
{
    /// <summary>The widget's refresh interval.</summary>
    TimeSpan Refresh { get; }

    /// <summary>What the source read last (Loading before its first read is back).</summary>
    GalleryScan Scan { get; }

    /// <summary>[S-773] Read the source now.</summary>
    void RefreshNow();
}

/// <summary>[S-3411] A decoded picture a frame is showing: holding it pins it in the shared cache; disposing lets go, exactly once.</summary>
public interface IGalleryPicture : IDisposable
{
    BitmapImage Image { get; }
}
