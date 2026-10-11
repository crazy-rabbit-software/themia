using Themia.App.Services.Web;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>What one scan was asked for. A value, so a background task can capture it.</summary>
/// <param name="Path">An already-validated, fully qualified folder path.</param>
/// <param name="Recursive">Walk the folders below it too.</param>
/// <param name="Shuffle">Take a random selection rather than the first by name.</param>
/// <param name="Seed">The shuffle's seed, rolled by the caller so the choice is reproducible here.</param>
/// <param name="MaxImages">How many to keep, already clamped by the config.</param>
internal readonly record struct GalleryRequest(
    string Path,
    bool Recursive,
    bool Shuffle,
    int Seed,
    int MaxImages);

/// <summary>
/// Finding the pictures in a folder.
/// <para>
/// Every method here is <b>background work</b>, for the same reason the folder widget's listing is:
/// walking a directory is I/O, and a recursive walk of a photo library on a share whose server has
/// gone away can sit for the better part of a minute. On the UI thread that is the whole desktop
/// frozen behind a picture frame.
/// </para>
/// <para>
/// Nothing here decodes anything. This produces paths; turning one into pixels is
/// <see cref="ImageLoader"/>'s job and happens per tile, at the size the tile actually is.
/// </para>
/// </summary>
internal static class ImageGallery
{
    private const string Scope = "gallery";

    /// <summary>
    /// The most files one scan will look at.
    /// <para>
    /// A picture library is exactly the kind of folder that holds six figures of files, and the
    /// widget shows at most sixty of them. Past this the walk stops and says so in the log: the
    /// selection is then drawn from the first <see cref="MaxScanned"/> the filesystem returned,
    /// which is a real limitation, but a bounded scan beats an unbounded one nobody is watching.
    /// </para>
    /// </summary>
    private const int MaxScanned = 20_000;

    /// <summary>
    /// How deep a recursive scan goes. A picture folder is nested a few levels at most, and an
    /// unbounded depth is how a widget pointed at a drive root walks the whole machine.
    /// </summary>
    private const int MaxDepth = 8;

    /// <summary>
    /// Scan a folder. Never throws: every failure comes back as a named
    /// <see cref="GalleryStatus"/> the widget has a sentence for.
    /// </summary>
    public static GalleryScan Read(GalleryRequest request, CancellationToken token)
    {
        try
        {
            // The order matters, as it does for the folder widget: "it is a file" and "it is not
            // there" want different sentences, and Directory.Exists answers false to both.
            if (!Directory.Exists(request.Path))
            {
                return GalleryScan.Of(File.Exists(request.Path)
                    ? GalleryStatus.NotAFolder
                    : GalleryStatus.NotFound);
            }

            var found = Scan(request, token);
            var chosen = ImageFiles.Choose(found, request.Shuffle, request.Seed, request.MaxImages);

            return chosen.Count == 0
                ? GalleryScan.Of(GalleryStatus.NoImages)
                : new GalleryScan(GalleryStatus.Ok, chosen);
        }
        catch (OperationCanceledException)
        {
            // The view moved on. Not a failure, and nothing will look at this result.
            return GalleryScan.Of(GalleryStatus.Loading);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warn(Scope, $"access denied scanning '{request.Path}'", ex);
            return GalleryScan.Of(GalleryStatus.AccessDenied);
        }
        catch (DirectoryNotFoundException ex)
        {
            // Racing a delete between the Exists check and the walk.
            Log.Warn(Scope, $"'{request.Path}' disappeared while it was being scanned", ex);
            return GalleryScan.Of(GalleryStatus.NotFound);
        }
        catch (IOException ex)
        {
            Log.Error(Scope, $"could not scan '{request.Path}'", ex);
            return GalleryScan.Of(GalleryStatus.Failed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            // The path passed validation but the framework still refused it, which means the two
            // disagree about what a path is. Worth saying out loud.
            Log.Error(Scope, $"'{request.Path}' was refused as a path", ex);
            return GalleryScan.Of(GalleryStatus.InvalidPath);
        }
    }

    /// <summary>
    /// [S-761] Check the one picture the file source shows. Background work for the same reason a
    /// scan is: an existence check on a dead share blocks. Never throws.
    /// </summary>
    public static GalleryScan ReadFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return ImageFiles.IsImage(path)
                    ? new GalleryScan(GalleryStatus.Ok, [path])
                    : GalleryScan.Of(GalleryStatus.NotAPicture);
            }

            // A folder typed into the picture box is "not a picture", which is truer than "gone".
            return GalleryScan.Of(Directory.Exists(path) ? GalleryStatus.NotAPicture : GalleryStatus.NotFound);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warn(Scope, $"access denied checking '{path}'", ex);
            return GalleryScan.Of(GalleryStatus.AccessDenied);
        }
        catch (IOException ex)
        {
            Log.Error(Scope, $"could not check '{path}'", ex);
            return GalleryScan.Of(GalleryStatus.Failed);
        }
    }

    /// <summary>
    /// [S-762] Ask Bing for today's photo. The reply is parsed through the source-generated contract
    /// and its one path checked to be on Bing's own host before anything decodes it. Never throws for
    /// a network reason; cancellation propagates.
    /// </summary>
    public static async Task<GalleryScan> ReadBingAsync(CancellationToken token)
    {
        var (status, wire, detail) = await WebFetch
            .GetJsonAsync(BingPhoto.Endpoint, BingWireContext.Default.BingArchiveWire, token)
            .ConfigureAwait(false);

        if (status == FetchStatus.Offline)
        {
            return GalleryScan.Of(GalleryStatus.Offline);
        }

        if (status != FetchStatus.Ok)
        {
            Log.Warn(Scope, $"photo of the day: {status} {detail}");
            return GalleryScan.Of(GalleryStatus.Unavailable);
        }

        if (BingPhoto.From(wire) is not { } photo)
        {
            Log.Warn(Scope, "photo of the day: the reply held no usable photo");
            return GalleryScan.Of(GalleryStatus.Unavailable);
        }

        return new GalleryScan(GalleryStatus.Ok, [photo.Image.AbsoluteUri], photo.Credit);
    }

    private static List<string> Scan(GalleryRequest request, CancellationToken token)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = request.Recursive,
            MaxRecursionDepth = MaxDepth,
            ReturnSpecialDirectories = false,

            // Hidden and system files are skipped here rather than offered as a setting: thumbnail
            // caches, `desktop.ini` and the junk a photo editor leaves behind are all hidden, and
            // none of them is a picture anybody put in the folder to look at.
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,

            // A subtree the user cannot read is ordinary inside a profile. Skipping it beats
            // failing the whole scan.
            IgnoreInaccessible = true,
        };

        var found = new List<string>();
        var scanned = 0;

        // foreach disposes the enumerator, and with it the Win32 find handle, on every path out of
        // this loop — including the throw from ThrowIfCancellationRequested.
        foreach (var file in new DirectoryInfo(request.Path).EnumerateFiles("*", options))
        {
            // Between entries is the only place cancellation can be checked: a MoveNext blocked
            // inside FindNextFile on a dead share cannot be interrupted, so a disconnected path is
            // abandoned rather than stopped. The view discards the result either way.
            token.ThrowIfCancellationRequested();

            if (scanned >= MaxScanned)
            {
                Log.Warn(Scope, $"'{request.Path}' holds more than {MaxScanned} files; the scan is truncated");
                break;
            }

            scanned++;

            // By name, from the find data already in hand. No second call per file: that is what
            // turns a scan into a per-file stat storm on a network share.
            if (ImageFiles.IsImage(file.Name))
            {
                found.Add(file.FullName);
            }
        }

        return found;
    }
}
