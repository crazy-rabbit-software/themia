using System.Globalization;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>
/// The strings a folder row and a folder's empty state are drawn from.
/// <para>
/// Here rather than in the view for the reason every other rule in this widget is: the view cannot
/// be run in the test suite, and "what does the tile say when the path is a file" is exactly the
/// kind of thing that quietly becomes a blank tile.
/// </para>
/// </summary>
public static class FolderFormatting
{
    /// <summary>
    /// An entry's size, or nothing at all for a directory.
    /// <para>
    /// Explorer leaves a folder's size column blank rather than printing "0 B", and it is right to:
    /// a folder's size is the recursive walk nobody asked this widget to do, and "0 B" would be a
    /// wrong answer rather than an absent one.
    /// </para>
    /// </summary>
    public static string Size(FolderEntry entry) => entry.IsDirectory ? string.Empty : Formatting.Bytes(entry.Size);

    /// <summary>
    /// When an entry was last written, at the precision that is still worth the column width:
    /// the time for something touched today, the day and month within the year, the year beyond it.
    /// </summary>
    /// <param name="when">The entry's last write time.</param>
    /// <param name="now">
    /// The clock to read "today" from. Both arguments must be in the same frame — the view passes
    /// local wall-clock times for both — so this stays a pure function of what it is given rather
    /// than of the machine's time zone.
    /// </param>
    public static string Modified(DateTimeOffset when, DateTimeOffset now) => Modified(Strings.Current, when, now);

    /// <summary><see cref="Modified(DateTimeOffset, DateTimeOffset)"/> in <paramref name="locale"/>.</summary>
    public static string Modified(Locale locale, DateTimeOffset when, DateTimeOffset now)
    {
        // A file with no usable timestamp comes back as the FILETIME epoch rather than as an error.
        // "1 Jan 1601" in a date column is noise pretending to be information.
        if (when.Year <= 1601)
        {
            return string.Empty;
        }

        var day = DateOnly.FromDateTime(when.DateTime);
        var today = DateOnly.FromDateTime(now.DateTime);

        if (day == today)
        {
            return LocaleFormat.Time(locale, when.DateTime);
        }

        return day.Year == today.Year
            ? Formatting.ShortDate(locale, day)
            : LocaleFormat.Date(locale, day, DateStyle.Medium);
    }

    /// <summary>
    /// What the tile says when it has no rows to show. Never empty: a widget that shows nothing and
    /// explains nothing is indistinguishable from one that is broken.
    /// </summary>
    /// <param name="status">The state the listing ended in.</param>
    /// <param name="problem">
    /// Why the path was refused, when that is the state. Each reason needs a different correction
    /// from the user, so they do not share a sentence.
    /// </param>
    public static string StatusMessage(FolderListingStatus status, FolderPathProblem problem = FolderPathProblem.None)
        => StatusMessage(Strings.Current, status, problem);

    /// <summary><see cref="StatusMessage(FolderListingStatus, FolderPathProblem)"/> in <paramref name="locale"/>.</summary>
    public static string StatusMessage(Locale locale, FolderListingStatus status, FolderPathProblem problem = FolderPathProblem.None)
        => status switch
        {
            FolderListingStatus.Ok => string.Empty,
            // A refused path with no recorded reason should not happen, but "no rows and no
            // sentence" is the one outcome this whole function exists to rule out.
            FolderListingStatus.InvalidPath when problem != FolderPathProblem.None => PathMessage(locale, problem),
            _ => Strings.Get(locale, status switch
            {
                FolderListingStatus.Loading => Str.WidgetsB_Folder_Status_Loading,
                FolderListingStatus.Empty => Str.WidgetsB_Folder_Status_Empty,
                FolderListingStatus.NotFound => Str.WidgetsB_Folder_Status_NotFound,
                FolderListingStatus.NotAFolder => Str.WidgetsB_Folder_Status_NotAFolder,
                FolderListingStatus.AccessDenied => Str.WidgetsB_Folder_Status_AccessDenied,
                FolderListingStatus.Failed => Str.WidgetsB_Folder_Status_Failed,
                FolderListingStatus.InvalidPath => Str.WidgetsB_Folder_Status_InvalidPath,
                _ => Str.WidgetsB_Folder_Status_Unreadable,
            }),
        };

    /// <summary>The one-line reason shown under the path box in settings, or nothing when it is fine.</summary>
    public static string PathMessage(FolderPathProblem problem) => PathMessage(Strings.Current, problem);

    /// <summary><see cref="PathMessage(FolderPathProblem)"/> in <paramref name="locale"/>.</summary>
    public static string PathMessage(Locale locale, FolderPathProblem problem) => problem == FolderPathProblem.None
        ? string.Empty
        : Strings.Get(locale, problem switch
        {
            FolderPathProblem.Empty => Str.WidgetsB_Folder_Path_Empty,
            FolderPathProblem.InvalidCharacters => Str.WidgetsB_Folder_Path_InvalidCharacters,
            FolderPathProblem.NotRooted => Str.WidgetsB_Folder_Path_NotRooted,
            FolderPathProblem.TooLong => Str.WidgetsB_Folder_Path_TooLong,
            _ => Str.WidgetsB_Folder_Path_Unreadable,
        });

    /// <summary>[S-1322] The Items column: a folder's entry count, capped where counting stops ("999+").</summary>
    public static string ItemCount(int? count, int cap) => count switch
    {
        null => string.Empty,
        var n when n >= cap => $"{(cap - 1).ToString(CultureInfo.InvariantCulture)}+",
        var n => n.Value.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// [S-1322] The Type column when the shell has no friendly name: "File folder", "TXT File", or
    /// "File" for a name without an extension — Explorer's own fallbacks.
    /// </summary>
    public static string TypeFallback(string name, bool isDirectory) => TypeFallback(Strings.Current, name, isDirectory);

    /// <summary><see cref="TypeFallback(string, bool)"/> in <paramref name="locale"/>.</summary>
    public static string TypeFallback(Locale locale, string name, bool isDirectory)
    {
        if (isDirectory)
        {
            return Strings.Get(locale, Str.WidgetsB_Folder_Type_Folder);
        }

        var dot = name.LastIndexOf('.');
        return dot > 0 && dot < name.Length - 1
            ? Strings.Format(locale, Str.WidgetsB_Folder_Type_Extension, ("ext", name[(dot + 1)..].ToUpperInvariant()))
            : Strings.Get(locale, Str.WidgetsB_Folder_Type_File);
    }

    /// <summary>Extensions whose every file carries its own icon (a program's, a shortcut's target's).</summary>
    private static readonly HashSet<string> OwnIcon = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".lnk", ".url", ".ico", ".cur", ".ani", ".msc", ".scr", ".appref-ms", ".cpl", ".website",
    };

    /// <summary>
    /// [S-1324] The icon cache key for an entry: most files look like every other file of their type,
    /// so ".txt" is extracted once, not once per file; programs, shortcuts and icon files, and folders
    /// Windows marks as customised (read-only or system: Documents, Downloads, a desktop.ini icon), are
    /// keyed by their own path.
    /// </summary>
    public static string IconKey(FolderEntry entry)
    {
        const int readOnly = 0x1;
        const int system = 0x4;

        if (entry.IsDirectory)
        {
            return (entry.Attributes & (readOnly | system)) != 0 ? entry.FullPath : "<folder>";
        }

        var dot = entry.Name.LastIndexOf('.');
        var extension = dot > 0 ? entry.Name[dot..] : string.Empty;

        return extension.Length == 0 ? "<file>"
            : OwnIcon.Contains(extension) ? entry.FullPath
            : extension.ToLowerInvariant();
    }
}
