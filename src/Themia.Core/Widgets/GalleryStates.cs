using Themia.Core.Model;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>
/// How trying to read a folder of pictures ended.
/// <para>
/// Every one of these is a state the widget has its own sentence for. A photo frame showing a blank
/// card is indistinguishable from a broken one, and each of these needs a different thing from the
/// reader — choose a folder, fix the path, grant access, put some pictures in it, or simply wait.
/// </para>
/// </summary>
public enum GalleryStatus
{
    /// <summary>The scan is in flight. A large tree or a disconnected share can sit here.</summary>
    Loading = 0,

    /// <summary>Read, and there are pictures.</summary>
    Ok = 1,

    /// <summary>No folder has been chosen yet.</summary>
    NoFolder = 2,

    /// <summary>The configured path cannot be a path at all, so nothing was opened.</summary>
    InvalidPath = 3,

    /// <summary>Nothing exists at that path any more.</summary>
    NotFound = 4,

    /// <summary>Something is there, but it is a file rather than a folder.</summary>
    NotAFolder = 5,

    /// <summary>Windows refused the read.</summary>
    AccessDenied = 6,

    /// <summary>Read, and there is nothing in it this app can decode.</summary>
    NoImages = 7,

    /// <summary>The read failed some other way. Details are in the log.</summary>
    Failed = 8,

    /// <summary>[S-761] The file source has no picture chosen yet.</summary>
    NoFile = 9,

    /// <summary>[S-761] The chosen file is not a picture this app can draw (or is a folder).</summary>
    NotAPicture = 10,

    /// <summary>[S-762] This machine has no network, so the photo of the day cannot be asked for.</summary>
    Offline = 11,

    /// <summary>[S-762] Bing did not answer, or answered with something that is not a photo.</summary>
    Unavailable = 12,
}

/// <summary>What one scan found: a named state, and the pictures that go with it.</summary>
/// <param name="Status">Which state the scan ended in.</param>
/// <param name="Paths">
/// The chosen pictures, already filtered, ordered and capped. Empty unless Ok. Fully qualified local
/// paths, except for the photo of the day, whose one entry is a validated https address.
/// </param>
/// <param name="Credit">[S-3112] The photo of the day's credit, for its hover tooltip. Null for local pictures.</param>
public sealed record GalleryScan(GalleryStatus Status, IReadOnlyList<string> Paths, BingCredit? Credit = null)
{
    /// <summary>A result carrying no pictures, for every state that has none.</summary>
    public static GalleryScan Of(GalleryStatus status) => new(status, []);
}

/// <summary>The sentences the gallery shows instead of a picture.</summary>
public static class GalleryFormatting
{
    /// <summary>
    /// What to say about a scan that produced nothing. Each names the state and, where there is one,
    /// the thing to do about it — a message that only says something went wrong leaves the reader
    /// with the same blank card they started with.
    /// </summary>
    public static string StatusMessage(GalleryStatus status, FolderPathProblem problem, GallerySource source = GallerySource.Folder)
        => StatusMessage(Strings.Current, status, problem, source);

    /// <summary><see cref="StatusMessage(GalleryStatus, FolderPathProblem, GallerySource)"/> in <paramref name="locale"/>.</summary>
    /// <remarks>Nothing chosen yet says nothing: the card shows its Set up button there ([S-1862]).</remarks>
    public static string StatusMessage(Locale locale, GalleryStatus status, FolderPathProblem problem, GallerySource source = GallerySource.Folder)
        => status is GalleryStatus.NoFolder or GalleryStatus.NoFile ? string.Empty
            : status == GalleryStatus.InvalidPath
            ? FolderFormatting.PathMessage(locale, problem)
            : Strings.Get(locale, status switch
            {
                GalleryStatus.Loading when source == GallerySource.Bing => Str.WidgetsB_Gallery_Status_LoadingPhoto,
                GalleryStatus.Loading => Str.WidgetsB_Gallery_Status_Loading,
                GalleryStatus.NotFound when source == GallerySource.File => Str.WidgetsB_Gallery_Status_PictureGone,
                GalleryStatus.NotFound => Str.WidgetsB_Gallery_Status_FolderGone,
                GalleryStatus.NotAPicture => Str.WidgetsB_Gallery_Status_NotAPicture,
                GalleryStatus.Offline => Str.WidgetsB_Gallery_Status_Offline,
                GalleryStatus.Unavailable => Str.WidgetsB_Gallery_Status_Unavailable,
                GalleryStatus.NotAFolder => Str.WidgetsB_Gallery_Status_NotAFolder,
                GalleryStatus.AccessDenied when source == GallerySource.File => Str.WidgetsB_Gallery_Status_PictureDenied,
                GalleryStatus.AccessDenied => Str.WidgetsB_Gallery_Status_FolderDenied,
                GalleryStatus.Failed when source == GallerySource.File => Str.WidgetsB_Gallery_Status_PictureFailed,
                GalleryStatus.Failed => Str.WidgetsB_Gallery_Status_FolderFailed,

                // Ok with nothing in it is the same emptiness as NoImages, and saying "no pictures" is
                // truer than saying nothing.
                _ => Str.WidgetsB_Gallery_Status_NoImages,
            });
}
