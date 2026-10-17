using System.Globalization;
using System.Text.RegularExpressions;

namespace Themia.Core.Diagnostics;

/// <summary>One file in the log folder, as the sweep sees it.</summary>
public readonly record struct LogSegment(string Name, long Bytes, DateTimeOffset LastWrite);

/// <summary>
/// [S-980] The rotating log folder's rules: when the current file is rotated, what an archive is
/// called, and which archives are deleted.
/// <para>
/// The bound is the point. The original's first version never deleted a rotated segment and a
/// real machine collected 628 files / 2.8 GB; the WinUI port's first version appended to one file
/// forever. Here the folder holds at most <see cref="MaxArchives"/> + 1 files of at most
/// <see cref="MaxFileBytes"/> each (one record over: a record is never split), and nothing older
/// than <see cref="MaxAge"/> survives a rotation or a start.
/// </para>
/// </summary>
public static partial class LogFileRules
{
    /// <summary>The file being written. Kept stable so "see themia.log" stays true.</summary>
    public const string CurrentName = "themia.log";

    /// <summary>A file is rotated before it would pass this.</summary>
    public const long MaxFileBytes = 2 * 1024 * 1024;

    /// <summary>Rotated files kept beside the current one: 20 MB at most in all.</summary>
    public const int MaxArchives = 9;

    /// <summary>The longest one record may be once formatted. A log line is not a data dump.</summary>
    public const int MaxRecordChars = 32 * 1024;

    /// <summary>An archive older than this is deleted even when the count allows it.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(14);

    /// <summary>
    /// Whether to rotate before writing <paramref name="incomingBytes"/> to a file of
    /// <paramref name="currentBytes"/>. An empty file is never rotated, so one oversized record
    /// cannot produce an endless chain of empty archives.
    /// </summary>
    public static bool ShouldRotate(long currentBytes, long incomingBytes)
        => currentBytes > 0 && currentBytes + incomingBytes > MaxFileBytes;

    /// <summary>
    /// The name a rotated file gets: its rotation time in UTC, sortable, plus a counter when two
    /// rotations land in the same millisecond (two processes, a burst).
    /// </summary>
    public static string ArchiveName(DateTimeOffset at, int attempt = 0)
    {
        var stamp = at.UtcDateTime.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        return attempt <= 0 ? $"themia-{stamp}.log" : $"themia-{stamp}-{attempt.ToString(CultureInfo.InvariantCulture)}.log";
    }

    /// <summary>
    /// Whether a file in the folder is one of ours to prune. Only names <see cref="ArchiveName"/>
    /// produces: the folder may hold something the user put there, and this list feeds a delete.
    /// </summary>
    public static bool IsArchive(string name) => name is not null && ArchivePattern().IsMatch(name);

    /// <summary>
    /// The archives to delete: everything past the newest <see cref="MaxArchives"/>, and anything
    /// older than <see cref="MaxAge"/>. The current file and foreign files are never listed.
    /// Newest is by last write, with the name as a stable tiebreak (the stamp sorts by time too).
    /// </summary>
    public static IReadOnlyList<string> ToDelete(IEnumerable<LogSegment> files, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(files);

        var archives = files
            .Where(f => IsArchive(f.Name))
            .OrderByDescending(f => f.LastWrite)
            .ThenByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var doomed = new List<string>();
        for (var i = 0; i < archives.Count; i++)
        {
            if (i >= MaxArchives || now - archives[i].LastWrite > MaxAge)
            {
                doomed.Add(archives[i].Name);
            }
        }

        return doomed;
    }

    [GeneratedRegex(@"^themia-\d{8}-\d{6}-\d{3}(-\d{1,4})?\.log$", RegexOptions.IgnoreCase)]
    private static partial Regex ArchivePattern();
}
