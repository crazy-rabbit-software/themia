namespace Themia.Core.Widgets;

/// <summary>
/// One item in a folder listing, as much of it as a widget row draws.
/// <para>
/// A plain value rather than a <c>FileSystemInfo</c>: the sorting and formatting rules are pure
/// functions that have to be testable without touching a disk, and every field here comes from the
/// find data the enumeration already returned, so building one costs no extra I/O.
/// </para>
/// </summary>
/// <param name="Name">The file or directory name, without its parent path.</param>
/// <param name="FullPath">The path to hand the shell when the row is opened.</param>
/// <param name="IsDirectory">Whether the entry is a directory.</param>
/// <param name="Size">Length in bytes. Always zero for a directory, which has no size to show.</param>
/// <param name="Modified">
/// Last write time, in the same frame as the clock it will be compared against — the view passes
/// local wall-clock times for both, so the formatting stays a pure function of its arguments.
/// </param>
/// <param name="Hidden">Whether Windows marks the entry hidden or system.</param>
/// <param name="Attributes">
/// The raw Win32 attribute bits from the same find data, kept for the cloud badge ([S-1324]): the
/// placeholder and pin flags a sync engine sets cost nothing to read here and a disk round trip later.
/// </param>
public readonly record struct FolderEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Size,
    DateTimeOffset Modified,
    bool Hidden,
    int Attributes = 0);

/// <summary>
/// What came back from trying to list a folder.
/// <para>
/// Every one of these is a state the widget has a sentence for. A folder widget that cannot read
/// its folder must say which of these happened, because each one needs a different thing from the
/// user — fix the path, grant access, pick a folder rather than a file — and a blank tile tells
/// them none of it.
/// </para>
/// </summary>
public enum FolderListingStatus
{
    /// <summary>The listing is in flight. A slow or disconnected network path can sit here.</summary>
    Loading = 0,

    /// <summary>Read, and there is something to show.</summary>
    Ok = 1,

    /// <summary>Read, and there is nothing in it.</summary>
    Empty = 2,

    /// <summary>The configured path never reached the filesystem, because it cannot be a path.</summary>
    InvalidPath = 3,

    /// <summary>Nothing exists at that path.</summary>
    NotFound = 4,

    /// <summary>Something exists there, but it is a file.</summary>
    NotAFolder = 5,

    /// <summary>Windows refused the read.</summary>
    AccessDenied = 6,

    /// <summary>The read failed some other way. Details are in the log.</summary>
    Failed = 7,
}

/// <summary>The result of one listing: a state, and the rows that go with it.</summary>
/// <param name="Status">Which of the named states the listing ended in.</param>
/// <param name="Entries">The rows to draw, already filtered, sorted and capped. Empty unless <see cref="FolderListingStatus.Ok"/>.</param>
public sealed record FolderSnapshot(FolderListingStatus Status, IReadOnlyList<FolderEntry> Entries)
{
    /// <summary>A snapshot carrying no rows, for every state that has none.</summary>
    public static FolderSnapshot Of(FolderListingStatus status) => new(status, []);
}
