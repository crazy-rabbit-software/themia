namespace Themia.Core.Widgets;

/// <summary>[S-4841] One Recent Items shortcut, read: when it was written (the last open) and where it points.</summary>
/// <param name="LinkPath">The <c>.lnk</c> in the Recent Items folder.</param>
/// <param name="Opened">The shortcut's last write: Windows rewrites it on every open.</param>
/// <param name="Target">The resolved target, or null when it could not be read.</param>
public readonly record struct RecentLink(string LinkPath, DateTimeOffset Opened, string? Target);

/// <summary>[S-4841] One file the widget lists.</summary>
/// <param name="Path">The file.</param>
/// <param name="Name">Its name.</param>
/// <param name="Folder">The folder holding it, as a name ("Reports"), or the drive ("C:\").</param>
/// <param name="Opened">When it was last opened.</param>
/// <param name="LinkPath">The Recent Items shortcut behind it, which "Remove from list" deletes.</param>
public sealed record RecentFile(string Path, string Name, string Folder, DateTimeOffset Opened, string LinkPath);

/// <summary>
/// [S-4841] The Recent files list, from Windows' Recent Items folder (<c>shell:Recent</c>, the
/// shortcuts Explorer's Home "Recent" is built from): newest first by each shortcut's last write; a
/// target that is not a fully qualified local or UNC path, is a folder, or is gone is left out
/// (Explorer would show it greyed; a desk widget has no use for it); one file opened from two
/// shortcuts once. Only the newest <see cref="MaxLinksRead"/> shortcuts are resolved at all.
/// </summary>
public static class RecentFileRules
{
    /// <summary>The most files listed.</summary>
    public const int MaxItems = 40;

    /// <summary>The most shortcuts resolved per read; the folder can hold hundreds.</summary>
    public const int MaxLinksRead = 120;

    /// <summary>Whether a file in the Recent Items folder is one of its shortcuts.</summary>
    public static bool IsRecentLink(string fileName)
        => fileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && fileName.Length > 4;

    /// <summary>[S-4841] The shortcuts to resolve, newest first, at most <see cref="MaxLinksRead"/>.</summary>
    public static IReadOnlyList<(string LinkPath, DateTimeOffset Written)> Newest(IEnumerable<(string LinkPath, DateTimeOffset Written)> links)
    {
        ArgumentNullException.ThrowIfNull(links);
        return [.. links
            .Where(l => IsRecentLink(System.IO.Path.GetFileName(l.LinkPath)))
            .OrderByDescending(l => l.Written)
            .ThenBy(l => l.LinkPath, StringComparer.OrdinalIgnoreCase)
            .Take(MaxLinksRead)];
    }

    /// <summary>[S-4841] The list: files that are there, newest first, each once, at most <paramref name="max"/>.</summary>
    /// <param name="links">The resolved shortcuts.</param>
    /// <param name="isFile">Whether a path is a file that exists now (not a folder).</param>
    public static IReadOnlyList<RecentFile> Select(IEnumerable<RecentLink> links, Func<string, bool> isFile, int max = MaxItems)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(isFile);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<RecentFile>();

        foreach (var link in links.OrderByDescending(l => l.Opened).ThenBy(l => l.LinkPath, StringComparer.OrdinalIgnoreCase))
        {
            if (files.Count >= max)
            {
                break;
            }

            if (Usable(link.Target) is not { } target || !seen.Add(target) || !isFile(target))
            {
                continue;
            }

            files.Add(new RecentFile(target, System.IO.Path.GetFileName(target), FolderName(target), link.Opened, link.LinkPath));
        }

        return files;
    }

    /// <summary>The folder a file is in, by its name; the drive root as written ("C:\").</summary>
    public static string FolderName(string path)
    {
        var parent = System.IO.Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(parent))
        {
            return string.Empty;
        }

        var name = System.IO.Path.GetFileName(parent.TrimEnd('\\', '/'));
        return string.IsNullOrEmpty(name) ? parent : name;
    }

    /// <summary>A target worth checking: a fully qualified path with no wildcard or control character.</summary>
    private static string? Usable(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        var path = target.Trim();
        if (path.Any(char.IsControl) || path.IndexOfAny(['*', '?', '"', '<', '>', '|']) >= 0 || !System.IO.Path.IsPathFullyQualified(path))
        {
            return null;
        }

        return path;
    }
}
