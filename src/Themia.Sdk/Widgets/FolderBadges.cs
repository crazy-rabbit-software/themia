
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-1324] An item's cloud sync state, as Explorer's status column shows it.</summary>
public enum CloudState
{
    /// <summary>Not managed by a sync engine.</summary>
    None = 0,

    /// <summary>A placeholder: the data is in the cloud, and opening it downloads it.</summary>
    CloudOnly,

    /// <summary>"Always keep on this device".</summary>
    Pinned,

    /// <summary>Downloaded, and Windows may free the space again.</summary>
    Synced,
}

/// <summary>[S-1324] What "Always keep on this device" / "Free up space" write.</summary>
public enum CloudPin
{
    Pin,
    Unpin,
}

/// <summary>
/// [S-1324] The cloud badge, from the attribute bits every Cloud Files provider (OneDrive, Dropbox,
/// Google Drive, Synology…) sets, ported from the original's <c>cloudStatus</c>. Pure.
/// </summary>
public static class CloudBadges
{
    public const int ReparsePoint = 0x400;
    public const int Offline = 0x1000;
    public const int RecallOnOpen = 0x40000;
    public const int PinnedBit = 0x80000;
    public const int UnpinnedBit = 0x100000;
    public const int RecallOnDataAccess = 0x400000;

    public static CloudState State(int attributes)
    {
        if (attributes == 0)
        {
            return CloudState.None;
        }

        if ((attributes & (Offline | RecallOnDataAccess | RecallOnOpen)) != 0)
        {
            return CloudState.CloudOnly;
        }

        if ((attributes & PinnedBit) != 0)
        {
            return CloudState.Pinned;
        }

        // A reparse point with none of the placeholder bits is a hydrated file the engine manages.
        // (A plain symlink is a reparse point too; the original accepted that, and so does this.)
        if ((attributes & ReparsePoint) != 0)
        {
            return CloudState.Synced;
        }

        return (attributes & UnpinnedBit) != 0 ? CloudState.CloudOnly : CloudState.None;
    }

    /// <summary>The attributes with the pin bits set for <paramref name="pin"/>; the two bits never both on.</summary>
    public static int WithPin(int attributes, CloudPin pin) => pin == CloudPin.Pin
        ? (attributes & ~UnpinnedBit) | PinnedBit
        : (attributes & ~PinnedBit) | UnpinnedBit;

    /// <summary>Whether reading an item's pixels would download it: thumbnails skip those ([S-1324]).</summary>
    public static bool WouldDownload(int attributes) => State(attributes) == CloudState.CloudOnly;

    public static string Describe(CloudState state) => state switch
    {
        CloudState.CloudOnly => Str.WidgetsB_Folder_Cloud_OnlineOnly.Text(),
        CloudState.Pinned => Str.Folder_CloudPinned.Text(),
        CloudState.Synced => Str.WidgetsB_Folder_Cloud_OnDevice.Text(),
        _ => string.Empty,
    };
}

/// <summary>[S-1325] A repository's state for the header badge and the Git column.</summary>
/// <param name="Branch">The branch, or the short "HEAD (detached)" note.</param>
/// <param name="Files">Status code per changed path, relative to the repository root, backslashed, lower-cased.</param>
public sealed record GitSummary(
    string Branch,
    int Staged,
    int Modified,
    int Untracked,
    int Deleted,
    int Conflicted,
    IReadOnlyDictionary<string, string> Files)
{
    public bool Clean => Staged + Modified + Untracked + Deleted + Conflicted == 0;

    /// <summary>The badge's counts, shortest form: "+2 ~3 -1 ?4 !1", or "" when clean.</summary>
    public string Counts()
    {
        var parts = new List<string>(5);

        if (Staged > 0) { parts.Add($"+{Staged}"); }
        if (Modified > 0) { parts.Add($"~{Modified}"); }
        if (Deleted > 0) { parts.Add($"-{Deleted}"); }
        if (Untracked > 0) { parts.Add($"?{Untracked}"); }
        if (Conflicted > 0) { parts.Add($"!{Conflicted}"); }

        return string.Join(' ', parts);
    }

    /// <summary>
    /// The Git column for one entry: a file's two-letter code trimmed ("M", "A", "??"), or for a folder
    /// a dot when anything beneath it changed. <paramref name="relative"/> is the entry's path relative
    /// to the repository root.
    /// </summary>
    public string Code(string relative, bool isDirectory)
    {
        ArgumentNullException.ThrowIfNull(relative);

        var key = GitStatusParser.Key(relative);

        if (!isDirectory)
        {
            return Files.TryGetValue(key, out var code) ? code.Trim() : string.Empty;
        }

        var prefix = key + "\\";
        return Files.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal)) || Files.ContainsKey(prefix)
            ? "•"
            : string.Empty;
    }
}

/// <summary>
/// [S-1325] <c>git status --porcelain=v1 --branch -z --untracked-files=normal</c>, parsed. Counted as
/// the original did: <c>??</c> untracked; any U, AA or DD conflicted; otherwise an index letter in
/// MADRC is staged and a worktree M / D is modified / deleted. Pure, so it is tested against
/// captured output.
/// </summary>
public static class GitStatusParser
{
    public static GitSummary Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var branch = string.Empty;
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var records = output.Split('\0');

        for (var i = 0; i < records.Length; i++)
        {
            var record = records[i];

            if (record.StartsWith("## ", StringComparison.Ordinal))
            {
                branch = BranchOf(record[3..]);
                continue;
            }

            if (record.Length < 4 || record[2] != ' ')
            {
                continue;
            }

            var code = record[..2];
            files[Key(record[3..])] = code;

            // -z writes a rename or copy as "R  new\0old": the old path is its own record and is
            // not an entry of its own.
            if (code[0] is 'R' or 'C')
            {
                i++;
            }
        }

        int staged = 0, modified = 0, untracked = 0, deleted = 0, conflicted = 0;

        foreach (var code in files.Values)
        {
            var (x, y) = (code[0], code[1]);

            if (code == "??")
            {
                untracked++;
            }
            else if (x == 'U' || y == 'U' || (x == 'A' && y == 'A') || (x == 'D' && y == 'D'))
            {
                conflicted++;
            }
            else
            {
                if ("MADRC".Contains(x, StringComparison.Ordinal))
                {
                    staged++;
                }

                if (y == 'M')
                {
                    modified++;
                }
                else if (y == 'D')
                {
                    deleted++;
                }
            }
        }

        return new GitSummary(branch, staged, modified, untracked, deleted, conflicted, files);
    }

    /// <summary>A repository-relative path as a lookup key: backslashed, no trailing separator, lower-case.</summary>
    public static string Key(string relative)
        => relative.Replace('/', '\\').TrimEnd('\\').ToLowerInvariant() + (relative.EndsWith('/') || relative.EndsWith('\\') ? "\\" : string.Empty);

    /// <summary>"main...origin/main [ahead 1]" → "main"; "No commits yet on main" → "main"; "HEAD (no branch)" → "HEAD".</summary>
    private static string BranchOf(string line)
    {
        const string fresh = "No commits yet on ";

        if (line.StartsWith(fresh, StringComparison.Ordinal))
        {
            return line[fresh.Length..].Trim();
        }

        var dots = line.IndexOf("...", StringComparison.Ordinal);
        var name = dots >= 0 ? line[..dots] : line.Split(' ')[0];
        return name.Trim();
    }
}
