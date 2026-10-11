using Microsoft.UI.Xaml.Media.Imaging;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Windows.Storage;

namespace Themia.App.Services;

/// <summary>
/// A decoded picture a frame is showing. Holding it pins the picture in the shared cache
/// ([S-3411]); disposing lets go, exactly once.
/// </summary>
internal sealed class PictureHandle : IDisposable
{
    private ImageLoader? _owner;

    public PictureHandle(ImageLoader owner, PictureKey key, BitmapImage image)
    {
        _owner = owner;
        Key = key;
        Image = image;
    }

    public PictureKey Key { get; }

    public BitmapImage Image { get; }

    public void Dispose()
    {
        var owner = _owner;
        _owner = null;
        owner?.Release(Key);
    }
}

/// <summary>
/// Turning a path into pixels, at the size it will actually be drawn, without stopping the desktop,
/// once for every gallery widget on every display ([S-3411]): owned by <see cref="GalleryService"/>
/// for the app's lifetime, so a view switch and back finds its pictures decoded.
/// <para>
/// Decoding a photograph is the most expensive thing any widget in this app does, and it is
/// expensive twice: the file has to come off the disk, and the JPEG has to be turned into a bitmap.
/// Doing either on the UI thread freezes every widget on every monitor.
/// </para>
/// <list type="number">
/// <item>
/// <b>Decode to the size shown.</b> <see cref="BitmapImage.DecodePixelWidth"/> makes WIC scale
/// during decode, so a 24-megapixel photograph in a 300px tile costs a 300px bitmap, not ninety
/// megabytes. Widths are bucketed (<see cref="DecodeWidth"/>), and a larger decode already held
/// serves a smaller ask.
/// </item>
/// <item>
/// <b>Bound what is held</b> by bytes (<see cref="PictureCache{TValue}"/>): what frames show is
/// pinned, and besides it only <see cref="PictureBudget.IdleAllowance"/>, least recently used out.
/// </item>
/// <item>
/// <b>Bound what is in flight.</b> Two decodes at once, across every widget: a grid of twenty-four
/// tiles asking at once would otherwise open twenty-four handles at a spinning disk or a share.
/// </item>
/// </list>
/// <para>
/// UI thread, and <i>asynchronous</i> rather than backgrounded: <see cref="BitmapImage"/> is a
/// dependency object and cannot be created anywhere else, and both the file open and the decode
/// happen off-thread inside the WinRT calls it awaits.
/// </para>
/// </summary>
internal sealed class ImageLoader
{
    private const string Scope = "gallery-image";

    /// <summary>At most two decodes at a time; the rest wait here rather than at the filesystem.</summary>
    private readonly SemaphoreSlim _gate = new(2, 2);

    private readonly PictureCache<BitmapImage> _cache = new();

    /// <summary>Pictures held (diagnostics: the switch check).</summary>
    public int Count => _cache.Count;

    /// <summary>Their estimated decoded size.</summary>
    public long Bytes => _cache.Bytes;

    /// <summary>
    /// The picture for a path at roughly <paramref name="pixelWidth"/> physical pixels, pinned until
    /// the handle is disposed, or null when it could not be read or decoded. A picture already held
    /// comes back without an await, so a widget built again draws it in the same pass. Never throws
    /// for one bad file: a corrupt picture costs that one tile, not the widget.
    /// </summary>
    public async Task<PictureHandle?> LoadAsync(string path, double pixelWidth, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var width = DecodeWidth.Bucket(pixelWidth);

        if (Hold(path, width) is { } held)
        {
            return held;
        }

        // [S-762] The photo of the day is an https address the read has already pinned to Bing's
        // host. It is handed to the bitmap as a URI rather than downloaded here: the platform fetches
        // it off-thread, decodes it at the asked width, and keeps it in the system's HTTP cache.
        if (Uri.TryCreate(path, UriKind.Absolute, out var remote) && remote.Scheme == Uri.UriSchemeHttps)
        {
            var key = new PictureKey(path, width);
            _cache.Add(key, Remote(remote, key), PictureBudget.Estimate(width, 16, 9));
            return Hold(path, width);
        }

        await _gate.WaitAsync(token).ConfigureAwait(true);

        try
        {
            // Another frame may have asked for the same picture while this one queued.
            if (Hold(path, width) is { } queued)
            {
                return queued;
            }

            token.ThrowIfCancellationRequested();

            var bitmap = await DecodeAsync(path, width).ConfigureAwait(true);

            if (bitmap is null)
            {
                return null;
            }

            _cache.Add(new PictureKey(path, width), bitmap, PictureBudget.Estimate(width, bitmap.PixelWidth, bitmap.PixelHeight));

            // Held even when this frame has moved on meanwhile: the decode is done, and the next ask
            // (the slide coming round again, the widget built again) finds it.
            token.ThrowIfCancellationRequested();
            return Hold(path, width);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>[S-3411] Drop every picture nobody shows (the desktop was suspended).</summary>
    public void Trim()
    {
        var dropped = _cache.Trim();

        if (dropped > 0)
        {
            Log.Debug(Scope, $"released {dropped} decoded pictures nobody shows");
        }
    }

    /// <summary>[S-3413] Drop the pictures of a released source that nobody shows.</summary>
    public void Forget(Func<string, bool> path) => _cache.RemoveUnpinned(path);

    public void Clear() => _cache.Clear();

    internal void Release(PictureKey key) => _cache.Unpin(key);

    private PictureHandle? Hold(string path, int width)
    {
        if (!_cache.TryGet(path, width, out var key, out var bitmap))
        {
            return null;
        }

        _cache.Pin(key);
        return new PictureHandle(this, key, bitmap);
    }

    /// <summary>
    /// Open the file and decode it, both through WinRT so both happen off this thread. Returns null
    /// and logs for anything that goes wrong with one file.
    /// </summary>
    private static async Task<BitmapImage?> DecodeAsync(string path, int width)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenReadAsync();

            var bitmap = new BitmapImage
            {
                // Physical, not logical: the caller has already multiplied by the display's scale.
                DecodePixelType = DecodePixelType.Physical,
                DecodePixelWidth = width,
            };

            // Awaited rather than fired off, so the stream is disposed only once WIC has read it.
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or System.Runtime.InteropServices.COMException)
        {
            // A file that went away, one this machine has no codec for, or one that is simply not
            // the picture its extension claims. One tile's problem, said out loud.
            Log.Warn(Scope, $"could not decode '{path}'", ex);
            return null;
        }
    }

    private BitmapImage Remote(Uri uri, PictureKey key)
    {
        var bitmap = new BitmapImage
        {
            DecodePixelType = DecodePixelType.Physical,
            DecodePixelWidth = key.Width,
        };

        // The real size, once it is known, so the budget counts it.
        bitmap.ImageOpened += (_, _) =>
            _cache.Resize(key, PictureBudget.Estimate(key.Width, bitmap.PixelWidth, bitmap.PixelHeight));

        // Logged, not thrown, and forgotten: the frame stays empty and the next refresh asks again
        // rather than finding the failed bitmap in the cache.
        bitmap.ImageFailed += (_, args) =>
        {
            Log.Warn(Scope, $"could not load the photo of the day: {args.ErrorMessage}");
            _cache.Remove(key);
        };
        bitmap.UriSource = uri;
        return bitmap;
    }
}
